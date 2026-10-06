using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.Features.Charts;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Centro de notificaciones: la bandeja dentro de la app (docs/NOTIFICATION_CENTER.md).
    /// No confundir con NotificationManager, que programa las push del sistema operativo.
    ///
    /// - Convierte cada recompensa (EventBus.OnRewardGranted) en una notificación.
    /// - Gestiona las ancladas (WHO-5): siguen en "Importante" hasta que se resuelven.
    /// - Publica el resumen semanal de los lunes.
    /// - Emite EventBus.OnNotificationsChanged para la "!" del menú principal.
    ///
    /// GameManager lo crea por código si no está en la escena.
    /// </summary>
    public class NotificationCenter : BaseService
    {
        public const int PageSize = 30;

        private DataRepository _repo;
        private DataRepository Repo => _repo ??= ServiceLocator.Get<DataRepository>();

        // Comprobación del resumen semanal en curso: evita crearlo dos veces si se pide a la vez
        private Task _weeklyCheck;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.OnRewardGranted += _onRewardGranted;
        }

        private void OnDisable()
        {
            EventBus.OnRewardGranted -= _onRewardGranted;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>true si hay una anclada pendiente o alguna notificación sin leer.</summary>
        public Task<bool> NeedsAttention() => Repo.HasNotificationsNeedingAttention();

        /// <summary>Ancladas pendientes (sección "Importante").</summary>
        public Task<List<AppNotification>> GetPinned() => Repo.GetPinnedNotifications();

        /// <summary>Página de notificaciones no ancladas, más recientes primero.</summary>
        public Task<List<AppNotification>> GetPage(int offset, int count = PageSize)
            => Repo.GetNotificationsPage(offset, count);

        /// <summary>Marca como leídas las no ancladas (al abrir el centro).</summary>
        public async Task MarkAllRead()
        {
            await Repo.MarkAllNotificationsRead();
            await _emitAttention();
        }

        /// <summary>
        /// Crea una notificación anclada con id determinista. Si ya existe (también si se creó en
        /// otro dispositivo y ya se sincronizó) no hace nada y la devuelve.
        /// </summary>
        public async Task<AppNotification> CreatePinned(string remoteId, NotificationType type, string title, string body)
        {
            var existing = await Repo.GetNotificationByRemoteId(remoteId);
            if (existing != null) return existing;

            var notification = new AppNotification
            {
                RemoteId  = remoteId,
                CreatedAt = DateTime.Now,
                Type      = type,
                Title     = title,
                Body      = body,
                Source    = RewardSource.None,
                IsPinned  = true
            };
            await Repo.SaveNotification(notification);
            await _emitAttention();
            return notification;
        }

        /// <summary>Resuelve una anclada (p. ej. WHO-5 enviado): deja de mostrarse y quita la "!".</summary>
        public async Task ResolvePinned(string remoteId)
        {
            var notification = await Repo.GetNotificationByRemoteId(remoteId);
            if (notification == null || notification.ResolvedAt.HasValue) return;

            notification.ResolvedAt = DateTime.Now;
            notification.IsPinned   = false;
            notification.IsRead     = true;
            await Repo.UpdateNotification(notification);
            await _emitAttention();
        }

        /// <summary>
        /// Notificación del protocolo de apoyo (no anclada). Sirve también de registro de cada
        /// activación para el informe profesional: <paramref name="trigger"/> queda en SourceRef.
        /// </summary>
        public async Task AddSupport(string trigger)
        {
            await Repo.SaveNotification(new AppNotification
            {
                CreatedAt = DateTime.Now,
                Type      = NotificationType.Support,
                Title     = "Recursos de apoyo",
                Body      = "Si lo necesitas, el 024 te atiende gratis y a cualquier hora. En emergencias, llama al 112.",
                Source    = RewardSource.None,
                SourceRef = trigger
            });
            await _emitAttention();
        }

        /// <summary>
        /// Publica el resumen de la semana anterior si aún no existe y hay datos suficientes.
        /// Se puede llamar todas las veces que haga falta (al enfocar el menú principal).
        /// </summary>
        public Task CheckWeeklySummary()
        {
            if (_weeklyCheck != null && !_weeklyCheck.IsCompleted) return _weeklyCheck;
            _weeklyCheck = _safeCheckWeeklySummary();
            return _weeklyCheck;
        }

        /// <summary>Recalcula y emite el estado de la "!" (p. ej. tras sincronizar).</summary>
        public void RefreshAttention() => _ = _safeEmitAttention();

        // ── Handlers ───────────────────────────────────────────────────

        private void _onRewardGranted(RewardGrant grant) => _ = _safeAddReward(grant);

        private async Task _safeAddReward(RewardGrant grant)
        {
            try
            {
                if (grant == null) return;

                await Repo.SaveNotification(new AppNotification
                {
                    CreatedAt = DateTime.Now,
                    Type      = NotificationType.Reward,
                    Title     = grant.Title,
                    Body      = grant.Body,
                    Coins     = grant.Coins,
                    ItemId    = grant.ItemId,
                    Source    = grant.Source,
                    SourceRef = grant.SourceRef
                });
                await _emitAttention();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NotificationCenter] _safeAddReward: {ex.Message}");
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        private async Task _safeCheckWeeklySummary()
        {
            try
            {
                DateTime monday  = WeeklySummaryBuilder.GetPreviousWeekMonday(DateTime.Today);
                string remoteId  = $"weekly-{monday:yyyy-MM-dd}";
                if (await Repo.GetNotificationByRemoteId(remoteId) != null) return;

                DateTime weekEnd = monday.AddDays(7).AddTicks(-1);
                var week         = await Repo.GetUserEmotionsForPeriod(monday, weekEnd);
                var previousWeek = await Repo.GetUserEmotionsForPeriod(monday.AddDays(-7), monday.AddTicks(-1));
                var sessions     = await Repo.GetSessionsForPeriod(monday, weekEnd);

                string body = WeeklySummaryBuilder.BuildBody(monday, week, previousWeek, sessions);
                if (body == null) return;

                await Repo.SaveNotification(new AppNotification
                {
                    RemoteId  = remoteId,
                    CreatedAt = DateTime.Now,
                    Type      = NotificationType.WeeklySummary,
                    Title     = WeeklySummaryBuilder.Title,
                    Body      = body,
                    Source    = RewardSource.None
                });
                await _emitAttention();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NotificationCenter] _safeCheckWeeklySummary: {ex.Message}");
            }
        }

        private async Task _safeEmitAttention()
        {
            try { await _emitAttention(); }
            catch (Exception ex) { Debug.LogError($"[NotificationCenter] _safeEmitAttention: {ex.Message}"); }
        }

        private async Task _emitAttention()
        {
            EventBus.EmitNotificationsChanged(await NeedsAttention());
        }
    }
}
