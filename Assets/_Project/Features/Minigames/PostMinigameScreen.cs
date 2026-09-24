using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Features.Charts;
using Lutra.Minigames;
using Lutra.UI.Components;
using Lutra.UI.Screens;

namespace Lutra.Features.Minigames
{
    /// <summary>
    /// Pantalla de resultados que aparece al terminar un minijuego (AppState.MinigameActive).
    /// Lee MinigameLoader.LastOutcome al recibir el foco (la sesión ya está guardada) y muestra
    /// puntuación, récord, duración, monedas y un mensaje positivo. El usuario puede registrar
    /// cómo se siente (se guarda en la sesión como EmotionAfter), jugar otra vez o volver.
    /// </summary>
    public class PostMinigameScreen : UIScreen
    {
        // ── AppState ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.MinigameActive;

        // ── Referencias serializadas ───────────────────────────────────

        [Header("Resultado")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _scoreLabel;
        [SerializeField] private TextMeshProUGUI _recordLabel;
        [SerializeField] private GameObject      _newRecordBadge;
        [SerializeField] private TextMeshProUGUI _durationLabel;
        [SerializeField] private TextMeshProUGUI _coinsLabel;
        [SerializeField] private TextMeshProUGUI _messageLabel;

        [Header("Emoción post-sesión (mismo índice)")]
        [SerializeField] private Button[]      _emotionButtons;
        [SerializeField] private EmotionType[] _emotionTypes;
        [SerializeField] private Color         _emotionSelectedColor   = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color         _emotionUnselectedColor = Color.white;

        [Header("Navegación")]
        [SerializeField] private Button _playAgainButton;
        [SerializeField] private Button _backButton;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private DataRepository _repo;
        private DataRepository Repo => _repo ??= ServiceLocator.Get<DataRepository>();

        private MinigameLoader _loader;
        private MinigameLoader Loader => _loader ??= ServiceLocator.Get<MinigameLoader>();

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

        private MinigameOutcome _outcome;
        private EmotionType?    _selectedEmotion;
        private bool            _navigating;

        // ── Unity lifecycle ────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _registerEmotionButtons();

            _playAgainButton?.onClick.AddListener(_onPlayAgainClicked);
            _backButton?.onClick.AddListener(_onBackClicked);
        }

        private void OnDestroy()
        {
            if (_emotionButtons != null)
                foreach (var btn in _emotionButtons)
                    btn?.onClick.RemoveAllListeners();

            _playAgainButton?.onClick.RemoveAllListeners();
            _backButton?.onClick.RemoveAllListeners();
        }

        // ── UIScreen overrides ─────────────────────────────────────────

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            _navigating = false;
            _selectedEmotion = null;
            _refreshEmotionButtons();

            ShowOutcome(Loader?.LastOutcome);
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Muestra el resultado de la última partida.</summary>
        public void ShowOutcome(MinigameOutcome outcome)
        {
            _outcome = outcome;

            if (outcome?.Session == null)
            {
                Debug.LogWarning("[PostMinigameScreen] No hay resultado de minijuego que mostrar");
                return;
            }

            var session = outcome.Session;
            int score = MinigameOutcome.ToDisplayScore(session.RelaxationScore);

            _setText(_titleLabel, outcome.DisplayName);
            _setText(_scoreLabel, $"Puntuación: {score}");
            _setText(_recordLabel, _buildRecordText(outcome));
            _setText(_durationLabel, ChartsCalculator.FormatDuration(Mathf.RoundToInt(session.DurationSeconds)));
            _setText(_coinsLabel, outcome.CoinsEarned > 0 ? $"+{outcome.CoinsEarned} monedas" : string.Empty);
            _setText(_messageLabel, _pickMessage(session.EmotionBefore));

            if (_newRecordBadge != null)
                _newRecordBadge.SetActive(outcome.IsNewRecord);
        }

        // ── Handlers ───────────────────────────────────────────────────

        private void _onEmotionSelected(EmotionType emotion)
        {
            _selectedEmotion = emotion;
            _refreshEmotionButtons();
            _ = _safeSaveEmotionAfter(emotion);
        }

        private async Task _safeSaveEmotionAfter(EmotionType emotion)
        {
            try
            {
                var session = _outcome?.Session;
                if (session == null) return;

                session.EmotionAfter = emotion;
                await Repo.UpdateMinigameSession(session);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PostMinigameScreen] _safeSaveEmotionAfter: {ex.Message}");
                ToastNotification.ShowError("No se pudo guardar cómo te sientes");
            }
        }

        private void _onPlayAgainClicked() => _ = _safePlayAgain();

        private async Task _safePlayAgain()
        {
            if (_navigating || _outcome?.Session == null) return;
            _navigating = true;

            try
            {
                var session = _outcome.Session;
                EmotionType emotion = _selectedEmotion ?? session.EmotionBefore;

                // Volver primero a Minijuegos: la escena aditiva se dibuja encima de esa pantalla
                AppStateMachine.Instance.TransitionTo(AppState.Minigames);
                await Loader.LoadMinigame(session.MinigameId, emotion);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PostMinigameScreen] _safePlayAgain: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
            finally
            {
                _navigating = false;
            }
        }

        private void _onBackClicked()
        {
            if (_navigating) return;
            AppStateMachine.Instance.TransitionTo(AppState.Minigames);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private static string _buildRecordText(MinigameOutcome outcome)
        {
            if (!outcome.PreviousBestScore.HasValue)
                return "¡Primera partida!";

            int previous = MinigameOutcome.ToDisplayScore(outcome.PreviousBestScore.Value);
            return outcome.IsNewRecord
                ? $"Récord anterior: {previous}"
                : $"Récord: {previous}";
        }

        private static string _pickMessage(EmotionType emotion)
        {
            string[] pool = _emotionMessages.TryGetValue(emotion, out var msgs) && msgs.Length > 0
                ? msgs
                : _genericMessages;

            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private void _registerEmotionButtons()
        {
            if (_emotionButtons == null || _emotionTypes == null) return;

            int count = Mathf.Min(_emotionButtons.Length, _emotionTypes.Length);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                _emotionButtons[index]?.onClick.AddListener(() => _onEmotionSelected(_emotionTypes[index]));
            }
        }

        private void _refreshEmotionButtons()
        {
            if (_emotionButtons == null || _emotionTypes == null) return;

            int count = Mathf.Min(_emotionButtons.Length, _emotionTypes.Length);
            for (int i = 0; i < count; i++)
            {
                var button = _emotionButtons[i];
                if (button == null || button.image == null) continue;

                bool selected = _selectedEmotion.HasValue && _selectedEmotion.Value == _emotionTypes[i];
                button.image.color = selected ? _emotionSelectedColor : _emotionUnselectedColor;
            }
        }

        private static void _setText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
