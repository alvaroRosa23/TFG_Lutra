using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Vista de la pantalla de registro. Expone los campos y eventos al controlador.
    /// </summary>
    public class RegisterView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField    _emailInput;
        [SerializeField] private TMP_InputField    _passwordInput;
        [SerializeField] private TMP_InputField    _confirmPasswordInput;
        [SerializeField] private TextMeshProUGUI   _errorLabel;
        [SerializeField] private Button            _registerButton;
        [SerializeField] private Button            _backButton;
        [SerializeField] private GameObject        _loadingIndicator;
        [SerializeField] private TextMeshProUGUI   _passwordRequirementsLabel;

        public Action OnRegisterClicked;
        public Action OnBackClicked;

        private void Awake()
        {
            if (_passwordRequirementsLabel != null)
                _passwordRequirementsLabel.text = "Mínimo 8 caracteres, 1 mayúscula y 1 número";

            _registerButton?.onClick.AddListener(() => OnRegisterClicked?.Invoke());
            _backButton?.onClick.AddListener(() => OnBackClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _registerButton?.onClick.RemoveAllListeners();
            _backButton?.onClick.RemoveAllListeners();
        }

        public string GetEmail()           => _emailInput.text.Trim();
        public string GetPassword()        => _passwordInput.text;
        public string GetConfirmPassword() => _confirmPasswordInput.text;

        public void ShowError(string message)
        {
            if (_errorLabel == null) return;
            _errorLabel.gameObject.SetActive(true);
            _errorLabel.text  = message;
            _errorLabel.color = Color.red;
        }

        public void ClearAll()
        {
            if (_emailInput           != null) _emailInput.text           = "";
            if (_passwordInput        != null) _passwordInput.text        = "";
            if (_confirmPasswordInput != null) _confirmPasswordInput.text = "";
            if (_errorLabel           != null) _errorLabel.text           = "";
        }

        public void SetLoading(bool loading)
        {
            _loadingIndicator?.SetActive(loading);
            if (_registerButton != null)
                _registerButton.interactable = !loading;
        }
    }
}
