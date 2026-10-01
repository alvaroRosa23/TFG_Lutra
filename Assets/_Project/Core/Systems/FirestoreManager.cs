using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Gestiona la persistencia remota de perfiles de usuario en Firestore.
    /// Actúa como capa de sincronización secundaria: SQLite es la fuente local,
    /// Firestore permite restaurar datos en dispositivos nuevos.
    /// </summary>
    public class FirestoreManager : BaseService
    {
        private FirebaseFirestore _db;

        private AuthManager _authManager;
        private AuthManager Auth => _authManager ??= ServiceLocator.Get<AuthManager>();

        // ── Unity lifecycle ────────────────────────────────────────────

        // Las monedas cambian en muchos sitios (minijuegos, tienda, ventas): se suben a Firestore
        // cada vez que alguien emite el nuevo total.
        private void OnEnable()  => EventBus.OnCoinsChanged += _onCoinsChanged;
        private void OnDisable() => EventBus.OnCoinsChanged -= _onCoinsChanged;

        private void _onCoinsChanged(int total) => _ = _safeSaveCoins();

        // Se lee el saldo de SQLite en vez de fiarse del evento (que emite 0 si no hay perfil)
        private async Task _safeSaveCoins()
        {
            try
            {
                if (_db == null || !Auth.IsLoggedIn) return;

                var profile = await ServiceLocator.Get<DataRepository>().GetUserProfile();
                if (profile == null) return;
                if (!string.IsNullOrEmpty(profile.FirebaseUserId) && profile.FirebaseUserId != Auth.CurrentUserId) return;

                await SaveCoins(Auth.CurrentUserId, profile.Coins);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] Sync de monedas fallido: {ex.Message}");
            }
        }

        public Task<bool> InitializeAsync()
        {
            try
            {
                _db = FirebaseFirestore.DefaultInstance;
                Debug.Log("[FirestoreManager] Firestore inicializado.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] InitializeAsync: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public async Task SaveUserProfile(UserProfile profile)
        {
            try
            {
                if (_db == null)
                {
                    Debug.LogError("[FirestoreManager] _db es null. ¿Se llamó InitializeAsync?");
                    return;
                }

                if (string.IsNullOrEmpty(profile.FirebaseUserId))
                {
                    Debug.LogError("[FirestoreManager] FirebaseUserId está vacío. No se puede guardar.");
                    return;
                }

                Debug.Log($"[FirestoreManager] Guardando perfil para userId: {profile.FirebaseUserId}");

                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(profile.FirebaseUserId);

                var data = new Dictionary<string, object>
                {
                    { "name",         profile.Name },
                    { "surname",      profile.Surname },
                    { "email",        profile.Email },
                    { "dateOfBirth",  profile.DateOfBirth.ToString("yyyy-MM-dd") },
                    { "culture",      (int)profile.Culture },
                    { "hobbiesJson",  profile.HobbiesJson },
                    { "coins",        profile.Coins },
                    { "creationDate", profile.CreationDate.ToString("yyyy-MM-dd") }
                };

                await docRef.SetAsync(data, SetOptions.MergeAll);
                Debug.Log($"[FirestoreManager] Perfil guardado correctamente en Firestore para: {profile.FirebaseUserId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] SaveUserProfile FAILED: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async Task<UserProfile> GetUserProfile(string firebaseUserId)
        {
            try
            {
                Debug.Log($"[FirestoreManager] Buscando perfil para userId: {firebaseUserId}");

                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                Debug.Log($"[FirestoreManager] Snapshot exists: {snapshot.Exists}");

                if (!snapshot.Exists) return null;

                var d = snapshot.ToDictionary();

                var profile = new UserProfile
                {
                    FirebaseUserId = firebaseUserId,
                    Name           = d.TryGetValue("name",         out var name)         ? name?.ToString()         : "",
                    Surname        = d.TryGetValue("surname",       out var surname)       ? surname?.ToString()       : "",
                    Email          = d.TryGetValue("email",         out var email)         ? email?.ToString()         : "",
                    HobbiesJson    = d.TryGetValue("hobbiesJson",   out var hobbies)       ? hobbies?.ToString()       : "",
                    Coins          = d.TryGetValue("coins",         out var coins)         ? Convert.ToInt32(coins)    : 0,
                    Culture        = d.TryGetValue("culture",       out var culture)       ? (CultureType)Convert.ToInt32(culture) : default,
                    DateOfBirth    = d.TryGetValue("dateOfBirth",   out var dob)  && DateTime.TryParse(dob?.ToString(),  out var dobParsed)  ? dobParsed  : default,
                    CreationDate   = d.TryGetValue("creationDate",  out var created) && DateTime.TryParse(created?.ToString(), out var createdParsed) ? createdParsed : DateTime.Now,
                };

                return profile;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] GetUserProfile: {ex.Message}");
                return null;
            }
        }

        public async Task SaveLastCheckIn(string firebaseUserId, DateTime date, EmotionType lastEmotion)
        {
            try
            {
                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                var checkInDates = new List<string>();
                if (snapshot.Exists && snapshot.ContainsField("checkInHistory"))
                {
                    var existing = snapshot.GetValue<List<object>>("checkInHistory");
                    foreach (var d in existing)
                        checkInDates.Add(d.ToString());
                }

                string dateStr = date.ToString("yyyy-MM-dd");
                if (!checkInDates.Contains(dateStr))
                    checkInDates.Add(dateStr);

                checkInDates.Sort();
                if (checkInDates.Count > 60)
                    checkInDates = checkInDates.Skip(checkInDates.Count - 60).ToList();

                var updates = new Dictionary<string, object>
                {
                    { "lastCheckInDate",  dateStr },
                    { "lastEmotionType",  (int)lastEmotion },
                    { "checkInHistory",   checkInDates }
                };

                await docRef.UpdateAsync(updates);
                Debug.Log($"[FirestoreManager] Check-in guardado. Historial: {checkInDates.Count} días");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] SaveLastCheckIn: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // DIARIO
        // ══════════════════════════════════════════════════════════════

        public async Task SaveDiaryEntry(string firebaseUserId, DiaryEntry entry)
        {
            try
            {
                string docId = entry.Date.Date.ToString("yyyy-MM-dd");

                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId)
                    .Collection("diary")
                    .Document(docId);

                var data = new Dictionary<string, object>
                {
                    { "date",      entry.Date.ToString("yyyy-MM-dd") },
                    { "title",     entry.Title   ?? string.Empty },
                    { "content",   entry.Content ?? string.Empty },
                    { "mood",      entry.Mood    ?? string.Empty },
                    { "timestamp", entry.Date.ToString("o") }
                };

                await docRef.SetAsync(data);
                Debug.Log($"[FirestoreManager] Entrada de diario guardada: {docId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] SaveDiaryEntry: {ex.Message}");
            }
        }

        public async Task<List<DiaryEntry>> GetDiaryEntries(string firebaseUserId)
        {
            try
            {
                CollectionReference colRef = _db
                    .Collection("users")
                    .Document(firebaseUserId)
                    .Collection("diary");

                QuerySnapshot snapshot = await colRef.GetSnapshotAsync();

                var entries = new List<DiaryEntry>();
                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();

                    if (!d.TryGetValue("date", out var rawDate) ||
                        !DateTime.TryParse(rawDate?.ToString(), out var date))
                        continue;

                    entries.Add(new DiaryEntry
                    {
                        Date    = date.Date,
                        Title   = d.TryGetValue("title",   out var t) ? t?.ToString() : string.Empty,
                        Content = d.TryGetValue("content", out var c) ? c?.ToString() : string.Empty,
                        Mood    = d.TryGetValue("mood",    out var m) ? m?.ToString() : string.Empty
                    });
                }

                Debug.Log($"[FirestoreManager] Entradas de diario recuperadas: {entries.Count}");
                return entries;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetDiaryEntries: {ex.Message}");
                return new List<DiaryEntry>();
            }
        }

        public async Task<List<DateTime>> GetCheckInHistory(string firebaseUserId)
        {
            try
            {
                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists || !snapshot.ContainsField("checkInHistory"))
                    return new List<DateTime>();

                var dates = new List<DateTime>();
                var rawDates = snapshot.GetValue<List<object>>("checkInHistory");

                foreach (var d in rawDates)
                {
                    if (DateTime.TryParse(d.ToString(), out DateTime parsed))
                        dates.Add(parsed.Date);
                }

                return dates;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetCheckInHistory: {ex.Message}");
                return new List<DateTime>();
            }
        }

        /// <summary>
        /// Elimina el documento del usuario y sus subcolecciones de diario y estrellas.
        /// Llamar antes de borrar la cuenta de Firebase Auth.
        /// </summary>
        public async Task DeleteUserData(string firebaseUserId)
        {
            try
            {
                // Borrar cada documento de la subcolección diary
                CollectionReference diaryRef = _db
                    .Collection("users")
                    .Document(firebaseUserId)
                    .Collection("diary");

                QuerySnapshot diarySnap = await diaryRef.GetSnapshotAsync();
                foreach (DocumentSnapshot doc in diarySnap.Documents)
                    await doc.Reference.DeleteAsync();

                // Borrar cada documento de la subcolección stars (colección de StarFisher)
                QuerySnapshot starsSnap = await _starsCollection(firebaseUserId).GetSnapshotAsync();
                foreach (DocumentSnapshot doc in starsSnap.Documents)
                    await doc.Reference.DeleteAsync();

                // Borrar el documento principal del usuario
                await _db.Collection("users").Document(firebaseUserId).DeleteAsync();

                Debug.Log($"[FirestoreManager] Datos de usuario eliminados de Firestore: {firebaseUserId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] DeleteUserData: {ex.Message}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // INVENTARIO
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Añade un itemId al array de inventario del usuario en Firestore.
        /// Idempotente: no añade duplicados.
        /// </summary>
        public async Task AddInventoryItem(string firebaseUserId, string itemId)
        {
            try
            {
                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                var itemIds = new List<string>();

                if (snapshot.Exists && snapshot.ContainsField("inventoryItems"))
                {
                    var existing = snapshot.GetValue<List<object>>("inventoryItems");
                    foreach (var d in existing)
                        itemIds.Add(d.ToString());
                }

                if (!itemIds.Contains(itemId))
                {
                    itemIds.Add(itemId);
                    await docRef.UpdateAsync(new Dictionary<string, object>
                        { { "inventoryItems", itemIds } });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] AddInventoryItem: {ex.Message}");
            }
        }

        /// <summary>Elimina un itemId del array de inventario del usuario en Firestore.</summary>
        public async Task RemoveInventoryItem(string firebaseUserId, string itemId)
        {
            try
            {
                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                if (!snapshot.Exists || !snapshot.ContainsField("inventoryItems")) return;

                var existing = snapshot.GetValue<List<object>>("inventoryItems");
                var itemIds  = new List<string>();
                foreach (var d in existing)
                    itemIds.Add(d.ToString());

                if (itemIds.Remove(itemId))
                    await docRef.UpdateAsync(new Dictionary<FieldPath, object>
                    {
                        { new FieldPath("inventoryItems"), itemIds },
                        { new FieldPath(PlacementsField, itemId), FieldValue.Delete }
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] RemoveInventoryItem: {ex.Message}");
            }
        }

        /// <summary>Devuelve los IDs de todos los ítems del inventario del usuario en Firestore.</summary>
        public async Task<List<string>> GetInventoryItemIds(string firebaseUserId)
        {
            try
            {
                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                if (!snapshot.Exists || !snapshot.ContainsField("inventoryItems"))
                    return new List<string>();

                var raw    = snapshot.GetValue<List<object>>("inventoryItems");
                var result = new List<string>();
                foreach (var d in raw)
                    result.Add(d.ToString());

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetInventoryItemIds: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Guarda dónde está colocado un ítem de SafeZone (mapa inventoryPlacements: itemId → índice
        /// del PlacementPoint). placementIndex &lt; 0 = no colocado (se borra la clave).
        /// </summary>
        public async Task SetInventoryPlacement(string firebaseUserId, string itemId, int placementIndex)
        {
            try
            {
                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                // FieldPath por segmentos: los itemId pueden ser numéricos ("2") o llevar puntos
                object value = placementIndex >= 0 ? placementIndex : FieldValue.Delete;
                await docRef.UpdateAsync(new Dictionary<FieldPath, object>
                    { { new FieldPath(PlacementsField, itemId), value } });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] SetInventoryPlacement: {ex.Message}");
            }
        }

        /// <summary>Devuelve itemId → índice de colocación de los ítems colocados en SafeZone.</summary>
        public async Task<Dictionary<string, int>> GetInventoryPlacements(string firebaseUserId)
        {
            var result = new Dictionary<string, int>();
            try
            {
                DocumentSnapshot snapshot = await _db
                    .Collection("users")
                    .Document(firebaseUserId)
                    .GetSnapshotAsync();

                if (!snapshot.Exists || !snapshot.ContainsField(PlacementsField))
                    return result;

                var raw = snapshot.GetValue<Dictionary<string, object>>(PlacementsField);
                foreach (var pair in raw)
                    if (int.TryParse(pair.Value?.ToString(), out int index) && index >= 0)
                        result[pair.Key] = index;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetInventoryPlacements: {ex.Message}");
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════
        // MONEDAS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Actualiza solo el saldo de monedas del perfil remoto.</summary>
        public async Task SaveCoins(string firebaseUserId, int coins)
        {
            try
            {
                await _db.Collection("users")
                    .Document(firebaseUserId)
                    .SetAsync(new Dictionary<string, object> { { "coins", coins } }, SetOptions.MergeAll);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] SaveCoins: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // COLECCIÓN DE ESTRELLAS (StarFisher)
        // ══════════════════════════════════════════════════════════════

        /// <summary>Guarda (sobrescribe) una estrella de la colección en users/{uid}/stars/{starId}.</summary>
        public async Task SaveStarEntry(string firebaseUserId, StarCollectionEntry entry)
        {
            try
            {
                var data = new Dictionary<string, object>
                {
                    { "timesCaught",   entry.TimesCaught },
                    { "firstCaughtAt", entry.FirstCaughtAt.ToString("o") },
                    { "lastCaughtAt",  entry.LastCaughtAt.ToString("o") }
                };

                await _starsCollection(firebaseUserId).Document(entry.StarId).SetAsync(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] SaveStarEntry: {ex.Message}");
            }
        }

        public async Task<List<StarCollectionEntry>> GetStarCollection(string firebaseUserId)
        {
            var entries = new List<StarCollectionEntry>();
            try
            {
                QuerySnapshot snapshot = await _starsCollection(firebaseUserId).GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();

                    int.TryParse(d.TryGetValue("timesCaught", out var times) ? times?.ToString() : null, out int timesCaught);
                    if (timesCaught <= 0) continue;

                    entries.Add(new StarCollectionEntry
                    {
                        StarId        = doc.Id,
                        TimesCaught   = timesCaught,
                        FirstCaughtAt = _parseDate(d, "firstCaughtAt"),
                        LastCaughtAt  = _parseDate(d, "lastCaughtAt")
                    });
                }

                Debug.Log($"[FirestoreManager] Estrellas de la colección recuperadas: {entries.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetStarCollection: {ex.Message}");
            }
            return entries;
        }

        public async Task<(DateTime? date, EmotionType emotion)> GetLastCheckIn(string firebaseUserId)
        {
            try
            {
                DocumentReference docRef = _db
                    .Collection("users")
                    .Document(firebaseUserId);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

                if (!snapshot.Exists) return (null, EmotionType.Calm);

                var d = snapshot.ToDictionary();

                DateTime? date = null;
                if (d.TryGetValue("lastCheckInDate", out var raw) && raw != null)
                {
                    if (DateTime.TryParse(raw.ToString(), out var parsed))
                        date = parsed;
                }

                EmotionType emotion = EmotionType.Calm;
                if (d.TryGetValue("lastEmotionType", out var emotionRaw) && emotionRaw != null)
                {
                    if (int.TryParse(emotionRaw.ToString(), out var emotionInt))
                        emotion = (EmotionType)emotionInt;
                }

                return (date, emotion);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetLastCheckIn: {ex.Message}");
                return (null, EmotionType.Calm);
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        private const string PlacementsField = "inventoryPlacements";

        private CollectionReference _starsCollection(string firebaseUserId) => _db
            .Collection("users")
            .Document(firebaseUserId)
            .Collection("stars");

        private static DateTime _parseDate(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out var raw) && DateTime.TryParse(raw?.ToString(), null,
                       System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.Now;
        }
    }
}
