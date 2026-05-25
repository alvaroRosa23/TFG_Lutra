using System;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Registro de una emoción reportada por el usuario.
    /// Persiste en SQLite a través de DataRepository.
    /// </summary>
    [Table("EmotionRecords")]
    public class EmotionRecord
    {
        /// <summary>Clave primaria autoincremental.</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Momento en que se registró la emoción (hora local del dispositivo).</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Emoción seleccionada por el usuario.</summary>
        public EmotionType EmotionType { get; set; }

        /// <summary>Intensidad percibida de 1 (muy baja) a 5 (muy alta).</summary>
        public int IntensityLevel { get; set; }

        /// <summary>Indica si el registro corresponde al check-in matutino.</summary>
        public bool IsMorningCheck { get; set; }

        /// <summary>Nota libre opcional escrita por el usuario.</summary>
        public string Notes { get; set; }

        /// <summary>
        /// Origen del registro. User = introducido por el usuario; valores Restored* = placeholder
        /// creado al sincronizar desde Firestore. La columna se añade automáticamente en BD existentes
        /// y su valor por defecto (0 = User) es correcto para todos los registros previos.
        /// </summary>
        public RecordSource Source { get; set; } = RecordSource.User;

        /// <summary>Nivel de ánimo general de 1 (muy mal) a 5 (muy bien).</summary>
        public int MoodLevel { get; set; }

        /// <summary>Tags de emoción seleccionados; JSON array de strings.</summary>
        public string SelectedEmotionTags { get; set; }

        /// <summary>Tags de motivo seleccionados; JSON array de strings.</summary>
        public string SelectedMotiveTags { get; set; }

        /// <summary>Ruta a la foto adjunta, o null si no hay foto.</summary>
        public string PhotoPath { get; set; }

        /// <summary>Título de la canción asociada (para uso futuro).</summary>
        public string SongTitle { get; set; }

        /// <summary>Artista de la canción asociada (para uso futuro).</summary>
        public string SongArtist { get; set; }

        // ── Constructor sin parámetros requerido por sqlite-net-pcl ──
        public EmotionRecord() { }

        public EmotionRecord(EmotionType emotion, int intensity, bool isMorning, string notes = "")
        {
            Timestamp     = DateTime.Now;
            EmotionType   = emotion;
            IntensityLevel = Math.Clamp(intensity, 1, 5);
            IsMorningCheck = isMorning;
            Notes          = notes ?? string.Empty;
        }
    }
}
