using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Features.Minigames
{
    /// <summary>
    /// Vista del selector de minijuegos. Gestiona la lista, el panel de detalle
    /// y el dropdown de filtros. El controlador es responsable de proporcionar
    /// los datos y reaccionar a los eventos.
    /// </summary>
    public class MinigamesView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_InputField _searchInput;
        [SerializeField] private Button         _filterButton;
        [SerializeField] private GameObject     _filterDropdown;
        [SerializeField] private Transform      _filterTagsContainer;

        [Header("Recomendados")]
        [SerializeField] private GameObject _recommendedSection;
        [SerializeField] private Transform  _recommendedContainer;

        [Header("Todos")]
        [SerializeField] private Transform _allMinigamesContainer;

        [Header("Panel detalle")]
        [SerializeField] private GameObject      _detailPanel;
        [SerializeField] private TextMeshProUGUI _detailTitle;
        [SerializeField] private Image           _detailPreviewImage;
        [SerializeField] private TextMeshProUGUI _detailDescription;
        [SerializeField] private TextMeshProUGUI _detailTime;
        [SerializeField] private TextMeshProUGUI _detailRecord;
        [SerializeField] private Transform       _detailTagsContainer;
        [SerializeField] private Button          _detailPlayButton;
        [SerializeField] private Button          _detailCloseButton;

        [Header("Layout")]
        [SerializeField] private RectTransform _scrollViewRect;
        [SerializeField] private RectTransform _filterDropdownRect;

        [Header("Prefabs")]
        [SerializeField] private GameObject _minigameCardPrefab;
        [SerializeField] private GameObject _tagPrefab;

        // ── Eventos públicos ───────────────────────────────────────────

        public Action<string>             OnSearchChanged;
        public Action<MinigameTag?>       OnFilterChanged;
        public Action<MinigameDefinition> OnCardSelected;
        public Action<MinigameType>       OnPlayRequested;
        public Action                     OnFilterDropdownRequested;

        // ── Estado interno ─────────────────────────────────────────────

        private MinigameDefinition _currentDetailDef;
        private MinigameTag?       _activeFilterTag;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _searchInput?.onValueChanged.AddListener(_onSearchInputChanged);
            _filterButton?.onClick.AddListener(_onFilterButtonClicked);
            _detailCloseButton?.onClick.AddListener(HideDetailPanel);
            _detailPlayButton?.onClick.AddListener(_onDetailPlayClicked);

            _detailPanel?.SetActive(false);
            _filterDropdown?.SetActive(false);
        }

        private void OnDestroy()
        {
            _searchInput?.onValueChanged.RemoveAllListeners();
            _filterButton?.onClick.RemoveAllListeners();
            _detailCloseButton?.onClick.RemoveAllListeners();
            _detailPlayButton?.onClick.RemoveAllListeners();

            OnSearchChanged           = null;
            OnFilterChanged           = null;
            OnCardSelected            = null;
            OnPlayRequested           = null;
            OnFilterDropdownRequested = null;
        }

        // ── API pública ────────────────────────────────────────────────

        public void ShowMinigames(List<MinigameDefinition> all, List<MinigameDefinition> recommended)
        {
            _populateContainer(_allMinigamesContainer, all);
            UpdateRecommendedSection(recommended);
        }

        /// <summary>Actualiza solo la sección "todos", sin tocar los recomendados.</summary>
        public void UpdateAllSection(List<MinigameDefinition> all)
        {
            _populateContainer(_allMinigamesContainer, all);
        }

        public void ShowDetailPanel(MinigameDefinition def, string recordText)
        {
            _currentDetailDef = def;

            if (_detailTitle != null)       _detailTitle.text       = def.displayName;
            if (_detailDescription != null) _detailDescription.text = def.description;
            if (_detailTime != null)        _detailTime.text        = _formatTime(def.estimatedTimeSeconds);
            if (_detailRecord != null)      _detailRecord.text      = recordText;

            if (_detailPreviewImage != null && def.previewImage != null)
                _detailPreviewImage.sprite = def.previewImage;

            _populateDetailTags(def);

            if (_detailPlayButton != null)
            {
                _detailPlayButton.interactable = def.isAvailable;
                var label = _detailPlayButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = def.isAvailable ? "Jugar" : "Próximamente";
            }

            _detailPanel?.SetActive(true);
        }

        public void HideDetailPanel()
        {
            _detailPanel?.SetActive(false);
            _currentDetailDef = null;
        }

        public void ShowFilterDropdown(List<MinigameTag> availableTags)
        {
            _clearContainer(_filterTagsContainer);

            if (availableTags != null && _tagPrefab != null && _filterTagsContainer != null)
            {
                foreach (var tag in availableTags)
                {
                    var tagObj = Instantiate(_tagPrefab, _filterTagsContainer, false);
                    var label  = tagObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                        label.text = tag.ToString();

                    var btn = tagObj.GetComponent<Button>() ?? tagObj.AddComponent<Button>();
                    MinigameTag captured = tag;
                    btn.onClick.AddListener(() => _onFilterTagClicked(captured));

                    _updateTagVisual(tagObj, _activeFilterTag.HasValue && _activeFilterTag.Value == tag);
                }
            }

            _filterDropdown?.SetActive(true);
            StartCoroutine(_adjustScrollViewAfterDropdown());
        }

        public void HideFilterDropdown()
        {
            _filterDropdown?.SetActive(false);

            if (_scrollViewRect != null)
                _scrollViewRect.offsetMax = new Vector2(0f, -160f);
        }

        public void UpdateRecommendedSection(List<MinigameDefinition> recommended)
        {
            bool hasRecommended = recommended != null && recommended.Count > 0;
            _recommendedSection?.SetActive(hasRecommended);

            if (hasRecommended)
                _populateContainer(_recommendedContainer, recommended);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _populateContainer(Transform container, List<MinigameDefinition> definitions)
        {
            _clearContainer(container);

            if (definitions == null || container == null || _minigameCardPrefab == null) return;

            foreach (var def in definitions)
            {
                if (def == null) continue;
                var cardObj = Instantiate(_minigameCardPrefab, container, false);
                var card    = cardObj.GetComponent<MinigameCard>();

                if (card != null)
                {
                    MinigameDefinition captured = def;
                    card.Setup(def, () => OnCardSelected?.Invoke(captured));
                }
            }

            if (container is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                if (rt.parent is RectTransform parentRt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }
        }

        private void _populateDetailTags(MinigameDefinition def)
        {
            _clearContainer(_detailTagsContainer);

            if (def.tags == null || _tagPrefab == null || _detailTagsContainer == null) return;

            foreach (var tag in def.tags)
            {
                var tagObj = Instantiate(_tagPrefab, _detailTagsContainer, false);
                var label  = tagObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = tag.ToString();
            }
        }

        private IEnumerator _adjustScrollViewAfterDropdown()
        {
            yield return null;
            if (_filterDropdownRect != null && _scrollViewRect != null)
            {
                float height = _filterDropdownRect.rect.height;
                _scrollViewRect.offsetMax = new Vector2(0f, -(160f + height));
            }
        }

        private void _clearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.gameObject.SetActive(false); // el LayoutGroup ignora inactivos inmediatamente
                Destroy(child.gameObject);
            }
        }

        private void _onFilterTagClicked(MinigameTag tag)
        {
            if (_activeFilterTag.HasValue && _activeFilterTag.Value == tag)
            {
                _activeFilterTag = null;
                OnFilterChanged?.Invoke(null);
            }
            else
            {
                _activeFilterTag = tag;
                OnFilterChanged?.Invoke(tag);
            }

            HideFilterDropdown();
        }

        private void _onFilterButtonClicked()
        {
            if (_filterDropdown != null && _filterDropdown.activeSelf)
                HideFilterDropdown();
            else
                OnFilterDropdownRequested?.Invoke();
        }

        private void _onSearchInputChanged(string value)
        {
            OnSearchChanged?.Invoke(value);
        }

        private void _onDetailPlayClicked()
        {
            if (_currentDetailDef != null)
                OnPlayRequested?.Invoke(_currentDetailDef.minigameType);
        }

        private static void _updateTagVisual(GameObject tagObj, bool isActive)
        {
            var img = tagObj.GetComponent<Image>();
            if (img != null)
                img.color = isActive ? new Color(0.3f, 0.7f, 1f, 1f) : Color.white;
        }

        private static string _formatTime(int seconds)
        {
            return seconds < 60 ? $"{seconds}s" : $"{seconds / 60}m";
        }
    }
}
