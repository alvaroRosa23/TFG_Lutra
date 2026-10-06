using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Systems;

namespace Lutra.Core.Data.Persistence
{
    /// <summary>
    /// Capa de acceso a datos. Usa directamente la API async de SQLiteAsyncConnection
    /// para evitar bloquear el hilo principal de Unity.
    ///
    /// Dependencia: DatabaseManager registrado en ServiceLocator antes de llamar a Initialize().
    ///
    /// Cada escritura se sube sola a Firestore (CloudSync.Push*). sync: false solo lo usa la
    /// propia sincronización al guardar datos que vienen de Firestore.
    /// </summary>
    public class DataRepository : IService
    {
        private SQLiteAsyncConnection _db;

        public void Initialize()
        {
            _db = ServiceLocator.Get<DatabaseManager>().GetConnection();
        }

        // ══════════════════════════════════════════════════════════════
        // EMOCIONES
        // ══════════════════════════════════════════════════════════════

        public async Task SaveEmotion(EmotionRecord record, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(record.RemoteId)) record.RemoteId = CloudSync.NewRemoteId();
                await _db.InsertAsync(record);
                if (sync) CloudSync.PushEmotion(record);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveEmotion: {ex.Message}");
                throw;
            }
        }

        /// <summary>Devuelve los registros emocionales dentro de un rango de fechas.</summary>
        public async Task<List<EmotionRecord>> GetEmotionsForPeriod(DateTime from, DateTime to)
        {
            try
            {
                return await _db.Table<EmotionRecord>()
                    .Where(r => r.Timestamp >= from && r.Timestamp <= to)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetEmotionsForPeriod: {ex.Message}");
                return new List<EmotionRecord>();
            }
        }

        /// <summary>
        /// Registros del usuario dentro de un rango de fechas, sin los placeholders restaurados
        /// desde Firestore. Es la consulta que deben usar estadísticas e informes.
        /// </summary>
        public async Task<List<EmotionRecord>> GetUserEmotionsForPeriod(DateTime from, DateTime to)
        {
            try
            {
                return await _db.Table<EmotionRecord>()
                    .Where(r => r.Timestamp >= from && r.Timestamp <= to && r.Source == RecordSource.User)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetUserEmotionsForPeriod: {ex.Message}");
                return new List<EmotionRecord>();
            }
        }

        /// <summary>
        /// Dato de ánimo (1-5) más reciente anterior a <paramref name="before"/>: el último check-in
        /// del usuario o la última valoración post-partida, el que sea más reciente. null si no hay ninguno.
        /// Se usa como ánimo previo de un minijuego (no se pregunta al empezar).
        /// </summary>
        public async Task<(int mood, DateTime recordedAt)?> GetLatestMood(DateTime before)
        {
            try
            {
                var record = await _db.Table<EmotionRecord>()
                    .Where(r => r.Source == RecordSource.User && r.MoodLevel > 0 && r.Timestamp <= before)
                    .OrderByDescending(r => r.Timestamp)
                    .FirstOrDefaultAsync();

                var session = await _db.Table<MinigameSession>()
                    .Where(s => s.MoodAfter != null && s.StartTime <= before)
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefaultAsync();

                // La valoración post-partida se hace al terminar: su momento es inicio + duración
                DateTime? sessionAt = session != null
                    ? session.StartTime.AddSeconds(session.DurationSeconds)
                    : (DateTime?)null;

                if (session != null && (record == null || sessionAt.Value > record.Timestamp))
                    return (session.MoodAfter.Value, sessionAt.Value);

                if (record != null)
                    return (record.MoodLevel, record.Timestamp);

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetLatestMood: {ex.Message}");
                return null;
            }
        }

        /// <summary>Devuelve el registro emocional más reciente del día actual. Puede ser null.</summary>
        public async Task<EmotionRecord> GetTodayEmotion()
        {
            try
            {
                var startOfDay = DateTime.Today;
                var endOfDay   = startOfDay.AddDays(1).AddTicks(-1);

                var results = await _db.Table<EmotionRecord>()
                    .Where(r => r.Timestamp >= startOfDay && r.Timestamp <= endOfDay)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync();

                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetTodayEmotion: {ex.Message}");
                return null;
            }
        }

        public async Task<EmotionRecord> GetLastEmotion()
        {
            try
            {
                var results = await _db.Table<EmotionRecord>()
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync();

                var todayReal = results.FirstOrDefault(r =>
                    r.Timestamp.Date == DateTime.Today &&
                    r.Source == RecordSource.User);

                if (todayReal != null) return todayReal;

                var anyReal = results.FirstOrDefault(r => r.Source == RecordSource.User);

                if (anyReal != null) return anyReal;

                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetLastEmotion: {ex.Message}");
                return null;
            }
        }

        public async Task UpdateEmotion(EmotionRecord record, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(record.RemoteId)) record.RemoteId = CloudSync.NewRemoteId();
                await _db.UpdateAsync(record);
                if (sync) CloudSync.PushEmotion(record);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] UpdateEmotion: {ex.Message}");
            }
        }

        public async Task<List<EmotionRecord>> GetAllEmotions()
        {
            try
            {
                return await _db.Table<EmotionRecord>().ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetAllEmotions: {ex.Message}");
                return new List<EmotionRecord>();
            }
        }

        /// <summary>Borra un registro solo en local (lo usa la sincronización para los placeholders).</summary>
        public async Task DeleteEmotion(EmotionRecord record)
        {
            try
            {
                await _db.DeleteAsync(record);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] DeleteEmotion: {ex.Message}");
            }
        }

        public async Task<int> GetTotalCheckIns()
        {
            try
            {
                return await _db.Table<EmotionRecord>().CountAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetTotalCheckIns: {ex.Message}");
                return 0;
            }
        }

        /// <summary>Devuelve todas las fechas (sin hora) con al menos un registro emocional.</summary>
        public async Task<List<DateTime>> GetAllEmotionDates()
        {
            try
            {
                var records = await _db.Table<EmotionRecord>().ToListAsync();
                return records
                    .Select(r => r.Timestamp.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetAllEmotionDates: {ex.Message}");
                return new List<DateTime>();
            }
        }

        // ══════════════════════════════════════════════════════════════
        // DIARIO
        // ══════════════════════════════════════════════════════════════

        public async Task<List<DiaryEntry>> GetAllDiaryEntries()
        {
            try
            {
                return await _db.Table<DiaryEntry>()
                    .OrderBy(e => e.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetAllDiaryEntries: {ex.Message}");
                return new List<DiaryEntry>();
            }
        }

        public async Task SaveDiaryEntry(DiaryEntry entry, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(entry.RemoteId)) entry.RemoteId = CloudSync.NewRemoteId();

                if (entry.Id == 0)
                    await _db.InsertAsync(entry);
                else
                    await _db.UpdateAsync(entry);

                if (sync) CloudSync.PushDiaryEntry(entry);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveDiaryEntry: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Borra la entrada en SQLite y (con sync) en Firestore, donde queda un marcador de borrado
        /// para que la reconciliación de otros dispositivos también la elimine.
        /// </summary>
        public async Task DeleteDiaryEntry(DiaryEntry entry, bool sync = true)
        {
            try
            {
                await _db.DeleteAsync<DiaryEntry>(entry.Id);
                if (sync && !string.IsNullOrEmpty(entry.RemoteId)) CloudSync.PushDiaryDelete(entry.RemoteId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] DeleteDiaryEntry: {ex.Message}");
                throw;
            }
        }

        /// <summary>Entradas del diario dentro del rango, más antiguas primero (estadísticas e informe).</summary>
        public async Task<List<DiaryEntry>> GetDiaryEntriesForPeriod(DateTime from, DateTime to)
        {
            try
            {
                return await _db.Table<DiaryEntry>()
                    .Where(e => e.Date >= from && e.Date <= to)
                    .OrderBy(e => e.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetDiaryEntriesForPeriod: {ex.Message}");
                return new List<DiaryEntry>();
            }
        }

        public async Task<List<DiaryEntry>> GetDiaryEntriesForMonth(int year, int month)
        {
            try
            {
                var startOfMonth = new DateTime(year, month, 1);
                var endOfMonth   = startOfMonth.AddMonths(1).AddTicks(-1);

                return await _db.Table<DiaryEntry>()
                    .Where(e => e.Date >= startOfMonth && e.Date <= endOfMonth)
                    .OrderBy(e => e.Date)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetDiaryEntriesForMonth: {ex.Message}");
                return new List<DiaryEntry>();
            }
        }

        public async Task<DiaryEntry> GetDiaryEntryByDate(DateTime date)
        {
            try
            {
                var day     = date.Date;
                var nextDay = day.AddDays(1);

                var results = await _db.Table<DiaryEntry>()
                    .Where(e => e.Date >= day && e.Date < nextDay)
                    .ToListAsync();

                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetDiaryEntryByDate: {ex.Message}");
                return null;
            }
        }

        public async Task<DiaryEntry> GetEntryFromOneYearAgo()
        {
            try
            {
                var targetDate = DateTime.Today.AddYears(-1);
                var from       = targetDate.AddDays(-1);
                var to         = targetDate.AddDays(1);

                var results = await _db.Table<DiaryEntry>()
                    .Where(e => e.Date >= from && e.Date <= to)
                    .OrderBy(e => e.Date)
                    .ToListAsync();

                return results.FirstOrDefault(e => e.Date == targetDate)
                    ?? results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetEntryFromOneYearAgo: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // MINIJUEGOS
        // ══════════════════════════════════════════════════════════════

        public async Task SaveMinigameSession(MinigameSession session, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(session.RemoteId)) session.RemoteId = CloudSync.NewRemoteId();
                await _db.InsertAsync(session);
                if (sync) CloudSync.PushMinigameSession(session);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveMinigameSession: {ex.Message}");
                throw;
            }
        }

        public async Task<List<MinigameSession>> GetAllMinigameSessions()
        {
            try
            {
                return await _db.Table<MinigameSession>().ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetAllMinigameSessions: {ex.Message}");
                return new List<MinigameSession>();
            }
        }

        public async Task<List<MinigameSession>> GetSessionsForMinigame(MinigameType type)
        {
            try
            {
                return await _db.Table<MinigameSession>()
                    .Where(s => s.MinigameId == type)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetSessionsForMinigame: {ex.Message}");
                return new List<MinigameSession>();
            }
        }

        /// <summary>Partidas de todos los minijuegos que empezaron dentro del rango, más recientes primero.</summary>
        public async Task<List<MinigameSession>> GetSessionsForPeriod(DateTime from, DateTime to)
        {
            try
            {
                return await _db.Table<MinigameSession>()
                    .Where(s => s.StartTime >= from && s.StartTime <= to)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetSessionsForPeriod: {ex.Message}");
                return new List<MinigameSession>();
            }
        }

        /// <summary>Actualiza una sesión ya guardada (p.ej. la emoción o el ánimo post-juego).</summary>
        public async Task UpdateMinigameSession(MinigameSession session, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(session.RemoteId)) session.RemoteId = CloudSync.NewRemoteId();
                await _db.UpdateAsync(session);
                if (sync) CloudSync.PushMinigameSession(session);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] UpdateMinigameSession: {ex.Message}");
                throw;
            }
        }

        /// <summary>Mejor RelaxationScore (0-1) histórico de un minijuego; null si nunca se ha jugado.</summary>
        public async Task<float?> GetBestRelaxationScore(MinigameType type)
        {
            try
            {
                var sessions = await GetSessionsForMinigame(type);
                if (sessions == null || sessions.Count == 0) return null;

                float best = 0f;
                foreach (var session in sessions)
                    if (session.RelaxationScore > best) best = session.RelaxationScore;

                return best;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetBestRelaxationScore: {ex.Message}");
                return null;
            }
        }

        /// <summary>Mejor valor histórico de una métrica (Metrics/MetricsJson) para un minijuego.</summary>
        public async Task<float> GetBestMetric(MinigameType type, string metricKey)
        {
            try
            {
                var sessions = await GetSessionsForMinigame(type);
                float best = 0f;

                foreach (var session in sessions)
                {
                    var metrics = session.Metrics;
                    if (metrics != null && metrics.TryGetValue(metricKey, out float value) && value > best)
                        best = value;
                }

                return best;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetBestMetric: {ex.Message}");
                return 0f;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // PERFIL
        // ══════════════════════════════════════════════════════════════

        public async Task<UserProfile> GetUserProfile()
        {
            try
            {
                return await _db.Table<UserProfile>().FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetUserProfile: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Guarda el perfil. Al actualizar se conservan las monedas de la BD: solo cambian con
        /// AddCoins/SpendCoins/SetCoins, así un perfil leído antes de sumar monedas no las pisa.
        /// </summary>
        public async Task SaveUserProfile(UserProfile profile, bool sync = true)
        {
            try
            {
                if (profile.Id == 0)
                {
                    await _db.InsertAsync(profile);
                }
                else
                {
                    var current = await _db.Table<UserProfile>().Where(p => p.Id == profile.Id).FirstOrDefaultAsync();
                    if (current != null) profile.Coins = current.Coins;
                    await _db.UpdateAsync(profile);
                }

                if (sync) CloudSync.PushProfile(profile);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveUserProfile: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Suma monedas al perfil activo mediante UPDATE atómico en SQL,
        /// evitando condiciones de carrera entre awaits concurrentes.
        /// </summary>
        public async Task AddCoins(int amount)
        {
            try
            {
                int rows = await _db.ExecuteAsync(
                    "UPDATE UserProfiles SET Coins = Coins + ? " +
                    "WHERE Id = (SELECT Id FROM UserProfiles LIMIT 1)",
                    amount);

                if (rows == 0)
                    Debug.LogWarning("[DataRepository] AddCoins: no existe perfil de usuario.");
                else
                    CloudSync.PushCoinsDelta(amount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] AddCoins: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Descuenta monedas si hay saldo suficiente. El UPDATE es condicional en SQL:
        /// solo ejecuta el descuento si Coins >= amount en el momento de la escritura.
        /// Devuelve false si el saldo es insuficiente o no existe perfil.
        /// </summary>
        public async Task<bool> SpendCoins(int amount)
        {
            try
            {
                var profile = await _db.Table<UserProfile>().FirstOrDefaultAsync();
                if (profile == null || profile.Coins < amount) return false;

                int rows = await _db.ExecuteAsync(
                    "UPDATE UserProfiles SET Coins = Coins - ? WHERE Id = ? AND Coins >= ?",
                    amount, profile.Id, amount);

                if (rows > 0) CloudSync.PushCoinsDelta(-amount);
                return rows > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SpendCoins: {ex.Message}");
                return false;
            }
        }

        /// <summary>Fija el saldo sin subirlo (lo usa la sincronización con el saldo de Firestore).</summary>
        public async Task SetCoins(int coins)
        {
            try
            {
                await _db.ExecuteAsync(
                    "UPDATE UserProfiles SET Coins = ? WHERE Id = (SELECT Id FROM UserProfiles LIMIT 1)",
                    coins);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SetCoins: {ex.Message}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // INVENTARIO
        // ══════════════════════════════════════════════════════════════

        public async Task<List<InventoryItem>> GetInventoryItems(int userId)
        {
            try
            {
                return await _db.Table<InventoryItem>()
                    .Where(u => u.UserId == userId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetInventoryItems: {ex.Message}");
                return new List<InventoryItem>();
            }
        }

        public async Task<List<string>> GetUnlockedItemIds(int userId)
        {
            try
            {
                var rows = await _db.Table<InventoryItem>()
                    .Where(u => u.UserId == userId)
                    .ToListAsync();
                return rows.ConvertAll(u => u.ItemId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetUnlockedItemIds: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task UnlockItem(int userId, string itemId, bool sync = true)
        {
            try
            {
                bool already = await IsItemUnlocked(userId, itemId);
                if (already) return;

                await _db.InsertAsync(new InventoryItem(userId, itemId));
                if (sync) CloudSync.PushInventoryAdd(itemId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] UnlockItem: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> IsItemUnlocked(int userId, string itemId)
        {
            try
            {
                var count = await _db.Table<InventoryItem>()
                    .Where(u => u.UserId == userId && u.ItemId == itemId)
                    .CountAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] IsItemUnlocked: {ex.Message}");
                return false;
            }
        }

        public async Task SetItemPlacement(int userId, string itemId, bool isPlaced, int placementIndex, bool sync = true)
        {
            try
            {
                var rows = await _db.Table<InventoryItem>()
                    .Where(u => u.UserId == userId && u.ItemId == itemId)
                    .ToListAsync();

                if (rows.Count == 0) return;

                var row = rows[0];
                row.IsPlaced       = isPlaced;
                row.PlacementIndex = isPlaced ? placementIndex : -1;
                await _db.UpdateAsync(row);
                if (sync) CloudSync.PushPlacement(itemId, row.PlacementIndex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SetItemPlacement: {ex.Message}");
                throw;
            }
        }

        public async Task RemoveItem(int userId, string itemId, bool sync = true)
        {
            try
            {
                var rows = await _db.Table<InventoryItem>()
                    .Where(u => u.UserId == userId && u.ItemId == itemId)
                    .ToListAsync();

                foreach (var row in rows)
                    await _db.DeleteAsync(row);

                if (sync && rows.Count > 0) CloudSync.PushInventoryRemove(itemId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] RemoveItem: {ex.Message}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // COLECCIÓN DE ESTRELLAS (StarFisher)
        // ══════════════════════════════════════════════════════════════

        public async Task<List<StarCollectionEntry>> GetStarCollection()
        {
            try
            {
                return await _db.Table<StarCollectionEntry>().ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetStarCollection: {ex.Message}");
                return new List<StarCollectionEntry>();
            }
        }

        /// <summary>
        /// Registra una captura: crea la fila si es la primera vez o suma una al contador.
        /// Devuelve la fila actualizada (TimesCaught == 1 ⇒ estrella nueva).
        /// </summary>
        public async Task<StarCollectionEntry> RegisterStarCatch(string starId, DateTime caughtAt)
        {
            try
            {
                var entry = await _db.Table<StarCollectionEntry>()
                    .Where(e => e.StarId == starId)
                    .FirstOrDefaultAsync();

                if (entry == null)
                {
                    entry = new StarCollectionEntry
                    {
                        StarId        = starId,
                        TimesCaught   = 1,
                        FirstCaughtAt = caughtAt,
                        LastCaughtAt  = caughtAt
                    };
                    await _db.InsertAsync(entry);
                }
                else
                {
                    entry.TimesCaught++;
                    entry.LastCaughtAt = caughtAt;
                    await _db.UpdateAsync(entry);
                }

                CloudSync.PushStar(entry);
                return entry;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] RegisterStarCatch: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fusiona una fila restaurada desde Firestore con la local: se queda con el mayor
        /// contador, la primera captura más antigua y la última más reciente.
        /// </summary>
        public async Task MergeStarCollectionEntry(StarCollectionEntry remote)
        {
            try
            {
                if (remote == null || string.IsNullOrEmpty(remote.StarId)) return;

                var local = await _db.Table<StarCollectionEntry>()
                    .Where(e => e.StarId == remote.StarId)
                    .FirstOrDefaultAsync();

                if (local == null)
                {
                    remote.Id = 0;
                    await _db.InsertAsync(remote);
                    return;
                }

                local.TimesCaught   = Math.Max(local.TimesCaught, remote.TimesCaught);
                local.FirstCaughtAt = remote.FirstCaughtAt < local.FirstCaughtAt ? remote.FirstCaughtAt : local.FirstCaughtAt;
                local.LastCaughtAt  = remote.LastCaughtAt  > local.LastCaughtAt  ? remote.LastCaughtAt  : local.LastCaughtAt;
                await _db.UpdateAsync(local);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] MergeStarCollectionEntry: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // NOTIFICACIONES (centro de notificaciones)
        // ══════════════════════════════════════════════════════════════

        public async Task SaveNotification(AppNotification notification, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(notification.RemoteId)) notification.RemoteId = CloudSync.NewRemoteId();
                await _db.InsertAsync(notification);
                if (sync) CloudSync.PushNotification(notification);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveNotification: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateNotification(AppNotification notification, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(notification.RemoteId)) notification.RemoteId = CloudSync.NewRemoteId();
                await _db.UpdateAsync(notification);
                if (sync) CloudSync.PushNotification(notification);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] UpdateNotification: {ex.Message}");
                throw;
            }
        }

        /// <summary>Notificación por id remoto (para las de id determinista); null si no existe.</summary>
        public async Task<AppNotification> GetNotificationByRemoteId(string remoteId)
        {
            try
            {
                return await _db.Table<AppNotification>()
                    .Where(n => n.RemoteId == remoteId)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetNotificationByRemoteId: {ex.Message}");
                return null;
            }
        }

        /// <summary>Última notificación del tipo indicado; null si no hay ninguna.</summary>
        public async Task<AppNotification> GetLastNotificationOfType(NotificationType type)
        {
            try
            {
                return await _db.Table<AppNotification>()
                    .Where(n => n.Type == type)
                    .OrderByDescending(n => n.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetLastNotificationOfType: {ex.Message}");
                return null;
            }
        }

        /// <summary>Notificaciones del tipo indicado dentro del rango, más antiguas primero (p. ej. activaciones de apoyo para el informe).</summary>
        public async Task<List<AppNotification>> GetNotificationsOfTypeForPeriod(NotificationType type, DateTime from, DateTime to)
        {
            try
            {
                return await _db.Table<AppNotification>()
                    .Where(n => n.Type == type && n.CreatedAt >= from && n.CreatedAt <= to)
                    .OrderBy(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetNotificationsOfTypeForPeriod: {ex.Message}");
                return new List<AppNotification>();
            }
        }

        /// <summary>Todas las notificaciones, incluidas las resueltas (para la sincronización).</summary>
        public async Task<List<AppNotification>> GetAllNotifications()
        {
            try
            {
                return await _db.Table<AppNotification>().ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetAllNotifications: {ex.Message}");
                return new List<AppNotification>();
            }
        }

        /// <summary>Ancladas pendientes (sección "Importante"), más recientes primero.</summary>
        public async Task<List<AppNotification>> GetPinnedNotifications()
        {
            try
            {
                return await _db.Table<AppNotification>()
                    .Where(n => n.IsPinned && n.ResolvedAt == null)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetPinnedNotifications: {ex.Message}");
                return new List<AppNotification>();
            }
        }

        /// <summary>Página de notificaciones no ancladas, más recientes primero.</summary>
        public async Task<List<AppNotification>> GetNotificationsPage(int offset, int count)
        {
            try
            {
                return await _db.Table<AppNotification>()
                    .Where(n => !n.IsPinned && n.ResolvedAt == null)
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(offset)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetNotificationsPage: {ex.Message}");
                return new List<AppNotification>();
            }
        }

        /// <summary>true si hay alguna anclada pendiente o alguna no anclada sin leer (la "!" del menú).</summary>
        public async Task<bool> HasNotificationsNeedingAttention()
        {
            try
            {
                int count = await _db.Table<AppNotification>()
                    .Where(n => n.ResolvedAt == null && (n.IsPinned || !n.IsRead))
                    .CountAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] HasNotificationsNeedingAttention: {ex.Message}");
                return false;
            }
        }

        /// <summary>Marca como leídas todas las no ancladas. Las ancladas siguen pendientes hasta resolverse.</summary>
        public async Task MarkAllNotificationsRead()
        {
            try
            {
                var unread = await _db.Table<AppNotification>()
                    .Where(n => !n.IsPinned && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in unread)
                {
                    notification.IsRead = true;
                    await _db.UpdateAsync(notification);
                    CloudSync.PushNotification(notification);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] MarkAllNotificationsRead: {ex.Message}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ESCALAS VALIDADAS (WHO-5)
        // ══════════════════════════════════════════════════════════════

        public async Task SaveScaleResponse(ScaleResponse response, bool sync = true)
        {
            try
            {
                if (string.IsNullOrEmpty(response.RemoteId)) response.RemoteId = CloudSync.NewRemoteId();
                await _db.InsertAsync(response);
                if (sync) CloudSync.PushScaleResponse(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveScaleResponse: {ex.Message}");
                throw;
            }
        }

        /// <summary>Último envío de la escala; null si nunca se ha respondido.</summary>
        public async Task<ScaleResponse> GetLastScaleResponse(ScaleType scale)
        {
            try
            {
                return await _db.Table<ScaleResponse>()
                    .Where(r => r.Scale == scale)
                    .OrderByDescending(r => r.CompletedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetLastScaleResponse: {ex.Message}");
                return null;
            }
        }

        /// <summary>Envíos de la escala dentro del rango, más antiguos primero (para gráficos e informe).</summary>
        public async Task<List<ScaleResponse>> GetScaleResponsesForPeriod(ScaleType scale, DateTime from, DateTime to)
        {
            try
            {
                return await _db.Table<ScaleResponse>()
                    .Where(r => r.Scale == scale && r.CompletedAt >= from && r.CompletedAt <= to)
                    .OrderBy(r => r.CompletedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetScaleResponsesForPeriod: {ex.Message}");
                return new List<ScaleResponse>();
            }
        }

        /// <summary>Todos los envíos de todas las escalas (para la sincronización).</summary>
        public async Task<List<ScaleResponse>> GetAllScaleResponses()
        {
            try
            {
                return await _db.Table<ScaleResponse>().ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetAllScaleResponses: {ex.Message}");
                return new List<ScaleResponse>();
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ADMINISTRACIÓN
        // ══════════════════════════════════════════════════════════════

        public async Task DeleteAllData()
        {
            try
            {
                await _db.DeleteAllAsync<EmotionRecord>();
                await _db.DeleteAllAsync<MinigameSession>();
                await _db.DeleteAllAsync<DiaryEntry>();
                await _db.DeleteAllAsync<UserProfile>();
                await _db.DeleteAllAsync<InventoryItem>();
                await _db.DeleteAllAsync<StarCollectionEntry>();
                await _db.DeleteAllAsync<AppNotification>();
                await _db.DeleteAllAsync<ScaleResponse>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] DeleteAllData: {ex.Message}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // RACHAS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calcula la racha actual usando solo fechas distintas (sin cargar objetos completos).
        /// La racha se mantiene si el último registro es de hoy o de ayer.
        /// </summary>
        public async Task<int> GetCurrentStreak()
        {
            try
            {
                var dates = await _getDistinctDates(descending: true);

                if (dates.Count == 0) return 0;

                var today     = DateTime.Today;
                var yesterday = today.AddDays(-1);

                if (dates[0] != today && dates[0] != yesterday) return 0;

                int streak = 1;
                for (int i = 1; i < dates.Count; i++)
                {
                    if ((dates[i - 1] - dates[i]).Days == 1)
                        streak++;
                    else
                        break;
                }
                return streak;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetCurrentStreak: {ex.Message}");
                return 0;
            }
        }

        /// <summary>Calcula la racha más larga registrada históricamente.</summary>
        public async Task<int> GetLongestStreak()
        {
            try
            {
                var dates = await _getDistinctDates(descending: false);

                if (dates.Count == 0) return 0;

                int longest = 1;
                int current = 1;

                for (int i = 1; i < dates.Count; i++)
                {
                    if ((dates[i] - dates[i - 1]).Days == 1)
                    {
                        current++;
                        if (current > longest) longest = current;
                    }
                    else
                    {
                        current = 1;
                    }
                }
                return longest;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] GetLongestStreak: {ex.Message}");
                return 0;
            }
        }

        public async Task<string> GetEmotionDatesDebug()
        {
            var records = await _db.Table<EmotionRecord>().ToListAsync();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Total registros: {records.Count}");
            foreach (var r in records.OrderByDescending(r => r.Timestamp))
            {
                sb.AppendLine($"ID:{r.Id} | " +
                              $"Timestamp: {r.Timestamp:yyyy-MM-dd HH:mm:ss} | " +
                              $"Source: {r.Source} | " +
                              $"Notes: {r.Notes}");
            }
            return sb.ToString();
        }

        // ── Helpers privados ───────────────────────────────────────────

        /// <summary>
        /// Devuelve fechas distintas de todos los registros emocionales.
        /// sqlite-net-pcl almacena DateTime como ticks, incompatibles con date() de SQLite,
        /// por lo que la deduplicación se hace en memoria con LINQ.
        /// </summary>
        private async Task<List<DateTime>> _getDistinctDates(bool descending)
        {
            var records = await _db.Table<EmotionRecord>().ToListAsync();
            var query = records.Select(r => r.Timestamp.Date).Distinct();
            return (descending
                ? query.OrderByDescending(d => d)
                : query.OrderBy(d => d)).ToList();
        }
    }
}
