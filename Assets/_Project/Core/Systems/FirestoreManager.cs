using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Estado del documento users/{uid} leído de una vez (perfil, monedas, inventario, colocaciones,
    /// preferencias y último check-in). Los campos que no existen en Firestore quedan a null.
    /// </summary>
    public class RemoteUserState
    {
        public bool                    Exists;
        public int                     SyncVersion;
        public int?                    Coins;
        public string                  PreferencesJson;
        public string                  Avatar;
        public HashSet<string>         InventoryItems   = new HashSet<string>();
        public HashSet<string>         InventoryRemoved = new HashSet<string>();
        /// <summary>null si el campo no existe (nunca se ha subido ninguna colocación).</summary>
        public Dictionary<string, int> Placements;
        public DateTime?               LastCheckInDate;
        public EmotionType             LastEmotion = EmotionType.Calm;
        public List<DateTime>          CheckInHistory = new List<DateTime>();
    }

    /// <summary>
    /// Entradas del diario en Firestore. Las borradas quedan como marcador sin contenido
    /// (deleted = true) para que otro dispositivo no vuelva a subir su copia local.
    /// </summary>
    public class RemoteDiary
    {
        public List<DiaryEntry> Entries    = new List<DiaryEntry>();
        public HashSet<string>  DeletedIds = new HashSet<string>();
    }

    /// <summary>
    /// Persistencia remota en Firestore. SQLite es la fuente local; Firestore permite restaurar
    /// los datos en otro dispositivo. La sincronización (subidas automáticas y reconciliación al
    /// iniciar) la coordina CloudSync; esta clase solo lee y escribe documentos.
    ///
    /// Estructura:
    ///   users/{uid}                       perfil, coins, preferencesJson, avatar, inventoryItems,
    ///                                     inventoryRemoved, inventoryPlacements, lastCheckInDate...
    ///   users/{uid}/diary/{remoteId}      entradas del diario (las antiguas usan la fecha como id;
    ///                                     las borradas quedan como { deleted: true, deletedAt })
    ///   users/{uid}/emotions/{remoteId}   registros emocionales (check-ins y momentos)
    ///   users/{uid}/minigameSessions/{remoteId}  partidas de minijuegos (récords y gráficas)
    ///   users/{uid}/stars/{starId}        colección de estrellas de StarFisher
    ///
    /// Los métodos de escritura propagan las excepciones; CloudSync las registra.
    /// </summary>
    public class FirestoreManager : BaseService
    {
        private const string PlacementsField = "inventoryPlacements";
        private const string InventoryField  = "inventoryItems";
        private const string RemovedField    = "inventoryRemoved";

        /// <summary>Versión del formato de sincronización (2 = monedas por incrementos).</summary>
        public const int SyncVersion = 2;

        private FirebaseFirestore _db;

        public bool IsReady => _db != null;

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

        // ══════════════════════════════════════════════════════════════
        // PERFIL
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Guarda los datos del perfil (fusionando con lo que ya haya). Las monedas solo se
        /// escriben con includeCoins (al crear la cuenta): después cambian con IncrementCoins.
        /// </summary>
        public async Task SaveUserProfile(UserProfile profile, bool includeCoins = false)
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

            var data = new Dictionary<string, object>
            {
                { "name",            profile.Name },
                { "surname",         profile.Surname },
                { "email",           profile.Email },
                { "avatar",          profile.Avatar ?? string.Empty },
                { "dateOfBirth",     profile.DateOfBirth.ToString("yyyy-MM-dd") },
                { "culture",         (int)profile.Culture },
                { "hobbiesJson",     profile.HobbiesJson },
                { "preferencesJson", profile.PreferencesJson ?? string.Empty },
                { "creationDate",    profile.CreationDate.ToString("yyyy-MM-dd") }
            };
            if (includeCoins)
            {
                data["coins"]       = profile.Coins;
                data["syncVersion"] = SyncVersion;
            }

            await _userDoc(profile.FirebaseUserId).SetAsync(data, SetOptions.MergeAll);
            Debug.Log($"[FirestoreManager] Perfil guardado en Firestore para: {profile.FirebaseUserId}");
        }

        public async Task<UserProfile> GetUserProfile(string firebaseUserId)
        {
            try
            {
                DocumentSnapshot snapshot = await _userDoc(firebaseUserId).GetSnapshotAsync();
                Debug.Log($"[FirestoreManager] Perfil en Firestore para {firebaseUserId}: {snapshot.Exists}");

                if (!snapshot.Exists) return null;

                var d = snapshot.ToDictionary();

                // Un documento solo con inventario/monedas (sin nombre) no es un perfil completo
                if (!d.ContainsKey("name")) return null;

                return new UserProfile
                {
                    FirebaseUserId  = firebaseUserId,
                    Name            = _string(d, "name"),
                    Surname         = _string(d, "surname"),
                    Email           = _string(d, "email"),
                    Avatar          = _string(d, "avatar"),
                    HobbiesJson     = _string(d, "hobbiesJson"),
                    PreferencesJson = _string(d, "preferencesJson"),
                    Coins           = d.TryGetValue("coins", out var coins) ? Convert.ToInt32(coins) : 0,
                    Culture         = d.TryGetValue("culture", out var culture) ? (CultureType)Convert.ToInt32(culture) : default,
                    DateOfBirth     = DateTime.TryParse(_string(d, "dateOfBirth"), out var dob) ? dob : default,
                    CreationDate    = DateTime.TryParse(_string(d, "creationDate"), out var created) ? created : DateTime.Now,
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] GetUserProfile: {ex.Message}");
                return null;
            }
        }

        /// <summary>Lee de una vez todo el estado del documento del usuario (null si falla la lectura).</summary>
        public async Task<RemoteUserState> GetUserState(string firebaseUserId)
        {
            try
            {
                DocumentSnapshot snapshot = await _userDoc(firebaseUserId).GetSnapshotAsync();
                var state = new RemoteUserState { Exists = snapshot.Exists };
                if (!snapshot.Exists) return state;

                var d = snapshot.ToDictionary();

                if (d.TryGetValue("coins", out var coins) && coins != null)
                    state.Coins = Convert.ToInt32(coins);
                state.SyncVersion = _int(d, "syncVersion");
                if (d.ContainsKey("preferencesJson")) state.PreferencesJson = _string(d, "preferencesJson");
                if (d.ContainsKey("avatar"))          state.Avatar          = _string(d, "avatar");

                foreach (var id in _stringList(d, InventoryField)) state.InventoryItems.Add(id);
                foreach (var id in _stringList(d, RemovedField))   state.InventoryRemoved.Add(id);

                if (d.TryGetValue(PlacementsField, out var rawPlacements) && rawPlacements is Dictionary<string, object> placements)
                {
                    state.Placements = new Dictionary<string, int>();
                    foreach (var pair in placements)
                        if (int.TryParse(pair.Value?.ToString(), out int index) && index >= 0)
                            state.Placements[pair.Key] = index;
                }

                if (DateTime.TryParse(_string(d, "lastCheckInDate"), out var lastCheckIn))
                    state.LastCheckInDate = lastCheckIn.Date;
                if (d.TryGetValue("lastEmotionType", out var emotion) && int.TryParse(emotion?.ToString(), out int emotionInt))
                    state.LastEmotion = (EmotionType)emotionInt;

                foreach (var raw in _stringList(d, "checkInHistory"))
                    if (DateTime.TryParse(raw, out var date)) state.CheckInHistory.Add(date.Date);

                return state;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetUserState: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // CHECK-IN Y EMOCIONES
        // ══════════════════════════════════════════════════════════════

        /// <summary>Último check-in (para saber en otro dispositivo si ya se hizo hoy).</summary>
        public async Task SaveLastCheckIn(string firebaseUserId, DateTime date, EmotionType lastEmotion)
        {
            try
            {
                string dateStr = date.ToString("yyyy-MM-dd");
                await _userDoc(firebaseUserId).SetAsync(new Dictionary<string, object>
                {
                    { "lastCheckInDate", dateStr },
                    { "lastEmotionType", (int)lastEmotion },
                    { "checkInHistory",  FieldValue.ArrayUnion(dateStr) }
                }, SetOptions.MergeAll);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] SaveLastCheckIn: {ex.Message}");
            }
        }

        public async Task SaveEmotion(string firebaseUserId, EmotionRecord record)
        {
            if (string.IsNullOrEmpty(record.RemoteId)) return;

            await _subcollection(firebaseUserId, "emotions").Document(record.RemoteId).SetAsync(new Dictionary<string, object>
            {
                { "timestamp",      record.Timestamp.ToString("o") },
                { "emotionType",    (int)record.EmotionType },
                { "intensity",      record.IntensityLevel },
                { "isMorningCheck", record.IsMorningCheck },
                { "notes",          record.Notes ?? string.Empty },
                { "moodLevel",      record.MoodLevel },
                { "emotionTags",    record.SelectedEmotionTags ?? string.Empty },
                { "motiveTags",     record.SelectedMotiveTags ?? string.Empty },
                { "songTitle",      record.SongTitle ?? string.Empty },
                { "songArtist",     record.SongArtist ?? string.Empty }
            });
        }

        /// <summary>Todos los registros emocionales del usuario (null si falla la lectura).</summary>
        public async Task<List<EmotionRecord>> GetEmotions(string firebaseUserId)
        {
            try
            {
                QuerySnapshot snapshot = await _subcollection(firebaseUserId, "emotions").GetSnapshotAsync();
                var records = new List<EmotionRecord>();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();
                    if (!_tryParseTimestamp(d, "timestamp", out var timestamp)) continue;

                    records.Add(new EmotionRecord
                    {
                        RemoteId            = doc.Id,
                        Timestamp           = timestamp,
                        EmotionType         = (EmotionType)_int(d, "emotionType"),
                        IntensityLevel      = _int(d, "intensity"),
                        IsMorningCheck      = d.TryGetValue("isMorningCheck", out var morning) && morning is bool b && b,
                        Notes               = _string(d, "notes"),
                        MoodLevel           = _int(d, "moodLevel"),
                        SelectedEmotionTags = _string(d, "emotionTags"),
                        SelectedMotiveTags  = _string(d, "motiveTags"),
                        SongTitle           = _string(d, "songTitle"),
                        SongArtist          = _string(d, "songArtist"),
                        Source              = RecordSource.User
                    });
                }
                return records;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetEmotions: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // DIARIO
        // ══════════════════════════════════════════════════════════════

        public async Task SaveDiaryEntry(string firebaseUserId, DiaryEntry entry)
        {
            if (string.IsNullOrEmpty(entry.RemoteId)) return;

            await _subcollection(firebaseUserId, "diary").Document(entry.RemoteId).SetAsync(new Dictionary<string, object>
            {
                { "date",      entry.Date.ToString("yyyy-MM-dd") },
                { "timestamp", entry.Date.ToString("o") },
                { "title",     entry.Title   ?? string.Empty },
                { "content",   entry.Content ?? string.Empty },
                { "mood",      entry.Mood    ?? string.Empty }
            });
        }

        /// <summary>
        /// Borra el contenido de la entrada en Firestore y deja solo un marcador de borrado, para
        /// que la reconciliación de otros dispositivos la elimine en vez de volver a subirla.
        /// </summary>
        public async Task DeleteDiaryEntry(string firebaseUserId, string remoteId)
        {
            if (string.IsNullOrEmpty(remoteId)) return;

            await _subcollection(firebaseUserId, "diary").Document(remoteId).SetAsync(new Dictionary<string, object>
            {
                { "deleted",   true },
                { "deletedAt", DateTime.Now.ToString("o") }
            });
        }

        /// <summary>
        /// Todas las entradas del diario y los ids de las borradas (null si falla la lectura).
        /// RemoteId = id del documento (en las entradas antiguas es la fecha "yyyy-MM-dd").
        /// </summary>
        public async Task<RemoteDiary> GetDiaryEntries(string firebaseUserId)
        {
            try
            {
                QuerySnapshot snapshot = await _subcollection(firebaseUserId, "diary").GetSnapshotAsync();
                var result = new RemoteDiary();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();

                    if (d.TryGetValue("deleted", out var deleted) && deleted is bool isDeleted && isDeleted)
                    {
                        result.DeletedIds.Add(doc.Id);
                        continue;
                    }

                    if (!_tryParseTimestamp(d, "timestamp", out var date) &&
                        !DateTime.TryParse(_string(d, "date"), out date))
                        continue;

                    result.Entries.Add(new DiaryEntry
                    {
                        RemoteId = doc.Id,
                        Date     = date,
                        Title    = _string(d, "title"),
                        Content  = _string(d, "content"),
                        Mood     = _string(d, "mood")
                    });
                }

                Debug.Log($"[FirestoreManager] Entradas de diario en Firestore: {result.Entries.Count} ({result.DeletedIds.Count} borradas)");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetDiaryEntries: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // MINIJUEGOS
        // ══════════════════════════════════════════════════════════════

        public async Task SaveMinigameSession(string firebaseUserId, MinigameSession session)
        {
            if (string.IsNullOrEmpty(session.RemoteId)) return;

            await _subcollection(firebaseUserId, "minigameSessions").Document(session.RemoteId).SetAsync(new Dictionary<string, object>
            {
                { "startTime",       session.StartTime.ToString("o") },
                { "durationSeconds", session.DurationSeconds },
                { "minigameId",      (int)session.MinigameId },
                { "emotionBefore",   (int)session.EmotionBefore },
                { "emotionAfter",    (int)session.EmotionAfter },
                { "relaxationScore", session.RelaxationScore },
                { "metricsJson",     session.MetricsJson ?? string.Empty },
                { "moodBefore",      session.MoodBefore },
                { "moodBeforeAt",    session.MoodBeforeRecordedAt?.ToString("o") },
                { "moodAfter",       session.MoodAfter }
            });
        }

        /// <summary>Todas las partidas de minijuegos (null si falla la lectura).</summary>
        public async Task<List<MinigameSession>> GetMinigameSessions(string firebaseUserId)
        {
            try
            {
                QuerySnapshot snapshot = await _subcollection(firebaseUserId, "minigameSessions").GetSnapshotAsync();
                var sessions = new List<MinigameSession>();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();
                    if (!_tryParseTimestamp(d, "startTime", out var startTime)) continue;

                    sessions.Add(new MinigameSession
                    {
                        RemoteId        = doc.Id,
                        StartTime       = startTime,
                        DurationSeconds = _float(d, "durationSeconds"),
                        MinigameId      = (MinigameType)_int(d, "minigameId"),
                        EmotionBefore   = (EmotionType)_int(d, "emotionBefore"),
                        EmotionAfter    = (EmotionType)_int(d, "emotionAfter"),
                        RelaxationScore = _float(d, "relaxationScore"),
                        MetricsJson     = _string(d, "metricsJson"),
                        MoodBefore      = _nullableInt(d, "moodBefore"),
                        MoodBeforeRecordedAt = _tryParseTimestamp(d, "moodBeforeAt", out var moodBeforeAt) ? moodBeforeAt : (DateTime?)null,
                        MoodAfter       = _nullableInt(d, "moodAfter")
                    });
                }
                return sessions;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetMinigameSessions: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // INVENTARIO
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Añade un ítem al inventario remoto (ArrayUnion: atómico e idempotente) y lo quita de la
        /// lista de vendidos por si se había vendido antes.
        /// </summary>
        public async Task AddInventoryItem(string firebaseUserId, string itemId)
        {
            await _userDoc(firebaseUserId).SetAsync(new Dictionary<string, object>
            {
                { InventoryField, FieldValue.ArrayUnion(itemId) },
                { RemovedField,   FieldValue.ArrayRemove(itemId) }
            }, SetOptions.MergeAll);
        }

        /// <summary>
        /// Quita un ítem del inventario remoto, lo apunta como vendido (para que otro dispositivo
        /// con datos antiguos no lo vuelva a subir) y borra su colocación.
        /// </summary>
        public async Task RemoveInventoryItem(string firebaseUserId, string itemId)
        {
            await _userDoc(firebaseUserId).SetAsync(new Dictionary<string, object>
            {
                { InventoryField,  FieldValue.ArrayRemove(itemId) },
                { RemovedField,    FieldValue.ArrayUnion(itemId) },
                { PlacementsField, new Dictionary<string, object> { { itemId, FieldValue.Delete } } }
            }, SetOptions.MergeAll);
        }

        /// <summary>
        /// Guarda dónde está colocado un ítem de SafeZone (mapa inventoryPlacements: itemId → índice
        /// del PlacementPoint). placementIndex &lt; 0 = no colocado (se borra la clave).
        /// </summary>
        public async Task SetInventoryPlacement(string firebaseUserId, string itemId, int placementIndex)
        {
            object value = placementIndex >= 0 ? placementIndex : FieldValue.Delete;
            await _userDoc(firebaseUserId).SetAsync(new Dictionary<string, object>
            {
                { PlacementsField, new Dictionary<string, object> { { itemId, value } } }
            }, SetOptions.MergeAll);
        }

        // ══════════════════════════════════════════════════════════════
        // MONEDAS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Suma (o resta) monedas en remoto con un incremento atómico.</summary>
        public async Task IncrementCoins(string firebaseUserId, int delta)
        {
            await _userDoc(firebaseUserId).SetAsync(new Dictionary<string, object>
            {
                { "coins", FieldValue.Increment(delta) }
            }, SetOptions.MergeAll);
        }

        /// <summary>
        /// Escribe el saldo absoluto y marca el documento con la versión actual (a partir de ahí
        /// el saldo remoto manda y solo cambia con IncrementCoins).
        /// </summary>
        public async Task SaveCoins(string firebaseUserId, int coins)
        {
            await _userDoc(firebaseUserId).SetAsync(new Dictionary<string, object>
            {
                { "coins",       coins },
                { "syncVersion", SyncVersion }
            }, SetOptions.MergeAll);
        }

        // ══════════════════════════════════════════════════════════════
        // COLECCIÓN DE ESTRELLAS (StarFisher)
        // ══════════════════════════════════════════════════════════════

        /// <summary>Guarda (sobrescribe) una estrella de la colección en users/{uid}/stars/{starId}.</summary>
        public async Task SaveStarEntry(string firebaseUserId, StarCollectionEntry entry)
        {
            await _subcollection(firebaseUserId, "stars").Document(entry.StarId).SetAsync(new Dictionary<string, object>
            {
                { "timesCaught",   entry.TimesCaught },
                { "firstCaughtAt", entry.FirstCaughtAt.ToString("o") },
                { "lastCaughtAt",  entry.LastCaughtAt.ToString("o") }
            });
        }

        /// <summary>Colección de estrellas guardada (null si falla la lectura).</summary>
        public async Task<List<StarCollectionEntry>> GetStarCollection(string firebaseUserId)
        {
            try
            {
                QuerySnapshot snapshot = await _subcollection(firebaseUserId, "stars").GetSnapshotAsync();
                var entries = new List<StarCollectionEntry>();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();

                    int timesCaught = _int(d, "timesCaught");
                    if (timesCaught <= 0) continue;

                    entries.Add(new StarCollectionEntry
                    {
                        StarId        = doc.Id,
                        TimesCaught   = timesCaught,
                        FirstCaughtAt = _tryParseTimestamp(d, "firstCaughtAt", out var first) ? first : DateTime.Now,
                        LastCaughtAt  = _tryParseTimestamp(d, "lastCaughtAt",  out var last)  ? last  : DateTime.Now
                    });
                }
                return entries;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetStarCollection: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // CENTRO DE NOTIFICACIONES
        // ══════════════════════════════════════════════════════════════

        public async Task SaveNotification(string firebaseUserId, AppNotification notification)
        {
            if (string.IsNullOrEmpty(notification.RemoteId)) return;

            await _subcollection(firebaseUserId, "notifications").Document(notification.RemoteId).SetAsync(new Dictionary<string, object>
            {
                { "createdAt",  notification.CreatedAt.ToString("o") },
                { "type",       (int)notification.Type },
                { "title",      notification.Title ?? string.Empty },
                { "body",       notification.Body  ?? string.Empty },
                { "coins",      notification.Coins },
                { "itemId",     notification.ItemId },
                { "source",     (int)notification.Source },
                { "sourceRef",  notification.SourceRef },
                { "isRead",     notification.IsRead },
                { "isPinned",   notification.IsPinned },
                { "resolvedAt", notification.ResolvedAt?.ToString("o") }
            });
        }

        /// <summary>Todas las notificaciones (null si falla la lectura).</summary>
        public async Task<List<AppNotification>> GetNotifications(string firebaseUserId)
        {
            try
            {
                QuerySnapshot snapshot = await _subcollection(firebaseUserId, "notifications").GetSnapshotAsync();
                var notifications = new List<AppNotification>();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();
                    if (!_tryParseTimestamp(d, "createdAt", out var createdAt)) continue;

                    notifications.Add(new AppNotification
                    {
                        RemoteId   = doc.Id,
                        CreatedAt  = createdAt,
                        Type       = (NotificationType)_int(d, "type"),
                        Title      = _string(d, "title"),
                        Body       = _string(d, "body"),
                        Coins      = _int(d, "coins"),
                        ItemId     = _nullableString(d, "itemId"),
                        Source     = (RewardSource)_int(d, "source"),
                        SourceRef  = _nullableString(d, "sourceRef"),
                        IsRead     = d.TryGetValue("isRead",   out var read)   && read   is bool r && r,
                        IsPinned   = d.TryGetValue("isPinned", out var pinned) && pinned is bool p && p,
                        ResolvedAt = _tryParseTimestamp(d, "resolvedAt", out var resolvedAt) ? resolvedAt : (DateTime?)null
                    });
                }
                return notifications;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetNotifications: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ESCALAS VALIDADAS (WHO-5)
        // ══════════════════════════════════════════════════════════════

        public async Task SaveScaleResponse(string firebaseUserId, ScaleResponse response)
        {
            if (string.IsNullOrEmpty(response.RemoteId)) return;

            await _subcollection(firebaseUserId, "scaleResponses").Document(response.RemoteId).SetAsync(new Dictionary<string, object>
            {
                { "scale",           (int)response.Scale },
                { "availableSince",  response.AvailableSince.ToString("yyyy-MM-dd") },
                { "completedAt",     response.CompletedAt.ToString("o") },
                { "answersJson",     response.AnswersJson ?? string.Empty },
                { "rawScore",        response.RawScore },
                { "score",           response.Score },
                { "durationSeconds", response.DurationSeconds }
            });
        }

        /// <summary>Todos los envíos de escalas (null si falla la lectura).</summary>
        public async Task<List<ScaleResponse>> GetScaleResponses(string firebaseUserId)
        {
            try
            {
                QuerySnapshot snapshot = await _subcollection(firebaseUserId, "scaleResponses").GetSnapshotAsync();
                var responses = new List<ScaleResponse>();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists) continue;
                    var d = doc.ToDictionary();
                    if (!_tryParseTimestamp(d, "completedAt", out var completedAt)) continue;

                    responses.Add(new ScaleResponse
                    {
                        RemoteId        = doc.Id,
                        Scale           = (ScaleType)_int(d, "scale"),
                        AvailableSince  = DateTime.TryParseExact(_string(d, "availableSince"), "yyyy-MM-dd",
                                              CultureInfo.InvariantCulture, DateTimeStyles.None, out var since)
                                          ? since : completedAt.Date,
                        CompletedAt     = completedAt,
                        AnswersJson     = _string(d, "answersJson"),
                        RawScore        = _int(d, "rawScore"),
                        Score           = _int(d, "score"),
                        DurationSeconds = _float(d, "durationSeconds")
                    });
                }
                return responses;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirestoreManager] GetScaleResponses: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // BORRADO DE CUENTA
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Elimina el documento del usuario y todas sus subcolecciones.
        /// Llamar antes de borrar la cuenta de Firebase Auth.
        /// </summary>
        public async Task DeleteUserData(string firebaseUserId)
        {
            try
            {
                foreach (var name in new[] { "diary", "emotions", "minigameSessions", "stars", "notifications", "scaleResponses" })
                {
                    QuerySnapshot snapshot = await _subcollection(firebaseUserId, name).GetSnapshotAsync();
                    foreach (DocumentSnapshot doc in snapshot.Documents)
                        await doc.Reference.DeleteAsync();
                }

                await _userDoc(firebaseUserId).DeleteAsync();
                Debug.Log($"[FirestoreManager] Datos de usuario eliminados de Firestore: {firebaseUserId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreManager] DeleteUserData: {ex.Message}");
                throw;
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        private DocumentReference _userDoc(string firebaseUserId) => _db.Collection("users").Document(firebaseUserId);

        private CollectionReference _subcollection(string firebaseUserId, string name) => _userDoc(firebaseUserId).Collection(name);

        private static string _string(Dictionary<string, object> d, string key)
            => d.TryGetValue(key, out var value) && value != null ? value.ToString() : string.Empty;

        private static int _int(Dictionary<string, object> d, string key)
            => d.TryGetValue(key, out var value) && value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;

        /// <summary>Texto opcional: null si el campo no existe o se guardó como null.</summary>
        private static string _nullableString(Dictionary<string, object> d, string key)
            => d.TryGetValue(key, out var value) && value != null ? value.ToString() : null;

        /// <summary>Entero opcional: null si el campo no existe o se guardó como null.</summary>
        private static int? _nullableInt(Dictionary<string, object> d, string key)
            => d.TryGetValue(key, out var value) && value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : (int?)null;

        private static float _float(Dictionary<string, object> d, string key)
            => d.TryGetValue(key, out var value) && value != null ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : 0f;

        private static List<string> _stringList(Dictionary<string, object> d, string key)
        {
            var result = new List<string>();
            if (d.TryGetValue(key, out var raw) && raw is IEnumerable<object> list)
                foreach (var item in list)
                    if (item != null) result.Add(item.ToString());
            return result;
        }

        /// <summary>Fechas guardadas con ToString("o"); se devuelven en hora local.</summary>
        private static bool _tryParseTimestamp(Dictionary<string, object> d, string key, out DateTime value)
        {
            value = default;
            if (!d.TryGetValue(key, out var raw) || raw == null) return false;
            if (!DateTime.TryParse(raw.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value))
                return false;
            if (value.Kind == DateTimeKind.Utc) value = value.ToLocalTime();
            return true;
        }
    }
}
