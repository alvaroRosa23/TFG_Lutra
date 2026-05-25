using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Vista del onboarding de perfil. Gestiona 5 paneles de pasos y expone
    /// los datos de cada uno al controlador.
    /// </summary>
    public class OnboardingProfileView : MonoBehaviour
    {
        [Header("Navegación")]
        [SerializeField] private Button          _nextButton;
        [SerializeField] private Button          _backButton;
        [SerializeField] private TextMeshProUGUI _stepLabel;
        [SerializeField] private TextMeshProUGUI _errorLabel;
        [SerializeField] private GameObject[]    _stepPanels;

        [Header("Paso 0 - Nombre")]
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TMP_InputField _surnameInput;

        [Header("Paso 1 - Fecha de nacimiento")]
        [SerializeField] private TMP_InputField _dayInput;
        [SerializeField] private TMP_InputField _monthInput;
        [SerializeField] private TMP_InputField _yearInput;

        [Header("Paso 2 - Cultura")]
        [SerializeField] private Toggle[]      _cultureToggles;
        [SerializeField] private CultureType[] _cultureTypes;

        [Header("Paso 3 - Aficiones")]
        [SerializeField] private Toggle[]    _hobbyToggles;
        [SerializeField] private HobbyType[] _hobbyTypes;

        public Action OnNextClicked;
        public Action OnBackClicked;

        private void Awake()
        {
            _nextButton?.onClick.AddListener(() => OnNextClicked?.Invoke());
            _backButton?.onClick.AddListener(() => OnBackClicked?.Invoke());

            _nameInput?.onValueChanged.AddListener(value =>
            {
                _nameInput.text = System.Text.RegularExpressions.Regex
                    .Replace(value, "[^a-zA-ZáéíóúüñÁÉÍÓÚÜÑ ]", "");
            });
            _surnameInput?.onValueChanged.AddListener(value =>
            {
                _surnameInput.text = System.Text.RegularExpressions.Regex
                    .Replace(value, "[^a-zA-ZáéíóúüñÁÉÍÓÚÜÑ ]", "");
            });

            if (_dayInput != null)
            {
                _dayInput.characterLimit = 2;
                _dayInput.onValueChanged.AddListener(value =>
                {
                    _dayInput.text = System.Text.RegularExpressions.Regex.Replace(value, "[^0-9]", "");
                });
            }
            if (_monthInput != null)
            {
                _monthInput.characterLimit = 2;
                _monthInput.onValueChanged.AddListener(value =>
                {
                    _monthInput.text = System.Text.RegularExpressions.Regex.Replace(value, "[^0-9]", "");
                });
            }
            if (_yearInput != null)
            {
                _yearInput.characterLimit = 4;
                _yearInput.onValueChanged.AddListener(value =>
                {
                    _yearInput.text = System.Text.RegularExpressions.Regex.Replace(value, "[^0-9]", "");
                });
            }
        }

        private void OnDestroy()
        {
            _nextButton?.onClick.RemoveAllListeners();
            _backButton?.onClick.RemoveAllListeners();
            _nameInput?.onValueChanged.RemoveAllListeners();
            _surnameInput?.onValueChanged.RemoveAllListeners();
            _dayInput?.onValueChanged.RemoveAllListeners();
            _monthInput?.onValueChanged.RemoveAllListeners();
            _yearInput?.onValueChanged.RemoveAllListeners();
        }

        public void ShowStep(int step)
        {
            if (_stepPanels != null)
                for (int i = 0; i < _stepPanels.Length; i++)
                    if (_stepPanels[i] != null)
                        _stepPanels[i].SetActive(i == step);

            if (_stepLabel != null)
                _stepLabel.text = $"Paso {step + 1} de 5";

            if (_backButton != null)
                _backButton.gameObject.SetActive(step > 0);

            var nextLabel = _nextButton?.GetComponentInChildren<TextMeshProUGUI>();
            if (nextLabel != null)
                nextLabel.text = step == 3 ? "Finalizar" : "Siguiente";

            ClearError();
        }

        public string GetName()    => _nameInput    != null ? _nameInput.text.Trim()    : "";
        public string GetSurname() => _surnameInput != null ? _surnameInput.text.Trim() : "";

        public DateTime GetDateOfBirth()
        {
            try
            {
                int day   = int.Parse(_dayInput.text.Trim());
                int month = int.Parse(_monthInput.text.Trim());
                int year  = int.Parse(_yearInput.text.Trim());
                return new DateTime(year, month, day);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public CultureType GetSelectedCulture()
        {
            if (_cultureToggles == null || _cultureTypes == null) return default;

            int count = Mathf.Min(_cultureToggles.Length, _cultureTypes.Length);
            for (int i = 0; i < count; i++)
                if (_cultureToggles[i] != null && _cultureToggles[i].isOn)
                    return _cultureTypes[i];

            return default;
        }

        public List<HobbyType> GetSelectedHobbies()
        {
            var result = new List<HobbyType>();
            if (_hobbyToggles == null || _hobbyTypes == null) return result;

            int count = Mathf.Min(_hobbyToggles.Length, _hobbyTypes.Length);
            for (int i = 0; i < count; i++)
                if (_hobbyToggles[i] != null && _hobbyToggles[i].isOn)
                    result.Add(_hobbyTypes[i]);

            return result;
        }

        public void ShowError(string message)
        {
            if (_errorLabel == null) return;
            _errorLabel.gameObject.SetActive(true);
            _errorLabel.text = message;
        }

        public void ClearError()
        {
            if (_errorLabel == null) return;
            _errorLabel.text = "";
            _errorLabel.gameObject.SetActive(false);
        }
    }
}
