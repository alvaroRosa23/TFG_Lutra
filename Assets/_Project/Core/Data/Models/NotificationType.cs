namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Tipo de notificación del centro de notificaciones (bandeja dentro de la app).
    /// Se persiste como int en AppNotification.Type: no reordenar.
    /// </summary>
    public enum NotificationType
    {
        Reward        = 0,   // monedas o ítems obtenidos
        Who5Available = 1,   // cuestionario WHO-5 disponible (anclada hasta enviarlo)
        Support       = 2,   // recursos de apoyo del protocolo de crisis
        WeeklySummary = 3    // resumen de la semana anterior (lunes)
    }
}
