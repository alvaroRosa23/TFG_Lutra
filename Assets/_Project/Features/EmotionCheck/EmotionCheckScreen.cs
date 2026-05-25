using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.EmotionCheck
{
    /// <summary>
    /// Pantalla del flujo de check-in emocional.
    ///
    /// El modo se determina mediante <see cref="PendingMode"/>, que MainMenuScreen
    /// establece antes de transicionar a este estado:
    ///   Day    → título "¿Qué tal el día?",        IsMorningCheck = true
    ///   Moment → título "¿Cómo te sientes ahora?", IsMorningCheck = false
    ///
    /// El botón Atrás y el botón Continuar siempre navegan a MainMenu al terminar.
    /// </summary>
    public class EmotionCheckScreen : UIScreen
    {
        // ── Campo estático de modo ─────────────────────────────────────

        /// <summary>
        /// Modo establecido por MainMenuScreen antes de navegar a EmotionCheck.
        /// Por defecto Day para el flujo de inicio del día.
        /// </summary>
        public static EmotionCheckMode PendingMode = EmotionCheckMode.Day;

        // ── Referencias serializadas ───────────────────────────────────

        [SerializeField] private EmotionCheckController    _controller;
        [SerializeField] private Button                    _backButton;
        [SerializeField] private TMPro.TextMeshProUGUI     _titleLabel;

        private bool _hasInitialized = false;

        // ── UIScreen ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.EmotionCheck;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        // ── Unity lifecycle ────────────────────────────────────────────

        private void OnEnable()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(_onBackClicked);

            Debug.Log("[EmotionCheckScreen] OnEnable — BottomNavBar ocultado por BottomNavBar._onScreenChanged.");
        }

        private void OnDisable()
        {
            if (_backButton != null)
                _backButton.onClick.RemoveListener(_onBackClicked);
        }

        // ── UIScreen callbacks ─────────────────────────────────────────

        public override void OnScreenFocused()
        {
            _ = _safeOnScreenFocused();
        }

        private async Task _safeOnScreenFocused()
        {
            try
            {
                if (_hasInitialized) return;
                _hasInitialized = true;

                Debug.Log($"[EmotionCheckScreen] OnScreenFocused — PendingMode: {PendingMode}");

                if (_backButton != null)
                    _backButton.gameObject.SetActive(true);

                if (_controller == null)
                {
                    Debug.LogWarning("[EmotionCheckScreen] _controller es null. Esperando input del usuario.");
                    return;
                }

                if (PendingMode == EmotionCheckMode.Day)
                    await _controller.OpenDayCheck();
                else
                    await _controller.OpenMomentCheck();
            }
            catch (Exception ex)
            { Debug.LogError($"[EmotionCheckScreen] OnScreenFocused: {ex.Message}"); }
        }

        public override void OnScreenUnfocused()
        {
            _hasInitialized = false;
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>Vuelve al MainMenu sin guardar ningún registro.</summary>
        private void _onBackClicked()
        {
            if (AppStateMachine.Instance != null)
                AppStateMachine.Instance.TransitionTo(AppState.MainMenu);
        }
    }
}
