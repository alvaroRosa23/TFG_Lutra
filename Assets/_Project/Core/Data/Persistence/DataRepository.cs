using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.Persistence
{
    /// <summary>
    /// Capa de acceso a datos. Usa directamente la API async de SQLiteAsyncConnection
    /// para evitar bloquear el hilo principal de Unity.
    ///
    /// Dependencia: DatabaseManager registrado en ServiceLocator antes de llamar a Initialize().
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

        public async Task SaveEmotion(EmotionRecord record)
        {
            try
            {
                await _db.InsertAsync(record);
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

        public async Task UpdateEmotion(EmotionRecord record)
        {
            try
            {
                await _db.UpdateAsync(record);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] UpdateEmotion: {ex.Message}");
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

        public async Task SaveDiaryEntry(DiaryEntry entry)
        {
            try
            {
                if (entry.Id == 0)
                    await _db.InsertAsync(entry);
                else
                    await _db.UpdateAsync(entry);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveDiaryEntry: {ex.Message}");
                throw;
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

        public async Task SaveMinigameSession(MinigameSession session)
        {
            try
            {
                await _db.InsertAsync(session);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SaveMinigameSession: {ex.Message}");
                throw;
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

        public async Task SaveUserProfile(UserProfile profile)
        {
            try
            {
                if (profile.Id == 0)
                    await _db.InsertAsync(profile);
                else
                    await _db.UpdateAsync(profile);
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

                return rows > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SpendCoins: {ex.Message}");
                return false;
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

        public async Task UnlockItem(int userId, string itemId)
        {
            try
            {
                bool already = await IsItemUnlocked(userId, itemId);
                if (already) return;

                await _db.InsertAsync(new InventoryItem(userId, itemId));
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

        public async Task SetItemPlacement(int userId, string itemId, bool isPlaced, int placementIndex)
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] SetItemPlacement: {ex.Message}");
                throw;
            }
        }

        public async Task RemoveItem(int userId, string itemId)
        {
            try
            {
                var rows = await _db.Table<InventoryItem>()
                    .Where(u => u.UserId == userId && u.ItemId == itemId)
                    .ToListAsync();

                foreach (var row in rows)
                    await _db.DeleteAsync(row);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataRepository] RemoveItem: {ex.Message}");
                throw;
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
