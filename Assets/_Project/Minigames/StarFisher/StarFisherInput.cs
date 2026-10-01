using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lutra.Minigames
{
    /// <summary>
    /// Zona táctil a pantalla completa (Image transparente con Raycast Target) de StarFisher.
    /// Solo sigue al primer dedo. Las posiciones se dan en coordenadas locales de _space (el
    /// contenedor de la escena, respecto a su pivote), las mismas que usa StarFisherRod.
    /// Debe quedar por debajo de los botones y paneles en la jerarquía para no taparlos.
    /// </summary>
    public class StarFisherInput : MonoBehaviour,
        IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _space;

        private const int NoPointer = int.MinValue;

        public event Action<Vector2> OnPressed;
        public event Action<Vector2> OnDragged;
        public event Action          OnReleased;

        private int _pointerId = NoPointer;

        public bool IsPressed => _pointerId != NoPointer;

        private void Awake()
        {
            if (_space == null) _space = (RectTransform)transform;
        }

        private void OnDisable() => _pointerId = NoPointer;

        private void OnDestroy()
        {
            OnPressed  = null;
            OnDragged  = null;
            OnReleased = null;
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

            _pointerId = eventData.pointerId;
            OnPressed?.Invoke(position);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            if (_toLocal(eventData, out var position)) OnDragged?.Invoke(position);
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
            OnReleased?.Invoke();
        }

        private bool _toLocal(PointerEventData eventData, out Vector2 position)
            => RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, eventData.position,
                                                                       eventData.pressEventCamera, out position);
    }
}
