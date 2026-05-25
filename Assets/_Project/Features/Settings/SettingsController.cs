using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Systems;

namespace Lutra.Features.Settings
{
    /// <summary>
    /// Controlador de la pantalla de Ajustes.
    /// Conecta SettingsView con SettingsManager aplicando el patrón MVC.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [SerializeField] private SettingsView _view;

        // ── Servicios (lazy) ──────────────────────────────────────────

        private SettingsManager _settingsManager;
        private DataRepository  _dataRepository;
        private AuthManager     _authManager;

        private SettingsManager Settings   => _settingsManager ??= ServiceLocator.Get<SettingsManager>();
        private DataRepository  Repository => _dataRepository  ??= ServiceLocator.Get<DataRepository>();
        private AuthManager     Auth       => _authManager     ??= ServiceLocator.Get<AuthManager>();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<SettingsView>();
        }

        private void OnEnable()
        {
            _subscribeToView();
        }

        private void OnDisable()
        {
            _unsubscribeFromView();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Abre la pantalla de ajustes cargando la configuración actual
        /// y el perfil del usuario en la vista.
        /// </summary>
        public void OpenSettings()
        {
            _view?.LoadSettings(Settings.Current);
            _ = _loadProfileAsync();
        }

        /// <summary>Aplica los ajustes de notificaciones recibidos desde la vista.</summary>
        public void ApplyNotificationSettings(bool enabled, int hour, int minute)
        {
            Settings.UpdateNotifications(enabled, hour, minute);
        }

        /// <summary>Aplica los ajustes de apariencia recibidos desde la vista.</summary>
        public void ApplyAppearanceSettings(bool darkMode, bool reduceAnimations,
            int fontSize, bool highContrast)
        {
            Settings.UpdateAppearance(darkMode, reduceAnimations, fontSize, highContrast);
        }

        /// <summary>Aplica los volúmenes de música y efectos de sonido.</summary>
        public void ApplyAudioSettings(float musicVolume, float sfxVolume)
        {
            Settings.UpdateAudio(musicVolume, sfxVolume);
        }

        /// <summary>Aplica el modo de daltonismo seleccionado.</summary>
        public void ApplyColorblindMode(ColorblindMode mode)
        {
            Settings.UpdateColorblindMode(mode);
        }

        /// <summary>Aplica el idioma de interfaz seleccionado.</summary>
        public void ApplyLanguage(string languageCode)
        {
            Settings.UpdateLanguage(languageCode);
        }

        /// <summary>
        /// Cambia la contraseña del usuario y muestra feedback en la vista.
        /// </summary>
        public async Task OnChangePasswordClicked(string oldPassword, string newPassword)
        {
            try
            {
                _view?.ShowFeedback("Cambiando contraseña…");
                await Settings.ChangePassword(oldPassword, newPassword);
                _view?.ShowFeedback("Contraseña actualizada correctamente.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsController] OnChangePasswordClicked: {ex.Message}");
                _view?.ShowFeedback(ex.Message, isError: true);
            }
        }

        /// <summary>
        /// Elimina la cuenta del usuario y navega a Login si la operación tiene éxito.
        /// </summary>
        public async Task OnDeleteAccountClicked()
        {
            try
            {
                await Settings.DeleteAccount();
                AppStateMachine.Instance.TransitionTo(AppState.Login);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsController] OnDeleteAccountClicked: {ex.Message}");
                _view?.ShowFeedback(ex.Message, isError: true);
            }
        }

        /// <summary>Cierra sesión y navega a Login.</summary>
        public void OnLogoutClicked()
        {
            Auth.Logout();
            AppStateMachine.Instance.TransitionTo(AppState.Login);
        }

        /// <summary>
        /// Exporta los datos del usuario. Muestra feedback en la vista
        /// antes y después de la operación.
        /// </summary>
        public async Task OnExportDataClicked()
        {
            try
            {
                _view?.ShowFeedback("Exportando datos…");
                await Settings.ExportUserData();
                _view?.ShowFeedback("Datos exportados correctamente.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsController] OnExportDataClicked: {ex.Message}\n{ex.StackTrace}");
                _view?.ShowFeedback("Error al exportar los datos.", isError: true);
            }
        }

        /// <summary>
        /// Elimina todos los datos del usuario tras la confirmación del panel.
        /// El panel de confirmación es mostrado por la vista antes de invocar este método.
        /// </summary>
        public async Task OnDeleteDataClicked()
        {
            try
            {
                await Settings.DeleteAllData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsController] OnDeleteDataClicked: {ex.Message}\n{ex.StackTrace}");
                _view?.ShowFeedback("Error al eliminar los datos.", isError: true);
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _subscribeToView()
        {
            if (_view == null) return;

            _view.OnNotificationSettingsChanged += ApplyNotificationSettings;
            _view.OnAppearanceSettingsChanged   += ApplyAppearanceSettings;
            _view.OnAudioSettingsChanged        += ApplyAudioSettings;
            _view.OnColorblindModeChanged       += ApplyColorblindMode;
            _view.OnLanguageChanged             += ApplyLanguage;
            _view.OnProfileSaved                += _onProfileSavedHandler;
            _view.OnChangePasswordRequested     += _onChangePasswordRequestedHandler;
            _view.OnDeleteAccountConfirmed      += _onDeleteAccountConfirmedHandler;
            _view.OnLogoutRequested             += _onLogoutRequestedHandler;
            _view.OnExportDataClicked           += _onExportDataClickedHandler;
            _view.OnDeleteDataConfirmed         += _onDeleteDataConfirmedHandler;
            _view.OnBackRequested               += _onBackRequestedHandler;
        }

        private void _unsubscribeFromView()
        {
            if (_view == null) return;

            _view.OnNotificationSettingsChanged -= ApplyNotificationSettings;
            _view.OnAppearanceSettingsChanged   -= ApplyAppearanceSettings;
            _view.OnAudioSettingsChanged        -= ApplyAudioSettings;
            _view.OnColorblindModeChanged       -= ApplyColorblindMode;
            _view.OnLanguageChanged             -= ApplyLanguage;
            _view.OnProfileSaved                -= _onProfileSavedHandler;
            _view.OnChangePasswordRequested     -= _onChangePasswordRequestedHandler;
            _view.OnDeleteAccountConfirmed      -= _onDeleteAccountConfirmedHandler;
            _view.OnLogoutRequested             -= _onLogoutRequestedHandler;
            _view.OnExportDataClicked           -= _onExportDataClickedHandler;
            _view.OnDeleteDataConfirmed         -= _onDeleteDataConfirmedHandler;
            _view.OnBackRequested               -= _onBackRequestedHandler;
        }

        private async Task _loadProfileAsync()
        {
            try
            {
                var profile = await Repository.GetUserProfile();
                if (profile != null)
                    _view?.LoadProfile(profile.Name, profile.Surname, profile.DateOfBirth, profile.Avatar);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsController] _loadProfileAsync: {ex.Message}");
            }
        }

        private void _onProfileSavedHandler(string name, string surname, string dob, string avatar)
        {
            if (!DateTime.TryParseExact(dob, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOfBirth))
            {
                _view?.ShowFeedback("Fecha de nacimiento no válida (usa dd/MM/yyyy).", isError: true);
                return;
            }
            Settings.UpdateProfile(name, surname, dateOfBirth, avatar);
            _view?.ShowFeedback("Perfil guardado correctamente.");
        }

        private void _onChangePasswordRequestedHandler(string oldPassword, string newPassword)
            => _ = OnChangePasswordClicked(oldPassword, newPassword);

        private void _onDeleteAccountConfirmedHandler()
            => _ = OnDeleteAccountClicked();

        private void _onLogoutRequestedHandler()
            => OnLogoutClicked();

        private void _onExportDataClickedHandler()
            => _ = OnExportDataClicked();

        private void _onDeleteDataConfirmedHandler()
            => _ = OnDeleteDataClicked();

        private void _onBackRequestedHandler()
            => AppStateMachine.Instance.TransitionTo(AppState.MainMenu);
    }
}
