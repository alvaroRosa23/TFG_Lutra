using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.Core.Events;
using Lutra.Core.Systems;

namespace Lutra.Features.StarCollection
{
    /// <summary>Resultado de registrar una captura (calculado al momento, sin esperar a la BD).</summary>
    public struct StarCatchRecord
    {
        public bool IsNew;
        public int  TimesCaught;
        /// <summary>Esta captura completa la colección (todas las estrellas normales descubiertas).</summary>
        public bool CompletedCollection;
    }

    /// <summary>
    /// Colección de estrellas de StarFisher en memoria + persistencia (SQLite y Firestore).
    /// La usan el minijuego (registrar capturas), el libro de colección y el telescopio de SafeZone.
    ///
    /// Las capturas se actualizan en memoria al instante y se guardan en segundo plano, en orden
    /// (cada guardado espera al anterior). Al completar la colección se regala el telescopio.
    /// </summary>
    public class StarCollectionStore
    {
        private readonly StarCatalog _catalog;
        private readonly Dictionary<string, StarCollectionEntry> _entries = new();

        private Task _saveChain = Task.CompletedTask;

        private DataRepository _repo;
        private DataRepository Repo => _repo ??= ServiceLocator.Get<DataRepository>();

        public StarCollectionStore(StarCatalog catalog) => _catalog = catalog;

        public StarCatalog Catalog  => _catalog;
        public bool        IsLoaded { get; private set; }

        // ── Lectura ────────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            var rows = await Repo.GetStarCollection();
            _entries.Clear();
            foreach (var row in rows)
                if (!string.IsNullOrEmpty(row.StarId)) _entries[row.StarId] = row;
            IsLoaded = true;
        }

        public bool IsDiscovered(string starId) => !string.IsNullOrEmpty(starId) && _entries.ContainsKey(starId);

        public int GetTimesCaught(string starId)
            => starId != null && _entries.TryGetValue(starId, out var entry) ? entry.TimesCaught : 0;

        public bool WasCaughtToday(string starId)
            => starId != null && _entries.TryGetValue(starId, out var entry) && entry.LastCaughtAt.Date == DateTime.Today;

        public int RegularCount => _catalog != null ? _catalog.GetRegularStars().Count : 0;

        public int DiscoveredRegularCount
        {
            get
            {
                if (_catalog == null) return 0;
                int count = 0;
                foreach (var star in _catalog.GetRegularStars())
                    if (IsDiscovered(star.starId)) count++;
                return count;
            }
        }

        public bool IsComplete => RegularCount > 0 && DiscoveredRegularCount == RegularCount;

        // ── Escritura ──────────────────────────────────────────────────

        /// <summary>
        /// Registra una captura en memoria y lanza el guardado (SQLite → Firestore → telescopio
        /// si completa la colección) sin bloquear.
        /// </summary>
        public StarCatchRecord RegisterCatch(StarDefinition star)
        {
            if (star == null || string.IsNullOrEmpty(star.starId)) return default;

            bool wasComplete = IsComplete;
            var now = DateTime.Now;

            if (!_entries.TryGetValue(star.starId, out var entry))
            {
                entry = new StarCollectionEntry { StarId = star.starId, FirstCaughtAt = now };
                _entries[star.starId] = entry;
            }
            entry.TimesCaught++;
            entry.LastCaughtAt = now;

            var record = new StarCatchRecord
            {
                IsNew               = entry.TimesCaught == 1,
                TimesCaught         = entry.TimesCaught,
                CompletedCollection = !wasComplete && IsComplete
            };

            _saveChain = _saveCatchAfter(_saveChain, star.starId, now, record.CompletedCollection);
            return record;
        }

        /// <summary>
        /// Si la colección está completa y el telescopio aún no está en el inventario, lo añade
        /// (SQLite + Firestore) y emite la recompensa. Devuelve true si lo ha regalado ahora.
        /// </summary>
        public async Task<bool> GrantRewardIfCompleteAsync()
        {
            if (!IsComplete || string.IsNullOrEmpty(_catalog?.telescopeItemId)) return false;

            var profile = await Repo.GetUserProfile();
            if (profile == null) return false;

            string itemId = _catalog.telescopeItemId;
            if (await Repo.IsItemUnlocked(profile.Id, itemId)) return false;

            await Repo.UnlockItem(profile.Id, itemId);
            await _syncToFirestore(firestore => firestore.AddInventoryItem(_userId(), itemId));
            EventBus.EmitRewardEarned(itemId, RewardType.RoomDecoration);

            Debug.Log("[StarCollectionStore] Colección completa: telescopio añadido al inventario");
            return true;
        }

        /// <summary>Fusiona en SQLite la colección guardada en Firestore (al iniciar sesión).</summary>
        public static async Task RestoreFromFirestoreAsync(DataRepository repo, string firebaseUserId)
        {
            try
            {
                var remote = await ServiceLocator.Get<FirestoreManager>().GetStarCollection(firebaseUserId);
                foreach (var entry in remote)
                    await repo.MergeStarCollectionEntry(entry);

                if (remote.Count > 0)
                    Debug.Log($"[StarCollectionStore] Estrellas restauradas desde Firestore: {remote.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StarCollectionStore] No se pudo restaurar la colección desde Firestore: {ex.Message}");
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        private async Task _saveCatchAfter(Task previous, string starId, DateTime caughtAt, bool completesCollection)
        {
            try { await previous; }
            catch { /* el guardado anterior ya registró su error */ }

            try
            {
                var saved = await Repo.RegisterStarCatch(starId, caughtAt);
                await _syncToFirestore(firestore => firestore.SaveStarEntry(_userId(), saved));

                if (completesCollection) await GrantRewardIfCompleteAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StarCollectionStore] Error al guardar la captura de {starId}: {ex.Message}");
            }
        }

        private static string _userId() => ServiceLocator.Get<AuthManager>().CurrentUserId;

        private static async Task _syncToFirestore(Func<FirestoreManager, Task> action)
        {
            try
            {
                if (!ServiceLocator.Get<AuthManager>().IsLoggedIn) return;
                await action(ServiceLocator.Get<FirestoreManager>());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StarCollectionStore] Sync con Firestore fallido: {ex.Message}");
            }
        }
    }
}
