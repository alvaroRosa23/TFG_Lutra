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

        /// <summary>Momento de inicio de la sesión (UTC).</summary>
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
}
