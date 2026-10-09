using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.UI.Theme;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Punto de acceso centralizado para leer y escribir la configuración de la app.
    /// Aplica los cambios en tiempo real y los guarda en dos sitios:
    ///   - UserProfile.Preferences["appSettings"]: la copia del usuario. Viaja con el perfil a
    ///     Firestore (CloudSync), así que al iniciar sesión en cualquier dispositivo se recupera.
    ///   - PlayerPrefs: la copia del dispositivo para el usuario con la sesión abierta.
    ///
    /// Sin usuario (Splash, Login, Registro y Onboarding) se usan los ajustes por defecto, sin filtro
    /// de daltonismo, y se borra la copia del dispositivo (era del usuario que cerró sesión). Al entrar
    /// en la app con sesión se cargan los ajustes del perfil (tras CloudSync en el login).
    /// </summary>
    public class SettingsManager : BaseService
    {
        // ── Estado ─────────────────────────────────────────────────────

        private AppSettings         _currentSettings;
        private NotificationManager _notificationManager;
        private DataRepository      _dataRepository;
        private AuthManager         _authManager;
        private FirestoreManager    _firestoreManager;

        private NotificationManager NotificationManagerService => _notificationManager ??= ServiceLocator.Get<NotificationManager>();
        private DataRepository      Repository                 => _dataRepository      ??= ServiceLocator.Get<DataRepository>();
        private AuthManager         Auth                       => _authManager         ??= ServiceLocator.Get<AuthManager>();
        private FirestoreManager    Firestore                  => _firestoreManager    ??= ServiceLocator.Get<FirestoreManager>();

        /// <summary>Acceso de solo lectura a la configuración actual.</summary>
        public AppSettings Current => _currentSettings;

        /// <summary>Clave de UserProfile.Preferences con los ajustes del usuario (JSON de AppSettings).</summary>
        public const string ProfilePreferenceKey = "appSettings";

        /// <summary>null hasta la primera pantalla; true en pantallas sin usuario (login, onboarding…).</summary>
        private bool? _guestMode;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _currentSettings = AppSettings.LoadFromPlayerPrefs();
            _applySettings();
        }

        private void OnEnable()  => EventBus.OnScreenChanged += _onScreenChanged;
        private void OnDisable() => EventBus.OnScreenChanged -= _onScreenChanged;

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Actualiza la configuración de notificaciones y reprograma o cancela
        /// el recordatorio diario según corresponda.
        /// </summary>
        public void UpdateNotifications(bool enabled, int hour, int minute)
        {
            _currentSettings.NotificationsEnabled = enabled;
            _currentSettings.ReminderHour         = hour;
            _currentSettings.ReminderMinute        = minute;

            if (enabled)
                NotificationManagerService.ScheduleDailyReminder(hour, minute);
            else
                NotificationManagerService.CancelDailyReminder();

            _persist();
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>
        /// Actualiza la configuración de apariencia y aplica los cambios en la sesión actual.
        /// </summary>
        public void UpdateAppearance(bool darkMode, bool reduceAnimations, int fontSize, bool highContrast)
        {
            _currentSettings.DarkMode          = darkMode;
            _currentSettings.ReduceAnimations  = reduceAnimations;
            _currentSettings.FontSize          = fontSize;
            _currentSettings.HighContrast      = highContrast;

            _persist();
            _applySettings();
        }

        /// <summary>Actualiza la preferencia de vibración táctil.</summary>
        public void UpdateHaptics(bool enabled)
        {
            _currentSettings.HapticsEnabled = enabled;
            _persist();
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>
        /// Serializa todos los datos del usuario a JSON (derecho de portabilidad, RGPD art. 20) en
        /// temporaryCachePath/lutra_datos.json y devuelve la ruta para compartirla.
        /// </summary>
        public async Task<string> ExportUserData()
        {
            try
            {
                var exportPayload = new
                {
                    ExportDate       = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UserProfile      = await Repository.GetUserProfile(),
                    EmotionRecords   = await Repository.GetEmotionsForPeriod(DateTime.MinValue, DateTime.MaxValue),
                    DiaryEntries     = await Repository.GetAllDiaryEntries(),
                    MinigameSessions = await Repository.GetAllMinigameSessions(),
                    ScaleResponses   = await Repository.GetAllScaleResponses(),
                    Notifications    = await Repository.GetAllNotifications()
                };

                string json = JsonConvert.SerializeObject(exportPayload, Formatting.Indented);
                string path = Path.Combine(Application.temporaryCachePath, "lutra_datos.json");

                await File.WriteAllTextAsync(path, json);

                Debug.Log($"[SettingsManager] Datos exportados en: {path}");
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsManager] ExportUserData: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Elimina todos los datos del usuario de la BD y PlayerPrefs,
        /// y reinicia la app al flujo de Onboarding.
        /// </summary>
        public async Task DeleteAllData()
        {
            try
            {
                await Repository.DeleteAllData();
                AppSettings.DeleteAll();

                Debug.Log("[SettingsManager] Todos los datos eliminados. Volviendo a Onboarding.");

                await GameManager.Instance.StartApp();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsManager] DeleteAllData: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>Actualiza los volúmenes de música y efectos de sonido.</summary>
        public void UpdateAudio(float musicVolume, float sfxVolume)
        {
            _currentSettings.MusicVolume = Mathf.Clamp01(musicVolume);
            _currentSettings.SfxVolume   = Mathf.Clamp01(sfxVolume);
            _persist();
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>Actualiza el modo de daltonismo activo y aplica el efecto visual.</summary>
        public void UpdateColorblindMode(ColorblindMode mode)
        {
            _currentSettings.Colorblind = mode;
            _persist();
            ColorblindFeature.CurrentMode = mode;
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>Actualiza el idioma de la interfaz (solo almacena, sin lógica de localización).</summary>
        public void UpdateLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return;
            _currentSettings.Language = languageCode;
            _persist();
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>
        /// Actualiza los datos de perfil en SQLite (DataRepository lo sube a Firestore).
        /// Usa el patrón fire-and-forget: los errores se registran en el log sin propagarse.
        /// </summary>
        public void UpdateProfile(string name, string surname, DateTime dateOfBirth, string avatar)
        {
            _ = _updateProfileAsync(name, surname, dateOfBirth, avatar);
        }

        /// <summary>
        /// Cambia la contraseña del usuario re-autenticando primero con la contraseña actual.
        /// Propaga la excepción con mensaje en español si la operación falla.
        /// </summary>
        public async Task ChangePassword(string oldPassword, string newPassword)
        {
            await Auth.ChangePassword(oldPassword, newPassword);
        }

        /// <summary>
        /// Elimina todos los datos locales (SQLite + PlayerPrefs) y la cuenta de Firebase Auth.
        /// Propaga la excepción con mensaje en español si la operación falla.
        /// </summary>
        public async Task DeleteAccount()
        {
            try
            {
                string userId = Auth.CurrentUserId;

                await Repository.DeleteAllData();
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();

                if (!string.IsNullOrEmpty(userId))
                {
                    try
                    {
                        await Firestore.DeleteUserData(userId);
                    }
                    catch (Exception firestoreEx)
                    {
                        // Los datos de Firestore no se pudieron eliminar (permisos o red),
                        // pero la cuenta local y de Auth sí se borran igualmente.
                        Debug.LogWarning($"[SettingsManager] Firestore no eliminado: {firestoreEx.Message}");
                    }
                }

                await Auth.DeleteAccount();
                Debug.Log("[SettingsManager] Cuenta eliminada.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsManager] DeleteAccount: {ex.Message}");
                throw;
            }
        }

        // ── Ajustes por usuario ────────────────────────────────────────

        /// <summary>Pantallas sin usuario identificado: se ven sin ajustes personales.</summary>
        private static bool _isGuestState(AppState state)
            => state == AppState.Splash || state == AppState.Login
            || state == AppState.Register || state == AppState.OnboardingProfile;

        /// <summary>Solo actúa al pasar de pantallas sin usuario a pantallas con usuario o al revés.</summary>
        private void _onScreenChanged(AppState state)
        {
            bool guest = _isGuestState(state);
            if (_guestMode == guest) return;
            _guestMode = guest;

            if (guest) _enterGuestMode();
            else       _ = _safeLoadUserSettings();
        }

        /// <summary>
        /// Ajustes por defecto (sin filtro de daltonismo) y fuera la copia del dispositivo: era del
        /// usuario que cerró sesión, y si no otra cuenta nueva la heredaría. Los del usuario siguen en su perfil.
        /// </summary>
        private void _enterGuestMode()
        {
            _currentSettings = new AppSettings();
            AppSettings.DeleteAll();
            _applySettings();
        }

        /// <summary>
        /// Carga los ajustes del perfil del usuario (en el login ya vienen de Firestore por CloudSync).
        /// Usuarios anteriores a la sincronización de ajustes: se usan los del dispositivo y se suben.
        /// </summary>
        private async Task _safeLoadUserSettings()
        {
            try
            {
                var profile = await Repository.GetUserProfile();
                if (profile == null) return;

                string json = null;
                profile.Preferences?.TryGetValue(ProfilePreferenceKey, out json);
                var fromProfile = AppSettings.FromJson(json);

                _currentSettings = fromProfile ?? AppSettings.LoadFromPlayerPrefs();
                _currentSettings.SaveToPlayerPrefs();
                _applySettings();
                _applyReminder();

                if (fromProfile == null)
                    await _saveToProfile();

                Debug.Log($"[SettingsManager] Ajustes del usuario cargados ({(fromProfile != null ? "perfil" : "dispositivo")}).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsManager] _safeLoadUserSettings: {ex.Message}");
            }
        }

        /// <summary>Guarda en el dispositivo y en el perfil del usuario (que se sube a Firestore).</summary>
        private void _persist()
        {
            _currentSettings.SaveToPlayerPrefs();
            if (_guestMode == true) return;   // sin usuario no hay perfil donde guardar
            _ = _safeSaveToProfile();
        }

        private async Task _safeSaveToProfile()
        {
            try   { await _saveToProfile(); }
            catch (Exception ex) { Debug.LogError($"[SettingsManager] _safeSaveToProfile: {ex.Message}"); }
        }

        private async Task _saveToProfile()
        {
            var profile = await Repository.GetUserProfile();
            if (profile == null) return;

            var preferences = profile.Preferences ?? new Dictionary<string, string>();
            preferences[ProfilePreferenceKey] = _currentSettings.ToJson();
            profile.Preferences = preferences;

            await Repository.SaveUserProfile(profile);   // DataRepository lo sube a Firestore
        }

        /// <summary>El recordatorio diario sigue los ajustes del usuario que entra.</summary>
        private void _applyReminder()
        {
            if (_currentSettings.NotificationsEnabled)
                NotificationManagerService.ScheduleDailyReminder(_currentSettings.ReminderHour, _currentSettings.ReminderMinute);
            else
                NotificationManagerService.CancelDailyReminder();
        }

        // ── Métodos privados ───────────────────────────────────────────

        private async Task _updateProfileAsync(string name, string surname, DateTime dateOfBirth, string avatar)
        {
            try
            {
                var profile = await Repository.GetUserProfile();
                if (profile == null)
                {
                    Debug.LogError("[SettingsManager] UpdateProfile: no se encontró el perfil.");
                    return;
                }

                profile.Name        = name;
                profile.Surname     = surname;
                profile.DateOfBirth = dateOfBirth;
                if (!string.IsNullOrEmpty(avatar))
                    profile.Avatar = avatar;

                await Repository.SaveUserProfile(profile);

                Debug.Log("[SettingsManager] Perfil actualizado.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsManager] UpdateProfile: {ex.Message}");
            }
        }



        /// <summary>
        /// Aplica las preferencias de apariencia en la sesión actual y notifica
        /// a los sistemas interesados (ThemeManager, UI) vía EventBus.
        /// </summary>
        private void _applySettings()
        {
            ColorblindFeature.CurrentMode = _currentSettings.Colorblind;
            EventBus.EmitSettingsChanged(_currentSettings);
        }
    }
}
