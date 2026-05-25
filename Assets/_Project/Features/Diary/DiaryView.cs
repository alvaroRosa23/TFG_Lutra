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
    /// Vista del Diario Personal. Gestiona la lista de entradas y el editor.
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
        public Action<DiaryEntry>  OnEntryCardClicked;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _newEntryButton?.onClick.AddListener(() => OnNewEntryClicked?.Invoke());
            _saveButton?.onClick.AddListener(() => OnSaveClicked?.Invoke());
            _backButton?.onClick.AddListener(() => OnBackClicked?.Invoke());
            _searchInput?.onValueChanged.AddListener(text => OnSearchChanged?.Invoke(text));
        }

        private void OnDestroy()
        {
            _newEntryButton?.onClick.RemoveAllListeners();
            _saveButton?.onClick.RemoveAllListeners();
            _backButton?.onClick.RemoveAllListeners();
            _searchInput?.onValueChanged.RemoveAllListeners();

            OnNewEntryClicked  = null;
            OnSaveClicked      = null;
            OnBackClicked      = null;
            OnSearchChanged    = null;
            OnEntryCardClicked = null;
        }

        // ── API pública ────────────────────────────────────────────────

        public void ShowMainView()
        {
            _mainView?.SetActive(true);
            _editorView?.SetActive(false);
        }

        public void ShowEditorView(DiaryEntry entry)
        {
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

        public void RefreshEntries(List<DiaryEntry> entries)
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
                    _emptyStateLabel.text = "Aún no tienes entradas. ¡Empieza a escribir!";
            }

            if (isEmpty || _entryCardPrefab == null) return;

            foreach (var entry in entries)
            {
                var go   = Instantiate(_entryCardPrefab, _entriesContainer, false);
                var card = go.GetComponent<DiaryEntryCard>();
                if (card == null) continue;

                card.SetupCard(entry, _getEmotionColor(entry.Mood));
                card.OnCardClicked = e => OnEntryCardClicked?.Invoke(e);
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
