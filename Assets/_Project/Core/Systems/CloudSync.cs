using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Sincronización entre SQLite (fuente local) y Firestore (copia para otros dispositivos).
    ///
    /// 1. Subidas automáticas: DataRepository llama a Push* después de cada escritura local, así
    ///    ninguna feature tiene que acordarse de subir nada. Son fire-and-forget: Firestore guarda
    ///    en cola las escrituras sin conexión y las envía al recuperarla.
    /// 2. Reconciliación (SyncAllAsync) al iniciar sesión y al abrir la app:
    ///    - Datos que solo se añaden (diario, emociones, partidas, estrellas, inventario): unión por
    ///      id estable. Lo que falta en local se descarga y lo que falta en remoto se sube.
    ///      El diario además admite edición (manda Firestore) y borrado (marcador en Firestore).
    ///    - Estado (monedas, colocaciones, preferencias): manda Firestore si tiene el dato; si no,
    ///      se sube el local.
    ///    Es idempotente: se puede ejecutar todas las veces que haga falta.
    /// </summary>
    public static class CloudSync
    {
        private static Task _syncing;

        public static string NewRemoteId() => Guid.NewGuid().ToString("N");

        // ══════════════════════════════════════════════════════════════
        // SUBIDAS AUTOMÁTICAS (las llama DataRepository)
        // ══════════════════════════════════════════════════════════════

        public static void PushDiaryEntry(DiaryEntry entry)
            => _push("entrada de diario", (fs, uid) => fs.SaveDiaryEntry(uid, entry));

        public static void PushDiaryDelete(string remoteId)
            => _push("borrado de entrada de diario", (fs, uid) => fs.DeleteDiaryEntry(uid, remoteId));

        /// <summary>Solo se suben los registros del usuario, nunca los placeholders restaurados.</summary>
        public static void PushEmotion(EmotionRecord record)
        {
            if (record.Source != RecordSource.User) return;
            _push("registro emocional", (fs, uid) => fs.SaveEmotion(uid, record));
        }

        public static void PushMinigameSession(MinigameSession session)
            => _push("partida de minijuego", (fs, uid) => fs.SaveMinigameSession(uid, session));

        public static void PushProfile(UserProfile profile)
        {
            _push("perfil", (fs, uid) =>
                profile.FirebaseUserId == uid ? fs.SaveUserProfile(profile) : Task.CompletedTask);
        }

        public static void PushCoinsDelta(int delta)
        {
            if (delta == 0) return;
            _push("monedas", (fs, uid) => fs.IncrementCoins(uid, delta));
        }

        public static void PushInventoryAdd(string itemId)
            => _push("ítem del inventario", (fs, uid) => fs.AddInventoryItem(uid, itemId));

        public static void PushInventoryRemove(string itemId)
            => _push("venta de ítem", (fs, uid) => fs.RemoveInventoryItem(uid, itemId));

        public static void PushPlacement(string itemId, int placementIndex)
            => _push("colocación", (fs, uid) => fs.SetInventoryPlacement(uid, itemId, placementIndex));

        public static void PushStar(StarCollectionEntry entry)
            => _push("estrella", (fs, uid) => fs.SaveStarEntry(uid, entry));

        public static void PushNotification(AppNotification notification)
            => _push("notificación", (fs, uid) => fs.SaveNotification(uid, notification));

        public static void PushScaleResponse(ScaleResponse response)
            => _push("cuestionario", (fs, uid) => fs.SaveScaleResponse(uid, response));

        // ══════════════════════════════════════════════════════════════
        // RECONCILIACIÓN
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reconcilia todos los datos del usuario con Firestore. Si ya hay una en curso devuelve
        /// esa misma. Cada bloque falla por separado sin cancelar los demás.
        /// </summary>
        public static Task SyncAllAsync(DataRepository repo)
        {
            if (_syncing != null && !_syncing.IsCompleted) return _syncing;
            _syncing = _safeSyncAll(repo);
            return _syncing;
        }

        private static async Task _safeSyncAll(DataRepository repo)
        {
            try { await _syncAll(repo); }
            catch (Exception ex) { Debug.LogError($"[CloudSync] Sincronización fallida: {ex.Message}"); }
        }

        private static async Task _syncAll(DataRepository repo)
        {
            if (!_tryGetTarget(out var fs, out var uid)) return;

            var profile = await repo.GetUserProfile();
            if (profile == null) return;
            if (!string.IsNullOrEmpty(profile.FirebaseUserId) && profile.FirebaseUserId != uid)
            {
                Debug.LogWarning("[CloudSync] El perfil local es de otra cuenta: no se sincroniza.");
                return;
            }

            var state = await fs.GetUserState(uid);

            await _step("perfil y monedas", () => _syncProfile(fs, uid, repo, profile, state));
            await _step("diario",           () => _syncDiary(fs, uid, repo));
            await _step("emociones",        () => _syncEmotions(fs, uid, repo, state));
            await _step("minijuegos",       () => _syncMinigameSessions(fs, uid, repo));
            await _step("estrellas",        () => _syncStars(fs, uid, repo));
            await _step("inventario",       () => _syncInventory(fs, uid, repo, profile.Id, state));
            await _step("cuestionarios",    () => _syncScaleResponses(fs, uid, repo));
            await _step("notificaciones",   () => _syncNotifications(fs, uid, repo));

            var updated = await repo.GetUserProfile();
            if (updated != null) EventBus.EmitCoinsChanged(updated.Coins);

            Debug.Log("[CloudSync] Sincronización con Firestore completada.");
        }

        // ── Perfil, monedas y preferencias ─────────────────────────────

        private static async Task _syncProfile(FirestoreManager fs, string uid, DataRepository repo,
                                               UserProfile profile, RemoteUserState state)
        {
            if (state == null) return;

            // Monedas: manda Firestore, donde cada variación se sube como incremento. La primera vez
            // con este sistema (o si no hay saldo remoto) manda el local: antes se subía el total y
            // podía haberse quedado atrás.
            if (state.Coins.HasValue && state.SyncVersion >= FirestoreManager.SyncVersion)
            {
                if (state.Coins.Value != profile.Coins) await repo.SetCoins(state.Coins.Value);
            }
            else
            {
                int coins = profile.Coins;
                _push("monedas", (f, u) => f.SaveCoins(u, coins));
            }

            // Preferencias (mascota, recompensa diaria del diario...): las claves remotas mandan
            var local  = profile.Preferences ?? new Dictionary<string, string>();
            var remote = _parsePreferences(state.PreferencesJson);
            var merged = new Dictionary<string, string>(local);
            foreach (var pair in remote) merged[pair.Key] = pair.Value;

            string avatar = !string.IsNullOrEmpty(state.Avatar) ? state.Avatar : profile.Avatar;

            bool localChanged  = !_sameDictionary(merged, local) || avatar != profile.Avatar;
            bool remoteChanged = !_sameDictionary(merged, remote) || (avatar ?? string.Empty) != (state.Avatar ?? string.Empty);

            if (localChanged)
            {
                profile.Preferences = merged;
                profile.Avatar      = avatar;
                await repo.SaveUserProfile(profile, sync: false);
            }

            if (remoteChanged)
            {
                if (string.IsNullOrEmpty(profile.FirebaseUserId)) profile.FirebaseUserId = uid;
                PushProfile(profile);
            }
        }

        // ── Diario ─────────────────────────────────────────────────────

        private static async Task _syncDiary(FirestoreManager fs, string uid, DataRepository repo)
        {
            var remoteDiary = await fs.GetDiaryEntries(uid);
            if (remoteDiary == null) return;

            var remote     = remoteDiary.Entries;
            var deletedIds = remoteDiary.DeletedIds;
            var remoteById = remote.ToDictionary(e => e.RemoteId);
            var local      = await repo.GetAllDiaryEntries();

            // Borradas en otro dispositivo: también se quitan aquí
            int deleted = 0;
            foreach (var entry in local.Where(e => !string.IsNullOrEmpty(e.RemoteId) && deletedIds.Contains(e.RemoteId)))
            {
                await repo.DeleteDiaryEntry(entry, sync: false);
                deleted++;
            }
            local.RemoveAll(e => !string.IsNullOrEmpty(e.RemoteId) && deletedIds.Contains(e.RemoteId));

            var usedIds = new HashSet<string>(local.Where(e => !string.IsNullOrEmpty(e.RemoteId)).Select(e => e.RemoteId));

            // Entradas locales de antes de los ids estables: si su documento antiguo (id = fecha)
            // tiene el mismo texto es la misma entrada; si no, es otra del mismo día que se había
            // sobrescrito en Firestore y se sube con un id nuevo. No se compara el título: los
            // documentos de antes de existir el título no lo tienen.
            foreach (var entry in local.Where(e => string.IsNullOrEmpty(e.RemoteId)))
            {
                string legacyId = entry.Date.ToString("yyyy-MM-dd");
                bool sameAsLegacy = remoteById.TryGetValue(legacyId, out var legacy)
                                 && !usedIds.Contains(legacyId)
                                 && legacy.Content == (entry.Content ?? string.Empty);

                entry.RemoteId = sameAsLegacy ? legacyId : NewRemoteId();
                usedIds.Add(entry.RemoteId);
                await repo.SaveDiaryEntry(entry, sync: false);
            }

            int uploaded = 0, restored = 0, updated = 0;
            foreach (var entry in local)
            {
                if (remoteById.TryGetValue(entry.RemoteId, out var remoteEntry))
                {
                    // Editada en otro dispositivo: manda la versión de Firestore
                    if (_sameDiaryText(entry, remoteEntry)) continue;
                    entry.Title   = remoteEntry.Title;
                    entry.Content = remoteEntry.Content;
                    entry.Mood    = remoteEntry.Mood;
                    await repo.SaveDiaryEntry(entry, sync: false);
                    updated++;
                    continue;
                }

                PushDiaryEntry(entry);
                uploaded++;
            }

            foreach (var entry in remote)
            {
                if (usedIds.Contains(entry.RemoteId)) continue;
                entry.Id = 0;
                await repo.SaveDiaryEntry(entry, sync: false);
                restored++;
            }

            _logCounts("diario", restored, uploaded);
            if (updated > 0 || deleted > 0)
                Debug.Log($"[CloudSync] diario: {updated} actualizadas, {deleted} borradas");
        }

        private static bool _sameDiaryText(DiaryEntry a, DiaryEntry b)
            => (a.Title   ?? string.Empty) == (b.Title   ?? string.Empty)
            && (a.Content ?? string.Empty) == (b.Content ?? string.Empty)
            && (a.Mood    ?? string.Empty) == (b.Mood    ?? string.Empty);

        // ── Emociones (check-ins, racha y gráficas) ────────────────────

        private static async Task _syncEmotions(FirestoreManager fs, string uid, DataRepository repo, RemoteUserState state)
        {
            var remote = await fs.GetEmotions(uid);
            if (remote != null)
            {
                var remoteIds = new HashSet<string>(remote.Select(r => r.RemoteId));
                var local     = await repo.GetAllEmotions();
                var localIds  = new HashSet<string>();

                int uploaded = 0, restored = 0;
                foreach (var record in local.Where(r => r.Source == RecordSource.User))
                {
                    if (string.IsNullOrEmpty(record.RemoteId))
                    {
                        record.RemoteId = NewRemoteId();
                        await repo.UpdateEmotion(record, sync: false);
                    }
                    localIds.Add(record.RemoteId);

                    if (remoteIds.Contains(record.RemoteId)) continue;
                    PushEmotion(record);
                    uploaded++;
                }

                foreach (var record in remote)
                {
                    if (localIds.Contains(record.RemoteId)) continue;
                    await repo.SaveEmotion(record, sync: false);
                    restored++;
                }

                _logCounts("emociones", restored, uploaded);
            }

            await _fillLegacyCheckIns(repo, state);
        }

        /// <summary>
        /// Check-ins anteriores a la subida de registros completos: solo se guardaba la fecha (y la
        /// emoción del último). Los días sin ningún registro reciben un placeholder para que la
        /// racha se mantenga; los placeholders sobran en cuanto hay un registro real ese día.
        /// </summary>
        private static async Task _fillLegacyCheckIns(DataRepository repo, RemoteUserState state)
        {
            var local     = await repo.GetAllEmotions();
            var realDates = new HashSet<DateTime>(local.Where(r => r.Source == RecordSource.User).Select(r => r.Timestamp.Date));

            foreach (var placeholder in local.Where(r => r.Source != RecordSource.User && realDates.Contains(r.Timestamp.Date)))
                await repo.DeleteEmotion(placeholder);

            if (state == null) return;

            var datesWithRecords = new HashSet<DateTime>(local.Select(r => r.Timestamp.Date));

            if (state.LastCheckInDate.HasValue && datesWithRecords.Add(state.LastCheckInDate.Value))
            {
                await repo.SaveEmotion(new EmotionRecord
                {
                    Timestamp      = state.LastCheckInDate.Value,
                    EmotionType    = state.LastEmotion,
                    IntensityLevel = 3,
                    IsMorningCheck = true,
                    Source         = RecordSource.RestoredFirestore
                }, sync: false);
            }

            foreach (var date in state.CheckInHistory)
            {
                if (!datesWithRecords.Add(date)) continue;
                await repo.SaveEmotion(new EmotionRecord
                {
                    Timestamp      = date.AddHours(12),
                    EmotionType    = EmotionType.Calm,
                    IntensityLevel = 3,
                    IsMorningCheck = true,
                    Source         = RecordSource.RestoredFirestoreHistory
                }, sync: false);
            }
        }

        // ── Partidas de minijuegos (récords) ───────────────────────────

        private static async Task _syncMinigameSessions(FirestoreManager fs, string uid, DataRepository repo)
        {
            var remote = await fs.GetMinigameSessions(uid);
            if (remote == null) return;

            var remoteIds = new HashSet<string>(remote.Select(s => s.RemoteId));
            var local     = await repo.GetAllMinigameSessions();
            var localIds  = new HashSet<string>();

            int uploaded = 0, restored = 0;
            foreach (var session in local)
            {
                if (string.IsNullOrEmpty(session.RemoteId))
                {
                    session.RemoteId = NewRemoteId();
                    await repo.UpdateMinigameSession(session, sync: false);
                }
                localIds.Add(session.RemoteId);

                if (remoteIds.Contains(session.RemoteId)) continue;
                PushMinigameSession(session);
                uploaded++;
            }

            foreach (var session in remote)
            {
                if (localIds.Contains(session.RemoteId)) continue;
                await repo.SaveMinigameSession(session, sync: false);
                restored++;
            }

            _logCounts("partidas", restored, uploaded);
        }

        // ── Colección de estrellas ─────────────────────────────────────

        private static async Task _syncStars(FirestoreManager fs, string uid, DataRepository repo)
        {
            var remote = await fs.GetStarCollection(uid);
            if (remote == null) return;

            foreach (var entry in remote)
                await repo.MergeStarCollectionEntry(entry);

            // Tras la fusión el local tiene el mayor contador: se sube lo que en remoto falta o es menor
            var remoteById = remote.ToDictionary(e => e.StarId);
            int uploaded = 0;
            foreach (var entry in await repo.GetStarCollection())
            {
                if (remoteById.TryGetValue(entry.StarId, out var r) && r.TimesCaught >= entry.TimesCaught) continue;
                PushStar(entry);
                uploaded++;
            }

            _logCounts("estrellas", remote.Count, uploaded);
        }

        // ── Cuestionarios (WHO-5) ──────────────────────────────────────

        /// <summary>Unión por RemoteId: los envíos no se editan nunca.</summary>
        private static async Task _syncScaleResponses(FirestoreManager fs, string uid, DataRepository repo)
        {
            var remote = await fs.GetScaleResponses(uid);
            if (remote == null) return;

            var remoteIds = new HashSet<string>(remote.Select(r => r.RemoteId));
            var localIds  = new HashSet<string>();
            int uploaded = 0, restored = 0;

            foreach (var response in await repo.GetAllScaleResponses())
            {
                localIds.Add(response.RemoteId);
                if (remoteIds.Contains(response.RemoteId)) continue;
                PushScaleResponse(response);
                uploaded++;
            }

            foreach (var response in remote)
            {
                if (localIds.Contains(response.RemoteId)) continue;
                await repo.SaveScaleResponse(response, sync: false);
                restored++;
            }

            _logCounts("cuestionarios", restored, uploaded);
        }

        // ── Centro de notificaciones ───────────────────────────────────

        /// <summary>
        /// Unión por RemoteId. Si existe en ambos lados, leída y resuelta ganan: lo que se marcó en
        /// un dispositivo (p. ej. el WHO-5 enviado) se aplica también en el otro.
        /// </summary>
        private static async Task _syncNotifications(FirestoreManager fs, string uid, DataRepository repo)
        {
            var remote = await fs.GetNotifications(uid);
            if (remote == null) return;

            var remoteById = new Dictionary<string, AppNotification>();
            foreach (var n in remote) remoteById[n.RemoteId] = n;

            var localIds = new HashSet<string>();
            int uploaded = 0, restored = 0;

            foreach (var local in await repo.GetAllNotifications())
            {
                if (string.IsNullOrEmpty(local.RemoteId))
                {
                    local.RemoteId = NewRemoteId();
                    await repo.UpdateNotification(local, sync: false);
                }
                localIds.Add(local.RemoteId);

                if (!remoteById.TryGetValue(local.RemoteId, out var r))
                {
                    PushNotification(local);
                    uploaded++;
                    continue;
                }

                bool localChanged = false;
                if (r.IsRead && !local.IsRead) { local.IsRead = true; localChanged = true; }
                if (r.ResolvedAt.HasValue && !local.ResolvedAt.HasValue)
                {
                    local.ResolvedAt = r.ResolvedAt;
                    local.IsPinned   = false;
                    localChanged     = true;
                }
                if (localChanged) await repo.UpdateNotification(local, sync: false);

                bool remoteBehind = (local.IsRead && !r.IsRead) || (local.ResolvedAt.HasValue && !r.ResolvedAt.HasValue);
                if (remoteBehind)
                {
                    PushNotification(local);
                    uploaded++;
                }
            }

            foreach (var n in remote)
            {
                if (localIds.Contains(n.RemoteId)) continue;
                await repo.SaveNotification(n, sync: false);
                restored++;
            }

            _logCounts("notificaciones", restored, uploaded);

            // La "!" del menú puede haber cambiado (recibidas o resueltas en otro dispositivo)
            if (ServiceLocator.TryGet<NotificationCenter>(out var center))
                center.RefreshAttention();
        }

        // ── Inventario y colocaciones ──────────────────────────────────

        private static async Task _syncInventory(FirestoreManager fs, string uid, DataRepository repo,
                                                 int userId, RemoteUserState state)
        {
            if (state == null) return;

            var local    = await repo.GetInventoryItems(userId);
            var localIds = new HashSet<string>(local.Select(i => i.ItemId));

            foreach (var itemId in state.InventoryItems)
                if (!localIds.Contains(itemId))
                    await repo.UnlockItem(userId, itemId, sync: false);

            foreach (var itemId in localIds)
            {
                if (state.InventoryItems.Contains(itemId)) continue;

                // Vendido en otro dispositivo: también se quita aquí
                if (state.InventoryRemoved.Contains(itemId))
                    await repo.RemoveItem(userId, itemId, sync: false);
                else
                    PushInventoryAdd(itemId);
            }

            await _syncPlacements(repo, userId, state);
        }

        /// <summary>
        /// Las colocaciones remotas mandan. Las locales que no están en remoto se suben si su punto
        /// sigue libre; si otro ítem lo ocupa en remoto, el local vuelve al inventario.
        /// </summary>
        private static async Task _syncPlacements(DataRepository repo, int userId, RemoteUserState state)
        {
            var local  = await repo.GetInventoryItems(userId);
            var remote = state.Placements ?? new Dictionary<string, int>();
            var used   = new HashSet<int>(remote.Values);

            foreach (var pair in remote)
            {
                var item = local.Find(i => i.ItemId == pair.Key);
                if (item == null)
                {
                    // Ítems por defecto que estaban colocados pero no en inventoryItems
                    if (state.InventoryRemoved.Contains(pair.Key)) continue;
                    await repo.UnlockItem(userId, pair.Key);
                }
                else if (item.IsPlaced && item.PlacementIndex == pair.Value)
                {
                    continue;
                }

                await repo.SetItemPlacement(userId, pair.Key, true, pair.Value, sync: false);
            }

            foreach (var item in local)
            {
                if (!item.IsPlaced || remote.ContainsKey(item.ItemId)) continue;

                if (used.Add(item.PlacementIndex))
                    PushPlacement(item.ItemId, item.PlacementIndex);
                else
                    await repo.SetItemPlacement(userId, item.ItemId, false, -1, sync: false);
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        private static void _push(string what, Func<FirestoreManager, string, Task> action) => _ = _safePush(what, action);

        private static async Task _safePush(string what, Func<FirestoreManager, string, Task> action)
        {
            try
            {
                if (!_tryGetTarget(out var fs, out var uid)) return;
                await action(fs, uid);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudSync] No se pudo subir {what}: {ex.Message}");
            }
        }

        private static async Task _step(string name, Func<Task> step)
        {
            try { await step(); }
            catch (Exception ex) { Debug.LogWarning($"[CloudSync] Error sincronizando {name}: {ex.Message}"); }
        }

        /// <summary>Firestore listo y sesión iniciada; false también si los servicios no existen (tests).</summary>
        private static bool _tryGetTarget(out FirestoreManager fs, out string uid)
        {
            fs  = null;
            uid = null;
            try
            {
                var auth = ServiceLocator.Get<AuthManager>();
                if (!auth.IsLoggedIn) return false;

                fs  = ServiceLocator.Get<FirestoreManager>();
                uid = auth.CurrentUserId;
                return fs.IsReady && !string.IsNullOrEmpty(uid);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static Dictionary<string, string> _parsePreferences(string json)
        {
            if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
            try   { return JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(); }
            catch { return new Dictionary<string, string>(); }
        }

        private static bool _sameDictionary(Dictionary<string, string> a, Dictionary<string, string> b)
            => a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value);

        private static void _logCounts(string what, int restored, int uploaded)
        {
            if (restored > 0 || uploaded > 0)
                Debug.Log($"[CloudSync] {what}: {restored} descargados, {uploaded} subidos");
        }
    }
}
