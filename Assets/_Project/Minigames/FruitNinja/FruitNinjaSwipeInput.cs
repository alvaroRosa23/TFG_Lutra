using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lutra.Minigames
{
    /// <summary>
    /// Zona táctil a pantalla completa (Image transparente con Raycast Target) que convierte el
    /// arrastre del dedo en segmentos de trazo con su velocidad. Solo sigue al primer dedo.
    /// Las posiciones se dan respecto al centro de _space (el contenedor de FruitNinjaField),
    /// así coinciden con las de los elementos aunque ese contenedor esté haciendo zoom.
    /// Debe quedar por debajo del botón de salida en la jerarquía para no taparlo.
    /// </summary>
    public class FruitNinjaSwipeInput : MonoBehaviour,
        IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _space;

        private const int NoPointer = int.MinValue;

        public event Action<Vector2> OnSwipeStarted;
        /// <summary>Segmento desde → hasta y velocidad del dedo (unidades/s).</summary>
        public event Action<Vector2, Vector2, float> OnSwipeMoved;
        public event Action OnSwipeEnded;

        private int     _pointerId = NoPointer;
        private Vector2 _lastPosition;
        private float   _lastTime;
        private float   _speed;

        private void Awake()
        {
            if (_space == null) _space = (RectTransform)transform;
        }

        private void OnDisable() => _pointerId = NoPointer;

        private void OnDestroy()
        {
            OnSwipeStarted = null;
            OnSwipeMoved   = null;
            OnSwipeEnded   = null;
        }

        // Al perder el foco (llamada, notificación...) nunca llega el PointerUp: se suelta aquí.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) _release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != NoPointer) return;
            if (!_toLocal(eventData, out var position)) return;

            _pointerId    = eventData.pointerId;
            _lastPosition = position;
            _lastTime     = Time.unscaledTime;
            _speed        = 0f;
            OnSwipeStarted?.Invoke(position);
        }

        // Sin umbral de arrastre: el trazo corta desde el primer píxel
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            if (!_toLocal(eventData, out var position)) return;

            float now = Time.unscaledTime;
            float deltaTime = now - _lastTime;
            if (deltaTime > 0.0001f) _speed = Vector2.Distance(_lastPosition, position) / deltaTime;

            OnSwipeMoved?.Invoke(_lastPosition, position, _speed);
            _lastPosition = position;
            _lastTime     = now;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _release();
        }

        private void _release()
        {
            if (_pointerId == NoPointer) return;

            _pointerId = NoPointer;
            OnSwipeEnded?.Invoke();
        }

        private bool _toLocal(PointerEventData eventData, out Vector2 position)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, eventData.position,
                                                                         eventData.pressEventCamera, out position))
                return false;

            position -= _space.rect.center; // relativo al centro, como los elementos
            return true;
        }
    }
}
