using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Registro de una sesión de minijuego completada por el usuario.
    /// Persiste en SQLite a través de DataRepository.
    /// </summary>
    [Table("MinigameSessions")]
    public class MinigameSession
    {
        /// <summary>Clave primaria autoincremental.</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Momento de inicio de la sesión (hora local del dispositivo).</summary>
        public DateTime StartTime { get; set; }

        /// <summary>Duración total de la sesión en segundos.</summary>
        public float DurationSeconds { get; set; }

        /// <summary>Identificador del minijuego jugado.</summary>
        public MinigameType MinigameId { get; set; }

        /// <summary>Emoción reportada antes de iniciar el minijuego.</summary>
        public EmotionType EmotionBefore { get; set; }

        /// <summary>Emoción reportada al terminar el minijuego.</summary>
        public EmotionType EmotionAfter { get; set; }

        /// <summary>
        /// Puntuación de relajación calculada al finalizar (0.0 - 1.0).
        /// Cada minijuego define su propia heurística.
        /// </summary>
        public float RelaxationScore { get; set; }

        /// <summary>JSON serializado de las métricas propias del minijuego (p.ej. "max_layers").</summary>
        [Column("MetricsJson")]
        public string MetricsJson { get; set; }

        /// <summary>
        /// Ánimo (1-5) antes de jugar. No se pregunta: se toma del dato de ánimo más reciente del
        /// usuario (último check-in o valoración post-partida). null = no había ningún dato.
        /// </summary>
        public int? MoodBefore { get; set; }

        /// <summary>
        /// Momento en que se registró MoodBefore. Para medir el efecto del minijuego solo cuentan
        /// las partidas con MoodBefore de pocas horas antes (ver docs/METRICS.md §4.6).
        /// </summary>
        public DateTime? MoodBeforeRecordedAt { get; set; }

        /// <summary>Ánimo (1-5) elegido con las caritas de PostMinigameScreen. null = no respondió.</summary>
        public int? MoodAfter { get; set; }

        /// <summary>
        /// Identificador estable del registro en Firestore (GUID, o la fecha en entradas antiguas
        /// del diario). Se asigna al guardar por primera vez; null = aún no sincronizado.
        /// </summary>
        [Indexed]
        public string RemoteId { get; set; }

        // ── Propiedad de conveniencia (ignorada por SQLite) ────────────

        /// <summary>
        /// Métricas específicas del minijuego. Se serializa/deserializa automáticamente
        /// desde/hacia <see cref="MetricsJson"/>.
        /// </summary>
        [Ignore]
        public Dictionary<string, float> Metrics
        {
            get => string.IsNullOrEmpty(MetricsJson)
                ? new Dictionary<string, float>()
                : JsonConvert.DeserializeObject<Dictionary<string, float>>(MetricsJson);
            set => MetricsJson = JsonConvert.SerializeObject(value);
        }

        // ── Constructor sin parámetros requerido por sqlite-net-pcl ──
        public MinigameSession() { }

        public MinigameSession(MinigameType minigame, EmotionType before)
        {
            StartTime    = DateTime.Now;
            MinigameId   = minigame;
            EmotionBefore = before;
        }
    }

    public static class MinigameSessionExtensions
    {
        /// <summary>Antigüedad máxima del ánimo previo para que la partida cuente al medir efecto.</summary>
        public const double MaxMoodBeforeAgeHours = 3.0;

        /// <summary>
        /// true si la partida sirve para medir el efecto en el ánimo: hay ánimo después y el ánimo
        /// previo es de como mucho 3 h antes de empezar (ver docs/METRICS.md §4.6).
        /// </summary>
        public static bool IsValidForMoodEffect(this MinigameSession session)
        {
            if (session?.MoodAfter == null || session.MoodBefore == null || session.MoodBeforeRecordedAt == null)
                return false;

            double ageHours = (session.StartTime - session.MoodBeforeRecordedAt.Value).TotalHours;
            return ageHours >= 0 && ageHours <= MaxMoodBeforeAgeHours;
        }

        /// <summary>MoodAfter − MoodBefore; null si la partida no es válida para medir efecto.</summary>
        public static int? MoodDelta(this MinigameSession session)
            => session.IsValidForMoodEffect() ? session.MoodAfter.Value - session.MoodBefore.Value : (int?)null;
    }
}
