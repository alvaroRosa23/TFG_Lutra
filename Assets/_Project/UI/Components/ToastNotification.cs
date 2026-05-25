using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Muestra mensajes breves (toasts) al usuario con fade in/out.
    /// Singleton: añadir un único GameObject con este componente en la escena principal.
    ///
    /// Setup en Inspector:
    ///   - _toastPanel: panel raíz del toast (debe tener CanvasGroup).
    ///   - _toastLabel: TextMeshProUGUI con el mensaje.
    ///   - _toastBackground: Image de fondo del panel.
    ///
    /// Uso estático:
    ///   ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
    ///   ToastNotification.ShowSuccess("Guardado correctamente");
    ///   ToastNotification.ShowInfo("Sin conexión");
    /// </summary>
    public class ToastNotification : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────

        private static ToastNotification _instance;

        // ── Referencias ────────────────────────────────────────────────

        [SerializeField] private GameObject      _toastPanel;
        [SerializeField] private TextMeshProUGUI _toastLabel;
        [SerializeField] private Image           _toastBackground;

        [Header("Colores")]
        [SerializeField] private Color _errorColor   = new Color(0.886f, 0.294f, 0.290f, 1f); // rojo
        [SerializeField] private Color _successColor = new Color(0.306f, 0.788f, 0.627f, 1f); // verde
        [SerializeField] private Color _infoColor    = new Color(0.259f, 0.522f, 0.957f, 1f); // azul

        // ── Estado ─────────────────────────────────────────────────────

        private CanvasGroup _canvasGroup;
        private Coroutine   _activeCoroutine;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_toastPanel != null)
            {
                _canvasGroup = _toastPanel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _toastPanel.AddComponent<CanvasGroup>();

                _canvasGroup.alpha          = 0f;
                _canvasGroup.interactable   = false;
                _canvasGroup.blocksRaycasts = false;
                _toastPanel.SetActive(false);
            }
        }

        // ── API estática ───────────────────────────────────────────────

        /// <summary>Muestra un toast de error (rojo) durante 2.5 s.</summary>
        public static void ShowError(string message)   => _instance?._show(message, _instance._errorColor);

        /// <summary>Muestra un toast de éxito (verde) durante 2.5 s.</summary>
        public static void ShowSuccess(string message) => _instance?._show(message, _instance._successColor);

        /// <summary>Muestra un toast informativo (azul) durante 2.5 s.</summary>
        public static void ShowInfo(string message)    => _instance?._show(message, _instance._infoColor);

        // ── Métodos privados ───────────────────────────────────────────

        private void _show(string message, Color color)
        {
            if (_toastPanel == null || _canvasGroup == null) return;

            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            _activeCoroutine = StartCoroutine(_toastCoroutine(message, color));
        }

        private IEnumerator _toastCoroutine(string message, Color color)
        {
            // Configurar contenido
            if (_toastLabel      != null) _toastLabel.text       = message;
            if (_toastBackground != null) _toastBackground.color = color;

            // Activar y fade in (0.2 s)
            _toastPanel.SetActive(true);
            yield return StartCoroutine(_fade(0f, 1f, 0.2f));

            // Esperar
            yield return new WaitForSeconds(2.5f);

            // Fade out (0.3 s)
            yield return StartCoroutine(_fade(1f, 0f, 0.3f));

            _toastPanel.SetActive(false);
            _activeCoroutine = null;
        }

        private IEnumerator _fade(float from, float to, float duration)
        {
            _canvasGroup.alpha          = from;
            _canvasGroup.interactable   = to > 0f;
            _canvasGroup.blocksRaycasts = to > 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed            += Time.deltaTime;
                _canvasGroup.alpha  = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            }
            _canvasGroup.alpha = to;
        }
    }
}
