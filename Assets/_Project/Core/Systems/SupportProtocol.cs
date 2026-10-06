using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.UI.Components;

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Reglas puras del protocolo de apoyo (sin Unity, con tests). Ver docs/PROFESSIONAL_REPORT.md §6.3.
    /// </summary>
    public static class SupportRules
    {
        /// <summary>Ánimo (1-5) a partir del cual un día cuenta como bajo.</summary>
        public const int LowMoodThreshold = 2;

        /// <summary>Días naturales consecutivos con ánimo bajo que activan el aviso.</summary>
        public const int LowMoodDays = 3;

        /// <summary>Días mínimos entre dos activaciones (evitar fatiga de avisos).</summary>
        public const int CooldownDays = 7;

        /// <summary>Índice WHO-5 a partir del cual se activa el aviso (probable depresión, Topp et al., 2015).</summary>
        public const int Who5Threshold = 28;

        /// <summary>
        /// true si los últimos <see cref="LowMoodDays"/> días naturales hasta <paramref name="today"/>
        /// (incluido) tienen check-in de Día del usuario con ánimo ≤ <see cref="LowMoodThreshold"/>.
        /// </summary>
        public static bool HasLowMoodStreak(IEnumerable<EmotionRecord> records, DateTime today)
        {
            if (records == null) return false;

            var moodByDay = records
                .Where(r => r.IsMorningCheck && r.Source == RecordSource.User && r.MoodLevel > 0)
                .GroupBy(r => r.Timestamp.Date)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Timestamp).First().MoodLevel);

            for (int i = 0; i < LowMoodDays; i++)
            {
                if (!moodByDay.TryGetValue(today.Date.AddDays(-i), out int mood) || mood > LowMoodThreshold)
                    return false;
            }
            return true;
        }

        public static bool IsWho5Low(int score) => score <= Who5Threshold;

        /// <summary>true si la última activación fue hace menos de <see cref="CooldownDays"/> días.</summary>
        public static bool IsInCooldown(DateTime? lastActivation, DateTime now)
            => lastActivation.HasValue && (now - lastActivation.Value).TotalDays < CooldownDays;
    }

    /// <summary>
    /// Protocolo de apoyo: tras un check-in de Día o un WHO-5, si se cumple un disparador y no está
    /// en periodo de espera, muestra SupportDialog (024 / 112) y guarda una notificación de tipo
    /// Support, que además sirve de registro para el informe profesional. No diagnostica: solo deriva.
    /// </summary>
    public static class SupportProtocol
    {
        public const string TriggerLowMood = "animo_bajo_3_dias";
        public const string TriggerWho5    = "who5_bajo";

        /// <summary>Tras guardar un check-in de Día.</summary>
        public static async Task CheckLowMoodStreak()
        {
            try
            {
                var repo    = ServiceLocator.Get<DataRepository>();
                var today   = DateTime.Today;
                var records = await repo.GetUserEmotionsForPeriod(
                    today.AddDays(-(SupportRules.LowMoodDays - 1)), today.AddDays(1).AddTicks(-1));

                if (SupportRules.HasLowMoodStreak(records, today))
                    await _activate(TriggerLowMood);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupportProtocol] CheckLowMoodStreak: {ex.Message}");
            }
        }

        /// <summary>Tras enviar un WHO-5.</summary>
        public static async Task CheckWho5Score(int score)
        {
            try
            {
                if (SupportRules.IsWho5Low(score))
                    await _activate(TriggerWho5);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupportProtocol] CheckWho5Score: {ex.Message}");
            }
        }

        private static async Task _activate(string trigger)
        {
            var repo = ServiceLocator.Get<DataRepository>();
            var last = await repo.GetLastNotificationOfType(NotificationType.Support);
            if (SupportRules.IsInCooldown(last?.CreatedAt, DateTime.Now))
            {
                Debug.Log($"[SupportProtocol] Disparador {trigger} en periodo de espera: no se muestra");
                return;
            }

            await ServiceLocator.Get<NotificationCenter>().AddSupport(trigger);
            SupportDialog.ShowAlert();
            Debug.Log($"[SupportProtocol] Protocolo de apoyo activado ({trigger})");
        }
    }
}
