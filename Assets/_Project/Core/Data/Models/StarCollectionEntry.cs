using System;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Estrella descubierta por el usuario en StarFisher (una fila por estrella).
    /// Las estrellas siempre se liberan: solo se guarda el registro en la colección.
    /// Se sincroniza con Firestore en users/{uid}/stars/{StarId}.
    /// </summary>
    [Table("StarCollection")]
    public class StarCollectionEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>StarDefinition.starId.</summary>
        [Unique]
        public string StarId { get; set; }

        public int TimesCaught { get; set; }

        public DateTime FirstCaughtAt { get; set; }

        public DateTime LastCaughtAt { get; set; }
    }
}
