using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Events;
using Lutra.UI.Components;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Períodos de visualización de la pantalla de Estadísticas (rangos en ChartPeriodExtensions).
    /// </summary>
    public enum ChartPeriod
    {
        Week,
        Month,
        AllTime
    }

    /// <summary>
    /// Controlador de la pantalla de Estadísticas. Pide el ReportData del período a
    /// ReportDataLoader (las mismas cifras que el informe profesional) y lo pasa a ChartsView.
    /// </summary>
    public class ChartsController : MonoBehaviour
    {
        /// <summary>Semanas que muestra el calendario en "Todo".</summary>
        private const int AllTimeHeatmapWeeks = 12;

        [SerializeField] private ChartsView _view;

        // ── Estado ─────────────────────────────────────────────────────

        private ChartPeriod _currentPeriod = ChartPeriod.Week;
        private int         _loadVersion;   // descarta resultados de cargas anteriores si se cambia de período rápido

        /// <summary>Último período mostrado (lo usa la exportación como valor por defecto).</summary>
        public ChartPeriod CurrentPeriod => _currentPeriod;

        /// <summary>Se pide exportar el informe (lo atiende el panel de exportación, Fase 7).</summary>
        public event Action OnExportRequested;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<ChartsView>();
        }

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnPeriodChanged   += SetPeriod;
            _view.OnExportRequested += _onExportRequested;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnPeriodChanged   -= SetPeriod;
            _view.OnExportRequested -= _onExportRequested;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Abre las estadísticas con el período actualmente seleccionado.</summary>
        public Task OpenCharts() => LoadDataForPeriod(_currentPeriod);

        /// <summary>Cambia el período y recarga los datos.</summary>
        public void SetPeriod(ChartPeriod period) => _ = LoadDataForPeriod(period);

        /// <summary>Calcula el período indicado y refresca la vista.</summary>
        public async Task LoadDataForPeriod(ChartPeriod period)
        {
            _currentPeriod = period;
            int version = ++_loadVersion;

            try
            {
                DateTime now = DateTime.Now;
                var (from, to) = period.GetRange(now);
                var data = await ReportDataLoader.LoadAsync(from, to);
                if (version != _loadVersion) return;

                DateTime today = now.Date;
                DateTime chartFrom, heatmapFrom;
                if (period == ChartPeriod.AllTime)
                {
                    chartFrom   = data.From <= today ? data.From : today;
                    heatmapFrom = ReportCalculator.WeekStart(today).AddDays(-7 * (AllTimeHeatmapWeeks - 1));
                    if (heatmapFrom < chartFrom) heatmapFrom = chartFrom;
                }
                else if (period == ChartPeriod.Week)
                {
                    // Calendario: la semana natural (lunes-domingo) en una sola fila. El lunes siempre
                    // cae dentro de los últimos 7 días, así que no hace falta cargar más datos
                    chartFrom   = from.Date;
                    heatmapFrom = ReportCalculator.WeekStart(today);
                }
                else
                {
                    chartFrom = heatmapFrom = from.Date;
                }

                _view?.Render(data, period, chartFrom, heatmapFrom, today);
                EventBus.EmitChartPeriodChanged(period);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChartsController] LoadDataForPeriod: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        // ── Handlers ───────────────────────────────────────────────────

        private void _onExportRequested() => OnExportRequested?.Invoke();
    }
}
