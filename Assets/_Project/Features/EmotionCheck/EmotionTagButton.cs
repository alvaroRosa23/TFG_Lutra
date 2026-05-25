using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.EmotionCheck
{
    /// <summary>
    /// Tag seleccionable reutilizado tanto para emociones como para motivos.
    /// El padre (EmotionCheckView) asigna OnToggled y llama a Initialize().
    /// </summary>
    public class EmotionTagButton : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image           _background;
        [SerializeField] private Color           _normalColor;
        [SerializeField] private Color           _selectedColor;
        [SerializeField] private Color           _normalTextColor;
        [SerializeField] private Color           _selectedTextColor;

        private bool   _isSelected = false;
        private string _tagValue;

        /// <summary>Callback invocado al pulsar: (valor, seleccionado).</summary>
        public Action<string, bool> OnToggled;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(_onClicked);
        }

        // ── API pública ────────────────────────────────────────────────

        public void Initialize(string value, string displayText,
                               Color normalColor, Color selectedColor)
        {
            _tagValue      = value;
            _label.text    = displayText;
            _normalColor   = normalColor;
            _selectedColor = selectedColor;
            _isSelected    = false;
            _updateVisuals();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            _updateVisuals();
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _onClicked()
        {
            _isSelected = !_isSelected;
            _updateVisuals();
            OnToggled?.Invoke(_tagValue, _isSelected);
        }

        private void OnDestroy()
        {
            GetComponent<UnityEngine.UI.Button>()
                ?.onClick.RemoveAllListeners();
        }

        private void _updateVisuals()
        {
            if (_background != null)
                _background.color = _isSelected ? _selectedColor : _normalColor;
            if (_label != null)
                _label.color = _isSelected ? _selectedTextColor : _normalTextColor;
        }
    }
}
