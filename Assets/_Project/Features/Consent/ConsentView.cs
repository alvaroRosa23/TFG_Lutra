using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.Consent
{
    /// <summary>
    /// Vista del consentimiento: texto informativo con scroll, casilla obligatoria, casilla
    /// opcional del análisis del diario (marcada por defecto) y botones Continuar / No acepto.
    /// "Continuar" solo se activa con la casilla obligatoria marcada.
    /// </summary>
    public class ConsentView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _bodyLabel;

        [Header("Casillas")]
        [SerializeField] private Toggle          _requiredToggle;
        [SerializeField] private TextMeshProUGUI _requiredLabel;
        [SerializeField] private Toggle          _diaryAnalysisToggle;
        [SerializeField] private TextMeshProUGUI _diaryAnalysisLabel;

        [Header("Botones")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _declineButton;

        public event Action<bool> OnContinueRequested;   // parámetro: análisis del diario permitido
        public event Action       OnDeclineRequested;

        private void Awake()
        {
            _requiredToggle?.onValueChanged.AddListener(_onRequiredChanged);
            _continueButton?.onClick.AddListener(() =>
                OnContinueRequested?.Invoke(_diaryAnalysisToggle == null || _diaryAnalysisToggle.isOn));
            _declineButton?.onClick.AddListener(() => OnDeclineRequested?.Invoke());
        }

        private void OnDestroy()
        {
            _requiredToggle?.onValueChanged.RemoveAllListeners();
            _continueButton?.onClick.RemoveAllListeners();
            _declineButton?.onClick.RemoveAllListeners();
        }

        /// <summary>Rellena los textos y deja las casillas en su estado inicial.</summary>
        public void Show(string title, string body, string requiredText, string diaryText)
        {
            if (_titleLabel != null)         _titleLabel.text         = title;
            if (_bodyLabel != null)          _bodyLabel.text          = body;
            if (_requiredLabel != null)      _requiredLabel.text      = requiredText;
            if (_diaryAnalysisLabel != null) _diaryAnalysisLabel.text = diaryText;

            _requiredToggle?.SetIsOnWithoutNotify(false);
            _diaryAnalysisToggle?.SetIsOnWithoutNotify(true);
            SetBusy(false);
        }

        /// <summary>Bloquea los botones mientras se guarda.</summary>
        public void SetBusy(bool busy)
        {
            if (_continueButton != null)
                _continueButton.interactable = !busy && _requiredToggle != null && _requiredToggle.isOn;
            if (_declineButton != null)
                _declineButton.interactable = !busy;
        }

        private void _onRequiredChanged(bool _) => SetBusy(false);
    }
}
