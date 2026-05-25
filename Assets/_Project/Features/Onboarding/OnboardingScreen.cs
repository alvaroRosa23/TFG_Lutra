using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.UI.Components;
using Lutra.UI.Screens;

namespace Lutra.Features.Onboarding
{
    /// <summary>
    /// Pantalla de onboarding: recoge el nombre del usuario, crea su perfil
    /// y navega al flujo de check-in emocional.
    /// </summary>
    public class OnboardingScreen : UIScreen
    {
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private Button         _continueButton;

        // ── UIScreen ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.Login;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.4f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        // ── Unity lifecycle ────────────────────────────────────────────

        private void OnEnable()
        {
            _continueButton.onClick.AddListener(_onContinueClicked);
        }

        private void OnDisable()
        {
            _continueButton.onClick.RemoveListener(_onContinueClicked);
        }

        private void OnDestroy()
        {
            _continueButton?.onClick.RemoveAllListeners();
        }

        // ── UIScreen callbacks ─────────────────────────────────────────

        public override void OnScreenFocused()
        {
            _nameInput.ActivateInputField();
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _onContinueClicked()
            => _ = _safeContinue();

        private async Task _safeContinue()
        {
            try
            {
                string name = (_nameInput == null || string.IsNullOrWhiteSpace(_nameInput.text))
                    ? "Jugador"
                    : _nameInput.text.Trim();

                var profile = new UserProfile(name, avatar: "default");

                var repo = ServiceLocator.Get<DataRepository>();
                await repo.SaveUserProfile(profile);

                AppStateMachine.Instance.TransitionTo(AppState.EmotionCheck);
            }
            catch (Exception ex)
            { Debug.LogError($"[OnboardingScreen] {ex.Message}"); }
        }
    }
}
