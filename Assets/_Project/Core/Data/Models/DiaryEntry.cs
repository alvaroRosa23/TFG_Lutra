using System;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Entrada del diario personal del usuario.
    /// Puede incluir texto, ruta de imagen y ruta de nota de voz.
    /// Persiste en SQLite a través de DataRepository.
    /// </summary>
    [Table("DiaryEntries")]
    public class DiaryEntry
    {
        /// <summary>Clave primaria autoincremental.</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Fecha de la entrada (solo fecha, sin hora). Indexada para búsquedas por mes.</summary>
        [Indexed]
        public DateTime Date { get; set; }

        /// <summary>Título opcional de la entrada.</summary>
        public string Title { get; set; }

        /// <summary>Contenido textual de la entrada.</summary>
        public string Content { get; set; }

        /// <summary>Emoción predominante del día (nombre del enum EmotionType como string).</summary>
        public string Mood { get; set; }

        /// <summary>Ruta relativa al archivo de nota de voz. Null si no hay audio.</summary>
        public string AudioPath { get; set; }

        /// <summary>Ruta relativa a la imagen adjunta. Null si no hay imagen.</summary>
        public string ImagePath { get; set; }

        // ── Constructor sin parámetros requerido por sqlite-net-pcl ──
        public DiaryEntry() { }

        public DiaryEntry(DateTime date, string content, string mood)
        {
            Date    = date.Date;   // Normalizar a medianoche
            Content = content ?? string.Empty;
            Mood    = mood ?? string.Empty;
        }
    }
}
