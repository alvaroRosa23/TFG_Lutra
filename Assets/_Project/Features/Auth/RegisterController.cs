using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Systems;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Controlador de la pantalla de registro.
    /// Orquesta la lógica de creación de cuenta entre RegisterView y AuthManager.
    /// </summary>
    public class RegisterController : MonoBehaviour
    {
        [SerializeField] private RegisterView _view;

        private AuthManager    _authManager;
        private DataRepository _dataRepository;

        private AuthManager    AuthManager    => _authManager    ??= ServiceLocator.Get<AuthManager>();
        private DataRepository DataRepository => _dataRepository ??= ServiceLocator.Get<DataRepository>();

        private void Awake()
        {
            _view.OnRegisterClicked = () => _ = OnRegisterClicked();
            _view.OnBackClicked     = OnBackClicked;
        }

        public Task OpenRegister()
        {
            _view.ClearAll();
            _view.SetLoading(false);
            return Task.CompletedTask;
        }

        public async Task OnRegisterClicked()
        {
            try
            {
                string email           = _view.GetEmail();
                string password        = _view.GetPassword();
                string confirmPassword = _view.GetConfirmPassword();

                if (string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(confirmPassword))
                {
                    _view.ShowError("Rellena todos los campos");
                    return;
                }

                if (password != confirmPassword)
                {
                    _view.ShowError("Las contraseñas no coinciden");
                    return;
                }

                _view.SetLoading(true);
                var (success, error) = await AuthManager.RegisterWithEmail(email, password);
                _view.SetLoading(false);

                if (!success)
                {
                    _view.ShowError(error);
                    return;
                }

                Debug.Log("[RegisterController] Cuenta creada correctamente");

                // Limpiar cualquier dato local de una cuenta anterior antes de comenzar el onboarding
                await DataRepository.DeleteAllData();

                AppStateMachine.Instance.TransitionTo(AppState.OnboardingProfile);
            }
            catch (Exception ex)
            {
                _view.SetLoading(false);
                _view.ShowError("Error inesperado");
                Debug.LogError($"[RegisterController] OnRegisterClicked: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void OnBackClicked()
        {
            AppStateMachine.Instance.TransitionTo(AppState.Login);
        }
    }
}
