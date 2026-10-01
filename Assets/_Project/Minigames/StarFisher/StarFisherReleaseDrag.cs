using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lutra.Minigames
{
    /// <summary>
    /// Estrella que el jugador arrastra hacia arriba para liberarla (en la Image de la estrella de
    /// la fase de liberación, con Raycast Target activado). Si se suelta lo bastante arriba o con
    /// un gesto rápido hacia arriba, sube sola y se desvanece; si no, vuelve a su sitio.
    /// </summary>
    public class StarFisherReleaseDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private float _releaseDistance   = 320f;
        [SerializeField] private float _releaseFlickSpeed = 1800f;
        [SerializeField] private float _flySpeed          = 2600f;
        [SerializeField] private float _flySeconds        = 1.1f;
        [SerializeField] private float _returnSmoothing   = 12f;

        /// <summary>La estrella ha terminado de subir y desaparecer.</summary>
        public event Action OnReleased;

        private RectTransform _rect;
        private CanvasGroup   _group;
        private Vector2 _home;
        private Vector2 _pointerOffset;
        private Vector2 _lastPointer;
        private float   _lastTime;
        private float   _verticalSpeed;
        private bool    _dragging;
        private bool    _flying;
        private float   _flyTime;
        private bool    _armed;
        private bool    _hasHome;

        private void Awake()
        {
            _rect  = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnDestroy() => OnReleased = null;

        /// <summary>Coloca la estrella en su sitio y permite arrastrarla.</summary>
        public void Arm()
        {
            if (_rect == null) Awake();
            ResetPosition();
            _group.alpha = 1f;
            _rect.localScale = Vector3.one;
            _dragging = _flying = false;
            _armed = true;
        }

        /// <summary>Deja la estrella en su posición de partida (para la próxima vez).</summary>
        public void ResetPosition()
        {
            if (_rect == null) return;

            // La primera vez se toma como casa la posición puesta en el editor
            if (!_hasHome)
            {
                _home    = _rect.anchoredPosition;
                _hasHome = true;
            }

            _rect.anchoredPosition = _home;
            _rect.localScale = Vector3.one;
            _armed = _flying = _dragging = false;
            _group.alpha = 1f;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_armed || _flying) return;
            if (!_toParent(eventData, out var pointer)) return;

            _dragging      = true;
            _pointerOffset = _rect.anchoredPosition - pointer;
            _lastPointer   = pointer;
            _lastTime      = Time.unscaledTime;
            _verticalSpeed = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !_toParent(eventData, out var pointer)) return;

            float now = Time.unscaledTime;
            float deltaTime = now - _lastTime;
            if (deltaTime > 0.0001f) _verticalSpeed = (pointer.y - _lastPointer.y) / deltaTime;
            _lastPointer = pointer;
            _lastTime    = now;

            // Libre hacia arriba; hacia abajo y a los lados cuesta más
            Vector2 target = pointer + _pointerOffset;
            Vector2 offset = target - _home;
            offset.x *= 0.5f;
            if (offset.y < 0f) offset.y *= 0.25f;
            _rect.anchoredPosition = _home + offset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;

            float lifted = _rect.anchoredPosition.y - _home.y;
            if (lifted >= _releaseDistance || _verticalSpeed >= _releaseFlickSpeed)
            {
                _flying  = true;
                _armed   = false;
                _flyTime = 0f;
            }
        }

        private void Update()
        {
            if (_flying)
            {
                _flyTime += Time.deltaTime;
                float t = Mathf.Clamp01(_flyTime / _flySeconds);
                _rect.anchoredPosition += Vector2.up * (_flySpeed * Time.deltaTime);
                _rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
                _group.alpha = 1f - t;

                if (t >= 1f)
                {
                    _flying = false;
                    OnReleased?.Invoke();
                }
                return;
            }

            if (_armed && !_dragging)
                _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, _home,
                                                      1f - Mathf.Exp(-_returnSmoothing * Time.deltaTime));
        }

        private bool _toParent(PointerEventData eventData, out Vector2 position)
        {
            position = default;
            var parent = _rect.parent as RectTransform;
            return parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out position);
        }
    }
}
