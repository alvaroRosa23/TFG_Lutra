using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Events;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Comprueba si la racha del usuario está en peligro cada vez que se actualiza
    /// y programa la notificación de aviso correspondiente.
    ///
    /// Cancela el aviso automáticamente cuando el usuario registra una emoción.
    /// </summary>
    public class StreakWarningChecker : MonoBehaviour
    {
        private StreakManager       _streakManager;
        private NotificationManager _notificationManager;

        private StreakManager       StreakManagerService       => _streakManager       ??= ServiceLocator.Get<StreakManager>();
        private NotificationManager NotificationManagerService => _notificationManager ??= ServiceLocator.Get<NotificationManager>();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.OnEmotionRegistered += _onEmotionRegistered;
            EventBus.OnStreakUpdated     += _onStreakUpdated;
        }

        private void OnDisable()
        {
            EventBus.OnEmotionRegistered -= _onEmotionRegistered;
            EventBus.OnStreakUpdated     -= _onStreakUpdated;
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _onStreakUpdated(int streak)
        {
            _ = _checkWarningWithStreak(streak);
        }

        private async Task _checkWarningWithStreak(int streak)
        {
            try
            {
                bool checkedInToday = await StreakManagerService.HasCheckedInToday();
                if (streak >= 3 && !checkedInToday)
                {
                    NotificationManagerService.ScheduleStreakWarning(streak);
                    Debug.Log($"[StreakWarningChecker] Racha de {streak} días en peligro → aviso programado.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreakWarningChecker] _checkWarningWithStreak: {ex.Message}");
            }
        }

        /// <summary>
        /// El usuario ha completado el check-in: cancela el aviso de racha.
        /// </summary>
        private void _onEmotionRegistered(EmotionRecord _)
        {
            NotificationManagerService.CancelStreakWarning();
        }
    }
}
