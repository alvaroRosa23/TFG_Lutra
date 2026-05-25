using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Controla la apariencia de un icono de día en la barra de racha de 7 días.
    /// Expone SetActive() para uso simple y SetStreak() para el estado completo
    /// (completado, hoy pendiente con pulso naranja, o inactivo).
    /// </summary>
    public class StreakIconController : MonoBehaviour
    {
        [SerializeField] private Image           _icon;
        [SerializeField] private TextMeshProUGUI _dayLabel;

        [Header("Colores")]
        [SerializeField] private Color _activeColor   = new Color(1f, 0.85f, 0f, 1f);   // amarillo
        [SerializeField] private Color _inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1f); // gris

        private static readonly Color TodayColor = new Color(1f, 0.55f, 0.1f, 1f); // naranja

        private Coroutine _pulseCoroutine;

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Cambia el color del icono y opcionalmente actualiza el label del día.
        /// </summary>
        public void SetActive(bool active, string dayText = "")
        {
            _stopPulse();

            if (_icon != null)
                _icon.color = active ? _activeColor : _inactiveColor;

            if (_dayLabel != null && !string.IsNullOrEmpty(dayText))
                _dayLabel.text = dayText;
        }

        /// <summary>
        /// Aplica el estado visual completo del icono según su posición en la racha:
        /// - completed: color amarillo/dorado.
        /// - isToday y no completado: pulso naranja animado.
        /// - inactivo: color gris.
        /// </summary>
        public void SetStreak(bool completed, bool isToday)
        {
            _stopPulse();

            if (_icon == null) return;

            if (completed)
            {
                _icon.color = _activeColor;
            }
            else if (isToday)
            {
                _pulseCoroutine = StartCoroutine(_pulseAnimation());
            }
            else
            {
                _icon.color = _inactiveColor;
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _stopPulse()
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
        }

        /// <summary>
        /// Coroutine que oscila el color del icono entre gris y naranja
        /// para indicar que hoy es el día a completar.
        /// </summary>
        private IEnumerator _pulseAnimation()
        {
            while (true)
            {
                float t = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
                if (_icon != null)
                    _icon.color = Color.Lerp(_inactiveColor, TodayColor, t);
                yield return null;
            }
        }
    }
}
