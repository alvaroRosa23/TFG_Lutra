using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Controla la apariencia visual de un icono de día en la barra de racha.
    /// Diseñado para un prefab sencillo: un círculo de color + un símbolo central.
    ///
    /// Setup en Inspector:
    ///   - _circleImage: Image del círculo de fondo.
    ///   - _checkmark: TextMeshProUGUI para el símbolo ("OK", "o" o "").
    ///   - Colores ajustables sin tocar código.
    /// </summary>
    public class StreakIconPrefabSetup : MonoBehaviour
    {
        [SerializeField] private Image           _circleImage;
        [SerializeField] private TextMeshProUGUI _checkmark;

        [Header("Colores")]
        [SerializeField] private Color _completedColor = new Color(0.306f, 0.788f, 0.627f, 1f); // #4EC9A0 verde
        [SerializeField] private Color _todayColor     = new Color(0.910f, 0.451f, 0.228f, 1f); // #E8733A naranja
        [SerializeField] private Color _emptyColor     = new Color(0.533f, 0.529f, 0.502f, 1f); // #888780 gris
        [SerializeField] private Color _missedColor    = new Color(0.886f, 0.294f, 0.290f, 1f); // #E24B4A rojo

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Aplica el estado visual del icono según su posición en la racha.
        /// - completed: círculo verde con marca de verificación.
        /// - isToday y !completed: círculo naranja con punto central (día pendiente).
        /// - !completed y !isToday: círculo gris sin símbolo.
        /// </summary>
        public void SetState(bool completed, bool isToday)
        {
            if (completed)
            {
                if (_circleImage != null) _circleImage.color = _completedColor;
                if (_checkmark   != null) _checkmark.text    = "OK";
            }
            else if (isToday)
            {
                if (_circleImage != null) _circleImage.color = _todayColor;
                if (_checkmark   != null) _checkmark.text    = "o";
            }
            else
            {
                if (_circleImage != null) _circleImage.color = _emptyColor;
                if (_checkmark   != null) _checkmark.text    = "";
            }
        }

        /// <summary>
        /// Estado de día pasado sin registro: círculo rojo con aspa.
        /// </summary>
        public void SetMissed()
        {
            if (_circleImage != null) _circleImage.color = _missedColor;
            if (_checkmark   != null) _checkmark.text    = "X";
        }
    }
}
