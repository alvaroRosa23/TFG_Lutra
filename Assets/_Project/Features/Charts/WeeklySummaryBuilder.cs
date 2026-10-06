using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Texto del resumen semanal que se publica en el centro de notificaciones
    /// (docs/NOTIFICATION_CENTER.md §9). Clase estática pura: no depende de Unity.
    /// Solo usa registros del usuario (sin placeholders) y la serie diaria de check-ins de Día.
    /// </summary>
    public static class WeeklySummaryBuilder
    {
        /// <summary>Check-ins de Día mínimos en la semana para publicar el resumen.</summary>
        public const int MinDayCheckIns = 3;

        /// <summary>Diferencia de ánimo medio a partir de la que se considera "mejor" o "más bajo".</summary>
        private const float TrendThreshold = 0.3f;

        public const string Title = "Tu resumen semanal";

        private static readonly CultureInfo _es = new CultureInfo("es-ES");

        /// <summary>Lunes de la semana anterior a la que contiene <paramref name="today"/>.</summary>
        public static DateTime GetPreviousWeekMonday(DateTime today)
        {
            int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;   // lunes = 0 … domingo = 6
            return today.Date.AddDays(-daysFromMonday - 7);
        }

        /// <summary>
        /// Construye el texto del resumen. Devuelve null si la semana no tiene suficientes check-ins de Día.
        /// </summary>
        /// <param name="monday">Lunes de la semana resumida.</param>
        /// <param name="week">Registros del usuario de esa semana.</param>
        /// <param name="previousWeek">Registros del usuario de la semana anterior (para la tendencia).</param>
        /// <param name="sessions">Partidas de minijuegos de esa semana.</param>
        public static string BuildBody(DateTime monday,
                                       List<EmotionRecord>   week,
                                       List<EmotionRecord>   previousWeek,
                                       List<MinigameSession> sessions)
        {
            var dayMoods = _dayMoods(week);
            if (dayMoods.Count < MinDayCheckIns) return null;

            float mean = (float)dayMoods.Average();
            var parts  = new List<string>
            {
                $"Semana del {_formatDay(monday)} al {_formatDay(monday.AddDays(6))}: " +
                $"tu ánimo medio fue {mean.ToString("0.0", _es)} de 5{_trendText(mean, _dayMoods(previousWeek))}."
            };

            var mostFrequent = week
                .GroupBy(r => r.EmotionType)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            if (mostFrequent != null)
                parts.Add($"La emoción que más registraste fue {mostFrequent.Key.ToDisplayName().ToLower(_es)}.");

            string minigameText = _minigameText(sessions);
            if (minigameText != null) parts.Add(minigameText);

            return string.Join(" ", parts);
        }

        // ── Helpers privados ───────────────────────────────────────────

        /// <summary>Ánimo de los check-ins de Día (uno por día como máximo).</summary>
        private static List<int> _dayMoods(List<EmotionRecord> records)
        {
            if (records == null) return new List<int>();
            return records
                .Where(r => r.IsMorningCheck && r.Source == RecordSource.User && r.MoodLevel > 0)
                .Select(r => r.MoodLevel)
                .ToList();
        }

        private static string _trendText(float mean, List<int> previousMoods)
        {
            if (previousMoods.Count < MinDayCheckIns) return string.Empty;

            float diff = mean - (float)previousMoods.Average();
            if (diff >= TrendThreshold)  return ", mejor que la semana anterior";
            if (diff <= -TrendThreshold) return ", algo más bajo que la semana anterior";
            return ", parecido a la semana anterior";
        }

        /// <summary>
        /// El minijuego con mayor mejora media de ánimo (solo partidas válidas); si ninguno mejora,
        /// el más jugado. null si no hubo partidas.
        /// </summary>
        private static string _minigameText(List<MinigameSession> sessions)
        {
            if (sessions == null || sessions.Count == 0) return null;

            var best = sessions
                .Where(s => s.IsValidForMoodEffect())
                .GroupBy(s => s.MinigameId)
                .Select(g => new { Type = g.Key, Delta = g.Average(s => s.MoodDelta().Value) })
                .OrderByDescending(x => x.Delta)
                .FirstOrDefault();

            if (best != null && best.Delta > 0)
                return $"Lo que más te ayudó: {best.Type.ToDisplayName()}.";

            var mostPlayed = sessions
                .GroupBy(s => s.MinigameId)
                .OrderByDescending(g => g.Count())
                .First();
            return $"Tu minijuego más jugado: {mostPlayed.Key.ToDisplayName()}.";
        }

        private static string _formatDay(DateTime date) => date.ToString("d MMM", _es).TrimEnd('.');
    }
}
