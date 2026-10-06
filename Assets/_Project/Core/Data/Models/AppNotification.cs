using System;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Notificación del centro de notificaciones (bandeja dentro de la app, ver
    /// docs/NOTIFICATION_CENTER.md). No confundir con las push del sistema (NotificationManager).
    /// Se guardan para siempre; las ancladas dejan de mostrarse al resolverse (ResolvedAt).
    /// </summary>
    [Table("Notifications")]
    public class AppNotification
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Id en Firestore notifications/{RemoteId}. GUID, o determinista para las que no deben
        /// duplicarse entre dispositivos ("who5-yyyy-MM-dd", "weekly-yyyy-MM-dd").
        /// </summary>
        [Indexed]
        public string RemoteId { get; set; }

        /// <summary>Momento de creación (hora local del dispositivo).</summary>
        [Indexed]
        public DateTime CreatedAt { get; set; }

        public NotificationType Type { get; set; }

        public string Title { get; set; }

        public string Body { get; set; }

        /// <summary>Monedas de la recompensa; 0 si no aplica.</summary>
        public int Coins { get; set; }

        /// <summary>Ítem desbloqueado; null si no aplica.</summary>
        public string ItemId { get; set; }

        /// <summary>Origen de la recompensa; None si la notificación no es una recompensa.</summary>
        public RewardSource Source { get; set; }

        /// <summary>Detalle del origen (MinigameType, rewardId…); puede ser null.</summary>
        public string SourceRef { get; set; }

        public bool IsRead { get; set; }

        /// <summary>true = sección "Importante", arriba del todo, hasta que se resuelva.</summary>
        public bool IsPinned { get; set; }

        /// <summary>Cuándo se completó la acción de una anclada (p. ej. enviar el WHO-5). null = pendiente.</summary>
        public DateTime? ResolvedAt { get; set; }

        public AppNotification() { }
    }
}
