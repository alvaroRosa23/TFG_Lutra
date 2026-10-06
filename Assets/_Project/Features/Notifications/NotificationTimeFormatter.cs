using System;
using System.Globalization;

namespace Lutra.Features.Notifications
{
    /// <summary>
    /// Textos de fecha del centro de notificaciones: cabeceras de grupo y hora de cada tarjeta.
    /// Clase estática pura (sin Unity) para poder probarla.
    /// </summary>
    public static class NotificationTimeFormatter
    {
        private static readonly CultureInfo _es = new CultureInfo("es-ES");

        /// <summary>"Hoy", "Ayer", "Esta semana" (últimos 7 días) o la fecha ("3 de octubre").</summary>
        public static string GroupLabel(DateTime createdAt, DateTime today)
        {
            int daysAgo = (today.Date - createdAt.Date).Days;

            if (daysAgo <= 0) return "Hoy";
            if (daysAgo == 1) return "Ayer";
            if (daysAgo < 7)  return "Esta semana";

            string format = createdAt.Year == today.Year ? "d 'de' MMMM" : "d 'de' MMMM 'de' yyyy";
            return createdAt.ToString(format, _es);
        }

        /// <summary>Hora de la tarjeta; en "Esta semana" con el día delante ("lunes, 18:42").</summary>
        public static string TimeText(DateTime createdAt, DateTime today)
        {
            int daysAgo = (today.Date - createdAt.Date).Days;
            return daysAgo >= 2 && daysAgo < 7
                ? createdAt.ToString("dddd, HH:mm", _es)
                : createdAt.ToString("HH:mm", _es);
        }
    }
}
