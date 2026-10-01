using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Features.StarCollection
{
    /// <summary>
    /// Pantalla del telescopio (SafeZone): cielo a pantalla completa en el que las estrellas de la
    /// colección aparecen y desaparecen cada una a su ritmo, en posiciones aleatorias. Cada
    /// estrella repite: oculta → aparece → brilla (late) → se apaga → cambia de sitio.
    /// Solo se ven las estrellas descubiertas.
    /// </summary>
    public class StarSkyView : MonoBehaviour
    {
        [SerializeField] private GameObject      _root;
        [SerializeField] private RectTransform   _skyArea;
        [SerializeField] private Button          _closeButton;
        [SerializeField] private TextMeshProUGUI _titleLabel;

        [Header("Estrellas")]
        [SerializeField] private Vector2 _starSize        = new Vector2(70f, 140f);
        [SerializeField] private Vector2 _hiddenSeconds   = new Vector2(0.5f, 4f);
        [SerializeField] private Vector2 _fadeSeconds     = new Vector2(0.8f, 1.6f);
        [SerializeField] private Vector2 _visibleSeconds  = new Vector2(2.5f, 6f);
        [SerializeField] private float   _twinkleAmount   = 0.12f;
        [SerializeField] private float   _glowScale       = 2.2f;
        [SerializeField, Range(0f, 1f)] private float _glowAlpha = 0.35f;

        private enum StarState { Hidden, FadingIn, Visible, FadingOut }

        private class SkyStar
        {
            public RectTransform Rect;
            public Image         Body;
            public Image         Glow;
            public Color         Color;
            public StarState     State;
            public float         Timer;
            public float         Duration;
            public float         Phase;
        }

        private readonly List<SkyStar> _stars = new List<SkyStar>();

        private void Awake()
        {
            if (_root == null) _root = gameObject;
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        }

        // ── API pública ────────────────────────────────────────────────

        public void Open(List<StarDefinition> stars, StarCatalog catalog)
        {
            _clear();
            _root.SetActive(true);

            if (_titleLabel != null)
                _titleLabel.text = stars.Count > 0 ? "Tus estrellas" : "Todavía no has pescado ninguna estrella";

            foreach (var star in stars)
                if (star != null) _stars.Add(_createStar(star, catalog));
        }

        public void Close()
        {
            _clear();
            _root.SetActive(false);
        }

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            foreach (var star in _stars) _tickStar(star, deltaTime);
        }

        // ── Privado ────────────────────────────────────────────────────

        private SkyStar _createStar(StarDefinition definition, StarCatalog catalog)
        {
            var parent = _skyArea != null ? _skyArea : (RectTransform)_root.transform;
            var color  = definition.sprite != null ? Color.white : definition.tint;

            var go = new GameObject(definition.starId, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var glow = _createImage("Glow", rect, StarSpriteFactory.Glow);
            glow.color = catalog != null ? catalog.GetRarityColor(definition.rarity) : color;

            var body = _createImage("Body", rect, StarSpriteFactory.Or(definition.sprite));
            body.preserveAspect = true;

            // El cuerpo ocupa toda la estrella; el brillo, centrado y más grande
            float size = Random.Range(_starSize.x, _starSize.y);
            rect.sizeDelta = new Vector2(size, size);
            var glowRect = glow.rectTransform;
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(size, size) * _glowScale;

            var star = new SkyStar
            {
                Rect = rect, Body = body, Glow = glow, Color = color,
                State = StarState.Hidden,
                // Arranque desfasado: cada estrella empieza en un momento distinto
                Duration = Random.Range(0f, _hiddenSeconds.y),
                Phase = Random.Range(0f, 10f)
            };
            _applyAlpha(star, 0f);
            return star;
        }

        private void _tickStar(SkyStar star, float deltaTime)
        {
            star.Timer += deltaTime;
            float t = star.Duration > 0f ? Mathf.Clamp01(star.Timer / star.Duration) : 1f;

            switch (star.State)
            {
                case StarState.Hidden:
                    _applyAlpha(star, 0f);
                    if (t >= 1f)
                    {
                        _moveToRandomPosition(star);
                        _setState(star, StarState.FadingIn, _fadeSeconds);
                    }
                    break;

                case StarState.FadingIn:
                    _applyAlpha(star, Mathf.SmoothStep(0f, 1f, t));
                    if (t >= 1f) _setState(star, StarState.Visible, _visibleSeconds);
                    break;

                case StarState.Visible:
                    star.Phase += deltaTime;
                    _applyAlpha(star, 1f - _twinkleAmount + _twinkleAmount * Mathf.Sin(star.Phase * 3f));
                    if (t >= 1f) _setState(star, StarState.FadingOut, _fadeSeconds);
                    break;

                case StarState.FadingOut:
                    _applyAlpha(star, 1f - Mathf.SmoothStep(0f, 1f, t));
                    if (t >= 1f) _setState(star, StarState.Hidden, _hiddenSeconds);
                    break;
            }
        }

        private static void _setState(SkyStar star, StarState state, Vector2 durationRange)
        {
            star.State    = state;
            star.Timer    = 0f;
            star.Duration = Random.Range(durationRange.x, durationRange.y);
        }

        private void _applyAlpha(SkyStar star, float alpha)
        {
            var body = star.Color;
            body.a *= alpha;
            star.Body.color = body;

            var glow = star.Glow.color;
            glow.a = alpha * _glowAlpha;
            star.Glow.color = glow;

            float scale = 0.85f + 0.15f * alpha;
            star.Rect.localScale = new Vector3(scale, scale, 1f);
        }

        private void _moveToRandomPosition(SkyStar star)
        {
            var area = ((RectTransform)star.Rect.parent).rect;
            float margin = star.Rect.sizeDelta.x * 0.5f;
            star.Rect.anchorMin = star.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            star.Rect.anchoredPosition = new Vector2(
                Random.Range(area.xMin + margin, area.xMax - margin) - area.center.x,
                Random.Range(area.yMin + margin, area.yMax - margin) - area.center.y);
        }

        private static Image _createImage(string name, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private void _clear()
        {
            foreach (var star in _stars)
                if (star.Rect != null) Destroy(star.Rect.gameObject);
            _stars.Clear();
        }
    }
}
