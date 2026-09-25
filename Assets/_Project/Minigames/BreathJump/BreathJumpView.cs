using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Minigames
{
    /// <summary>
    /// HUD de BreathJump (vista pura): círculo guía de respiración, fundido por los bordes
    /// (viñeta), progreso, mensaje de valoración de cada respiración y diálogo de salida.
    /// Reenvía la entrada de BreathJumpInputArea como eventos para el controlador.
    ///
    /// Si _vignette no tiene sprite, se genera uno radial en tiempo de ejecución.
    /// </summary>
    public class BreathJumpView : MonoBehaviour
    {
        [Header("Entrada")]
        [SerializeField] private BreathJumpInputArea _inputArea;

        [Header("Respiración")]
        [SerializeField] private Image           _vignette;
        [SerializeField] private Color           _vignetteColor    = new Color(0.04f, 0.09f, 0.18f, 1f);
        [SerializeField, Range(0f, 1f)] private float _vignetteMaxAlpha = 0.6f;
        [SerializeField] private RectTransform   _guideRing;
        [SerializeField] private float           _guideMinScale    = 0.55f;
        [SerializeField] private float           _guideMaxScale    = 1f;
        [Tooltip("Color del círculo con los pulmones vacíos / al terminar de inspirar (hay que soltar)")]
        [SerializeField] private Color           _guideEmptyColor  = Color.white;
        [SerializeField] private Color           _guideFullColor   = new Color(0.9f, 0.15f, 0.15f, 1f);
        [SerializeField] private TextMeshProUGUI _guideLabel;

        [Header("Progreso y valoración")]
        [SerializeField] private TextMeshProUGUI _progressLabel;
        [SerializeField] private TextMeshProUGUI _scoreLabel;
        [SerializeField] private TextMeshProUGUI _feedbackLabel;
        [SerializeField] private CanvasGroup     _feedbackGroup;
        [SerializeField] private float           _feedbackSeconds  = 1.6f;

        [Header("Salida (el diálogo pausa la partida)")]
        [SerializeField] private Button     _exitButton;
        [SerializeField] private GameObject _exitConfirmPanel;
        [SerializeField] private Button     _exitConfirmButton;
        [SerializeField] private Button     _exitCancelButton;

        private const int VignetteTextureSize = 128;

        // ── Eventos ────────────────────────────────────────────────────

        public event Action OnHoldStarted;
        public event Action OnHoldEnded;
        public event Action OnExitRequested;
        public event Action OnExitConfirmed;
        public event Action OnExitCancelled;

        private BreathGuidePhase _guidePhase = (BreathGuidePhase)(-1);
        private int   _lastProgress = -1;
        private int   _lastScore    = -1;
        private Image _guideRingImage;
        private float _feedbackTimer;
        private Texture2D _vignetteTexture;
        private Sprite    _vignetteSprite;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_inputArea != null)
            {
                _inputArea.OnHoldStarted += _onHoldStarted;
                _inputArea.OnHoldEnded   += _onHoldEnded;
            }

            if (_exitButton != null)        _exitButton.onClick.AddListener(() => OnExitRequested?.Invoke());
            if (_exitConfirmButton != null) _exitConfirmButton.onClick.AddListener(() => OnExitConfirmed?.Invoke());
            if (_exitCancelButton != null)  _exitCancelButton.onClick.AddListener(() => OnExitCancelled?.Invoke());

            if (_feedbackGroup == null && _feedbackLabel != null)
            {
                _feedbackGroup = _feedbackLabel.GetComponent<CanvasGroup>();
                if (_feedbackGroup == null) _feedbackGroup = _feedbackLabel.gameObject.AddComponent<CanvasGroup>();
            }

            if (_guideRing != null) _guideRingImage = _guideRing.GetComponent<Image>();

            _setupVignette();
        }

        private void OnDestroy()
        {
            if (_inputArea != null)
            {
                _inputArea.OnHoldStarted -= _onHoldStarted;
                _inputArea.OnHoldEnded   -= _onHoldEnded;
            }

            OnHoldStarted   = null;
            OnHoldEnded     = null;
            OnExitRequested = null;
            OnExitConfirmed = null;
            OnExitCancelled = null;

            if (_vignetteSprite != null)  Destroy(_vignetteSprite);
            if (_vignetteTexture != null) Destroy(_vignetteTexture);
        }

        // ── API pública ────────────────────────────────────────────────

        public void Initialize(int totalBreaths)
        {
            _lastProgress = -1;
            _lastScore    = -1;
            SetProgress(0, totalBreaths);
            SetScore(0);
            SetBreath(0f);
            SetGuidePhase(BreathGuidePhase.Ready);
            ShowExitConfirm(false);

            _feedbackTimer = 0f;
            if (_feedbackGroup != null) _feedbackGroup.alpha = 0f;
        }

        /// <summary>
        /// 0 = pulmones vacíos, 1 = inspiración completa: viñeta, tamaño del círculo guía y su
        /// color (blanco → rojo al inspirar; rojo máximo = soltar; vuelve a blanco al espirar).
        /// </summary>
        public void SetBreath(float breath01)
        {
            float eased = Mathf.SmoothStep(0f, 1f, breath01);

            if (_vignette != null)
            {
                var color = _vignetteColor;
                color.a = _vignetteMaxAlpha * eased;
                _vignette.color = color;
            }

            if (_guideRing != null)
                _guideRing.localScale = Vector3.one * Mathf.Lerp(_guideMinScale, _guideMaxScale, eased);

            if (_guideRingImage != null)
                _guideRingImage.color = Color.Lerp(_guideEmptyColor, _guideFullColor, eased);
        }

        public void SetGuidePhase(BreathGuidePhase phase)
        {
            if (phase == _guidePhase) return;
            _guidePhase = phase;

            if (_guideLabel != null) _guideLabel.text = _guideText(phase);
        }

        public void SetProgress(int completed, int total)
        {
            if (_progressLabel == null || completed == _lastProgress) return;
            _lastProgress = completed;
            _progressLabel.text = $"{completed} / {total}";
        }

        /// <summary>Puntuación 0-100; solo reescribe el texto si cambia.</summary>
        public void SetScore(int score)
        {
            if (_scoreLabel == null || score == _lastScore) return;
            _lastScore = score;
            _scoreLabel.text = $"Puntuación: {score}";
        }

        public void ShowFeedback(BreathQuality quality)
        {
            if (_feedbackLabel == null) return;

            _feedbackLabel.text = _feedbackText(quality);
            _feedbackTimer = _feedbackSeconds;
            if (_feedbackGroup != null) _feedbackGroup.alpha = 1f;
        }

        public void ShowExitConfirm(bool show)
        {
            if (_exitConfirmPanel != null) _exitConfirmPanel.SetActive(show);
        }

        /// <summary>Desvanece el mensaje de valoración.</summary>
        public void Tick(float deltaTime)
        {
            if (_feedbackTimer <= 0f || _feedbackGroup == null) return;

            _feedbackTimer = Mathf.Max(0f, _feedbackTimer - deltaTime);
            _feedbackGroup.alpha = Mathf.Clamp01(_feedbackTimer / (_feedbackSeconds * 0.4f));
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _onHoldStarted() => OnHoldStarted?.Invoke();
        private void _onHoldEnded()   => OnHoldEnded?.Invoke();

        private static string _guideText(BreathGuidePhase phase)
        {
            switch (phase)
            {
                case BreathGuidePhase.Ready:   return "Mantén pulsado e inspira";
                case BreathGuidePhase.Inhale:  return "Inspira...";
                case BreathGuidePhase.Release: return "Suelta y espira";
                case BreathGuidePhase.Exhale:  return "Espira...";
                case BreathGuidePhase.Goal:    return "¡Has llegado!";
                default:                       return string.Empty;
            }
        }

        private static string _feedbackText(BreathQuality quality)
        {
            switch (quality)
            {
                case BreathQuality.Perfect:   return "¡Respiración perfecta!";
                case BreathQuality.Good:      return "Muy bien";
                case BreathQuality.OffRhythm: return "Sigue el ritmo del círculo";
                default:                      return "Sin prisa, otra vez";
            }
        }

        /// <summary>Viñeta radial: transparente en el centro y opaca en los bordes.</summary>
        private void _setupVignette()
        {
            if (_vignette == null) return;

            _vignette.raycastTarget = false;
            if (_vignette.sprite != null) return;

            _vignetteTexture = new Texture2D(VignetteTextureSize, VignetteTextureSize, TextureFormat.RGBA32, false)
            {
                name     = "BreathJumpVignette",
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[VignetteTextureSize * VignetteTextureSize];
            float half = (VignetteTextureSize - 1) * 0.5f;
            for (int y = 0; y < VignetteTextureSize; y++)
            {
                for (int x = 0; x < VignetteTextureSize; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy); // 1 = centro de cada borde
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.15f, distance));
                    pixels[y * VignetteTextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            _vignetteTexture.SetPixels32(pixels);
            _vignetteTexture.Apply(false, true);

            _vignetteSprite = Sprite.Create(_vignetteTexture,
                                            new Rect(0, 0, VignetteTextureSize, VignetteTextureSize),
                                            new Vector2(0.5f, 0.5f));
            _vignette.sprite = _vignetteSprite;
        }
    }
}
