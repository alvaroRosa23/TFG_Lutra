using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.UI.Components;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Controlador de la exportación: abre el panel cuando ChartsView pide exportar, genera el PDF
    /// (y el ZIP de CSV) con ReportExporter y abre el menú de compartir del sistema. La exportación
    /// la inicia siempre el usuario: el paciente decide con quién comparte su informe.
    /// </summary>
    public class ReportExportController : MonoBehaviour
    {
        [SerializeField] private ChartsController _charts;
        [SerializeField] private ExportPanelView  _view;

        private ExportPeriod _period = ExportPeriod.Last30Days;
        private bool         _exporting;

        private void Awake()
        {
            if (_charts == null) _charts = GetComponentInParent<ChartsController>();
            if (_view == null)   _view   = GetComponentInChildren<ExportPanelView>(true);
        }

        private void OnEnable()
        {
            if (_charts != null) _charts.OnExportRequested += _onExportRequested;
            if (_view == null) return;
            _view.OnPeriodSelected    += _onPeriodSelected;
            _view.OnGenerateRequested += _onGenerateRequested;
            _view.OnCancelRequested   += _onCancelRequested;
        }

        private void OnDisable()
        {
            if (_charts != null) _charts.OnExportRequested -= _onExportRequested;
            if (_view == null) return;
            _view.OnPeriodSelected    -= _onPeriodSelected;
            _view.OnGenerateRequested -= _onGenerateRequested;
            _view.OnCancelRequested   -= _onCancelRequested;
        }

        // ── Handlers ───────────────────────────────────────────────────

        /// <summary>Por defecto, últimos 30 días; si en Estadísticas se ve la semana o todo, ese mismo periodo.</summary>
        private void _onExportRequested()
        {
            if (_charts != null)
            {
                switch (_charts.CurrentPeriod)
                {
                    case ChartPeriod.Week:    _period = ExportPeriod.Last7Days;  break;
                    case ChartPeriod.AllTime: _period = ExportPeriod.AllTime;    break;
                    default:                  _period = ExportPeriod.Last30Days; break;
                }
            }
            _view?.Show(_period);
        }

        private void _onPeriodSelected(ExportPeriod period)
        {
            _period = period;
            _view?.SetSelectedPeriod(period);
        }

        private void _onCancelRequested()
        {
            if (!_exporting) _view?.Hide();
        }

        private void _onGenerateRequested(bool includeNotes, bool includeCsv) => _ = _safeExport(includeNotes, includeCsv);

        private async Task _safeExport(bool includeNotes, bool includeCsv)
        {
            if (_exporting) return;
            _exporting = true;
            _view?.SetBusy(true, "Generando tu informe…");

            try
            {
                var files = await ReportExporter.ExportAsync(_period, includeNotes, includeCsv);
                _view?.Hide();
                FileSharer.Share(files, "Informe de seguimiento emocional (Lutra)",
                                 "Te comparto mi informe de seguimiento emocional generado con Lutra.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ReportExportController] _safeExport: {ex.Message}\n{ex.StackTrace}");
                _view?.SetBusy(false, null);
                ToastNotification.ShowError("No se pudo generar el informe, inténtalo de nuevo");
            }
            finally
            {
                _exporting = false;
            }
        }
    }
}
