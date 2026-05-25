using System;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Representa un ítem desbloqueado por el usuario (decoración, avatar, etc.).
    /// Tabla normalizada en lugar de serializar JSON en UserProfile.
    /// </summary>
    [Table("UnlockedItems")]
    public class UnlockedItem
    {
        /// <summary>Clave primaria autoincremental.</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Foreign key al UserProfile que posee el ítem.</summary>
        [Indexed]
        public int UserId { get; set; }

        /// <summary>Identificador del ítem (coincide con SafeZoneItem.itemId / RewardDefinition.itemId).</summary>
        public string ItemId { get; set; }

        /// <summary>Momento en que se desbloqueó el ítem (UTC).</summary>
        public DateTime UnlockedAt { get; set; }

        // ── Constructor sin parámetros requerido por sqlite-net-pcl ──
        public UnlockedItem() { }

        public UnlockedItem(int userId, string itemId)
        {
            UserId     = userId;
            ItemId     = itemId;
            UnlockedAt = DateTime.Now;
        }
    }
}
