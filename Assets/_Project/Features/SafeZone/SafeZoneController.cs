using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.Core.Systems;
using Lutra.UI.Components;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Controlador de la Zona Segura.
    /// Gestiona inventario, compras con confirmación, colocación en la habitación
    /// y venta de ítems a un 50% de su precio original.
    /// </summary>
    public class SafeZoneController : MonoBehaviour
    {
        [SerializeField] private SafeZoneView   _view;
        [SerializeField] private SafeZoneItem[] _allItems;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private DataRepository _dataRepository;
        private StreakManager  _streakManager;
        private AuthManager    _authManager;
        private FirestoreManager _firestoreManager;

        private DataRepository   Repo      => _dataRepository  ??= ServiceLocator.Get<DataRepository>();
        private StreakManager    Streak    => _streakManager    ??= ServiceLocator.Get<StreakManager>();
        private AuthManager      Auth      => _authManager      ??= ServiceLocator.Get<AuthManager>();
        private FirestoreManager Firestore => _firestoreManager ??= ServiceLocator.Get<FirestoreManager>();

        // ── Estado ─────────────────────────────────────────────────────

        private List<InventoryItem> _inventory    = new();
        private UserProfile         _cachedProfile;
        private SafeZoneItem        _pendingBuy;
        private SafeZoneItem        _pendingPlace;
        private SafeZoneItem        _pendingSell;
        private SafeZoneItem        _pendingRemove;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<SafeZoneView>();
        }

        private void OnEnable()  => _subscribeToView();
        private void OnDisable() => _unsubscribeFromView();

        // ── API pública ────────────────────────────────────────────────

        public async Task OpenSafeZone()
        {
            try
            {
                _cachedProfile = await Repo.GetUserProfile();
                if (_cachedProfile == null)
                {
                    Debug.LogWarning("[SafeZoneController] OpenSafeZone: sin perfil de usuario.");
                    return;
                }

                await _loadInventory();
                await _unlockDefaultItems();
                await _unlockStreakItems();

                _refreshView();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeZoneController] OpenSafeZone: {ex.Message}\n{ex.StackTrace}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        // ── Handlers de vista ──────────────────────────────────────────

        private void _onBuyRequested(SafeZoneItem item)
        {
            if (item == null || _cachedProfile == null) return;
            _pendingBuy = item;
            int coinsAfter = _cachedProfile.Coins - item.coinCost;
            _view?.ShowConfirmBuyDialog(item, _cachedProfile.Coins, coinsAfter);
        }

        private void _onBuyConfirmed()
            => _ = _safeBuyConfirmed();

        private async Task _safeBuyConfirmed()
        {
            if (_pendingBuy == null) return;
            var item = _pendingBuy;
            _pendingBuy = null;

            try
            {
                _view?.HideAllDialogs();

                // Desbloqueo por racha (gratis)
                if (item.requiredStreakDays > 0)
                {
                    int streak = await Streak.GetCurrentStreak();
                    if (streak < item.requiredStreakDays)
                    {
                        ToastNotification.ShowError(
                            $"Necesitas {item.requiredStreakDays} días de racha");
                        return;
                    }
                    await _addToInventory(item.itemId);
                }
                else
                {
                    bool ok = await Repo.SpendCoins(item.coinCost);
                    if (!ok)
                    {
                        _view?.ShowNotEnoughCoins();
                        return;
                    }
                    await _addToInventory(item.itemId);

                    _cachedProfile = await Repo.GetUserProfile();
                    EventBus.EmitCoinsChanged(_cachedProfile?.Coins ?? 0);
                }

                await _loadInventory();
                _refreshView();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeZoneController] _safeBuyConfirmed: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        private void _onBuyCancelled()
        {
            _pendingBuy = null;
            _view?.HideAllDialogs();
        }

        private void _onPlaceRequested(SafeZoneItem item)
        {
            _pendingPlace = item;
            _view?.EnterPlacementMode(item.placementType);
        }

        private void _onPlacementConfirmed(int pointIndex)
            => _ = _safePlacementConfirmed(pointIndex);

        private async Task _safePlacementConfirmed(int pointIndex)
        {
            if (_pendingPlace == null || _cachedProfile == null) return;
            var item = _pendingPlace;
            _pendingPlace = null;

            try
            {
                _view?.ExitPlacementMode();

                // Liberar el punto si ya tenía otro item del mismo tipo
                var existing = _inventory.Find(i =>
                    i.IsPlaced && i.PlacementIndex == pointIndex && i.UserId == _cachedProfile.Id);

                if (existing != null)
                    await Repo.SetItemPlacement(_cachedProfile.Id, existing.ItemId, false, -1);

                await Repo.SetItemPlacement(_cachedProfile.Id, item.itemId, true, pointIndex);

                await _loadInventory();
                _refreshView();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeZoneController] _safePlacementConfirmed: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        private void _onPlacementCancelled()
        {
            _pendingPlace = null;
            _view?.ExitPlacementMode();
        }

        private void _onSellInventoryRequested(SafeZoneItem item)
        {
            if (item == null) return;
            _pendingSell   = item;
            _pendingRemove = null;
            _view?.ShowSellConfirmDialog(item, item.coinCost / 2);
        }

        private void _onRoomItemTapped(int pointIndex)
        {
            var invItem = _inventory.Find(i =>
                i.IsPlaced && i.PlacementIndex == pointIndex);

            if (invItem == null) return;

            var def = _findItem(invItem.ItemId);
            if (def == null) return;

            _pendingSell   = def;
            _pendingRemove = def;
            _view?.ShowRoomItemOptions(def);
        }

        private void _onSellPreviewRequested()
        {
            if (_pendingSell == null) return;
            int refund = _pendingSell.coinCost / 2;
            _view?.ShowSellConfirmDialog(_pendingSell, refund);
        }

        private void _onRemoveConfirmed()
            => _ = _safeRemoveConfirmed();

        private async Task _safeRemoveConfirmed()
        {
            if (_pendingRemove == null || _cachedProfile == null) return;
            var item = _pendingRemove;
            _pendingRemove = null;
            _pendingSell   = null;

            try
            {
                _view?.HideAllDialogs();
                await Repo.SetItemPlacement(_cachedProfile.Id, item.itemId, false, -1);
                await _loadInventory();
                _refreshView();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeZoneController] _safeRemoveConfirmed: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        private void _onSellConfirmed()
            => _ = _safeSellConfirmed();

        private async Task _safeSellConfirmed()
        {
            if (_pendingSell == null || _cachedProfile == null) return;
            var item = _pendingSell;
            _pendingSell   = null;
            _pendingRemove = null;

            try
            {
                _view?.HideAllDialogs();
                int refund = item.coinCost / 2;

                // Si el ítem está colocado en la habitación, descolocarlo primero
                var invItem = _inventory.Find(i => i.ItemId == item.itemId);
                if (invItem != null && invItem.IsPlaced)
                    await Repo.SetItemPlacement(_cachedProfile.Id, item.itemId, false, -1);

                await Repo.RemoveItem(_cachedProfile.Id, item.itemId);
                if (refund > 0)
                    await Repo.AddCoins(refund);

                _ = _syncRemoveToFirestore(item.itemId);

                _cachedProfile = await Repo.GetUserProfile();
                EventBus.EmitCoinsChanged(_cachedProfile?.Coins ?? 0);

                await _loadInventory();
                _refreshView();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeZoneController] _safeSellConfirmed: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        private void _onSellCancelled()
        {
            _pendingSell   = null;
            _pendingRemove = null;
            _view?.HideAllDialogs();
        }

        private void _onDropReceived(int pointIndex, string itemId)
            => _ = _safePlacementFromDrop(pointIndex, itemId);

        private async Task _safePlacementFromDrop(int pointIndex, string itemId)
        {
            if (_cachedProfile == null) return;

            try
            {
                _view?.ExitPlacementMode();

                var existing = _inventory.Find(i =>
                    i.IsPlaced && i.PlacementIndex == pointIndex);

                if (existing != null)
                    await Repo.SetItemPlacement(_cachedProfile.Id, existing.ItemId, false, -1);

                await Repo.SetItemPlacement(_cachedProfile.Id, itemId, true, pointIndex);

                await _loadInventory();
                _refreshView();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeZoneController] _safePlacementFromDrop: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        // ── Helpers ────────────────────────────────────────────────────

        private async Task _loadInventory()
        {
            if (_cachedProfile == null) return;
            _inventory = await Repo.GetInventoryItems(_cachedProfile.Id);
        }

        private void _refreshView()
        {
            if (_cachedProfile == null) return;

            var ownedIds = _inventory.ConvertAll(i => i.ItemId);
            _view?.RefreshRoom(_inventory, _allItems);
            _view?.RefreshInventoryBar(_inventory, _allItems);
            _view?.RefreshShop(_allItems, ownedIds, _cachedProfile.Coins);
            _view?.UpdateCoins(_cachedProfile.Coins);
        }

        private async Task _addToInventory(string itemId)
        {
            if (_cachedProfile == null) return;
            await Repo.UnlockItem(_cachedProfile.Id, itemId);
            _ = _syncAddToFirestore(itemId);
        }

        private async Task _unlockDefaultItems()
        {
            if (_allItems == null || _cachedProfile == null) return;
            var ownedIds = await Repo.GetUnlockedItemIds(_cachedProfile.Id);

            foreach (var item in _allItems)
            {
                if (item == null || !item.isUnlockedByDefault) continue;
                if (ownedIds.Contains(item.itemId)) continue;

                await Repo.UnlockItem(_cachedProfile.Id, item.itemId);
            }
        }

        private async Task _unlockStreakItems()
        {
            if (_allItems == null || _cachedProfile == null) return;
            int streak   = await Streak.GetCurrentStreak();
            var ownedIds = await Repo.GetUnlockedItemIds(_cachedProfile.Id);

            foreach (var item in _allItems)
            {
                if (item == null || item.requiredStreakDays <= 0) continue;
                if (ownedIds.Contains(item.itemId)) continue;
                if (streak < item.requiredStreakDays) continue;

                await Repo.UnlockItem(_cachedProfile.Id, item.itemId);
                EventBus.EmitRewardEarned(item.itemId, RewardType.RoomDecoration);
            }
        }

        private SafeZoneItem _findItem(string itemId)
        {
            if (_allItems == null) return null;
            foreach (var item in _allItems)
                if (item != null && item.itemId == itemId) return item;
            return null;
        }

        private async Task _syncAddToFirestore(string itemId)
        {
            try
            {
                if (!Auth.IsLoggedIn) return;
                await Firestore.AddInventoryItem(Auth.CurrentUserId, itemId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SafeZoneController] Sync add a Firestore fallido: {ex.Message}");
            }
        }

        private async Task _syncRemoveToFirestore(string itemId)
        {
            try
            {
                if (!Auth.IsLoggedIn) return;
                await Firestore.RemoveInventoryItem(Auth.CurrentUserId, itemId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SafeZoneController] Sync remove a Firestore fallido: {ex.Message}");
            }
        }

        // ── Suscripciones ──────────────────────────────────────────────

        private void _subscribeToView()
        {
            if (_view == null) return;
            _view.OnBuyRequested             += _onBuyRequested;
            _view.OnBuyConfirmed             += _onBuyConfirmed;
            _view.OnBuyCancelled             += _onBuyCancelled;
            _view.OnPlaceRequested           += _onPlaceRequested;
            _view.OnPlacementConfirmed       += _onPlacementConfirmed;
            _view.OnPlacementCancelled       += _onPlacementCancelled;
            _view.OnSellInventoryRequested   += _onSellInventoryRequested;
            _view.OnRoomItemTapped           += _onRoomItemTapped;
            _view.OnRemoveConfirmed          += _onRemoveConfirmed;
            _view.OnSellPreviewRequested     += _onSellPreviewRequested;
            _view.OnSellConfirmed            += _onSellConfirmed;
            _view.OnSellCancelled            += _onSellCancelled;
            _view.OnDropReceived             += _onDropReceived;
        }

        private void _unsubscribeFromView()
        {
            if (_view == null) return;
            _view.OnBuyRequested             -= _onBuyRequested;
            _view.OnBuyConfirmed             -= _onBuyConfirmed;
            _view.OnBuyCancelled             -= _onBuyCancelled;
            _view.OnPlaceRequested           -= _onPlaceRequested;
            _view.OnPlacementConfirmed       -= _onPlacementConfirmed;
            _view.OnPlacementCancelled       -= _onPlacementCancelled;
            _view.OnSellInventoryRequested   -= _onSellInventoryRequested;
            _view.OnRoomItemTapped           -= _onRoomItemTapped;
            _view.OnRemoveConfirmed          -= _onRemoveConfirmed;
            _view.OnSellPreviewRequested     -= _onSellPreviewRequested;
            _view.OnSellConfirmed            -= _onSellConfirmed;
            _view.OnSellCancelled            -= _onSellCancelled;
            _view.OnDropReceived             -= _onDropReceived;
        }
    }
}
