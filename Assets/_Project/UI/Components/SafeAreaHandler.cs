using UnityEngine;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Adapta un RectTransform al área segura del dispositivo (notch, barra de inicio, etc.).
    /// Asignar en Inspector el RectTransform hijo que debe respetar el safe area.
    /// Se aplica en Start y cada vez que las dimensiones del RectTransform cambian
    /// (orientación, resolución), sin coste de Update por frame.
    /// </summary>
    public class SafeAreaHandler : MonoBehaviour
    {
        [SerializeField] private RectTransform _safeAreaRect;

        private Rect _lastSafeArea = Rect.zero;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Start()
        {
            _applySafeArea(Screen.safeArea);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Screen.safeArea != _lastSafeArea)
                _applySafeArea(Screen.safeArea);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _applySafeArea(Rect safeArea)
        {
            if (_safeAreaRect == null)
            {
                Debug.LogWarning("[SafeAreaHandler] _safeAreaRect no asignado.");
                return;
            }

            _safeAreaRect.anchorMin = new Vector2(
                safeArea.x / Screen.width,
                safeArea.y / Screen.height);

            _safeAreaRect.anchorMax = new Vector2(
                (safeArea.x + safeArea.width)  / Screen.width,
                (safeArea.y + safeArea.height) / Screen.height);

            _lastSafeArea = safeArea;
        }
    }
}
