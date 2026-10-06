using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Systems;
using Lutra.UI.Theme;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Controlador de la pantalla de login.
    /// Orquesta la lógica de autenticación entre LoginView y AuthManager,
    /// y decide el estado de navegación tras un login correcto.
    /// </summary>
    public class LoginController : MonoBehaviour
    {
        // ── Referencias ────────────────────────────────────────────────

        [SerializeField] private LoginView _view;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private AuthManager _authManager;
        private ThemeManager _themeManager;

        private AuthManager  AuthManagerService  => _authManager  ??= ServiceLocator.Get<AuthManager>();
        private ThemeManager ThemeManagerService => _themeManager ??= ServiceLocator.Get<ThemeManager>();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _view.OnLoginClicked          = () => _ = OnLoginClicked();
            _view.OnRegisterClicked       = () => _ = OnRegisterClicked();
            _view.OnForgotPasswordClicked = () => _ = OnForgotPasswordClicked();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Inicializa la vista al entrar en la pantalla de login.</summary>
        public async Task OpenLogin()
        {
            _view.ClearError();
            _view.SetLoading(false);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Valida los campos, llama a Firebase y navega al estado correcto según el perfil.
        /// </summary>
        public async Task OnLoginClicked()
        {
            try
            {
                string email    = _view.GetEmail();
                string password = _view.GetPassword();

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    _view.ShowError("Rellena todos los campos");
                    return;
                }

                _view.SetLoading(true);
                var (success, error) = await AuthManagerService.LoginWithEmail(email, password);
                _view.SetLoading(false);

                if (!success)
                {
                    _view.ShowError(error);
                    return;
                }

                await _navigateAfterAuth();
            }
            catch (Exception ex)
            {
                _view.SetLoading(false);
                _view.ShowError("Error inesperado");
                Debug.LogError($"[LoginController] OnLoginClicked: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>Navega a la pantalla de registro.</summary>
        public Task OnRegisterClicked()
        {
            AppStateMachine.Instance.TransitionTo(AppState.Register);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Envía un email de recuperación de contraseña al email introducido en el campo.
        /// </summary>
        public async Task OnForgotPasswordClicked()
        {
            try
            {
                string email = _view.GetEmail();

                if (string.IsNullOrWhiteSpace(email))
                {
                    _view.ShowError("Introduce tu email primero");
                    return;
                }

                _view.SetLoading(true);
                var (success, error) = await AuthManagerService.SendPasswordResetEmail(email);
                _view.SetLoading(false);

                if (success)
                    _view.ShowError("Email de recuperación enviado", isSuccess: true);
                else
                    _view.ShowError(error);
            }
            catch (Exception ex)
            {
                _view.SetLoading(false);
                _view.ShowError("Error inesperado");
                Debug.LogError($"[LoginController] OnForgotPasswordClicked: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Tras un login correcto decide el siguiente estado:
        ///   - Perfil local de otra cuenta → se borran los datos locales.
        ///   - Sin perfil local → intenta restaurarlo desde Firestore.
        ///   - Sin perfil en ningún lado → OnboardingProfile.
        ///   - Con perfil → sincroniza todo con Firestore (CloudSync) y va a MainMenu si ya hizo
        ///     el check-in de hoy (en este u otro dispositivo) o a EmotionCheck si no.
        /// </summary>
        private async Task _navigateAfterAuth()
        {
            var repo    = ServiceLocator.Get<DataRepository>();
            var profile = await repo.GetUserProfile();

            // Si el perfil local pertenece a otra cuenta, descartar datos locales
            if (profile != null &&
                !string.IsNullOrEmpty(profile.FirebaseUserId) &&
                profile.FirebaseUserId != AuthManagerService.CurrentUserId)
            {
                Debug.Log("[LoginController] Perfil local de otra cuenta → limpiando datos locales.");
                await repo.DeleteAllData();
                profile = null;
            }

            // Sin perfil local, intentar restaurar desde Firestore
            if (profile == null)
            {
                try
                {
                    Debug.Log("[LoginController] Sin perfil local, buscando en Firestore...");
                    var firestoreProfile = await ServiceLocator.Get<FirestoreManager>()
                        .GetUserProfile(AuthManagerService.CurrentUserId);

                    if (firestoreProfile != null)
                    {
                        await repo.SaveUserProfile(firestoreProfile, sync: false);
                        profile = firestoreProfile;
                        Debug.Log("[LoginController] Perfil restaurado desde Firestore.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LoginController] No se pudo consultar Firestore: {ex.Message}");
                }
            }

            if (profile == null)
            {
                Debug.Log("[LoginController] Login correcto, sin perfil → OnboardingProfile.");
                AppStateMachine.Instance.TransitionTo(AppState.OnboardingProfile);
                return;
            }

            // Aplicar paleta cultural del perfil del usuario
            ThemeManagerService.SetActiveCulture(profile.Culture);

            await CloudSync.SyncAllAsync(repo);

            bool checkedInToday = await ServiceLocator.Get<StreakManager>().HasCheckedInToday();
            if (checkedInToday)
                Debug.Log($"[LoginController] {profile.Name} ya hizo check-in → MainMenu.");
            else
                Debug.Log($"[LoginController] Bienvenido de nuevo, {profile.Name} → EmotionCheck.");

            // Sin consentimiento de datos de salud pasa antes por la pantalla de consentimiento
            await ConsentGate.ContinueTo(checkedInToday ? AppState.MainMenu : AppState.EmotionCheck);
        }
    }
}
