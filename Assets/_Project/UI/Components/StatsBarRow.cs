using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Fila de barra horizontal para Estadísticas: etiqueta, barra proporcional y valor.
    /// La barra es una Image en modo Filled (Horizontal) y se rellena con fillAmount.
    /// </summary>
    public class StatsBarRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [Tooltip("Image con Image Type = Filled, Fill Method = Horizontal.")]
        [SerializeField] private Image           _fill;
        [SerializeField] private TextMeshProUGUI _valueLabel;

        /// <param name="fraction">0-1, proporción de la barra.</param>
        public void Setup(string label, float fraction, string valueText, Color color)
        {
            if (_label != null)      _label.text      = label;
            if (_valueLabel != null) _valueLabel.text = valueText;
            if (_fill != null)
            {
                _fill.fillAmount = Mathf.Clamp01(fraction);
                _fill.color      = color;
            }
        }
    }
}
