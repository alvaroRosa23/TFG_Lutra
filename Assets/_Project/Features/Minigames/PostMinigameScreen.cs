using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Events;
using Lutra.Minigames;
using Lutra.UI.Screens;

namespace Lutra.Features.Minigames
{
    /// <summary>
    /// Pantalla de resultados que aparece al terminar un minijuego.
    /// Muestra duración, mensaje positivo y permite al usuario registrar
    /// cómo se siente después de jugar.
    /// </summary>
    public class PostMinigameScreen : UIScreen
    {
        // ── AppState ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.MinigameActive;

        // ── Referencias serializadas ───────────────────────────────────

        [Header("Resultado")]
        [SerializeField] private TextMeshProUGUI _durationLabel;
        [SerializeField] private TextMeshProUGUI _messageLabel;

        [Header("Emoción post-sesión (mismo índice)")]
        [SerializeField] private Button[]      _emotionButtons;
        [SerializeField] private EmotionType[] _emotionTypes;

        // ── Evento público ─────────────────────────────────────────────

        /// <summary>Disparado cuando el usuario selecciona su emoción post-juego.</summary>
        public event Action<EmotionType> OnPostEmotionSelected;

        // ── Mensajes positivos por emoción ─────────────────────────────

        private static readonly Dictionary<EmotionType, string[]> _emotionMessages
            = new Dictionary<EmotionType, string[]>
        {
            { EmotionType.Anxiety,     new[] { "Respira. Lo estás haciendo muy bien.", "Cada momento de calma es un logro." } },
            { EmotionType.Sadness,     new[] { "Eres más fuerte de lo que crees.", "Has elegido cuidarte. Eso importa." } },
            { EmotionType.Frustration, new[] { "Has canalizado esa energía de forma increíble.", "Soltar también es una victoria." } },
            { EmotionType.Overwhelm,   new[] { "Un paso a la vez. Ya diste uno hoy.", "Pequeñas pausas, grandes cambios." } },
            { EmotionType.Joy,         new[] { "¡Qué energía tan bonita!", "Celebra cada momento así." } },
            { EmotionType.Calm,        new[] { "La calma es tu superpoder.", "Llevas ese equilibrio contigo." } },
            { EmotionType.Nostalgia,   new[] { "Honrar el pasado también es cuidarse.", "Tu historia te hace único/a." } },
            { EmotionType.Energy,      new[] { "¡Imparable! Muy bien.", "Esa energía lo mueve todo." } },
        };

        private static readonly string[] _genericMessages =
        {
            "Has hecho algo increíble por ti.",
            "Cada momento que cuidas tu bienestar cuenta.",
            "Tu mente merece este descanso.",
            "Pequeñas acciones, grandes cambios.",
        };

        // ── Estado ─────────────────────────────────────────────────────

        private MinigameResult _lastResult;

        // ── Unity lifecycle ────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _registerEmotionButtons();
        }

        private void OnEnable()
        {
            EventBus.OnMinigameCompleted += _onMinigameCompleted;
        }

        private void OnDisable()
        {
            EventBus.OnMinigameCompleted -= _onMinigameCompleted;
        }

        private void OnDestroy()
        {
            OnPostEmotionSelected = null;

            if (_emotionButtons != null)
                foreach (var btn in _emotionButtons)
                    btn?.onClick.RemoveAllListeners();
        }

        // ── UIScreen overrides ─────────────────────────────────────────

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Muestra los resultados de la sesión de minijuego.</summary>
        public void ShowResult(MinigameResult result)
        {
            if (result == null) return;
            _lastResult = result;

            _updateDurationLabel(result.DurationSeconds);
            _updateMessageLabel(result.EmotionBefore);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _updateDurationLabel(int seconds)
        {
            if (_durationLabel == null) return;

            int mins = seconds / 60;
            int secs = seconds % 60;

            _durationLabel.text = mins > 0
                ? $"{mins} min {secs:D2} s"
                : $"{secs} segundos";
        }

        private void _updateMessageLabel(EmotionType emotion)
        {
            if (_messageLabel == null) return;

            string[] pool = _emotionMessages.TryGetValue(emotion, out var msgs) && msgs.Length > 0
                ? msgs
                : _genericMessages;

            _messageLabel.text = pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private void _registerEmotionButtons()
        {
            if (_emotionButtons == null || _emotionTypes == null) return;

            int count = Mathf.Min(_emotionButtons.Length, _emotionTypes.Length);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                _emotionButtons[index]?.onClick.AddListener(() =>
                    OnPostEmotionSelected?.Invoke(_emotionTypes[index]));
            }
        }

        /// <summary>
        /// Recibe MinigameSession desde EventBus y construye un MinigameResult para mostrar.
        /// </summary>
        private void _onMinigameCompleted(MinigameSession session)
        {
            if (session == null) return;

            var result = new MinigameResult(session.MinigameId, session.EmotionBefore)
            {
                DurationSeconds    = Mathf.RoundToInt(session.DurationSeconds),
                RelaxationScore    = session.RelaxationScore,
                EmotionAfter       = session.EmotionAfter,
                CompletedNaturally = true
            };

            ShowResult(result);
        }
    }
}
