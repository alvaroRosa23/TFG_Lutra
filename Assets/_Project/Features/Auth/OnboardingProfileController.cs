using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Systems;
using Lutra.UI.Theme;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Controlador del flujo de onboarding de perfil (5 pasos).
    /// Orquesta la navegación entre pasos, validación y persistencia del UserProfile.
    /// </summary>
    public class OnboardingProfileController : MonoBehaviour
    {
        [SerializeField] private OnboardingProfileView _view;

        private DataRepository _dataRepository;
        private AuthManager    _authManager;
        private ThemeManager   _themeManager;

        private DataRepository DataRepository =>
            _dataRepository ??= ServiceLocator.Get<DataRepository>();

        private AuthManager  AuthManager  =>
            _authManager  ??= ServiceLocator.Get<AuthManager>();

        private ThemeManager ThemeManagerService =>
            _themeManager ??= ServiceLocator.Get<ThemeManager>();

        private OnboardingProfileData _data = new OnboardingProfileData();
        private int _currentStep = 0;

        private void Awake()
        {
            _view.OnNextClicked = OnNextClicked;
            _view.OnBackClicked = OnBackClicked;
        }

        public Task OpenOnboarding()
        {
            _currentStep = 0;
            _data        = new OnboardingProfileData();
            _view.ShowStep(0);
            return Task.CompletedTask;
        }

        public void OnNextClicked()
        {
            if (!_validateCurrentStep()) return;
            _saveCurrentStepData();

            if (_currentStep < 4)
            {
                _currentStep++;
                _view.ShowStep(_currentStep);
            }
            else
            {
                _ = _finishOnboarding();
            }
        }

        public void OnBackClicked()
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                _view.ShowStep(_currentStep);
            }
            else
            {
                AppStateMachine.Instance.TransitionTo(AppState.Login);
            }
        }

        private bool _validateCurrentStep()
        {
            switch (_currentStep)
            {
                case 0:
                    if (string.IsNullOrWhiteSpace(_view.GetName()) ||
                        string.IsNullOrWhiteSpace(_view.GetSurname()))
                    {
                        _view.ShowError("Introduce tu nombre y apellido");
                        return false;
                    }
                    break;

                case 1:
                    var dob = _view.GetDateOfBirth();
                    if (dob == DateTime.MinValue)
                    {
                        _view.ShowError("Introduce una fecha válida");
                        return false;
                    }
                    if ((DateTime.Now - dob).TotalDays < 13 * 365.25)
                    {
                        _view.ShowError("Debes tener al menos 13 años");
                        return false;
                    }
                    break;

                case 2:
                    // Toggle group garantiza selección; validación defensiva
                    break;

                case 3:
                    if (_view.GetSelectedHobbies().Count == 0)
                    {
                        _view.ShowError("Selecciona al menos una afición");
                        return false;
                    }
                    break;
            }

            return true;
        }

        private void _saveCurrentStepData()
        {
            switch (_currentStep)
            {
                case 0:
                    _data.Name    = _view.GetName();
                    _data.Surname = _view.GetSurname();
                    break;
                case 1:
                    _data.DateOfBirth = _view.GetDateOfBirth();
                    break;
                case 2:
                    _data.Culture = _view.GetSelectedCulture();
                    break;
                case 3:
                    _data.Hobbies = _view.GetSelectedHobbies();
                    break;
            }
        }

        private async Task _finishOnboarding()
        {
            try
            {
                var profile = new UserProfile
                {
                    Name           = _data.Name,
                    Surname        = _data.Surname,
                    DateOfBirth    = _data.DateOfBirth,
                    FirebaseUserId = AuthManager.CurrentUserId,
                    Email          = AuthManager.CurrentEmail,
                    CreationDate   = DateTime.Now,
                    Coins          = 0,
                    Culture        = _data.Culture,
                    HobbiesJson    = JsonConvert.SerializeObject(_data.Hobbies)
                };

                await DataRepository.SaveUserProfile(profile);

                // Activar paleta cultural desde el primer check-in
                ThemeManagerService.SetActiveCulture(profile.Culture);

                try
                {
                    await ServiceLocator.Get<FirestoreManager>().SaveUserProfile(profile);
                }
                catch (Exception firestoreEx)
                {
                    Debug.LogWarning($"[OnboardingProfileController] Firestore no disponible, perfil solo local: {firestoreEx.Message}");
                }

                Debug.Log("[OnboardingProfileController] Perfil guardado");
                AppStateMachine.Instance.TransitionTo(AppState.EmotionCheck);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OnboardingProfileController] _finishOnboarding: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
