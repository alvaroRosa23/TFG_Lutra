using System;
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
    /// Persiste en PlayerPrefs vía AppSettings y aplica los cambios en tiempo real.
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

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _currentSettings = AppSettings.LoadFromPlayerPrefs();
            _applySettings();
        }

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

            _currentSettings.SaveToPlayerPrefs();
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

            _currentSettings.SaveToPlayerPrefs();
            _applySettings();
        }

        /// <summary>Actualiza la preferencia de vibración táctil.</summary>
        public void UpdateHaptics(bool enabled)
        {
            _currentSettings.HapticsEnabled = enabled;
            _currentSettings.SaveToPlayerPrefs();
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>
        /// Serializa todos los datos del usuario a JSON y los guarda en
        /// Application.persistentDataPath/owlet_export.json.
        /// </summary>
        public async Task ExportUserData()
        {
            try
            {
                var records = await Repository.GetEmotionsForPeriod(DateTime.MinValue, DateTime.MaxValue);
                var entries = await Repository.GetAllDiaryEntries();
                var profile = await Repository.GetUserProfile();

                var exportPayload = new
                {
                    ExportDate    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UserProfile   = profile,
                    EmotionRecords = records,
                    DiaryEntries  = entries
                };

                string json = JsonConvert.SerializeObject(exportPayload, Formatting.Indented);
                string path = Path.Combine(Application.persistentDataPath, "lutra_export.json");

                await File.WriteAllTextAsync(path, json);

                Debug.Log($"[SettingsManager] Datos exportados en: {path}");
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
            _currentSettings.SaveToPlayerPrefs();
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>Actualiza el modo de daltonismo activo y aplica el efecto visual.</summary>
        public void UpdateColorblindMode(ColorblindMode mode)
        {
            _currentSettings.Colorblind = mode;
            _currentSettings.SaveToPlayerPrefs();
            ColorblindFeature.CurrentMode = mode;
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>Actualiza el idioma de la interfaz (solo almacena, sin lógica de localización).</summary>
        public void UpdateLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return;
            _currentSettings.Language = languageCode;
            _currentSettings.SaveToPlayerPrefs();
            EventBus.EmitSettingsChanged(_currentSettings);
        }

        /// <summary>
        /// Actualiza los datos de perfil en SQLite y en Firestore.
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

                if (Auth.IsLoggedIn)
                    await Firestore.SaveUserProfile(profile);

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
