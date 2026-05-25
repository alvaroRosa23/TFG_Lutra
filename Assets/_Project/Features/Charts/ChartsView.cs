using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Vista de la sección de Informes. Renderiza la línea de emoción, el heatmap
    /// y las métricas textuales. No contiene lógica de cálculo.
    /// </summary>
    public class ChartsView : MonoBehaviour
    {
        // ── Referencias serializadas ───────────────────────────────────

        [Header("Gráfica de línea")]
        [SerializeField] private Transform  _emotionLineContainer;
        [SerializeField] private GameObject _emotionDotPrefab;

        [Header("Heatmap")]
        [SerializeField] private Transform  _heatmapContainer;
        [SerializeField] private GameObject _heatmapCellPrefab;

        [Header("Métricas")]
        [SerializeField] private TextMeshProUGUI _streakCurrentLabel;
        [SerializeField] private TextMeshProUGUI _streakMaxLabel;
        [SerializeField] private TextMeshProUGUI _totalCheckInsLabel;
        [SerializeField] private TextMeshProUGUI _mostFrequentEmotionLabel;
        [SerializeField] private TextMeshProUGUI _mostBeneficialMinigameLabel;
        [SerializeField] private TextMeshProUGUI _avgSessionDurationLabel;
        [SerializeField] private TextMeshProUGUI _weeklyReportLabel;

        [Header("Botones de período")]
        [SerializeField] private Button _weekButton;
        [SerializeField] private Button _monthButton;
        [SerializeField] private Button _allTimeButton;

        [Header("Temas emocionales (para colores)")]
        [SerializeField] private EmotionTheme[] _emotionThemes;

        // ── Cache ──────────────────────────────────────────────────────

        private RectTransform _emotionLineContainerRt;

        // ── Evento público ─────────────────────────────────────────────

        public event Action<ChartPeriod> OnPeriodChanged;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _weekButton?.onClick.AddListener(()    => OnPeriodChanged?.Invoke(ChartPeriod.Week));
            _monthButton?.onClick.AddListener(()   => OnPeriodChanged?.Invoke(ChartPeriod.Month));
            _allTimeButton?.onClick.AddListener(() => OnPeriodChanged?.Invoke(ChartPeriod.AllTime));

            _emotionLineContainerRt = _emotionLineContainer?.GetComponent<RectTransform>();

            if (_weeklyReportLabel != null)
                _weeklyReportLabel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _weekButton?.onClick.RemoveAllListeners();
            _monthButton?.onClick.RemoveAllListeners();
            _allTimeButton?.onClick.RemoveAllListeners();
            OnPeriodChanged = null;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Refresca toda la vista con los datos calculados del período.</summary>
        public void RefreshAll(ChartsData data, ChartPeriod period)
        {
            if (data == null) return;

            _refreshEmotionLine(data);
            _refreshHeatmap(data);
            _refreshMetrics(data);
            _updatePeriodButtons(period);
        }

        /// <summary>Activa y muestra el informe semanal generado.</summary>
        public void ShowWeeklyReport(string report)
        {
            if (_weeklyReportLabel == null) return;
            _weeklyReportLabel.gameObject.SetActive(true);
            _weeklyReportLabel.text = report;
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Instancia un dot por cada entrada de DailyEmotionMap.
        /// X = posición normalizada en el tiempo, Y = índice de emoción como indicador visual.
        /// Colorea cada dot según el EmotionTheme correspondiente.
        /// </summary>
        private void _refreshEmotionLine(ChartsData data)
        {
            _clearContainer(_emotionLineContainer);
            if (_emotionDotPrefab == null || data.DailyEmotionMap == null) return;

            var rect      = _emotionLineContainerRt;
            if (rect == null) return;

            float width   = rect.rect.width;
            float height  = rect.rect.height;
            int   total   = data.DailyEmotionMap.Count;
            if (total == 0) return;

            var sortedDays = new List<KeyValuePair<DateTime, EmotionType>>(data.DailyEmotionMap);
            sortedDays.Sort((a, b) => a.Key.CompareTo(b.Key));

            for (int i = 0; i < sortedDays.Count; i++)
            {
                var (date, emotion) = (sortedDays[i].Key, sortedDays[i].Value);

                var dot     = Instantiate(_emotionDotPrefab, _emotionLineContainer, false);
                var dotRect = dot.GetComponent<RectTransform>();

                if (dotRect != null)
                {
                    // X: distribuido uniformemente en el ancho
                    float xNorm = total > 1 ? (float)i / (total - 1) : 0.5f;
                    // Y: índice del enum como indicador de posición (0-7)
                    float yNorm = (float)(int)emotion / (Enum.GetValues(typeof(EmotionType)).Length - 1);

                    dotRect.anchoredPosition = new Vector2(xNorm * width, yNorm * height);
                }

                // Colorear según tema emocional
                var image = dot.GetComponent<Image>();
                if (image != null)
                    image.color = _getEmotionColor(emotion);
            }
        }

        /// <summary>
        /// Instancia una celda por cada día del mes actual.
        /// Colorea en el color primario de la emoción si hay registro, gris si no.
        /// </summary>
        private void _refreshHeatmap(ChartsData data)
        {
            _clearContainer(_heatmapContainer);
            if (_heatmapCellPrefab == null) return;

            var today      = DateTime.Now;
            int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(today.Year, today.Month, day);
                var cell = Instantiate(_heatmapCellPrefab, _heatmapContainer, false);

                var image = cell.GetComponent<Image>();
                if (image == null) continue;

                if (data.DailyEmotionMap != null
                    && data.DailyEmotionMap.TryGetValue(date.Date, out var emotion))
                {
                    image.color = _getEmotionColor(emotion);
                }
                else
                {
                    image.color = new Color(0.75f, 0.75f, 0.75f, 0.4f); // gris translúcido
                }
            }
        }

        /// <summary>Actualiza todos los labels de métricas con los datos calculados.</summary>
        private void _refreshMetrics(ChartsData data)
        {
            if (_streakCurrentLabel != null)
                _streakCurrentLabel.text = ChartsCalculator.FormatStreak(data.CurrentStreak);

            if (_streakMaxLabel != null)
                _streakMaxLabel.text = ChartsCalculator.FormatStreak(data.LongestStreak);

            if (_totalCheckInsLabel != null)
                _totalCheckInsLabel.text = data.TotalCheckIns.ToString();

            if (_mostFrequentEmotionLabel != null)
                _mostFrequentEmotionLabel.text = data.MostFrequentEmotion.ToString();

            if (_mostBeneficialMinigameLabel != null)
                _mostBeneficialMinigameLabel.text = data.MostBeneficialMinigame.ToString();

            if (_avgSessionDurationLabel != null)
                _avgSessionDurationLabel.text = ChartsCalculator.FormatDuration(
                    Mathf.RoundToInt(data.AverageSessionDuration));
        }

        /// <summary>Resalta el botón del período activo.</summary>
        private void _updatePeriodButtons(ChartPeriod period)
        {
            var activeColor  = Color.white;
            var defaultColor = new Color(0.6f, 0.6f, 0.6f, 1f);

            _setButtonColor(_weekButton,    period == ChartPeriod.Week    ? activeColor : defaultColor);
            _setButtonColor(_monthButton,   period == ChartPeriod.Month   ? activeColor : defaultColor);
            _setButtonColor(_allTimeButton, period == ChartPeriod.AllTime ? activeColor : defaultColor);
        }

        private void _clearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private Color _getEmotionColor(EmotionType emotion)
        {
            if (_emotionThemes != null)
                foreach (var theme in _emotionThemes)
                    if (theme != null && theme.emotionType == emotion)
                        return theme.primaryColor;

            return Color.white;
        }

        private static void _setButtonColor(Button btn, Color color)
        {
            if (btn?.targetGraphic != null)
                btn.targetGraphic.color = color;
        }
    }
}
