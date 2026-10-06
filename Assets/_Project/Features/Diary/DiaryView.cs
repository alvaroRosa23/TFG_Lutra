using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Diary
{
    /// <summary>
    /// Vista del Diario Personal. Gestiona la lista de entradas del mes, el editor y el
    /// diálogo de confirmación de borrado.
    /// No contiene lógica de negocio; delega en DiaryController.
    /// </summary>
    public class DiaryView : MonoBehaviour
    {
        // ── Referencias ────────────────────────────────────────────────

        [Header("Vista principal")]
        [SerializeField] private Transform         _entriesContainer;
        [SerializeField] private GameObject        _entryCardPrefab;
        [SerializeField] private Button            _newEntryButton;
        [SerializeField] private TMP_InputField    _searchInput;
        [SerializeField] private TextMeshProUGUI   _emptyStateLabel;
        [SerializeField] private GameObject        _mainView;

        [Header("Navegación por meses")]
        [SerializeField] private Button            _previousMonthButton;
        [SerializeField] private Button            _nextMonthButton;
        [SerializeField] private TextMeshProUGUI   _monthLabel;

        [Header("Confirmación de borrado")]
        [SerializeField] private GameObject        _deleteConfirmPanel;
        [SerializeField] private TextMeshProUGUI   _deleteConfirmLabel;
        [SerializeField] private Button            _deleteConfirmButton;
        [SerializeField] private Button            _deleteCancelButton;

        [Header("Editor de entrada")]
        [SerializeField] private GameObject        _editorView;
        [SerializeField] private Button            _backButton;
        [SerializeField] private TextMeshProUGUI   _editorDateLabel;
        [SerializeField] private TMP_InputField    _titleInput;
        [SerializeField] private TMP_InputField    _contentInput;
        [SerializeField] private Image             _emotionColorDot;
        [SerializeField] private Button            _saveButton;
        [SerializeField] private TextMeshProUGUI   _errorLabel;

        // ── Eventos públicos ───────────────────────────────────────────

        public Action              OnNewEntryClicked;
        public Action              OnSaveClicked;
        public Action              OnBackClicked;
        public Action<string>      OnSearchChanged;
        public Action              OnPreviousMonthClicked;
        public Action              OnNextMonthClicked;
        public Action<DiaryEntry>  OnEditEntryClicked;
        public Action<DiaryEntry>  OnDeleteEntryClicked;
        public Action              OnDeleteConfirmed;
        public Action              OnDeleteCancelled;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _newEntryButton?.onClick.AddListener(() => OnNewEntryClicked?.Invoke());
            _saveButton?.onClick.AddListener(() => OnSaveClicked?.Invoke());
            _backButton?.onClick.AddListener(() => OnBackClicked?.Invoke());
            _searchInput?.onValueChanged.AddListener(text => OnSearchChanged?.Invoke(text));
            _previousMonthButton?.onClick.AddListener(() => OnPreviousMonthClicked?.Invoke());
            _nextMonthButton?.onClick.AddListener(() => OnNextMonthClicked?.Invoke());
            _deleteConfirmButton?.onClick.AddListener(() => OnDeleteConfirmed?.Invoke());
            _deleteCancelButton?.onClick.AddListener(() => OnDeleteCancelled?.Invoke());

            HideDeleteConfirm();
        }

        private void OnDestroy()
        {
            _newEntryButton?.onClick.RemoveAllListeners();
            _saveButton?.onClick.RemoveAllListeners();
            _backButton?.onClick.RemoveAllListeners();
            _searchInput?.onValueChanged.RemoveAllListeners();
            _previousMonthButton?.onClick.RemoveAllListeners();
            _nextMonthButton?.onClick.RemoveAllListeners();
            _deleteConfirmButton?.onClick.RemoveAllListeners();
            _deleteCancelButton?.onClick.RemoveAllListeners();

            OnNewEntryClicked      = null;
            OnSaveClicked          = null;
            OnBackClicked          = null;
            OnSearchChanged        = null;
            OnPreviousMonthClicked = null;
            OnNextMonthClicked     = null;
            OnEditEntryClicked     = null;
            OnDeleteEntryClicked   = null;
            OnDeleteConfirmed      = null;
            OnDeleteCancelled      = null;
        }

        // ── API pública ────────────────────────────────────────────────

        public void ShowMainView()
        {
            _mainView?.SetActive(true);
            _editorView?.SetActive(false);
        }

        public void ShowEditorView(DiaryEntry entry)
        {
            HideDeleteConfirm();
            _mainView?.SetActive(false);
            _editorView?.SetActive(true);

            if (_editorDateLabel != null)
            {
                string dateStr = entry.Date.ToString("dddd, dd MMMM yyyy",
                    new CultureInfo("es-ES"));
                if (!string.IsNullOrEmpty(dateStr))
                    dateStr = char.ToUpper(dateStr[0]) + dateStr.Substring(1);
                _editorDateLabel.text = dateStr;
            }

            if (_titleInput != null)   _titleInput.text   = entry.Title   ?? "";
            if (_contentInput != null) _contentInput.text = entry.Content ?? "";

            if (_emotionColorDot != null && !string.IsNullOrEmpty(entry.Mood))
                _emotionColorDot.color = _getEmotionColor(entry.Mood);

            ClearError();
        }

        /// <summary>Texto entre las flechas ("Abril 2026") y qué flechas se pueden pulsar.</summary>
        public void SetMonthNavigation(string label, bool canGoPrevious, bool canGoNext)
        {
            if (_monthLabel != null)          _monthLabel.text = label;
            if (_previousMonthButton != null) _previousMonthButton.interactable = canGoPrevious;
            if (_nextMonthButton != null)     _nextMonthButton.interactable     = canGoNext;
        }

        public void ClearSearch()
        {
            _searchInput?.SetTextWithoutNotify(string.Empty);
        }

        public void ShowDeleteConfirm(DiaryEntry entry)
        {
            if (_deleteConfirmLabel != null)
            {
                string title = string.IsNullOrEmpty(entry.Title) ? "esta entrada" : $"«{entry.Title}»";
                _deleteConfirmLabel.text = $"¿Seguro que quieres borrar {title}? No se podrá recuperar.";
            }

            _deleteConfirmPanel?.SetActive(true);
        }

        public void HideDeleteConfirm()
        {
            _deleteConfirmPanel?.SetActive(false);
        }

        /// <param name="emptyMessage">Texto que se muestra si la lista está vacía.</param>
        public void RefreshEntries(List<DiaryEntry> entries, string emptyMessage)
        {
            if (_entriesContainer == null) return;

            for (int i = _entriesContainer.childCount - 1; i >= 0; i--)
            {
                var child = _entriesContainer.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            bool isEmpty = entries == null || entries.Count == 0;

            if (_emptyStateLabel != null)
            {
                _emptyStateLabel.gameObject.SetActive(isEmpty);
                if (isEmpty)
                    _emptyStateLabel.text = emptyMessage;
            }

            if (isEmpty || _entryCardPrefab == null) return;

            foreach (var entry in entries)
            {
                var go   = Instantiate(_entryCardPrefab, _entriesContainer, false);
                var card = go.GetComponent<DiaryEntryCard>();
                if (card == null) continue;

                card.SetupCard(entry, _getEmotionColor(entry.Mood));
                card.OnEditClicked   = e => OnEditEntryClicked?.Invoke(e);
                card.OnDeleteClicked = e => OnDeleteEntryClicked?.Invoke(e);
            }

            if (_entriesContainer is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                if (rt.parent is RectTransform parentRt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }
        }

        public string GetTitle()   => _titleInput?.text.Trim()   ?? string.Empty;
        public string GetContent() => _contentInput?.text.Trim() ?? string.Empty;

        public void ShowError(string msg)
        {
            if (_errorLabel == null) return;
            _errorLabel.gameObject.SetActive(true);
            _errorLabel.text = msg;
        }

        public void ClearError()
        {
            if (_errorLabel == null) return;
            _errorLabel.text = "";
            _errorLabel.gameObject.SetActive(false);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private static Color _getEmotionColor(string mood)
        {
            if (!Enum.TryParse<EmotionType>(mood, out var emotion))
                return _hex("#CCCCCC");

            return emotion switch
            {
                EmotionType.Joy         => _hex("#F5C842"),
                EmotionType.Calm        => _hex("#4EC9A0"),
                EmotionType.Sadness     => _hex("#5B8FCC"),
                EmotionType.Anxiety     => _hex("#8B7ED8"),
                EmotionType.Frustration => _hex("#E8733A"),
                EmotionType.Overwhelm   => _hex("#D45E7A"),
                EmotionType.Nostalgia   => _hex("#B08FC4"),
                EmotionType.Energy      => _hex("#FF6B6B"),
                _                       => _hex("#CCCCCC")
            };
        }

        private static Color _hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
