using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Vista de la Zona Segura. Gestiona la habitación decorable, el inventario de objetos
    /// comprados y la tienda. No contiene lógica de negocio.
    /// </summary>
    public class SafeZoneView : MonoBehaviour
    {
        // ── Tabs ───────────────────────────────────────────────────────

        [Header("Tabs")]
        [SerializeField] private Button     _roomTabButton;
        [SerializeField] private Button     _shopTabButton;
        [SerializeField] private GameObject _roomPanel;
        [SerializeField] private GameObject _shopPanel;

        // ── Habitación ─────────────────────────────────────────────────

        [Header("Habitación")]
        [SerializeField] private PlacementPoint[]    _placementPoints;
        [SerializeField] private TextMeshProUGUI     _coinsLabel;

        // ── Inventario (barra inferior de items sin colocar) ───────────

        [Header("Inventario")]
        [SerializeField] private Canvas     _rootCanvasOverride;
        [SerializeField] private GameObject _inventoryBar;
        [SerializeField] private Transform  _inventoryContainer;
        [SerializeField] private GameObject _inventoryItemPrefab;

        // ── Tienda ─────────────────────────────────────────────────────

        [Header("Tienda")]
        [SerializeField] private Transform  _shopContainer;
        [SerializeField] private GameObject _shopItemPrefab;
        [SerializeField] private Button     _shopTabDecorationButton;
        [SerializeField] private Button     _shopTabAccessoryButton;

        // ── Diálogo de confirmación de compra ──────────────────────────

        [Header("Diálogo compra")]
        [SerializeField] private GameObject      _confirmBuyDialog;
        [SerializeField] private Image           _confirmBuyPreview;
        [SerializeField] private TextMeshProUGUI _confirmBuyNameLabel;
        [SerializeField] private TextMeshProUGUI _confirmBuyPriceLabel;
        [SerializeField] private TextMeshProUGUI _confirmBuyCurrentCoinsLabel;
        [SerializeField] private TextMeshProUGUI _confirmBuyAfterCoinsLabel;
        [SerializeField] private Button          _confirmBuyButton;
        [SerializeField] private Button          _cancelBuyButton;

        // ── Diálogo de opciones de item en habitación (guardar / vender) ─────

        [Header("Diálogo opciones habitación")]
        [SerializeField] private GameObject      _roomItemOptionsDialog;
        [SerializeField] private TextMeshProUGUI _roomItemNameLabel;
        [SerializeField] private Button          _removeButton;
        [SerializeField] private Button          _sellButton;
        [SerializeField] private Button          _cancelRoomOptionsButton;

        // ── Diálogo de confirmación de venta ───────────────────────────

        [Header("Diálogo confirmación venta")]
        [SerializeField] private GameObject      _sellConfirmDialog;
        [SerializeField] private TextMeshProUGUI _sellMessageLabel;
        [SerializeField] private TextMeshProUGUI _sellCoinsLabel;
        [SerializeField] private Button          _confirmSellButton;
        [SerializeField] private Button          _cancelSellButton;

        // ── Popup de opciones de item del inventario ───────────────────

        [Header("Popup inventario")]
        [SerializeField] private GameObject      _inventoryItemPopup;
        [SerializeField] private TextMeshProUGUI _popupNameLabel;
        [SerializeField] private Button          _popupPlaceButton;
        [SerializeField] private Button          _popupSellButton;
        /// <summary>Botón invisible que cubre la pantalla; cierra el popup al pulsar fuera.</summary>
        [SerializeField] private Button          _popupBackdrop;

        // ── Feedback ───────────────────────────────────────────────────

        [Header("Feedback")]
        [SerializeField] private GameObject _notEnoughCoinsPanel;

        // ── Eventos públicos ───────────────────────────────────────────

        public Action<SafeZoneItem> OnBuyRequested;
        public Action               OnBuyConfirmed;
        public Action               OnBuyCancelled;

        public Action<SafeZoneItem> OnPlaceRequested;
        public Action<int>          OnPlacementConfirmed;
        public Action               OnPlacementCancelled;

        /// <summary>El usuario pulsa "Vender" en un ítem del inventario (no colocado).</summary>
        public Action<SafeZoneItem> OnSellInventoryRequested;

        public Action<int>          OnRoomItemTapped;
        public Action               OnRemoveConfirmed;
        /// <summary>El usuario pulsa "Vender" en el contextMenu; la View muestra SellConfirm.</summary>
        public Action               OnSellPreviewRequested;
        public Action               OnSellConfirmed;
        public Action               OnSellCancelled;

        /// <summary>Se dispara cuando DraggableItem suelta sobre un PlacementPoint.</summary>
        public Action<int, string>  OnDropReceived;

        // ── Estado interno ─────────────────────────────────────────────

        private bool          _placementModeActive;
        private PlacementType _pendingPlacementType;
        private Canvas        _rootCanvas;
        private SafeZoneItem  _selectedInventoryItem;
        private bool          _hasUnplacedItems;
        private bool?         _activeShopFilter;   // null = todos, false = decoración, true = accesorios

        private readonly List<(GameObject go, ItemCategory category)> _shopItems = new();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _rootCanvas = _rootCanvasOverride != null
                ? _rootCanvasOverride
                : GetComponentInParent<Canvas>();

            _roomTabButton?.onClick.AddListener(_onRoomTabClicked);
            _shopTabButton?.onClick.AddListener(_onShopTabClicked);
            _confirmBuyButton?.onClick.AddListener(() => OnBuyConfirmed?.Invoke());
            _cancelBuyButton?.onClick.AddListener(() => OnBuyCancelled?.Invoke());
            _removeButton?.onClick.AddListener(() => OnRemoveConfirmed?.Invoke());
            _sellButton?.onClick.AddListener(() => OnSellPreviewRequested?.Invoke());
            _cancelRoomOptionsButton?.onClick.AddListener(() => OnSellCancelled?.Invoke());
            _confirmSellButton?.onClick.AddListener(() => OnSellConfirmed?.Invoke());
            _cancelSellButton?.onClick.AddListener(() => OnSellCancelled?.Invoke());

            _shopTabDecorationButton?.onClick.AddListener(_onShopDecorationTabClicked);
            _shopTabAccessoryButton?.onClick.AddListener(_onShopAccessoryTabClicked);
            _popupBackdrop?.onClick.AddListener(HideInventoryItemPopup);
            _popupPlaceButton?.onClick.AddListener(_onPopupPlaceClicked);
            _popupSellButton?.onClick.AddListener(_onPopupSellClicked);

            _confirmBuyDialog?.SetActive(false);
            _roomItemOptionsDialog?.SetActive(false);
            _sellConfirmDialog?.SetActive(false);
            _inventoryItemPopup?.SetActive(false);
            _notEnoughCoinsPanel?.SetActive(false);
            _shopPanel?.SetActive(false);
            _inventoryBar?.SetActive(true);

            _subscribeToPlacementPoints();
        }

        private void OnDestroy()
        {
            _roomTabButton?.onClick.RemoveAllListeners();
            _shopTabButton?.onClick.RemoveAllListeners();
            _confirmBuyButton?.onClick.RemoveAllListeners();
            _cancelBuyButton?.onClick.RemoveAllListeners();
            _removeButton?.onClick.RemoveAllListeners();
            _sellButton?.onClick.RemoveAllListeners();
            _cancelRoomOptionsButton?.onClick.RemoveAllListeners();
            _confirmSellButton?.onClick.RemoveAllListeners();
            _cancelSellButton?.onClick.RemoveAllListeners();
            _shopTabDecorationButton?.onClick.RemoveAllListeners();
            _shopTabAccessoryButton?.onClick.RemoveAllListeners();
            _popupBackdrop?.onClick.RemoveAllListeners();
            _popupPlaceButton?.onClick.RemoveAllListeners();
            _popupSellButton?.onClick.RemoveAllListeners();

            _unsubscribeFromPlacementPoints();

            OnBuyRequested           = null;
            OnBuyConfirmed           = null;
            OnBuyCancelled           = null;
            OnPlaceRequested         = null;
            OnPlacementConfirmed     = null;
            OnPlacementCancelled     = null;
            OnSellInventoryRequested = null;
            OnRoomItemTapped         = null;
            OnRemoveConfirmed        = null;
            OnSellPreviewRequested   = null;
            OnSellConfirmed          = null;
            OnSellCancelled          = null;
            OnDropReceived           = null;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Actualiza la habitación con los ítems colocados.</summary>
        public void RefreshRoom(List<InventoryItem> inventory, SafeZoneItem[] allItems)
        {
            if (_placementPoints == null) return;

            // Limpiar todos los puntos
            foreach (var point in _placementPoints)
                if (point != null) point.SetOccupied(false);

            if (inventory == null || allItems == null) return;

            foreach (var inv in inventory)
            {
                if (!inv.IsPlaced || inv.PlacementIndex < 0) continue;
                if (inv.PlacementIndex >= _placementPoints.Length) continue;

                var point = _placementPoints[inv.PlacementIndex];
                if (point == null) continue;

                var def = _findItem(inv.ItemId, allItems);
                if (def == null) continue;

                if (def.category != ItemCategory.MascotAccessory)
                    point.SetOccupied(true, inv.ItemId, def.prefab);
            }
        }

        /// <summary>Actualiza la barra de inventario con los ítems comprados pero no colocados.</summary>
        public void RefreshInventoryBar(List<InventoryItem> inventory, SafeZoneItem[] allItems)
        {
            _clearContainer(_inventoryContainer);

            if (inventory == null || allItems == null || _inventoryItemPrefab == null) return;

            bool hasUnplaced = false;

            foreach (var inv in inventory)
            {
                if (inv.IsPlaced) continue;

                var def = _findItem(inv.ItemId, allItems);
                if (def == null || def.category == ItemCategory.MascotAccessory) continue;
                if (def.placementType == PlacementType.None) continue;

                hasUnplaced = true;

                var go   = Instantiate(_inventoryItemPrefab, _inventoryContainer, false);
                var view = go.GetComponent<InventoryItemView>();

                if (view != null)
                    view.Setup(def, (item, anchor) => _showInventoryItemPopup(item, anchor));

                // Reutilizar DraggableItem si el prefab ya lo tiene, si no añadir uno nuevo
                var draggable = go.GetComponent<DraggableItem>() ?? go.AddComponent<DraggableItem>();
                draggable.Setup(def.itemId, def.placementType, _rootCanvas);
                draggable.OnDragStarted   += d => { HideInventoryItemPopup(); EnterPlacementMode(d.PlacementType); };
                draggable.OnDragCancelled += d => ExitPlacementMode();
            }

            _hasUnplacedItems = hasUnplaced;
            _inventoryBar?.SetActive(hasUnplaced);

            if (_inventoryContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        /// <summary>Actualiza la tienda con los ítems disponibles para comprar.</summary>
        public void RefreshShop(SafeZoneItem[] items, List<string> ownedIds, int coins)
        {
            _shopItems.Clear();
            _clearContainer(_shopContainer);
            UpdateCoins(coins);

            if (items == null || _shopItemPrefab == null) return;

            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.isUnlockedByDefault) continue;
                if (ownedIds != null && ownedIds.Contains(item.itemId)) continue;

                var go   = Instantiate(_shopItemPrefab, _shopContainer, false);
                var view = go.GetComponent<ShopItemView>();
                if (view != null)
                {
                    SafeZoneItem captured = item;
                    view.Setup(item, i => OnBuyRequested?.Invoke(i));
                }
                _shopItems.Add((go, item.category));
            }

            _filterShopItems();
        }

        /// <summary>Muestra el diálogo de confirmación de compra.</summary>
        public void ShowConfirmBuyDialog(SafeZoneItem item, int currentCoins, int coinsAfter)
        {
            if (_confirmBuyDialog == null) return;

            if (_confirmBuyNameLabel  != null) _confirmBuyNameLabel.text  = item.displayName;
            if (_confirmBuyPreview    != null && item.previewSprite != null)
                _confirmBuyPreview.sprite = item.previewSprite;

            if (_confirmBuyPriceLabel != null)
            {
                _confirmBuyPriceLabel.text = item.requiredStreakDays > 0
                    ? $"Requiere {item.requiredStreakDays} días de racha"
                    : $"{item.coinCost} monedas";
            }

            if (_confirmBuyCurrentCoinsLabel != null)
                _confirmBuyCurrentCoinsLabel.text = $"{currentCoins}";

            if (_confirmBuyAfterCoinsLabel != null)
                _confirmBuyAfterCoinsLabel.text = item.requiredStreakDays > 0
                    ? "-"
                    : $"{coinsAfter}";

            _confirmBuyDialog.SetActive(true);
        }

        /// <summary>Muestra el menú contextual de un item (mover / guardar / vender).</summary>
        public void ShowRoomItemOptions(SafeZoneItem item)
        {
            if (_roomItemOptionsDialog == null) return;

            if (_roomItemNameLabel != null) _roomItemNameLabel.text = item.displayName;
            if (_sellButton != null)
                _sellButton.gameObject.SetActive(item.coinCost > 0);

            _roomItemOptionsDialog.SetActive(true);
        }

        /// <summary>Muestra el diálogo de confirmación de venta con el precio de reembolso.</summary>
        public void ShowSellConfirmDialog(SafeZoneItem item, int refundAmount)
        {
            if (_sellConfirmDialog == null) return;

            _roomItemOptionsDialog?.SetActive(false);

            if (_sellMessageLabel != null) _sellMessageLabel.text = $"¿Vender {item.displayName}?";
            if (_sellCoinsLabel   != null)
                _sellCoinsLabel.text = refundAmount > 0
                    ? $"Recibirás: {refundAmount} monedas"
                    : "Sin reembolso";

            _sellConfirmDialog.SetActive(true);
        }

        public void HideAllDialogs()
        {
            _confirmBuyDialog?.SetActive(false);
            _roomItemOptionsDialog?.SetActive(false);
            _sellConfirmDialog?.SetActive(false);
            HideInventoryItemPopup();
        }

        public void HideInventoryItemPopup()
        {
            _inventoryItemPopup?.SetActive(false);
            _selectedInventoryItem = null;
        }

        public void UpdateCoins(int amount)
        {
            if (_coinsLabel != null)
                _coinsLabel.text = amount.ToString();
        }

        public void ShowNotEnoughCoins()
        {
            if (_notEnoughCoinsPanel == null) return;
            StartCoroutine(_notEnoughCoinsFeedback());
        }

        /// <summary>Entra en modo de colocación: resalta los PlacementPoints válidos.</summary>
        public void EnterPlacementMode(PlacementType type)
        {
            _placementModeActive  = true;
            _pendingPlacementType = type;

            // Bloquear el resto del inventario para evitar drags simultáneos
            _setInventoryContainerRaycasts(false);

            if (_placementPoints == null) return;
            foreach (var p in _placementPoints)
                if (p != null) p.SetPlacementMode(true, type);
        }

        /// <summary>Sale del modo de colocación.</summary>
        public void ExitPlacementMode()
        {
            _placementModeActive = false;

            _setInventoryContainerRaycasts(true);

            if (_placementPoints == null) return;
            foreach (var p in _placementPoints)
                if (p != null) p.SetPlacementMode(false, PlacementType.None);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _setInventoryContainerRaycasts(bool enabled)
        {
            if (_inventoryContainer == null) return;
            var cg = _inventoryContainer.GetComponent<CanvasGroup>();
            if (cg == null) cg = _inventoryContainer.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = enabled;
        }

        private void _showInventoryItemPopup(SafeZoneItem item, RectTransform anchor)
        {
            if (_inventoryItemPopup == null) return;

            _selectedInventoryItem = item;

            if (_popupNameLabel  != null) _popupNameLabel.text = item.displayName;
            if (_popupSellButton != null) _popupSellButton.gameObject.SetActive(item.coinCost > 0);

            _inventoryItemPopup.SetActive(true);
        }

        private void _onPopupPlaceClicked()
        {
            var item = _selectedInventoryItem;
            HideInventoryItemPopup();
            OnPlaceRequested?.Invoke(item);
        }

        private void _onPopupSellClicked()
        {
            var item = _selectedInventoryItem;
            HideInventoryItemPopup();
            OnSellInventoryRequested?.Invoke(item);
        }

        private void _onShopDecorationTabClicked()
        {
            _activeShopFilter = false;
            _filterShopItems();
        }

        private void _onShopAccessoryTabClicked()
        {
            _activeShopFilter = true;
            _filterShopItems();
        }

        private void _filterShopItems()
        {
            foreach (var (go, category) in _shopItems)
            {
                if (go == null) continue;
                bool show = _activeShopFilter == null
                    || (_activeShopFilter.Value
                        ? category == ItemCategory.MascotAccessory
                        : category != ItemCategory.MascotAccessory);
                go.SetActive(show);
            }

            if (_shopContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private void _onRoomTabClicked()
        {
            _shopPanel?.SetActive(false);
            _roomPanel?.SetActive(true);
            _inventoryBar?.SetActive(_hasUnplacedItems);
            HideAllDialogs();
        }

        private void _onShopTabClicked()
        {
            _roomPanel?.SetActive(false);
            _shopPanel?.SetActive(true);
            _inventoryBar?.SetActive(false);
            HideInventoryItemPopup();
        }

        private void _onPlacementPointTapped(int index)
        {
            if (_placementModeActive)
            {
                // Solo proceder si el punto es válido para el tipo pendiente
                var point = _getPoint(index);
                if (point != null && !point.IsOccupied && point.AcceptsType(_pendingPlacementType))
                    OnPlacementConfirmed?.Invoke(index);
                else
                    OnPlacementCancelled?.Invoke();
            }
            else
            {
                // Ver si hay un item colocado para gestionar
                var point = _getPoint(index);
                if (point != null && point.IsOccupied)
                    OnRoomItemTapped?.Invoke(index);
            }
        }

        private void _onDropReceivedFromPoint(int index, string itemId)
        {
            OnDropReceived?.Invoke(index, itemId);
        }

        private void _subscribeToPlacementPoints()
        {
            if (_placementPoints == null) return;
            foreach (var p in _placementPoints)
            {
                if (p == null) continue;
                p.OnTapped       += _onPlacementPointTapped;
                p.OnDropReceived += _onDropReceivedFromPoint;
            }
        }

        private void _unsubscribeFromPlacementPoints()
        {
            if (_placementPoints == null) return;
            foreach (var p in _placementPoints)
            {
                if (p == null) continue;
                p.OnTapped       -= _onPlacementPointTapped;
                p.OnDropReceived -= _onDropReceivedFromPoint;
            }
        }

        private PlacementPoint _getPoint(int index)
        {
            if (_placementPoints == null || index < 0 || index >= _placementPoints.Length)
                return null;
            return _placementPoints[index];
        }

        private static SafeZoneItem _findItem(string itemId, SafeZoneItem[] all)
        {
            if (all == null) return null;
            foreach (var item in all)
                if (item != null && item.itemId == itemId) return item;
            return null;
        }

        private static void _clearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private IEnumerator _notEnoughCoinsFeedback()
        {
            _notEnoughCoinsPanel.SetActive(true);
            yield return new WaitForSeconds(2f);
            _notEnoughCoinsPanel.SetActive(false);
        }
    }
}
