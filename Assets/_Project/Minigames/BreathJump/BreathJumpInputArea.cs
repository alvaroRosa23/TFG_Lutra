using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lutra.Minigames
{
    /// <summary>
    /// Zona táctil a pantalla completa (Image transparente con Raycast Target) que detecta
    /// mantener pulsado / soltar. Solo sigue al primer dedo; los demás se ignoran.
    /// Debe quedar por debajo del botón de salida en la jerarquía para no taparlo.
    /// </summary>
    public class BreathJumpInputArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;

        public event Action OnHoldStarted;
        public event Action OnHoldEnded;

        private int _pointerId = NoPointer;

        public bool IsHolding => _pointerId != NoPointer;

        private void OnDisable() => _pointerId = NoPointer;

        private void OnDestroy()
        {
            OnHoldStarted = null;
            OnHoldEnded   = null;
        }

        // Al perder el foco (llamada, notificación...) nunca llega el PointerUp: se suelta aquí.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) _release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHolding) return;

            _pointerId = eventData.pointerId;
            OnHoldStarted?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _release();
        }

        private void _release()
        {
            if (!IsHolding) return;

            _pointerId = NoPointer;
            OnHoldEnded?.Invoke();
        }
    }
}
