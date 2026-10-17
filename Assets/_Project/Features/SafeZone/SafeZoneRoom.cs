using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Habitación panorámica de la Zona Segura. El fondo es una sola imagen partida en 3 trozos
    /// iguales (uno por pantalla) que se desliza en horizontal con el dedo (ScrollRect).
    ///
    /// Jerarquía (la monta el menú Lutra > Zona Segura > Montar habitación panorámica):
    ///   Panel_Room       ← ScrollRect + SafeZoneRoom (pantalla completa)
    ///   └── Viewport     ← RectMask2D
    ///       └── RoomContent  ← tamaño del arte en píxeles (3 × 1080 × 1920)
    ///           ├── Background_1..3   (Image, una por trozo)
    ///           └── PlacementPoint0..N
    ///
    /// RoomContent mide lo mismo que el arte y se escala para llenar la altura de la pantalla,
    /// así el fondo no se deforma en ninguna resolución y todo lo que cuelga de él
    /// (puntos de colocación, objetos) se queda pegado al mismo sitio del dibujo.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(ScrollRect))]
    public class SafeZoneRoom : MonoBehaviour
    {
        [Header("Fondo")]
        [Tooltip("Contenedor que se desliza; mide lo mismo que el arte completo (en píxeles)")]
        [SerializeField] private RectTransform _content;
        [Tooltip("Las 3 imágenes del fondo, de izquierda a derecha")]
        [SerializeField] private Image[]       _backgroundPanels;
        [SerializeField] private Sprite[]      _daySprites;
        [SerializeField] private Sprite[]      _nightSprites;

        [Header("Día / noche (hora local)")]
        [Range(0, 23)] [SerializeField] private int _nightStartHour = 20;
        [Range(0, 23)] [SerializeField] private int _nightEndHour   = 7;

        [Header("Desplazamiento automático al arrastrar un objeto")]
        [Tooltip("Ancho de la franja de cada borde que desplaza la habitación (fracción del ancho visible)")]
        [Range(0.05f, 0.4f)] [SerializeField] private float _edgeZone        = 0.15f;
        [Tooltip("Velocidad máxima en unidades de pantalla por segundo")]
        [SerializeField]                      private float _edgeScrollSpeed = 1400f;
        [Tooltip("Duración de la animación al centrar un punto de colocación")]
        [SerializeField]                      private float _revealDuration  = 0.35f;

        private const float TimeCheckInterval = 30f;

        private ScrollRect    _scroll;
        private RectTransform _viewport;
        private Canvas        _canvas;
        private bool?         _isNight;
        private float         _nextTimeCheck;
        private bool          _edgeScrollActive;
        private Vector2       _edgePointer;
        private Coroutine     _revealRoutine;

        public bool IsNight => _isNight ?? false;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake() => _cacheReferences();

        private void OnEnable()
        {
            _cacheReferences();
            _applyFit();
            if (Application.isPlaying)
                ApplyTimeOfDay(DateTime.Now);
        }

        private void OnDisable()
        {
            ClearDragPointer();
            _stopReveal();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && Application.isPlaying && isActiveAndEnabled)
                ApplyTimeOfDay(DateTime.Now);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (Time.unscaledTime >= _nextTimeCheck)
                ApplyTimeOfDay(DateTime.Now);

            if (_edgeScrollActive)
                _updateEdgeScroll();
        }

        // En el editor también, para que el fondo se vea igual que en el móvil al colocar los puntos
        private void LateUpdate() => _applyFit();

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Muestra el trozo central de la habitación, sin animación.</summary>
        public void CenterView()
        {
            if (_scroll == null) return;
            _stopReveal();
            _applyFit();
            Canvas.ForceUpdateCanvases();
            _scroll.StopMovement();
            _scroll.horizontalNormalizedPosition = 0.5f;
        }

        /// <summary>Elige el fondo de día o de noche según la hora indicada.</summary>
        public void ApplyTimeOfDay(DateTime now)
        {
            _nextTimeCheck = Time.unscaledTime + TimeCheckInterval;

            bool night = IsNightHour(now.Hour, _nightStartHour, _nightEndHour);
            if (_isNight == night) return;
            _isNight = night;

            var sprites = night ? _nightSprites : _daySprites;
            if (_backgroundPanels == null || sprites == null) return;

            for (int i = 0; i < _backgroundPanels.Length && i < sprites.Length; i++)
                if (_backgroundPanels[i] != null && sprites[i] != null)
                    _backgroundPanels[i].sprite = sprites[i];
        }

        /// <summary>True si la hora cae en la franja de noche [inicio, fin). Admite franjas que cruzan medianoche.</summary>
        public static bool IsNightHour(int hour, int nightStart, int nightEnd)
        {
            if (nightStart == nightEnd) return false;
            return nightStart > nightEnd
                ? hour >= nightStart || hour < nightEnd
                : hour >= nightStart && hour < nightEnd;
        }

        /// <summary>
        /// Posición del dedo mientras se arrastra un objeto del inventario: si está cerca de un borde,
        /// la habitación se desplaza sola para llegar a los puntos que quedan fuera de la pantalla.
        /// </summary>
        public void SetDragPointer(Vector2 screenPosition)
        {
            _edgePointer      = screenPosition;
            _edgeScrollActive = true;
            _stopReveal();
        }

        public void ClearDragPointer() => _edgeScrollActive = false;

        /// <summary>True si el centro del objetivo se ve en pantalla (con un margen).</summary>
        public bool IsVisible(RectTransform target)
        {
            if (!_tryGetScrollMetrics(out float range, out float visibleWidth)) return true;
            float x       = _contentX(target);
            float left    = _scroll.horizontalNormalizedPosition * range;
            float margin  = visibleWidth * 0.1f;
            return x >= left + margin && x <= left + visibleWidth - margin;
        }

        /// <summary>Desliza la habitación (animado) hasta centrar el objetivo.</summary>
        public void Reveal(RectTransform target)
        {
            if (target == null || !isActiveAndEnabled) return;
            if (!_tryGetScrollMetrics(out float range, out float visibleWidth)) return;

            float targetPos = Mathf.Clamp01((_contentX(target) - visibleWidth * 0.5f) / range);
            _stopReveal();
            _revealRoutine = StartCoroutine(_animateTo(targetPos));
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _cacheReferences()
        {
            if (_scroll == null) _scroll = GetComponent<ScrollRect>();
            if (_scroll != null)
            {
                // Sin rebote elástico: más allá de los bordes del arte solo hay vacío
                if (_scroll.movementType != ScrollRect.MovementType.Clamped)
                    _scroll.movementType = ScrollRect.MovementType.Clamped;

                _viewport = _scroll.viewport != null ? _scroll.viewport : (RectTransform)_scroll.transform;
                if (_content == null) _content = _scroll.content;
            }
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        }

        /// <summary>Escala RoomContent para que ocupe todo el alto visible sin deformar el arte.</summary>
        private void _applyFit()
        {
            if (_content == null || _viewport == null) return;

            float artHeight = _content.rect.height;
            float viewHeight = _viewport.rect.height;
            if (artHeight <= 0f || viewHeight <= 0f) return;

            float scale = viewHeight / artHeight;
            if (Mathf.Abs(_content.localScale.x - scale) > 0.0001f || Mathf.Abs(_content.localScale.y - scale) > 0.0001f)
                _content.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>range = recorrido desplazable y visibleWidth = ancho visible, ambos en unidades de RoomContent.</summary>
        private bool _tryGetScrollMetrics(out float range, out float visibleWidth)
        {
            range = visibleWidth = 0f;
            if (_scroll == null || _content == null || _viewport == null) return false;

            float scale = _content.localScale.x;
            if (scale <= 0f) return false;

            visibleWidth = _viewport.rect.width / scale;
            range        = _content.rect.width - visibleWidth;
            return range > 0.5f;
        }

        /// <summary>X del centro del objetivo medida desde el borde izquierdo de RoomContent.</summary>
        private float _contentX(RectTransform target)
        {
            Vector3 world = target.TransformPoint(target.rect.center);
            return _content.InverseTransformPoint(world).x - _content.rect.xMin;
        }

        private void _updateEdgeScroll()
        {
            if (_viewport == null || !_tryGetScrollMetrics(out float range, out _)) return;

            Camera cam = _canvas != null && _canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.rootCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, _edgePointer, cam, out Vector2 local))
                return;

            Rect  r    = _viewport.rect;
            float zone = r.width * _edgeZone;
            float dir  = 0f;

            if (local.x < r.xMin + zone)      dir = -(r.xMin + zone - local.x) / zone;
            else if (local.x > r.xMax - zone) dir =  (local.x - (r.xMax - zone)) / zone;
            if (Mathf.Approximately(dir, 0f)) return;

            // range está en unidades de RoomContent; la velocidad, en unidades de pantalla
            float rangeOnScreen = range * _content.localScale.x;
            float delta         = Mathf.Clamp(dir, -1f, 1f) * _edgeScrollSpeed * Time.unscaledDeltaTime / rangeOnScreen;

            _scroll.StopMovement();
            _scroll.horizontalNormalizedPosition = Mathf.Clamp01(_scroll.horizontalNormalizedPosition + delta);
        }

        private IEnumerator _animateTo(float target)
        {
            _scroll.StopMovement();
            float from = _scroll.horizontalNormalizedPosition;
            float t    = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _revealDuration);
                _scroll.horizontalNormalizedPosition = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            _revealRoutine = null;
        }

        private void _stopReveal()
        {
            if (_revealRoutine == null) return;
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }
    }
}
