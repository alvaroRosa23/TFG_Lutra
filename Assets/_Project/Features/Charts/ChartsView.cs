using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.UI.Components;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Vista de la pantalla de Estadísticas (docs/PROFESSIONAL_REPORT.md §2.2). Pinta un ReportData
    /// con los textos de StatsTextBuilder; no calcula nada. Bloques, de arriba abajo:
    /// periodo · resumen · tu ánimo (gráfico) · calendario · tus emociones · estabilidad ·
    /// qué influye en ti · qué te ayuda · bienestar (WHO-5) · hábitos · exportar.
    /// Todos los campos son opcionales: un bloque sin referencias simplemente no se pinta.
    /// </summary>
    public class ChartsView : MonoBehaviour
    {
        [Header("Periodo")]
        [SerializeField] private Button _weekButton;
        [SerializeField] private Button _monthButton;
        [SerializeField] private Button _allTimeButton;
        [SerializeField] private Color  _periodActiveColor   = Color.white;
        [SerializeField] private Color  _periodInactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Header("Resumen")]
        [SerializeField] private TextMeshProUGUI _summaryLabel;
        [SerializeField] private Image           _summaryFace;
        [Tooltip("5 caritas: índice 0 = muy mal … 4 = muy bien.")]
        [SerializeField] private Sprite[]        _moodFaceSprites;
        [Tooltip("Iconos de tendencia (opcionales): solo se activa el que corresponda.")]
        [SerializeField] private GameObject      _trendUpIcon;
        [SerializeField] private GameObject      _trendFlatIcon;
        [SerializeField] private GameObject      _trendDownIcon;

        [Header("Tu ánimo (gráfico)")]
        [SerializeField] private UILineChart     _moodChart;
        [SerializeField] private TextMeshProUGUI _moodChartEmptyLabel;

        [Header("Calendario")]
        [Tooltip("Contenedor con GridLayoutGroup de 7 columnas (lunes a domingo).")]
        [SerializeField] private Transform  _heatmapContainer;
        [SerializeField] private GameObject _heatmapCellPrefab;
        [SerializeField] private Color      _heatmapEmptyColor = new Color(0.75f, 0.75f, 0.75f, 0.4f);
        [Tooltip("Con más semanas que estas (\"Todo\"), las celdas se encogen para que el calendario no ocupe tanto.")]
        [SerializeField] private int        _heatmapCompactAfterWeeks = 6;
        [SerializeField, Range(0.3f, 1f)] private float _heatmapCompactScale = 0.6f;
        [Tooltip("GridLayoutGroup de la fila L M X J V S D (opcional). Se encoge en horizontal junto al calendario para que las letras sigan alineadas con las columnas.")]
        [SerializeField] private GridLayoutGroup _heatmapWeekdays;

        [Header("Leyenda de emociones")]
        [Tooltip("Contenedores donde pintar la leyenda (p. ej. bajo el gráfico y bajo el calendario). Se rellenan una vez: las 8 emociones + \"Sin registro\".")]
        [SerializeField] private Transform[] _legendContainers;
        [Tooltip("Prefab con HeatmapCell (muestra de color, marca de sin registro y texto).")]
        [SerializeField] private GameObject  _legendItemPrefab;
        [SerializeField] private string      _noRecordLegendText = "Sin registro";

        [Header("Tus emociones")]
        [SerializeField] private TextMeshProUGUI _emotionsSummaryLabel;
        [SerializeField] private Transform       _emotionBarsContainer;
        [Tooltip("Prefab con StatsBarRow.")]
        [SerializeField] private GameObject      _emotionBarPrefab;

        [Header("Estabilidad (se oculta sin datos suficientes)")]
        [SerializeField] private GameObject      _stabilitySection;
        [SerializeField] private TextMeshProUGUI _stabilityLabel;

        [Header("Qué influye en ti")]
        [SerializeField] private Transform       _motivesContainer;
        [SerializeField] private TextMeshProUGUI _motivesEmptyLabel;

        [Header("Qué te ayuda")]
        [SerializeField] private Transform       _helpContainer;
        [SerializeField] private TextMeshProUGUI _helpEmptyLabel;
        [SerializeField] private TextMeshProUGUI _breathingLabel;

        [Tooltip("Prefab de una línea de texto (TextMeshProUGUI) para motivos y minijuegos.")]
        [SerializeField] private GameObject _textRowPrefab;

        [Header("Bienestar (WHO-5)")]
        [SerializeField] private UILineChart     _who5Chart;
        [SerializeField] private TextMeshProUGUI _who5Label;

        [Header("Hábitos")]
        [SerializeField] private TextMeshProUGUI _streakCurrentLabel;
        [SerializeField] private TextMeshProUGUI _streakMaxLabel;
        [SerializeField] private TextMeshProUGUI _adherenceLabel;
        [SerializeField] private TextMeshProUGUI _diaryLabel;

        [Header("Exportar")]
        [SerializeField] private Button _exportButton;

        [Header("Temas emocionales (colores)")]
        [SerializeField] private EmotionTheme[] _emotionThemes;

        // ── Eventos ────────────────────────────────────────────────────

        public event Action<ChartPeriod> OnPeriodChanged;
        public event Action              OnExportRequested;

        private GridLayoutGroup _heatmapGrid;
        private Vector2         _heatmapCellSize;
        private Vector2         _heatmapSpacing;
        private Vector2         _weekdaysCellSize;
        private Vector2         _weekdaysSpacing;
        private bool            _legendBuilt;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_heatmapContainer != null) _heatmapGrid = _heatmapContainer.GetComponent<GridLayoutGroup>();
            if (_heatmapGrid != null)
            {
                _heatmapCellSize = _heatmapGrid.cellSize;
                _heatmapSpacing  = _heatmapGrid.spacing;
            }
            if (_heatmapWeekdays != null)
            {
                _weekdaysCellSize = _heatmapWeekdays.cellSize;
                _weekdaysSpacing  = _heatmapWeekdays.spacing;
            }

            _weekButton?.onClick.AddListener(()    => OnPeriodChanged?.Invoke(ChartPeriod.Week));
            _monthButton?.onClick.AddListener(()   => OnPeriodChanged?.Invoke(ChartPeriod.Month));
            _allTimeButton?.onClick.AddListener(() => OnPeriodChanged?.Invoke(ChartPeriod.AllTime));
            _exportButton?.onClick.AddListener(()  => OnExportRequested?.Invoke());
        }

        private void OnDestroy()
        {
            _weekButton?.onClick.RemoveAllListeners();
            _monthButton?.onClick.RemoveAllListeners();
            _allTimeButton?.onClick.RemoveAllListeners();
            _exportButton?.onClick.RemoveAllListeners();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <param name="chartFrom">Primer día del eje del gráfico de ánimo.</param>
        /// <param name="heatmapFrom">Primer día del calendario.</param>
        /// <param name="today">Último día de ambos.</param>
        public void Render(ReportData data, ChartPeriod period, DateTime chartFrom, DateTime heatmapFrom, DateTime today)
        {
            if (data == null) return;
            _buildLegend();

            _updatePeriodButtons(period);
            _renderSummary(data, period);
            _renderMoodChart(data, chartFrom.Date, today.Date);
            _renderHeatmap(data, heatmapFrom.Date, today.Date);
            _renderEmotions(data);
            _renderStability(data, period);
            _renderLines(_motivesContainer, StatsTextBuilder.MotiveLines(data), _motivesEmptyLabel, StatsTextBuilder.MotivesEmpty(data));
            _renderLines(_helpContainer, StatsTextBuilder.HelpLines(data), _helpEmptyLabel, StatsTextBuilder.HelpEmpty(data));
            _setOptionalText(_breathingLabel, StatsTextBuilder.Breathing(data));
            _renderWho5(data);
            _renderHabits(data);
        }

        // ── Bloques ────────────────────────────────────────────────────

        private void _renderSummary(ReportData data, ChartPeriod period)
        {
            _setText(_summaryLabel, StatsTextBuilder.Summary(data, period));

            int? level = StatsTextBuilder.SummaryMoodLevel(data);
            if (_summaryFace != null)
            {
                bool hasFace = level.HasValue && _moodFaceSprites != null && _moodFaceSprites.Length >= 5;
                _summaryFace.gameObject.SetActive(hasFace);
                if (hasFace) _summaryFace.sprite = _moodFaceSprites[level.Value - 1];
            }

            int? trend = period == ChartPeriod.AllTime ? null : StatsTextBuilder.TrendDirection(data);
            if (_trendUpIcon != null)   _trendUpIcon.SetActive(trend == 1);
            if (_trendFlatIcon != null) _trendFlatIcon.SetActive(trend == 0);
            if (_trendDownIcon != null) _trendDownIcon.SetActive(trend == -1);
        }

        /// <summary>X = días desde axisFrom; Y = ánimo 1-5. Puntos del color de la emoción del día y media de 7 días.</summary>
        private void _renderMoodChart(ReportData data, DateTime axisFrom, DateTime axisTo)
        {
            string empty = StatsTextBuilder.MoodChartEmpty(data);
            _setOptionalText(_moodChartEmptyLabel, empty);
            if (_moodChart == null) return;

            var days      = data.Mood.Daily.Where(d => d.Date >= axisFrom && d.Date <= axisTo).ToList();
            var points    = days.Select(d => new Vector2((float)(d.Date - axisFrom).TotalDays, d.Mood)).ToList();
            var colors    = days.Select(d => _emotionColor(d.Emotion)).ToList();
            var average   = days.Select(d => new Vector2((float)(d.Date - axisFrom).TotalDays, d.MovingAverage7)).ToList();
            float lastX   = Mathf.Max(1f, (float)(axisTo - axisFrom).TotalDays);

            _moodChart.SetData(points, new Rect(0f, 0.5f, lastX, 5f), colors, average,
                               new List<float> { 1f, 2f, 3f, 4f, 5f }, maxGap: 1.5f,
                               secondaryMaxGap: ReportCalculator.MovingAverageWindowDays);
        }

        /// <summary>Una celda por día, alineada por semanas (lunes primero), del color de la emoción del día.</summary>
        private void _renderHeatmap(ReportData data, DateTime from, DateTime to)
        {
            _clearContainer(_heatmapContainer);
            if (_heatmapContainer == null || _heatmapCellPrefab == null) return;

            var byDate = data.Mood.Daily.ToDictionary(d => d.Date, d => d.Emotion);
            int leading = ((int)from.DayOfWeek + 6) % 7;

            for (int i = 0; i < leading; i++)
                _setCell(Instantiate(_heatmapCellPrefab, _heatmapContainer, false), Color.clear, false);

            for (DateTime day = from; day <= to; day = day.AddDays(1))
            {
                var cell = Instantiate(_heatmapCellPrefab, _heatmapContainer, false);
                bool recorded = byDate.TryGetValue(day, out var emotion);
                _setCell(cell, recorded ? _emotionColor(emotion) : _heatmapEmptyColor, !recorded);
            }

            // Completar la última semana (días futuros, invisibles) para que las filas queden enteras
            int total    = leading + (int)(to - from).TotalDays + 1;
            int trailing = (7 - total % 7) % 7;
            for (int i = 0; i < trailing; i++)
                _setCell(Instantiate(_heatmapCellPrefab, _heatmapContainer, false), Color.clear, false);

            if (_heatmapGrid != null)
            {
                float scale = (total + trailing) / 7 > _heatmapCompactAfterWeeks ? _heatmapCompactScale : 1f;
                _heatmapGrid.cellSize = _heatmapCellSize * scale;
                _heatmapGrid.spacing  = _heatmapSpacing  * scale;

                // Las letras de los días solo se encogen en horizontal: mismo ancho de columna que el calendario
                if (_heatmapWeekdays != null)
                {
                    _heatmapWeekdays.cellSize = new Vector2(_weekdaysCellSize.x * scale, _weekdaysCellSize.y);
                    _heatmapWeekdays.spacing  = new Vector2(_weekdaysSpacing.x * scale, _weekdaysSpacing.y);
                }
            }

            _rebuildLayout(_heatmapContainer);
        }

        /// <summary>Leyenda fija (orden de valencia del enum): una muestra por emoción y "Sin registro".</summary>
        private void _buildLegend()
        {
            if (_legendBuilt || _legendItemPrefab == null || _legendContainers == null) return;
            _legendBuilt = true;

            foreach (var container in _legendContainers)
            {
                if (container == null) continue;
                _clearContainer(container);
                foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType)))
                    _setCell(Instantiate(_legendItemPrefab, container, false), _emotionColor(emotion), false, emotion.ToDisplayName());
                _setCell(Instantiate(_legendItemPrefab, container, false), _heatmapEmptyColor, true, _noRecordLegendText);
                _rebuildLayout(container);
            }
        }

        private void _renderEmotions(ReportData data)
        {
            _setText(_emotionsSummaryLabel, StatsTextBuilder.EmotionsSummary(data));

            _clearContainer(_emotionBarsContainer);
            if (_emotionBarsContainer == null || _emotionBarPrefab == null) return;

            var counts = data.Emotions.Counts.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ToList();
            int max = counts.Count > 0 ? counts[0].Value : 1;

            foreach (var kv in counts)
            {
                var row = Instantiate(_emotionBarPrefab, _emotionBarsContainer, false).GetComponent<StatsBarRow>();
                row?.Setup(kv.Key.ToDisplayName(), (float)kv.Value / max, kv.Value.ToString(), _emotionColor(kv.Key));
            }

            _rebuildLayout(_emotionBarsContainer);
        }

        private void _renderStability(ReportData data, ChartPeriod period)
        {
            string text = StatsTextBuilder.Stability(data, period);
            if (_stabilitySection != null) _stabilitySection.SetActive(text != null);
            _setText(_stabilityLabel, text);
        }

        private void _renderWho5(ReportData data)
        {
            _setText(_who5Label, StatsTextBuilder.Who5(data));
            if (_who5Chart == null) return;

            bool hasData = data.Who5.Count > 0;
            _who5Chart.gameObject.SetActive(hasData);
            if (!hasData) return;

            DateTime first = data.Who5[0].CompletedAt.Date;
            var points = data.Who5.Select(p => new Vector2((float)(p.CompletedAt.Date - first).TotalDays, p.Score)).ToList();
            float lastX = Mathf.Max(1f, points[points.Count - 1].x);
            _who5Chart.SetData(points, new Rect(0f, 0f, lastX, 100f), gridLines: new List<float> { 0f, 50f, 100f });
        }

        private void _renderHabits(ReportData data)
        {
            var a = data.Adherence;
            _setText(_streakCurrentLabel, ChartsCalculator.FormatStreak(a.CurrentStreak ?? 0));
            _setText(_streakMaxLabel,     ChartsCalculator.FormatStreak(a.LongestStreak ?? 0));
            _setText(_adherenceLabel,     StatsTextBuilder.Adherence(data));
            _setText(_diaryLabel,         StatsTextBuilder.Diary(data));
        }

        // ── Helpers ────────────────────────────────────────────────────

        /// <summary>Una línea de texto por elemento; si no hay ninguno, muestra el texto de vacío.</summary>
        private void _renderLines(Transform container, List<string> lines, TextMeshProUGUI emptyLabel, string emptyText)
        {
            _clearContainer(container);
            bool hasLines = lines.Count > 0;
            _setOptionalText(emptyLabel, hasLines ? null : emptyText);

            if (container == null || _textRowPrefab == null) return;
            foreach (string line in lines)
            {
                var label = Instantiate(_textRowPrefab, container, false).GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = line;
            }
            _rebuildLayout(container);
        }

        private void _updatePeriodButtons(ChartPeriod period)
        {
            _setButtonColor(_weekButton,    period == ChartPeriod.Week    ? _periodActiveColor : _periodInactiveColor);
            _setButtonColor(_monthButton,   period == ChartPeriod.Month   ? _periodActiveColor : _periodInactiveColor);
            _setButtonColor(_allTimeButton, period == ChartPeriod.AllTime ? _periodActiveColor : _periodInactiveColor);
        }

        private Color _emotionColor(EmotionType emotion)
        {
            if (_emotionThemes != null)
                foreach (var theme in _emotionThemes)
                    if (theme != null && theme.emotionType == emotion)
                        return theme.primaryColor;
            return Color.white;
        }

        /// <summary>Usa HeatmapCell si el prefab lo tiene; si no, pinta la Image raíz.</summary>
        private static void _setCell(GameObject cell, Color color, bool noRecord, string label = null)
        {
            var heatmapCell = cell.GetComponent<HeatmapCell>();
            if (heatmapCell != null)
            {
                heatmapCell.Setup(color, noRecord, label);
                return;
            }
            var image = cell.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private static void _setText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }

        /// <summary>Muestra el texto, u oculta la etiqueta si es null.</summary>
        private static void _setOptionalText(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.gameObject.SetActive(text != null);
            if (text != null) label.text = text;
        }

        private static void _setButtonColor(Button button, Color color)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = color;
        }

        private static void _clearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.gameObject.SetActive(false);  // síncrono; LayoutGroup ignora inactivos inmediatamente
                Destroy(child.gameObject);
            }
        }

        private static void _rebuildLayout(Transform container)
        {
            if (container is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                if (rt.parent is RectTransform parentRt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }
        }
    }
}
