using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lutra.Features.Who5
{
    /// <summary>
    /// Vista del cuestionario WHO-5. Tres paneles: introducción, pregunta (una por pantalla, con
    /// 6 opciones) y agradecimiento. Sin lógica de negocio: los textos de ítems y opciones los
    /// pone el controlador desde Who5Questionnaire.
    /// </summary>
    public class Who5View : MonoBehaviour
    {
        [Header("Cabecera")]
        [Tooltip("Salir sin enviar: se descartan las respuestas y la notificación sigue pendiente.")]
        [SerializeField] private Button _closeButton;

        [Header("Introducción")]
        [SerializeField] private GameObject      _introPanel;
        [SerializeField] private TextMeshProUGUI _introLabel;
        [SerializeField] private Button          _startButton;

        [Header("Pregunta")]
        [SerializeField] private GameObject      _questionPanel;
        [SerializeField] private TextMeshProUGUI _progressLabel;     // "Pregunta 2 de 5"
        [Tooltip("Image en modo Filled (opcional).")]
        [SerializeField] private Image           _progressFill;
        [SerializeField] private TextMeshProUGUI _itemPrefixLabel;   // "Durante las últimas dos semanas…"
        [SerializeField] private TextMeshProUGUI _itemLabel;
        [Tooltip("6 botones en el orden de Who5Questionnaire.Options (de \"Todo el tiempo\" a \"Nunca\").")]
        [SerializeField] private Button[]          _optionButtons;
        [SerializeField] private TextMeshProUGUI[] _optionLabels;
        [SerializeField] private Color           _optionSelectedColor = new Color(0.40f, 0.60f, 1.00f, 1f);
        [SerializeField] private Color           _optionNormalColor   = Color.white;
        [SerializeField] private Button          _previousButton;
        [SerializeField] private Button          _nextButton;
        [SerializeField] private TextMeshProUGUI _nextButtonLabel;   // "Siguiente" / "Enviar"

        [Header("Agradecimiento")]
        [SerializeField] private GameObject      _thanksPanel;
        [SerializeField] private TextMeshProUGUI _scoreLabel;
        [SerializeField] private TextMeshProUGUI _coinsLabel;
        [SerializeField] private TextMeshProUGUI _nextDateLabel;
        [SerializeField] private Button          _doneButton;

        // ── Eventos ────────────────────────────────────────────────────

        public event Action      OnCloseRequested;
        public event Action      OnStartRequested;
        public event Action<int> OnOptionSelected;   // índice de la opción
        public event Action      OnPreviousRequested;
        public event Action      OnNextRequested;
        public event Action      OnDoneRequested;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _closeButton?.onClick.AddListener(() => OnCloseRequested?.Invoke());
            _startButton?.onClick.AddListener(() => OnStartRequested?.Invoke());
            _previousButton?.onClick.AddListener(() => OnPreviousRequested?.Invoke());
            _nextButton?.onClick.AddListener(() => OnNextRequested?.Invoke());
            _doneButton?.onClick.AddListener(() => OnDoneRequested?.Invoke());

            if (_optionButtons != null)
                for (int i = 0; i < _optionButtons.Length; i++)
                {
                    int index = i;
                    _optionButtons[i]?.onClick.AddListener(() => OnOptionSelected?.Invoke(index));
                }
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveAllListeners();
            _startButton?.onClick.RemoveAllListeners();
            _previousButton?.onClick.RemoveAllListeners();
            _nextButton?.onClick.RemoveAllListeners();
            _doneButton?.onClick.RemoveAllListeners();

            if (_optionButtons != null)
                foreach (var button in _optionButtons)
                    button?.onClick.RemoveAllListeners();
        }

        // ── API pública ────────────────────────────────────────────────

        public void ShowIntro(string instructions)
        {
            _showPanel(_introPanel);
            _setText(_introLabel, instructions);
            _setCloseVisible(true);
        }

        /// <param name="selectedOption">Índice de la opción ya elegida en este ítem, o null.</param>
        public void ShowQuestion(int itemIndex, int itemCount, string prefix, string item,
                                 string[] optionLabels, int? selectedOption, bool isLast)
        {
            _showPanel(_questionPanel);
            _setCloseVisible(true);

            _setText(_progressLabel, $"Pregunta {itemIndex + 1} de {itemCount}");
            if (_progressFill != null) _progressFill.fillAmount = (itemIndex + 1) / (float)itemCount;
            _setText(_itemPrefixLabel, prefix);
            _setText(_itemLabel, item);

            if (_optionLabels != null)
                for (int i = 0; i < _optionLabels.Length && i < optionLabels.Length; i++)
                    _setText(_optionLabels[i], optionLabels[i]);

            SetSelectedOption(selectedOption);

            if (_previousButton != null) _previousButton.gameObject.SetActive(itemIndex > 0);
            _setText(_nextButtonLabel, isLast ? "Enviar" : "Siguiente");
        }

        /// <summary>Resalta la opción elegida y activa "Siguiente"/"Enviar" solo si hay una.</summary>
        public void SetSelectedOption(int? selectedOption)
        {
            if (_optionButtons != null)
                for (int i = 0; i < _optionButtons.Length; i++)
                {
                    var image = _optionButtons[i] != null ? _optionButtons[i].image : null;
                    if (image != null)
                        image.color = selectedOption == i ? _optionSelectedColor : _optionNormalColor;
                }

            if (_nextButton != null) _nextButton.interactable = selectedOption.HasValue;
        }

        /// <summary>Bloquea los botones mientras se guarda el envío.</summary>
        public void SetSubmitting(bool submitting)
        {
            if (_nextButton != null)     _nextButton.interactable     = !submitting;
            if (_previousButton != null) _previousButton.interactable = !submitting;
            if (_closeButton != null)    _closeButton.interactable    = !submitting;
        }

        public void ShowThanks(int score, int coins, string nextDateText)
        {
            _showPanel(_thanksPanel);
            _setCloseVisible(false);

            _setText(_scoreLabel, $"Tu índice de bienestar: {score} / 100");
            _setText(_coinsLabel, coins > 0 ? $"+{coins} monedas" : string.Empty);
            _setText(_nextDateLabel, nextDateText);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _showPanel(GameObject panel)
        {
            if (_introPanel != null)    _introPanel.SetActive(panel == _introPanel);
            if (_questionPanel != null) _questionPanel.SetActive(panel == _questionPanel);
            if (_thanksPanel != null)   _thanksPanel.SetActive(panel == _thanksPanel);
        }

        private void _setCloseVisible(bool visible)
        {
            if (_closeButton != null)
            {
                _closeButton.gameObject.SetActive(visible);
                _closeButton.interactable = true;
            }
        }

        private static void _setText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
        }
    }
}
