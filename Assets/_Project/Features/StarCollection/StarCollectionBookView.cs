using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.StarCollection
{
    /// <summary>
    /// Vista del libro de colección de estrellas: lista con filtro por rareza, contador de
    /// descubiertas y ficha de detalle (nombre, peso, edad, rareza, descripción, frase y veces
    /// pescada). Se usa dentro de StarFisher y en SafeZone. Sin lógica de negocio.
    /// </summary>
    public class StarCollectionBookView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject      _root;
        [SerializeField] private Button          _closeButton;
        [SerializeField] private TextMeshProUGUI _counterLabel;

        [Header("Filtros (Todas + una por rareza, de Común a Legendaria)")]
        [SerializeField] private Button   _filterAllButton;
        [SerializeField] private Button[] _filterRarityButtons = new Button[StarRarityExtensions.Count];
        [SerializeField] private Color    _filterSelectedColor   = Color.white;
        [SerializeField] private Color    _filterUnselectedColor = new Color(1f, 1f, 1f, 0.45f);

        [Header("Lista")]
        [SerializeField] private Transform         _listContainer;
        [SerializeField] private StarBookEntryView _entryPrefab;
        [SerializeField] private GameObject        _emptyLabel;

        [Header("Ficha")]
        [SerializeField] private GameObject      _detailPanel;
        [SerializeField] private Image           _detailImage;
        [SerializeField] private TextMeshProUGUI _detailName;
        [SerializeField] private TextMeshProUGUI _detailRarity;
        [SerializeField] private TextMeshProUGUI _detailWeight;
        [SerializeField] private TextMeshProUGUI _detailAge;
        [SerializeField] private TextMeshProUGUI _detailDescription;
        [SerializeField] private TextMeshProUGUI _detailPhrase;
        [SerializeField] private TextMeshProUGUI _detailTimesCaught;
        [SerializeField] private Button          _detailCloseButton;

        public event Action              OnCloseRequested;
        /// <summary>null = todas las rarezas.</summary>
        public event Action<StarRarity?> OnFilterSelected;
        public event Action<StarBookEntryData> OnEntrySelected;

        private void Awake()
        {
            if (_root == null) _root = gameObject;

            if (_closeButton != null)       _closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
            if (_detailCloseButton != null) _detailCloseButton.onClick.AddListener(HideDetail);
            if (_filterAllButton != null)   _filterAllButton.onClick.AddListener(() => OnFilterSelected?.Invoke(null));

            for (int i = 0; i < _filterRarityButtons.Length; i++)
            {
                var rarity = (StarRarity)i;
                if (_filterRarityButtons[i] != null)
                    _filterRarityButtons[i].onClick.AddListener(() => OnFilterSelected?.Invoke(rarity));
            }
        }

        private void OnDestroy()
        {
            OnCloseRequested = null;
            OnFilterSelected = null;
            OnEntrySelected  = null;
        }

        // ── API pública ────────────────────────────────────────────────

        public void Show(bool show)
        {
            if (!show) HideDetail();
            _root.SetActive(show);
        }

        public void SetCounter(int discovered, int total)
        {
            if (_counterLabel != null) _counterLabel.text = $"{discovered}/{total} descubiertas";
        }

        public void SetFilter(StarRarity? filter)
        {
            _tintFilter(_filterAllButton, filter == null);
            for (int i = 0; i < _filterRarityButtons.Length; i++)
                _tintFilter(_filterRarityButtons[i], filter.HasValue && (int)filter.Value == i);
        }

        public void ShowEntries(List<StarBookEntryData> entries, int streakDays)
        {
            _clearContainer(_listContainer);

            if (_emptyLabel != null) _emptyLabel.SetActive(entries.Count == 0);
            if (_listContainer == null || _entryPrefab == null) return;

            foreach (var entry in entries)
            {
                var view = Instantiate(_entryPrefab, _listContainer, false);
                view.Setup(entry, streakDays, data => OnEntrySelected?.Invoke(data));
            }

            if (_listContainer is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                if (rt.parent is RectTransform parentRt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }
        }

        public void ShowDetail(StarBookEntryData data)
        {
            if (_detailPanel == null) return;

            var star = data.Star;
            if (_detailImage != null)
            {
                _detailImage.sprite = StarSpriteFactory.Or(star.sprite);
                _detailImage.color  = star.sprite != null ? Color.white : star.tint;
                _detailImage.preserveAspect = true;
            }

            _setText(_detailName, star.displayName);
            if (_detailRarity != null)
            {
                _detailRarity.text  = star.rarity.ToDisplayName();
                _detailRarity.color = data.RarityColor;
            }
            _setText(_detailWeight,      $"Peso: {star.weight}");
            _setText(_detailAge,         $"Edad: {star.age}");
            _setText(_detailDescription, star.description);
            _setText(_detailPhrase,      string.IsNullOrEmpty(star.phrase) ? string.Empty : $"“{star.phrase}”");
            _setText(_detailTimesCaught, data.TimesCaught == 1 ? "Pescada 1 vez" : $"Pescada {data.TimesCaught} veces");

            _detailPanel.SetActive(true);
        }

        public void HideDetail()
        {
            if (_detailPanel != null) _detailPanel.SetActive(false);
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _tintFilter(Button button, bool selected)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = selected ? _filterSelectedColor : _filterUnselectedColor;
        }

        private static void _setText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text ?? string.Empty;
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
    }
}
