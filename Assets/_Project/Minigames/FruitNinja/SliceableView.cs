using UnityEngine;
using UnityEngine.UI;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Elemento cortable de FruitNinja (UI). Movimiento balístico simple que avanza
    /// FruitNinjaField con Tick(). Coordenadas = anchoredPosition respecto al centro del campo.
    ///
    /// Al cortarlo, el elemento gira para que su eje X quede alineado con el trazo y se muestran
    /// dos mitades (Image Filled vertical al 50 %: mitad inferior y superior) que se separan en
    /// perpendicular al corte mientras se desvanecen.
    ///
    /// Puede ser un prefab vacío (solo este componente): Body, HalfA, HalfB y Marker se crean
    /// solos si no están asignados. Marker es el aro que distingue a los especiales.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SliceableView : MonoBehaviour
    {
        [SerializeField] private Image _body;
        [SerializeField] private Image _halfA;
        [SerializeField] private Image _halfB;
        [SerializeField] private Image _marker;

        [Header("Corte")]
        [SerializeField] private float _sliceFadeSeconds    = 0.6f;
        [SerializeField] private float _halfSeparationSpeed = 280f;
        [SerializeField] private float _halfSpinDegrees     = 60f;

        [Header("Especial")]
        [SerializeField] private float _markerScale = 1.35f;
        [SerializeField] private float _markerPulse = 0.08f;

        private RectTransform _rect;
        private CanvasGroup   _group;
        private Vector2 _velocity;
        private float   _gravity;
        private float   _spin;
        private float   _angle;
        private float   _age;
        private float   _sliceTime;
        private bool    _built;

        public SliceableKind Kind     { get; private set; }
        public float         Radius   { get; private set; }
        public bool          IsSliced { get; private set; }
        public Vector2       Position => _rect.anchoredPosition;
        public Vector2       Velocity => _velocity;
        public bool          IsFinished => IsSliced && _sliceTime >= _sliceFadeSeconds;

        /// <summary>Crea las partes que falten. Lo llama FruitNinjaField una vez por instancia.</summary>
        public void Build(Sprite fallbackSprite, Sprite ringSprite)
        {
            if (_built) return;
            _built = true;

            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable   = false;

            if (_marker == null) _marker = _createImage("Marker", ringSprite);
            if (_body == null)   _body   = _createImage("Body", fallbackSprite);
            if (_halfA == null)  _halfA  = _createImage("HalfA", fallbackSprite);
            if (_halfB == null)  _halfB  = _createImage("HalfB", fallbackSprite);

            _setupHalf(_halfA, Image.OriginVertical.Bottom);
            _setupHalf(_halfB, Image.OriginVertical.Top);
        }

        /// <summary>Aspecto y tamaño (diámetro en unidades del Canvas).</summary>
        public void Setup(Sprite sprite, Color color, float diameter, SliceableKind kind, Color markerColor)
        {
            Kind     = kind;
            Radius   = diameter * 0.5f;
            IsSliced = false;
            _age = _sliceTime = _angle = 0f;

            _rect.sizeDelta = new Vector2(diameter, diameter);
            _rect.localRotation = Quaternion.identity;
            _rect.localScale = Vector3.one;
            _group.alpha = 1f;

            _resetPart(_body, sprite, color);
            _resetPart(_halfA, sprite, color);
            _resetPart(_halfB, sprite, color);

            _body.enabled  = true;
            _halfA.enabled = false;
            _halfB.enabled = false;

            _marker.enabled = kind != SliceableKind.Normal;
            _marker.color   = markerColor;
            _marker.rectTransform.localScale = Vector3.one * _markerScale;
        }

        public void Launch(Vector2 position, Vector2 velocity, float gravity, float spin)
        {
            _rect.anchoredPosition = position;
            _velocity = velocity;
            _gravity  = gravity;
            _spin     = spin;
        }

        public void Tick(float deltaTime)
        {
            _age += deltaTime;
            _velocity.y -= _gravity * deltaTime;
            _rect.anchoredPosition += _velocity * deltaTime;
            _angle += _spin * deltaTime;
            _rect.localRotation = Quaternion.Euler(0f, 0f, _angle);

            if (_marker.enabled)
                _marker.rectTransform.localScale = Vector3.one * (_markerScale + Mathf.Sin(_age * 6f) * _markerPulse);

            if (!IsSliced) return;

            _sliceTime += deltaTime;
            float offset = _halfSeparationSpeed * _sliceTime;
            float tilt   = _halfSpinDegrees * _sliceTime;
            _halfA.rectTransform.localPosition = new Vector3(0f, -offset, 0f);
            _halfB.rectTransform.localPosition = new Vector3(0f,  offset, 0f);
            _halfA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -tilt);
            _halfB.rectTransform.localRotation = Quaternion.Euler(0f, 0f,  tilt);
            _group.alpha = 1f - Mathf.Clamp01(_sliceTime / _sliceFadeSeconds);
        }

        /// <summary>Corta el elemento en la dirección del trazo.</summary>
        public void Slice(Vector2 direction)
        {
            if (IsSliced) return;

            IsSliced   = true;
            _sliceTime = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                _angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _spin *= 0.3f;
            _rect.localRotation = Quaternion.Euler(0f, 0f, _angle);

            _body.enabled   = false;
            _marker.enabled = false;
            _halfA.enabled  = true;
            _halfB.enabled  = true;
        }

        // ── Helpers privados ───────────────────────────────────────────

        private Image _createImage(string childName, Sprite sprite)
        {
            var go = new GameObject(childName, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static void _resetPart(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.color  = color;
            image.rectTransform.localPosition = Vector3.zero;
            image.rectTransform.localRotation = Quaternion.identity;
        }

        private static void _setupHalf(Image half, Image.OriginVertical origin)
        {
            half.type          = Image.Type.Filled;
            half.fillMethod    = Image.FillMethod.Vertical;
            half.fillOrigin    = (int)origin;
            half.fillAmount    = 0.5f;
            half.raycastTarget = false;
            half.enabled       = false;
        }
    }
}
