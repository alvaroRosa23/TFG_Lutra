using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.Features.StarCollection;

namespace Lutra.Minigames
{
    /// <summary>Datos que muestra la ficha de una estrella recién pescada.</summary>
    public struct StarRevealData
    {
        public StarDefinition Star;
        public Color          RarityColor;
        public bool           IsNew;
        public int            TimesCaught;
    }

    /// <summary>
    /// Vista de StarFisher (sin lógica de juego): astronauta (3 poses), HUD, barra de fuerza,
    /// aviso de picada, zoom y viñeta de color de la recogida, destello blanco, ficha de la
    /// estrella, fase de liberación, botón del libro y diálogo de salida.
    ///
    /// _zoomRoot es el contenedor a pantalla completa de la escena (pivote en el centro); el zoom
    /// lo escala dejando fijo el punto de la estrella enganchada.
    /// </summary>
    public class StarFisherView : MonoBehaviour
    {
        [Header("Escena")]
        [SerializeField] private RectTransform _zoomRoot;
        [SerializeField] private float         _zoomSmoothing = 6f;
        [SerializeField] private float         _zoomPunch     = 0.06f;

        [Header("Astronauta")]
        [SerializeField] private Image  _astronaut;
        [SerializeField] private Sprite _poseCast;
        [SerializeField] private Sprite _poseWait;
        [SerializeField] private Sprite _poseReel;

        [Header("HUD (se oculta al liberar la estrella)")]
        [SerializeField] private CanvasGroup     _hud;
        [SerializeField] private TextMeshProUGUI _castsLabel;
        [SerializeField] private TextMeshProUGUI _hintLabel;
        [SerializeField] private TextMeshProUGUI _messageLabel;
        [SerializeField] private float           _messageSeconds = 2f;

        [Header("Barra de fuerza")]
        [SerializeField] private GameObject _powerBar;
        [SerializeField] private Image      _powerFill;
        [Tooltip("Opcional: marca del tramo perfecto; se coloca sola según perfectZone (anchors verticales)")]
        [SerializeField] private RectTransform _powerPerfectZone;
        [SerializeField] private Color _powerColor        = new Color(0.55f, 0.75f, 1f);
        [SerializeField] private Color _powerPerfectColor = new Color(1f, 0.85f, 0.3f);

        [Header("Picada")]
        [SerializeField] private GameObject _biteIndicator;
        [SerializeField] private Image      _biteTimerFill;

        [Header("Recogida")]
        [Tooltip("Opcional: barra que se vacía mientras no se toca (1,5 s para que escape)")]
        [SerializeField] private Image _reelIdleFill;
        [Tooltip("Image a pantalla completa con un sprite de bordes (si no hay, se genera)")]
        [SerializeField] private Image _vignette;
        [SerializeField, Range(0f, 1f)] private float _vignetteAlpha      = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _vignettePulseAlpha = 0.4f;
        [SerializeField] private float _vignettePulseSeconds = 0.6f;

        [Header("Captura")]
        [SerializeField] private Image _whiteFlash;

        [Header("Ficha de la estrella")]
        [SerializeField] private GameObject      _revealPanel;
        [SerializeField] private Image           _revealImage;
        [SerializeField] private TextMeshProUGUI _revealName;
        [SerializeField] private TextMeshProUGUI _revealRarity;
        [SerializeField] private TextMeshProUGUI _revealWeight;
        [SerializeField] private TextMeshProUGUI _revealAge;
        [SerializeField] private TextMeshProUGUI _revealDescription;
        [SerializeField] private TextMeshProUGUI _revealPhrase;
        [SerializeField] private TextMeshProUGUI _revealTimesCaught;
        [Tooltip("\"¡Nueva estrella! Añadida a tu colección\"")]
        [SerializeField] private GameObject      _revealNewBadge;
        [SerializeField] private Button          _releaseButton;

        [Header("Liberación")]
        [SerializeField] private GameObject            _releasePanel;
        [SerializeField] private Image                 _releaseStar;
        [SerializeField] private StarFisherReleaseDrag _releaseDrag;
        [SerializeField] private TextMeshProUGUI       _releaseHint;

        [Header("Libro y salida")]
        [SerializeField] private Button     _bookButton;
        [SerializeField] private Button     _exitButton;
        [SerializeField] private GameObject _exitConfirmPanel;
        [SerializeField] private Button     _exitConfirmButton;
        [SerializeField] private Button     _exitCancelButton;

        public event Action OnReleaseClicked;
        /// <summary>La estrella empieza a subir hacia el cielo.</summary>
        public event Action OnStarReleased;
        public event Action OnBookRequested;
        public event Action OnExitRequested;
        public event Action OnExitConfirmed;
        public event Action OnExitCancelled;

        private CanvasGroup _messageGroup;
        private float _messageTimer;

        private Vector2 _zoomBasePosition;
        private float   _zoomTarget = 1f;
        private float   _zoomCurrent = 1f;
        private float   _zoomKick;
        private Vector2 _zoomPoint;

        private float _flashFadeSeconds;
        private float _flashFadeLeft;

        private Color _vignetteColor;
        private float _vignetteBase;
        private float _vignettePulse;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_releaseButton != null)     _releaseButton.onClick.AddListener(() => OnReleaseClicked?.Invoke());
            if (_bookButton != null)        _bookButton.onClick.AddListener(() => OnBookRequested?.Invoke());
            if (_exitButton != null)        _exitButton.onClick.AddListener(() => OnExitRequested?.Invoke());
            if (_exitConfirmButton != null) _exitConfirmButton.onClick.AddListener(() => OnExitConfirmed?.Invoke());
            if (_exitCancelButton != null)  _exitCancelButton.onClick.AddListener(() => OnExitCancelled?.Invoke());
            if (_releaseDrag != null)       _releaseDrag.OnLaunched += _onReleaseDragLaunched;

            if (_messageLabel != null)
            {
                _messageGroup = _messageLabel.GetComponent<CanvasGroup>();
                if (_messageGroup == null) _messageGroup = _messageLabel.gameObject.AddComponent<CanvasGroup>();
                _messageGroup.blocksRaycasts = false;
            }

            if (_vignette != null)
            {
                if (_vignette.sprite == null) _vignette.sprite = StarSpriteFactory.Vignette;
                _vignette.raycastTarget = false;
            }
            if (_whiteFlash != null) _whiteFlash.raycastTarget = false;
            if (_zoomRoot != null)   _zoomBasePosition = _zoomRoot.anchoredPosition;
        }

        private void OnDestroy()
        {
            if (_releaseDrag != null) _releaseDrag.OnLaunched -= _onReleaseDragLaunched;

            OnReleaseClicked = null;
            OnStarReleased   = null;
            OnBookRequested  = null;
            OnExitRequested  = null;
            OnExitConfirmed  = null;
            OnExitCancelled  = null;
        }

        // ── API pública: general ───────────────────────────────────────

        public void Initialize(StarFisherTuning tuning)
        {
            ShowPowerBar(false);
            ShowBite(false);
            SetReelIdle(-1f);
            ResetReelEffects(immediate: true);
            SetWhiteFlash(0f);
            HideReveal();
            EndRelease();
            ShowExitConfirm(false);
            SetHudVisible(true);
            SetBookButtonVisible(true);

            _messageTimer = 0f;
            if (_messageGroup != null) _messageGroup.alpha = 0f;

            _placePerfectZone(tuning.perfectZone);
        }

        public void SetPose(AstronautPose pose)
        {
            if (_astronaut == null) return;

            Sprite sprite = pose switch
            {
                AstronautPose.Cast => _poseCast,
                AstronautPose.Wait => _poseWait,
                _                  => _poseReel
            };
            if (sprite != null) _astronaut.sprite = sprite;
        }

        public void SetCasts(int current, int total)
        {
            if (_castsLabel != null) _castsLabel.text = $"Lanzamiento {current} de {total}";
        }

        public void SetHint(string text)
        {
            if (_hintLabel == null) return;
            _hintLabel.text = text ?? string.Empty;
            _hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void ShowMessage(string text)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = text;
            _messageTimer = _messageSeconds;
            if (_messageGroup != null) _messageGroup.alpha = 1f;
        }

        public void SetHudVisible(bool visible)
        {
            if (_hud == null) return;
            _hud.alpha = visible ? 1f : 0f;
            _hud.blocksRaycasts = visible;
            _hud.interactable = visible;
        }

        public void SetBookButtonVisible(bool visible)
        {
            if (_bookButton != null) _bookButton.gameObject.SetActive(visible);
        }

        public void ShowExitConfirm(bool show)
        {
            if (_exitConfirmPanel != null) _exitConfirmPanel.SetActive(show);
        }

        // ── Lanzamiento y picada ───────────────────────────────────────

        public void ShowPowerBar(bool show)
        {
            if (_powerBar != null) _powerBar.SetActive(show);
        }

        public void SetPower(float power, bool inPerfectZone)
        {
            if (_powerFill == null) return;
            _powerFill.fillAmount = power;
            _powerFill.color = inPerfectZone ? _powerPerfectColor : _powerColor;
        }

        public void ShowBite(bool show)
        {
            if (_biteIndicator != null) _biteIndicator.SetActive(show);
        }

        /// <summary>Tiempo restante de la picada 0-1 (&lt; 0 = sin límite: estrella de racha).</summary>
        public void SetBiteTimeLeft(float fraction)
        {
            if (_biteTimerFill == null) return;
            _biteTimerFill.enabled = fraction >= 0f;
            _biteTimerFill.fillAmount = Mathf.Clamp01(fraction);
        }

        // ── Recogida ───────────────────────────────────────────────────

        /// <summary>Tiempo que queda antes de que escape, 0-1 (&lt; 0 oculta la barra).</summary>
        public void SetReelIdle(float fraction)
        {
            if (_reelIdleFill == null) return;
            _reelIdleFill.gameObject.SetActive(fraction >= 0f);
            _reelIdleFill.fillAmount = Mathf.Clamp01(fraction);
        }

        /// <summary>
        /// Nivel de la recogida: zoom acumulado hacia la estrella y viñeta del color de la rareza
        /// mínima que ya se sabe que tiene. pulse = se acaba de superar un umbral (golpe de zoom
        /// y destello de la viñeta).
        /// </summary>
        public void SetReelTier(float zoom, Color rarityColor, Vector2 focusWorldPosition, bool pulse)
        {
            _zoomTarget = zoom;
            if (_zoomRoot != null)
                _zoomPoint = _zoomRoot.InverseTransformPoint(focusWorldPosition);

            _vignetteColor = rarityColor;
            _vignetteBase  = _vignetteAlpha;
            if (pulse)
            {
                _vignettePulse = _vignettePulseSeconds;
                _zoomKick      = _zoomPunch;
            }
        }

        public void ResetReelEffects(bool immediate = false)
        {
            _zoomTarget    = 1f;
            _zoomKick      = 0f;
            _vignetteBase  = 0f;
            _vignettePulse = 0f;

            if (!immediate) return;
            _zoomCurrent = 1f;
            _applyZoom(1f);
            _applyVignette(0f);
        }

        public void SetWhiteFlash(float alpha)
        {
            _flashFadeLeft = 0f;
            _applyWhiteFlash(alpha);
        }

        /// <summary>Desvanece el destello blanco por su cuenta (independiente de la fase del juego).</summary>
        public void FadeOutWhiteFlash(float seconds)
        {
            if (seconds <= 0f)
            {
                SetWhiteFlash(0f);
                return;
            }

            _flashFadeSeconds = seconds;
            _flashFadeLeft    = seconds;
        }

        private void _applyWhiteFlash(float alpha)
        {
            if (_whiteFlash == null) return;
            _whiteFlash.gameObject.SetActive(alpha > 0f);
            _whiteFlash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        // ── Ficha y liberación ─────────────────────────────────────────

        public void ShowReveal(StarRevealData data)
        {
            if (_revealPanel == null) return;

            var star = data.Star;
            _setStarImage(_revealImage, star);
            _setText(_revealName, star.displayName);
            if (_revealRarity != null)
            {
                _revealRarity.text  = star.isStreakSpecial ? $"{star.rarity.ToDisplayName()} · Estrella de racha" : star.rarity.ToDisplayName();
                _revealRarity.color = data.RarityColor;
            }
            _setText(_revealWeight,      string.IsNullOrEmpty(star.weight) ? string.Empty : $"Peso: {star.weight}");
            _setText(_revealAge,         string.IsNullOrEmpty(star.age) ? string.Empty : $"Edad: {star.age}");
            _setText(_revealDescription, star.description);
            _setText(_revealPhrase,      string.IsNullOrEmpty(star.phrase) ? string.Empty : $"“{star.phrase}”");
            _setText(_revealTimesCaught, data.TimesCaught <= 1 ? "Pescada 1 vez" : $"Pescada {data.TimesCaught} veces");
            if (_revealNewBadge != null) _revealNewBadge.SetActive(data.IsNew);

            _revealPanel.SetActive(true);
        }

        public void HideReveal()
        {
            if (_revealPanel != null) _revealPanel.SetActive(false);
        }

        /// <summary>
        /// Muestra el panel de liberación (con fondo propio que tapa la partida): solo la estrella y
        /// el texto; el jugador la arrastra hacia arriba.
        /// </summary>
        public void BeginRelease(StarDefinition star)
        {
            HideReveal();
            SetHudVisible(false);

            if (_releasePanel != null) _releasePanel.SetActive(true);
            _setStarImage(_releaseStar, star);
            if (_releaseHint != null)
            {
                _releaseHint.text = "Arrastra hacia arriba para soltarla";
                _releaseHint.gameObject.SetActive(true);
            }

            if (_releaseDrag != null) _releaseDrag.Arm();
            else OnStarReleased?.Invoke(); // sin arrastre configurado se libera directamente
        }

        public void EndRelease()
        {
            if (_releaseDrag != null) _releaseDrag.ResetPosition();
            if (_releasePanel != null) _releasePanel.SetActive(false);
            SetHudVisible(true);
        }

        /// <summary>Avanza las animaciones de la vista.</summary>
        public void Tick(float deltaTime)
        {
            if (_messageTimer > 0f && _messageGroup != null)
            {
                _messageTimer = Mathf.Max(0f, _messageTimer - deltaTime);
                _messageGroup.alpha = Mathf.Clamp01(_messageTimer / (_messageSeconds * 0.4f));
            }

            if (_flashFadeLeft > 0f)
            {
                _flashFadeLeft = Mathf.Max(0f, _flashFadeLeft - deltaTime);
                _applyWhiteFlash(_flashFadeLeft / _flashFadeSeconds);
            }

            // Zoom suavizado + golpe al superar un umbral
            _zoomKick    = Mathf.Lerp(_zoomKick, 0f, 1f - Mathf.Exp(-7f * deltaTime));
            _zoomCurrent = Mathf.Lerp(_zoomCurrent, _zoomTarget, 1f - Mathf.Exp(-_zoomSmoothing * deltaTime));
            _applyZoom(_zoomCurrent + _zoomKick);

            if (_vignettePulse > 0f) _vignettePulse = Mathf.Max(0f, _vignettePulse - deltaTime);
            float pulse = _vignettePulseSeconds > 0f ? _vignettePulse / _vignettePulseSeconds : 0f;
            _applyVignette(_vignetteBase + _vignettePulseAlpha * pulse);
        }

        // ── Helpers privados ───────────────────────────────────────────

        // Se avisa al empezar a subir (no al terminar) para que el destello se mezcle con la subida
        private void _onReleaseDragLaunched() => OnStarReleased?.Invoke();

        /// <summary>
        /// Escala _zoomRoot manteniendo _zoomPoint (coordenadas locales respecto al pivote, que no
        /// dependen de la escala) en el mismo sitio de la pantalla que sin zoom.
        /// </summary>
        private void _applyZoom(float scale)
        {
            if (_zoomRoot == null) return;

            _zoomRoot.localScale = new Vector3(scale, scale, 1f);
            _zoomRoot.anchoredPosition = _zoomBasePosition + _zoomPoint * (1f - scale);
        }

        private void _applyVignette(float alpha)
        {
            if (_vignette == null) return;
            var color = _vignetteColor;
            color.a = Mathf.Clamp01(alpha);
            _vignette.color = color;
            _vignette.enabled = color.a > 0.001f;
        }

        private void _placePerfectZone(Vector2 zone)
        {
            if (_powerPerfectZone == null) return;
            _powerPerfectZone.anchorMin = new Vector2(_powerPerfectZone.anchorMin.x, Mathf.Clamp01(zone.x));
            _powerPerfectZone.anchorMax = new Vector2(_powerPerfectZone.anchorMax.x, Mathf.Clamp01(zone.y));
            _powerPerfectZone.offsetMin = new Vector2(_powerPerfectZone.offsetMin.x, 0f);
            _powerPerfectZone.offsetMax = new Vector2(_powerPerfectZone.offsetMax.x, 0f);
        }

        private static void _setStarImage(Image image, StarDefinition star)
        {
            if (image == null || star == null) return;
            image.sprite = StarSpriteFactory.Or(star.sprite);
            image.color  = star.sprite != null ? Color.white : star.tint;
            image.preserveAspect = true;
        }

        private static void _setText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }
    }
}
