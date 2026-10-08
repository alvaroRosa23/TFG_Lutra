using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Celda del calendario de Estadísticas: fondo del color de la emoción del día y marca
    /// opcional de "sin registro". Sirve también para la leyenda (muestra + texto).
    /// </summary>
    public class HeatmapCell : MonoBehaviour
    {
        [SerializeField] private Image           _background;
        [Tooltip("Marca de día sin registro (p. ej. una X). Opcional.")]
        [SerializeField] private GameObject      _noRecordMark;
        [Tooltip("Solo en la leyenda. Opcional.")]
        [SerializeField] private TextMeshProUGUI _label;

        /// <param name="label">null = no tocar el texto (celdas del calendario).</param>
        public void Setup(Color color, bool noRecord, string label = null)
        {
            if (_background != null)   _background.color = color;
            if (_noRecordMark != null) _noRecordMark.SetActive(noRecord);
            if (_label != null && label != null) _label.text = label;
        }
    }
}
