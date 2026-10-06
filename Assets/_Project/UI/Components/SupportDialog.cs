using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Diálogo de recursos de apoyo (024 / 112). Dos modos:
    ///   - ShowAlert(): lo abre el protocolo de apoyo (SupportProtocol) tras varios días de ánimo
    ///     bajo o un WHO-5 bajo. Tono cálido y no diagnóstico.
    ///   - ShowResources(): desde Ajustes → "Recursos de ayuda" o desde una notificación de apoyo.
    ///
    /// Singleton como ToastNotification: un único GameObject en la escena principal, por encima de
    /// las pantallas. Los textos los pone el código.
    /// </summary>
    public class SupportDialog : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────

        private static SupportDialog _instance;

        // ── Referencias ────────────────────────────────────────────────

        [Tooltip("Panel raíz (con fondo que bloquea la pantalla). Empieza desactivado.")]
        [SerializeField] private GameObject      _panel;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _messageLabel;
        [SerializeField] private Button          _call024Button;
        [SerializeField] private Button          _call112Button;
        [SerializeField] private Button          _closeButton;
        [SerializeField] private TextMeshProUGUI _closeLabel;

        // ── Textos ─────────────────────────────────────────────────────

        private const string AlertTitle = "Estamos contigo";
        private const string AlertMessage =
            "Parece que estos días están siendo difíciles. No tienes que pasarlo solo/a.\n\n" +
            "Si necesitas hablar con alguien ahora, el 024 te atiende gratis, de forma confidencial y a cualquier hora.\n\n" +
            "También puedes compartir cómo te sientes con tu profesional.";

        private const string ResourcesTitle = "Recursos de ayuda";
        private const string ResourcesMessage =
            "024 · Línea de atención a la conducta suicida: gratuita, confidencial, 24 horas.\n\n" +
            "112 · Emergencias.\n\n" +
            "Lutra no es un servicio de emergencias ni una herramienta de diagnóstico. " +
            "Si lo necesitas, habla con tu profesional.";

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _call024Button?.onClick.AddListener(() => Application.OpenURL("tel:024"));
            _call112Button?.onClick.AddListener(() => Application.OpenURL("tel:112"));
            _closeButton?.onClick.AddListener(_hide);

            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            _call024Button?.onClick.RemoveAllListeners();
            _call112Button?.onClick.RemoveAllListeners();
            _closeButton?.onClick.RemoveAllListeners();
            if (_instance == this) _instance = null;
        }

        // ── API estática ───────────────────────────────────────────────

        /// <summary>Aviso del protocolo de apoyo.</summary>
        public static void ShowAlert()
        {
            if (_instance == null)
            {
                Debug.LogWarning("[SupportDialog] No hay SupportDialog en la escena.");
                return;
            }
            _instance._show(AlertTitle, AlertMessage, "Ahora no");
        }

        /// <summary>Recursos de ayuda (Ajustes, notificación de apoyo).</summary>
        public static void ShowResources()
        {
            if (_instance == null)
            {
                Debug.LogWarning("[SupportDialog] No hay SupportDialog en la escena.");
                return;
            }
            _instance._show(ResourcesTitle, ResourcesMessage, "Cerrar");
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _show(string title, string message, string closeText)
        {
            if (_titleLabel != null)   _titleLabel.text   = title;
            if (_messageLabel != null) _messageLabel.text = message;
            if (_closeLabel != null)   _closeLabel.text   = closeText;

            if (_panel != null)
            {
                _panel.SetActive(true);
                _panel.transform.SetAsLastSibling();
            }
        }

        private void _hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
