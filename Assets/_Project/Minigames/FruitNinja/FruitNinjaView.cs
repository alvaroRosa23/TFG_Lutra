using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// HUD de FruitNinja (vista pura): tiempo restante, puntuación, racha, contador de combo,
    /// mensajes (fase, especiales, final), destello al caerse un elemento, zoom de combo grande
    /// sobre _zoomRoot y diálogo de salida. Todas las animaciones usan tiempo real.
    ///
    /// _zoomRoot es el contenedor a pantalla completa de los elementos y la estela (pivote en el
    /// centro); el zoom escala ese contenedor dejando fijo el punto del combo.
    /// </summary>
    public class FruitNinjaView : MonoBehaviour
    {
        [Header("Zoom de combo grande")]
        [SerializeField] private RectTransform _zoomRoot;
        [SerializeField] private float _zoomScale       = 1.25f;
        [SerializeField] private float _zoomInSeconds   = 0.06f;
        [SerializeField] private float _zoomHoldSeconds = 0.12f;
        [SerializeField] private float _zoomOutSeconds  = 0.35f;

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI _timerLabel;
        [SerializeField] private TextMeshProUGUI _scoreLabel;
        [SerializeField] private TextMeshProUGUI _streakLabel;

        [Header("Mensajes")]
        [SerializeField] private TextMeshProUGUI _messageLabel;
        [SerializeField] private float           _messageSeconds = 1.8f;
        [SerializeField] private TextMeshProUGUI _comboLabel;
        [SerializeField] private float           _comboSeconds   = 0.9f;

        [Header("Caída (opcional: Image a pantalla completa, Raycast Target off)")]
        [SerializeField] private Image _missFlash;
        [SerializeField] private Color _missFlashColor   = new Color(1f, 0.25f, 0.25f, 0.35f);
        [SerializeField] private float _missFlashSeconds = 0.35f;

        [Header("Salida (el diálogo pausa la partida)")]
        [SerializeField] private Button     _exitButton;
        [SerializeField] private GameObject _exitConfirmPanel;
        [SerializeField] private Button     _exitConfirmButton;
        [SerializeField] private Button     _exitCancelButton;

        public event Action OnExitRequested;
        public event Action OnExitConfirmed;
        public event Action OnExitCancelled;

        private CanvasGroup _messageGroup;
        private CanvasGroup _comboGroup;
        private float _messageTimer;
        private float _comboTimer;
        private float _missTimer;

        private bool    _zooming;
        private float   _zoomTime;
        private Vector2 _zoomPoint;
        private Vector2 _zoomBasePosition;

        private int _lastSeconds = -1;
        private int _lastScore   = -1;
        private int _lastStreak  = -1;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_exitButton != null)        _exitButton.onClick.AddListener(() => OnExitRequested?.Invoke());
            if (_exitConfirmButton != null) _exitConfirmButton.onClick.AddListener(() => OnExitConfirmed?.Invoke());
            if (_exitCancelButton != null)  _exitCancelButton.onClick.AddListener(() => OnExitCancelled?.Invoke());

            _messageGroup = _ensureGroup(_messageLabel);
            _comboGroup   = _ensureGroup(_comboLabel);

            if (_missFlash != null) _missFlash.raycastTarget = false;
            if (_zoomRoot != null)  _zoomBasePosition = _zoomRoot.anchoredPosition;
        }

        private void OnDestroy()
        {
            OnExitRequested = null;
            OnExitConfirmed = null;
            OnExitCancelled = null;
        }

        // ── API pública ────────────────────────────────────────────────

        public void Initialize(float durationSeconds)
        {
            _lastSeconds = _lastScore = _lastStreak = -1;
            SetTimeLeft(durationSeconds);
            SetScore(0);
            SetStreak(0);
            ShowExitConfirm(false);

            _messageTimer = _comboTimer = _missTimer = 0f;
            if (_messageGroup != null) _messageGroup.alpha = 0f;
            if (_comboGroup != null)   _comboGroup.alpha = 0f;
            _applyMissFlash(0f);

            _zooming = false;
            _applyZoom(1f);
        }

        public void SetTimeLeft(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.CeilToInt(seconds));
            if (_timerLabel == null || whole == _lastSeconds) return;

            _lastSeconds = whole;
            _timerLabel.text = $"{whole / 60}:{whole % 60:00}";
        }

        /// <summary>Puntuación 0-100; solo reescribe el texto si cambia.</summary>
        public void SetScore(int score)
        {
            if (_scoreLabel == null || score == _lastScore) return;
            _lastScore = score;
            _scoreLabel.text = $"Puntuación: {score}";
        }

        public void SetStreak(int streak)
        {
            if (_streakLabel == null || streak == _lastStreak) return;
            _lastStreak = streak;
            _streakLabel.text = $"Racha: {streak}";
        }

        public void ShowPhase(FruitNinjaPhase phase)
        {
            switch (phase)
            {
                case FruitNinjaPhase.Release:    ShowMessage("¡Córtalo todo!"); break;
                case FruitNinjaPhase.Transition: ShowMessage("Poco a poco, más despacio..."); break;
                case FruitNinjaPhase.Calm:       ShowMessage("Con calma. Respira."); break;
            }
        }

        public void ShowSpecial(SliceableKind kind)
        {
            switch (kind)
            {
                case SliceableKind.Burst:      ShowMessage("¡Ráfaga!"); break;
                case SliceableKind.Rain:       ShowMessage("¡Lluvia!"); break;
                case SliceableKind.Crossfire:  ShowMessage("¡Desde los lados!"); break;
                case SliceableKind.SlowMotion: ShowMessage("Cámara lenta"); break;
            }
        }

        public void ShowMessage(string text)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = text;
            _messageTimer = _messageSeconds;
            if (_messageGroup != null) _messageGroup.alpha = 1f;
        }

        public void ShowCombo(int count)
        {
            if (_comboLabel == null) return;
            _comboLabel.text = $"Combo x{count}";
            _comboTimer = _comboSeconds;
            if (_comboGroup != null) _comboGroup.alpha = 1f;
        }

        public void FlashMiss() => _missTimer = _missFlashSeconds;

        /// <summary>Zoom rápido hacia un punto del campo (coordenadas respecto a su centro).</summary>
        public void PunchZoom(Vector2 centerRelativePoint)
        {
            if (_zoomRoot == null) return;
            _zooming   = true;
            _zoomTime  = 0f;
            _zoomPoint = centerRelativePoint;
        }

        public void ShowExitConfirm(bool show)
        {
            if (_exitConfirmPanel != null) _exitConfirmPanel.SetActive(show);
        }

        /// <summary>Avanza las animaciones (tiempo real).</summary>
        public void Tick(float deltaTime)
        {
            _messageTimer = _fade(_messageGroup, _messageTimer, _messageSeconds, deltaTime);
            _comboTimer   = _fade(_comboGroup, _comboTimer, _comboSeconds, deltaTime);

            if (_missTimer > 0f)
            {
                _missTimer = Mathf.Max(0f, _missTimer - deltaTime);
                _applyMissFlash(_missTimer / _missFlashSeconds);
            }

            if (_zooming) _tickZoom(deltaTime);
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _tickZoom(float deltaTime)
        {
            _zoomTime += deltaTime;
            float t = _zoomTime;
            float scale;

            if (t < _zoomInSeconds)
            {
                float k = t / _zoomInSeconds;
                scale = Mathf.Lerp(1f, _zoomScale, 1f - (1f - k) * (1f - k));
            }
            else if ((t -= _zoomInSeconds) < _zoomHoldSeconds)
            {
                scale = _zoomScale;
            }
            else if ((t -= _zoomHoldSeconds) < _zoomOutSeconds)
            {
                scale = Mathf.Lerp(_zoomScale, 1f, Mathf.SmoothStep(0f, 1f, t / _zoomOutSeconds));
            }
            else
            {
                scale = 1f;
                _zooming = false;
            }

            _applyZoom(scale);
        }

        /// <summary>Escala _zoomRoot manteniendo _zoomPoint en el mismo sitio de la pantalla.</summary>
        private void _applyZoom(float scale)
        {
            if (_zoomRoot == null) return;

            Vector2 pivotRelative = _zoomPoint + _zoomRoot.rect.center;
            _zoomRoot.localScale = new Vector3(scale, scale, 1f);
            _zoomRoot.anchoredPosition = _zoomBasePosition + pivotRelative * (1f - scale);
        }

        private void _applyMissFlash(float intensity)
        {
            if (_missFlash == null) return;

            var color = _missFlashColor;
            color.a *= Mathf.Clamp01(intensity);
            _missFlash.color = color;
        }

        /// <summary>Opaco al principio y se desvanece en el último 40 % del tiempo.</summary>
        private static float _fade(CanvasGroup group, float timer, float duration, float deltaTime)
        {
            if (timer <= 0f || group == null) return timer;

            timer = Mathf.Max(0f, timer - deltaTime);
            group.alpha = Mathf.Clamp01(timer / (duration * 0.4f));
            return timer;
        }

        private static CanvasGroup _ensureGroup(Component label)
        {
            if (label == null) return null;

            var group = label.GetComponent<CanvasGroup>();
            if (group == null) group = label.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            return group;
        }
    }
}
