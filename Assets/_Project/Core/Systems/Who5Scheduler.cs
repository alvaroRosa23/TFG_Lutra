using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Features.Who5;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Decide cuándo está disponible el WHO-5 y crea su notificación anclada en el centro de
    /// notificaciones (docs/PROFESSIONAL_REPORT.md §3.2). No se muestra tras el check-in: solo
    /// aparece en el centro, con la "!" del menú principal, hasta que se envía completo.
    ///
    /// Primera vez: disponible desde el alta del perfil (línea base). Después: 14 días tras el
    /// último envío. La comprueba MainMenuScreen al recibir el foco.
    /// </summary>
    public static class Who5Scheduler
    {
        private static Task _checking;

        /// <summary>Crea la notificación anclada si toca y aún no existe. Idempotente.</summary>
        public static Task CheckAvailability()
        {
            if (_checking != null && !_checking.IsCompleted) return _checking;
            _checking = _safeCheck();
            return _checking;
        }

        /// <summary>Tras un envío: programa el push del sistema para el día en que vuelva a tocar.</summary>
        public static void ScheduleNextReminder(DateTime completedAt)
        {
            if (ServiceLocator.TryGet<NotificationManager>(out var notifications))
                notifications.ScheduleWho5Reminder(Who5Questionnaire.NextAvailableDate(completedAt));
        }

        // ── Métodos privados ───────────────────────────────────────────

        private static async Task _safeCheck()
        {
            try
            {
                var repo    = ServiceLocator.Get<DataRepository>();
                var profile = await repo.GetUserProfile();
                if (profile == null) return;

                var last = await repo.GetLastScaleResponse(ScaleType.Who5);
                DateTime? availableSince = Who5Questionnaire.GetAvailableSince(
                    profile.CreationDate, last?.CompletedAt, DateTime.Today);
                if (availableSince == null) return;

                // CreatePinned no duplica: si ya existe (pendiente o resuelta) la devuelve tal cual
                await ServiceLocator.Get<NotificationCenter>().CreatePinned(
                    Who5Questionnaire.NotificationRemoteId(availableSince.Value),
                    NotificationType.Who5Available,
                    Who5Questionnaire.NotificationTitle,
                    Who5Questionnaire.NotificationBody);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Who5Scheduler] CheckAvailability: {ex.Message}");
            }
        }
    }
}
