using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Vista pasiva de la pantalla de login.
    /// Expone los datos introducidos por el usuario y delega los eventos de los botones
    /// al controlador mediante Actions públicas.
    /// </summary>
    public class LoginView : MonoBehaviour
    {
        // ── Referencias serializadas ───────────────────────────────────

        [Header("Campos de entrada")]
        [SerializeField] private TMP_InputField _emailInput;
        [SerializeField] private TMP_InputField _passwordInput;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI _errorLabel;

        [Header("Botones")]
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _registerButton;
        [SerializeField] private Button _forgotPasswordButton;

        [Header("Estado de carga")]
        [SerializeField] private GameObject _loadingIndicator;

        // ── Eventos públicos ───────────────────────────────────────────

        public Action OnLoginClicked;
        public Action OnRegisterClicked;
        public Action OnForgotPasswordClicked;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _loginButton?.onClick.AddListener(() => OnLoginClicked?.Invoke());
            _registerButton?.onClick.AddListener(() => OnRegisterClicked?.Invoke());
            _forgotPasswordButton?.onClick.AddListener(() => OnForgotPasswordClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _loginButton?.onClick.RemoveAllListeners();
            _registerButton?.onClick.RemoveAllListeners();
            _forgotPasswordButton?.onClick.RemoveAllListeners();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Devuelve el email introducido, sin espacios.</summary>
        public string GetEmail() => _emailInput != null ? _emailInput.text.Trim() : string.Empty;

        /// <summary>Devuelve la contraseña introducida (sin trim para respetar espacios intencionales).</summary>
        public string GetPassword() => _passwordInput != null ? _passwordInput.text : string.Empty;

        /// <summary>
        /// Muestra un mensaje en el label de error.
        /// </summary>
        /// <param name="message">Texto a mostrar.</param>
        /// <param name="isSuccess">Si true muestra en verde (confirmación); si false en rojo (error).</param>
        public void ShowError(string message, bool isSuccess = false)
        {
            if (_errorLabel == null) return;

            _errorLabel.text    = message;
            _errorLabel.color   = isSuccess
                ? new Color(0.20f, 0.75f, 0.35f)   // verde confirmación
                : new Color(0.90f, 0.25f, 0.25f);   // rojo error
            _errorLabel.gameObject.SetActive(true);
        }

        /// <summary>Borra el texto del label de error.</summary>
        public void ClearError()
        {
            if (_errorLabel == null) return;
            _errorLabel.text = string.Empty;
        }

        /// <summary>
        /// Activa o desactiva el estado de carga:
        /// muestra el spinner y deshabilita los botones interactivos.
        /// </summary>
        public void SetLoading(bool loading)
        {
            _loadingIndicator?.SetActive(loading);

            if (_loginButton    != null) _loginButton.interactable    = !loading;
            if (_registerButton != null) _registerButton.interactable = !loading;
        }
    }
}
