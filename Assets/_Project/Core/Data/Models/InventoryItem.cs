using System;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Ítem desbloqueado (comprado o ganado) por el usuario.
    /// Sustituye a UnlockedItem añadiendo estado de colocación en la habitación.
    /// </summary>
    [Table("InventoryItems")]
    public class InventoryItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        public string ItemId { get; set; }

        public DateTime UnlockedAt { get; set; }

        public bool IsPlaced { get; set; }

        /// <summary>Índice en el array PlacementPoint[] de SafeZoneView. -1 = no colocado.</summary>
        public int PlacementIndex { get; set; } = -1;

        public InventoryItem() { }

        public InventoryItem(int userId, string itemId)
        {
            UserId         = userId;
            ItemId         = itemId;
            UnlockedAt     = DateTime.Now;
            IsPlaced       = false;
            PlacementIndex = -1;
        }
    }
}
