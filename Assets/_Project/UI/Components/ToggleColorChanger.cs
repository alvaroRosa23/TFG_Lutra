using UnityEngine;
using UnityEngine.UI;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Cambia el color del fondo de un Toggle según su estado activo/inactivo.
    /// Añadir al mismo GameObject que el Toggle y asignar _background en el Inspector.
    /// </summary>
    public class ToggleColorChanger : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Color _normalColor   = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.267f, 0.267f, 0.255f, 1f); // #444441

        private Toggle _toggle;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            if (_toggle == null) _toggle = GetComponentInParent<Toggle>();
            _toggle.onValueChanged.AddListener(_onValueChanged);
            _background.color = _toggle.isOn ? _selectedColor : _normalColor;
        }

        private void OnDestroy()
        {
            _toggle?.onValueChanged.RemoveListener(_onValueChanged);
        }

        private void _onValueChanged(bool isOn)
        {
            if (_background != null)
                _background.color = isOn ? _selectedColor : _normalColor;
        }
    }
}
