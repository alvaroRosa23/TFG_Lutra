using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Charts
{
    /// <summary>Rangos y textos de los periodos de la pantalla de Estadísticas.</summary>
    public static class ChartPeriodExtensions
    {
        /// <summary>
        /// Semana = últimos 7 días; Mes = últimos 30 días (ventanas móviles: así el periodo anterior
        /// tiene la misma duración y la comparación es justa); Todo = desde el alta.
        /// </summary>
        public static (DateTime from, DateTime to) GetRange(this ChartPeriod period, DateTime now)
        {
            switch (period)
            {
                case ChartPeriod.Month:   return (now.Date.AddDays(-29), now);
                case ChartPeriod.AllTime: return (DateTime.MinValue, now);
                default:                  return (now.Date.AddDays(-6), now);
            }
        }

        /// <summary>"esta semana", "estos 30 días", "desde que empezaste".</summary>
        public static string CurrentText(this ChartPeriod period)
        {
            switch (period)
            {
                case ChartPeriod.Month:   return "estos 30 días";
                case ChartPeriod.AllTime: return "desde que empezaste";
                default:                  return "esta semana";
            }
        }

        /// <summary>"la semana anterior", "los 30 días anteriores"; null en Todo (no hay periodo anterior).</summary>
        public static string PreviousText(this ChartPeriod period)
        {
            switch (period)
            {
                case ChartPeriod.Month:   return "los 30 días anteriores";
                case ChartPeriod.AllTime: return null;
                default:                  return "la semana anterior";
            }
        }
    }

    /// <summary>
    /// Frases de la pantalla de Estadísticas a partir de ReportData (docs/PROFESSIONAL_REPORT.md §2.2).
    /// Lenguaje sencillo y en positivo, sin términos técnicos ni etiquetas clínicas: los valores
    /// técnicos solo van en el informe profesional. null = ocultar el bloque.
    /// Clase estática pura, con tests.
    /// </summary>
    public static class StatsTextBuilder
    {
        /// <summary>Diferencia de ánimo medio a partir de la que se habla de "mejor" o "más bajo".</summary>
        public const float TrendThreshold = 0.3f;

        /// <summary>Cambio relativo de la variabilidad día a día para hablar de "más estable".</summary>
        public const float StabilityThreshold = 0.10f;

        private static readonly CultureInfo _es = new CultureInfo("es-ES");

        // ── Resumen ────────────────────────────────────────────────────

        /// <summary>+1 mejor, −1 más bajo, 0 parecido, null sin comparación posible.</summary>
        public static int? TrendDirection(ReportData data)
        {
            float? change = data?.Mood.ChangeVsPrevious;
            if (!change.HasValue) return null;
            if (change.Value >= TrendThreshold)  return 1;
            if (change.Value <= -TrendThreshold) return -1;
            return 0;
        }

        public static string Summary(ReportData data, ChartPeriod period)
        {
            var mood = data.Mood;
            if (!mood.Mean.HasValue)
            {
                int missing = ReportCalculator.MinDaysForMean - mood.N;
                return missing == 1
                    ? "Te falta 1 check-in del día para ver tu resumen."
                    : $"Te faltan {missing} check-ins del día para ver tu resumen.";
            }

            string text = $"Tu ánimo medio {period.CurrentText()} ha sido {_n1(mood.Mean.Value)} de 5";

            string previous = period.PreviousText();
            switch (previous != null ? TrendDirection(data) : null)
            {
                case 1:  text += $", mejor que {previous}"; break;
                case -1: text += $", algo más bajo que {previous}"; break;
                case 0:  text += $", parecido a {previous}"; break;
            }
            return text + ".";
        }

        /// <summary>Carita del resumen: 1-5 (redondeo de la media) o null sin datos suficientes.</summary>
        public static int? SummaryMoodLevel(ReportData data)
            => data.Mood.Mean.HasValue ? Math.Max(1, Math.Min(5, (int)Math.Round(data.Mood.Mean.Value))) : (int?)null;

        public static string MoodChartEmpty(ReportData data)
            => data.Mood.N == 0 ? "Aún no hay check-ins del día en este periodo." : null;

        // ── Emociones ──────────────────────────────────────────────────

        public static string EmotionsSummary(ReportData data)
        {
            var e = data.Emotions;
            if (e.Total == 0) return "Cuando registres cómo te sientes, aquí verás tus emociones.";

            string distinct = e.DistinctEmotions == 1 ? "1 emoción" : $"{e.DistinctEmotions} emociones distintas";
            string text = $"Has registrado {distinct}";
            if (e.MostFrequent.HasValue && e.DistinctEmotions > 1)
                text += $"; la más frecuente, {e.MostFrequent.Value.ToDisplayName().ToLower(_es)}";
            return text + ".";
        }

        // ── Estabilidad ────────────────────────────────────────────────

        /// <summary>Solo con suficientes días seguidos (≥ 14 pares); si no, null y el bloque se oculta.</summary>
        public static string Stability(ReportData data, ChartPeriod period)
        {
            var d = data.Dynamics;
            if (!d.Mssd.HasValue) return null;

            string previous = period.PreviousText();
            if (previous != null && d.PreviousMssd.HasValue && d.PreviousMssd.Value > 0f)
            {
                float relative = (d.Mssd.Value - d.PreviousMssd.Value) / d.PreviousMssd.Value;
                if (relative <= -StabilityThreshold) return $"Tu ánimo ha estado más estable que {previous}.";
                if (relative >= StabilityThreshold)  return $"Tu ánimo ha tenido más altibajos que {previous}.";
                return $"Tu ánimo se ha mantenido tan estable como {previous}.";
            }

            // Sin comparación: cambio típico de un día a otro (raíz del MSSD), en puntos de ánimo
            return $"De un día a otro, tu ánimo cambia de media unos {_n1((float)Math.Sqrt(d.Mssd.Value))} puntos.";
        }

        // ── Qué influye en ti ──────────────────────────────────────────

        /// <summary>Hasta <paramref name="max"/> motivos con la mayor diferencia de ánimo (≥ 0,3 puntos).</summary>
        public static List<string> MotiveLines(ReportData data, int max = 3)
        {
            return data.Motives
                .Where(m => m.Difference.HasValue && Math.Abs(m.Difference.Value) >= TrendThreshold)
                .OrderByDescending(m => Math.Abs(m.Difference.Value))
                .Take(max)
                .Select(m =>
                {
                    string direction = m.Difference.Value > 0 ? "más alto" : "más bajo";
                    return $"{m.DisplayName}: cuando aparece, tu ánimo suele ser {direction} " +
                           $"({_n1(m.MeanMoodWith)} frente a {_n1(m.MeanMoodWithout.Value)}).";
                })
                .ToList();
        }

        public static string MotivesEmpty(ReportData data)
            => data.Motives.Count == 0
                ? "Añade motivos a tus check-ins para descubrir qué influye en cómo te sientes."
                : "Por ahora ningún motivo marca una diferencia clara en tu ánimo.";

        // ── Qué te ayuda ───────────────────────────────────────────────

        /// <summary>Minijuegos que mejoran el ánimo (≥ 3 partidas válidas), de más a menos.</summary>
        public static List<string> HelpLines(ReportData data, int max = 3)
        {
            return data.Minigames.ByGame
                .Where(g => g.MeanDelta.HasValue && g.MeanDelta.Value > 0f)
                .OrderByDescending(g => g.MeanDelta.Value)
                .Take(max)
                .Select(g => $"{g.Type.ToDisplayName()}: tu ánimo mejora {_n1(g.MeanDelta.Value)} puntos de media " +
                             $"({g.ValidSessions} partidas).")
                .ToList();
        }

        public static string HelpEmpty(ReportData data)
        {
            if (data.Minigames.Sessions == 0)
                return "Juega a los minijuegos para descubrir cuáles te ayudan más.";
            return "Elige cómo te sientes al terminar cada partida para ver qué te ayuda " +
                   $"(hacen falta al menos {ReportCalculator.MinValidSessions} partidas de un mismo juego).";
        }

        /// <summary>Evolución del ritmo de respiración en Breath Jump; null sin partidas con esa métrica.</summary>
        public static string Breathing(ReportData data)
        {
            var points = data.Minigames.Breathing;
            if (points.Count == 0) return null;

            float last = points[points.Count - 1].BreathsPerMinute;
            if (points.Count == 1)
                return $"Breath Jump: respiraste a {_n1(last)} respiraciones por minuto (lo ideal para relajarse, unas 6).";

            return $"Breath Jump: tu ritmo pasó de {_n1(points[0].BreathsPerMinute)} a {_n1(last)} " +
                   "respiraciones por minuto (lo ideal para relajarse, unas 6).";
        }

        // ── Bienestar (WHO-5) ──────────────────────────────────────────

        public static string Who5(ReportData data)
        {
            if (data.Who5.Count == 0)
                return "Completa el cuestionario de bienestar cuando aparezca en tus notificaciones.";

            var last = data.Who5[data.Who5.Count - 1];
            string text = $"Tu último índice de bienestar: {last.Score} / 100";
            if (last.ChangeVsPrevious.HasValue && last.ChangeVsPrevious.Value != 0)
                text += $" ({(last.ChangeVsPrevious.Value > 0 ? "+" : "")}{last.ChangeVsPrevious.Value} respecto al anterior)";
            return text + ".";
        }

        // ── Hábitos ────────────────────────────────────────────────────

        public static string Adherence(ReportData data)
        {
            var a = data.Adherence;
            if (a.DaysInRange <= 0 || !a.Pct.HasValue) return "Aún no hay días que contar en este periodo.";
            return $"Has hecho tu check-in {a.DaysWithCheckIn} de {_days(a.DaysInRange)} ({Math.Round(a.Pct.Value)} %).";
        }

        public static string Diary(ReportData data)
        {
            var d = data.Diary;
            if (d.Entries == 0) return "Aún no has escrito en tu diario en este periodo.";
            string entries = d.Entries == 1 ? "1 entrada" : $"{d.Entries} entradas";
            string words   = d.TotalWords == 1 ? "1 palabra" : $"{d.TotalWords.ToString("N0", _es)} palabras";
            return $"{entries} en tu diario · {words}.";
        }

        // ── Formato ────────────────────────────────────────────────────

        private static string _n1(float value) => value.ToString("0.0", _es);

        private static string _days(int n) => n == 1 ? "1 día" : $"{n} días";
    }
}
