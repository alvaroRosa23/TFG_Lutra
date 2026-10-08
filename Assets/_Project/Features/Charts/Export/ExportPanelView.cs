using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Panel de exportación del informe (docs/PROFESSIONAL_REPORT.md §4.1): periodo (7 días, 30 días,
    /// 3 meses, todo), incluir notas y diario (desmarcado por defecto), incluir datos en bruto
    /// (marcado) y Generar / Cancelar. Sin lógica: la lleva ReportExportController.
    /// </summary>
    public class ExportPanelView : MonoBehaviour
    {
        [Tooltip("Raíz del panel (con fondo que bloquee la pantalla). Empieza desactivado.")]
        [SerializeField] private GameObject _panel;

        [Header("Periodo")]
        [SerializeField] private Button _last7Button;
        [SerializeField] private Button _last30Button;
        [SerializeField] private Button _last90Button;
        [SerializeField] private Button _allTimeButton;
        [SerializeField] private Color  _selectedColor   = new Color(0.40f, 0.60f, 1.00f, 1f);
        [SerializeField] private Color  _unselectedColor = Color.white;

        [Header("Opciones")]
        [SerializeField] private Toggle _includeNotesToggle;
        [SerializeField] private Toggle _includeCsvToggle;

        [Header("Acciones")]
        [SerializeField] private Button          _generateButton;
        [SerializeField] private Button          _cancelButton;
        [SerializeField] private TextMeshProUGUI _statusLabel;

        public event Action<ExportPeriod> OnPeriodSelected;
        public event Action<bool, bool>   OnGenerateRequested;   // incluir notas, incluir CSV
        public event Action               OnCancelRequested;

        private void Awake()
        {
            _last7Button?.onClick.AddListener(()   => OnPeriodSelected?.Invoke(ExportPeriod.Last7Days));
            _last30Button?.onClick.AddListener(()  => OnPeriodSelected?.Invoke(ExportPeriod.Last30Days));
            _last90Button?.onClick.AddListener(()  => OnPeriodSelected?.Invoke(ExportPeriod.Last90Days));
            _allTimeButton?.onClick.AddListener(() => OnPeriodSelected?.Invoke(ExportPeriod.AllTime));
            _generateButton?.onClick.AddListener(() => OnGenerateRequested?.Invoke(
                _includeNotesToggle != null && _includeNotesToggle.isOn,
                _includeCsvToggle == null || _includeCsvToggle.isOn));
            _cancelButton?.onClick.AddListener(() => OnCancelRequested?.Invoke());

            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            _last7Button?.onClick.RemoveAllListeners();
            _last30Button?.onClick.RemoveAllListeners();
            _last90Button?.onClick.RemoveAllListeners();
            _allTimeButton?.onClick.RemoveAllListeners();
            _generateButton?.onClick.RemoveAllListeners();
            _cancelButton?.onClick.RemoveAllListeners();
        }

        /// <summary>Abre el panel con las opciones por defecto.</summary>
        public void Show(ExportPeriod period)
        {
            _includeNotesToggle?.SetIsOnWithoutNotify(false);
            _includeCsvToggle?.SetIsOnWithoutNotify(true);
            SetSelectedPeriod(period);
            SetBusy(false, null);
            if (_panel != null)
            {
                _panel.SetActive(true);
                _panel.transform.SetAsLastSibling();
            }
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        public void SetSelectedPeriod(ExportPeriod period)
        {
            _setColor(_last7Button,   period == ExportPeriod.Last7Days);
            _setColor(_last30Button,  period == ExportPeriod.Last30Days);
            _setColor(_last90Button,  period == ExportPeriod.Last90Days);
            _setColor(_allTimeButton, period == ExportPeriod.AllTime);
        }

        /// <summary>Bloquea el panel mientras se genera; <paramref name="status"/> null oculta el texto.</summary>
        public void SetBusy(bool busy, string status)
        {
            if (_generateButton != null) _generateButton.interactable = !busy;
            if (_cancelButton != null)   _cancelButton.interactable   = !busy;
            if (_statusLabel != null)
            {
                _statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(status));
                _statusLabel.text = status ?? string.Empty;
            }
        }

        private void _setColor(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = selected ? _selectedColor : _unselectedColor;
        }
    }
}
