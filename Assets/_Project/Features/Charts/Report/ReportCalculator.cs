using System;
using System.Collections.Generic;
using System.Linq;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Calcula todas las métricas de seguimiento de un periodo (docs/METRICS.md §4). Clase estática
    /// pura, sin Unity: la usan la pantalla de Estadísticas y el informe profesional, así ambos
    /// muestran exactamente las mismas cifras. Tiene tests en Tests/EditMode.
    ///
    /// Reglas generales:
    /// - Solo registros del usuario: los placeholders restaurados se excluyen y se cuentan.
    /// - Serie diaria = check-in de Día (uno por día como máximo). Los de Momento se analizan aparte.
    /// - Cada métrica tiene un mínimo de datos; por debajo vale null ("registra X días más").
    /// </summary>
    public static class ReportCalculator
    {
        // ── Mínimos de datos (docs/METRICS.md §4) ──────────────────────

        public const int MinDaysForMean            = 3;
        public const int MinDaysForTrend           = 7;
        public const int MinDaysForStdDev          = 7;
        /// <summary>Días de la media móvil del ánimo (MovingAverage7). Los gráficos no la unen a través de huecos mayores.</summary>
        public const int MovingAverageWindowDays   = 7;
        public const int MinPairsForDynamics       = 14;
        public const int MinIntradayPairs          = 5;
        public const int MinRecordsForValence      = 5;
        public const int MinRecordsForEmodiversity = 10;
        public const int MinMotiveOccurrences      = 3;
        public const int MinWeekdayCheckIns        = 2;
        public const int MinValidSessions          = 3;
        public const int MinWeeklyWords            = 50;
        public const int MinGapDays                = 2;

        public const int   GoodMood           = 4;
        public const int   BadMood            = 2;
        public const float InattentiveSeconds = 10f;

        private static readonly int EmotionCount = Enum.GetValues(typeof(EmotionType)).Length;

        // ══════════════════════════════════════════════════════════════

        public static ReportData Calculate(ReportInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var data     = new ReportData();
            var all      = input.Records ?? new List<EmotionRecord>();
            var user     = all.Where(r => r.Source == RecordSource.User).ToList();
            var previous = (input.PreviousRecords ?? new List<EmotionRecord>())
                           .Where(r => r.Source == RecordSource.User).ToList();

            data.ExcludedPlaceholders = all.Count - user.Count;
            data.TotalUserRecords     = user.Count;
            data.DayCheckIns          = user.Count(r => r.IsMorningCheck);
            data.MomentRecords        = user.Count - data.DayCheckIns;

            var daily         = DailySeries(user);
            var previousDaily = DailySeries(previous);

            _range(input, user, daily, data);
            _mood(data.Mood, daily, previousDaily, user, DailyEmotions(user));
            _dynamics(data.Dynamics, daily, previousDaily);
            _emotions(data.Emotions, user);
            data.Motives = _motives(user);
            _patterns(data.Patterns, daily, user);
            _minigames(data.Minigames, input.Sessions ?? new List<MinigameSession>());
            _diary(data.Diary, input, data.DaysInRange);
            data.Who5 = _who5(input);
            _adherence(data.Adherence, data, daily, input);

            data.SupportActivations = (input.SupportNotifications ?? new List<AppNotification>())
                .OrderBy(n => n.CreatedAt)
                .Select(n => new SupportActivation { At = n.CreatedAt, Trigger = n.SourceRef })
                .ToList();

            return data;
        }

        // ── Utilidades públicas (también para tests y para la UI) ──────

        /// <summary>Ánimo de cada día con check-in de Día del usuario (el más reciente si hubiera varios), ordenado.</summary>
        public static SortedDictionary<DateTime, int> DailySeries(IEnumerable<EmotionRecord> records)
        {
            var series = new SortedDictionary<DateTime, int>();
            foreach (var group in records
                         .Where(r => r.IsMorningCheck && r.Source == RecordSource.User && r.MoodLevel > 0)
                         .GroupBy(r => r.Timestamp.Date))
                series[group.Key] = group.OrderByDescending(r => r.Timestamp).First().MoodLevel;
            return series;
        }

        /// <summary>Emoción del check-in de Día de cada día (el más reciente si hubiera varios).</summary>
        public static Dictionary<DateTime, EmotionType> DailyEmotions(IEnumerable<EmotionRecord> records)
            => records
                .Where(r => r.IsMorningCheck && r.Source == RecordSource.User && r.MoodLevel > 0)
                .GroupBy(r => r.Timestamp.Date)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Timestamp).First().EmotionType);

        /// <summary>Pares (ayer, hoy) de días consecutivos de la serie: los huecos no se comparan (Jahng et al., 2008).</summary>
        public static List<(int previous, int current)> ConsecutivePairs(SortedDictionary<DateTime, int> daily)
        {
            var pairs = new List<(int, int)>();
            DateTime? lastDate = null;
            int lastMood = 0;
            foreach (var day in daily)
            {
                if (lastDate.HasValue && (day.Key - lastDate.Value).Days == 1)
                    pairs.Add((lastMood, day.Value));
                lastDate = day.Key;
                lastMood = day.Value;
            }
            return pairs;
        }

        /// <summary>Media de los cuadrados de las diferencias sucesivas.</summary>
        public static float? Mssd(List<(int previous, int current)> pairs)
        {
            if (pairs == null || pairs.Count < MinPairsForDynamics) return null;
            return (float)pairs.Average(p => (double)(p.current - p.previous) * (p.current - p.previous));
        }

        /// <summary>Inercia: correlación de Pearson entre el ánimo de un día y el del anterior.</summary>
        public static float? Inertia(List<(int previous, int current)> pairs)
        {
            if (pairs == null || pairs.Count < MinPairsForDynamics) return null;
            return Pearson(pairs.Select(p => (double)p.previous).ToList(),
                           pairs.Select(p => (double)p.current).ToList());
        }

        /// <summary>Entropía de Shannon normalizada entre 0 (siempre la misma emoción) y 1 (las 8 por igual).</summary>
        public static float Emodiversity(IEnumerable<int> countsPerEmotion)
        {
            var counts = countsPerEmotion.Where(c => c > 0).ToList();
            int total = counts.Sum();
            if (total == 0) return 0f;

            double h = 0;
            foreach (int c in counts)
            {
                double p = (double)c / total;
                h -= p * Math.Log(p);
            }
            return (float)(h / Math.Log(EmotionCount));
        }

        public static float? Pearson(IList<double> x, IList<double> y)
        {
            if (x.Count != y.Count || x.Count < 2) return null;
            double mx = x.Average(), my = y.Average();
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < x.Count; i++)
            {
                double dx = x[i] - mx, dy = y[i] - my;
                sxy += dx * dy; sxx += dx * dx; syy += dy * dy;
            }
            if (sxx <= 0 || syy <= 0) return null;
            return (float)(sxy / Math.Sqrt(sxx * syy));
        }

        /// <summary>Lunes de la semana de <paramref name="date"/>.</summary>
        public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        // ── Periodo ────────────────────────────────────────────────────

        private static void _range(ReportInput input, List<EmotionRecord> user,
                                   SortedDictionary<DateTime, int> daily, ReportData data)
        {
            DateTime from = input.From.Date;
            if (input.ProfileCreatedAt.HasValue && input.ProfileCreatedAt.Value.Year >= 2000
                && input.ProfileCreatedAt.Value.Date > from)
                from = input.ProfileCreatedAt.Value.Date;
            if (from.Year < 2000)
                from = user.Count > 0 ? user.Min(r => r.Timestamp).Date : input.Today.Date;

            DateTime to = input.To.Date > input.Today.Date ? input.Today.Date : input.To.Date;
            // Hoy sin check-in todavía no cuenta como día sin registro
            if (to == input.Today.Date && !daily.ContainsKey(to)) to = to.AddDays(-1);

            data.From        = from;
            data.To          = to;
            data.DaysInRange = to >= from ? (to - from).Days + 1 : 0;
        }

        // ── §4.1 Estado de ánimo ───────────────────────────────────────

        private static void _mood(MoodSummary mood, SortedDictionary<DateTime, int> daily,
                                  SortedDictionary<DateTime, int> previousDaily, List<EmotionRecord> user,
                                  Dictionary<DateTime, EmotionType> dailyEmotions)
        {
            var values = daily.Values.ToList();
            mood.N = values.Count;

            foreach (var day in daily)
            {
                var window = daily.Where(d => d.Key > day.Key.AddDays(-MovingAverageWindowDays) && d.Key <= day.Key).Select(d => d.Value);
                mood.Daily.Add(new DailyMood
                {
                    Date           = day.Key,
                    Mood           = day.Value,
                    Emotion        = dailyEmotions[day.Key],
                    MovingAverage7 = (float)window.Average()
                });
            }

            if (mood.N >= MinDaysForMean)
            {
                mood.Mean        = (float)values.Average();
                mood.PctGoodDays = 100f * values.Count(v => v >= GoodMood) / mood.N;
                mood.PctBadDays  = 100f * values.Count(v => v <= BadMood)  / mood.N;
            }

            if (mood.N >= MinDaysForStdDev)
                mood.StdDev = _sampleStdDev(values.Select(v => (double)v).ToList());

            if (mood.N >= MinDaysForTrend)
            {
                DateTime first = daily.Keys.First();
                var x = daily.Keys.Select(d => (d - first).TotalDays).ToList();
                var y = values.Select(v => (double)v).ToList();
                float? slope = _slope(x, y);
                if (slope.HasValue) mood.SlopePerWeek = slope.Value * 7f;
            }

            if (previousDaily.Count >= MinDaysForMean)
            {
                mood.PreviousMean = (float)previousDaily.Values.Average();
                if (mood.Mean.HasValue) mood.ChangeVsPrevious = mood.Mean.Value - mood.PreviousMean.Value;
            }

            var intraday = user
                .Where(r => !r.IsMorningCheck && r.MoodLevel > 0 && daily.ContainsKey(r.Timestamp.Date))
                .Select(r => r.MoodLevel - daily[r.Timestamp.Date])
                .ToList();
            mood.IntradayPairs = intraday.Count;
            if (intraday.Count >= MinIntradayPairs) mood.IntradayDelta = (float)intraday.Average();
        }

        // ── §4.2 Dinámica ──────────────────────────────────────────────

        private static void _dynamics(DynamicsSummary dynamics, SortedDictionary<DateTime, int> daily,
                                      SortedDictionary<DateTime, int> previousDaily)
        {
            var pairs = ConsecutivePairs(daily);
            dynamics.ConsecutivePairs = pairs.Count;
            dynamics.Mssd             = Mssd(pairs);
            dynamics.Inertia          = Inertia(pairs);

            var previousPairs = ConsecutivePairs(previousDaily);
            dynamics.PreviousConsecutivePairs = previousPairs.Count;
            dynamics.PreviousMssd             = Mssd(previousPairs);
            dynamics.PreviousInertia          = Inertia(previousPairs);
        }

        // ── §4.3 Perfil emocional ──────────────────────────────────────

        private static void _emotions(EmotionProfile profile, List<EmotionRecord> user)
        {
            profile.Total = user.Count;
            foreach (var record in user)
            {
                profile.Counts.TryGetValue(record.EmotionType, out int c);
                profile.Counts[record.EmotionType] = c + 1;

                var quadrant = record.EmotionType.QuadrantOf();
                profile.QuadrantCounts.TryGetValue(quadrant, out int q);
                profile.QuadrantCounts[quadrant] = q + 1;
            }

            profile.DistinctEmotions = profile.Counts.Count;
            if (profile.Counts.Count > 0)
                profile.MostFrequent = profile.Counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;

            if (profile.Total >= MinRecordsForValence)
            {
                profile.PctPleasant   = 100f * user.Count(r => r.EmotionType.ValenceOf() == Valence.Pleasant)   / profile.Total;
                profile.PctUnpleasant = 100f * user.Count(r => r.EmotionType.ValenceOf() == Valence.Unpleasant) / profile.Total;
                profile.PctMixed      = 100f * user.Count(r => r.EmotionType.ValenceOf() == Valence.Mixed)      / profile.Total;
            }

            if (profile.Total >= MinRecordsForEmodiversity)
                profile.Emodiversity = Emodiversity(profile.Counts.Values);
        }

        // ── §4.4 Motivos ───────────────────────────────────────────────

        private static List<MotiveStat> _motives(List<EmotionRecord> user)
        {
            var rated = user.Where(r => r.MoodLevel > 0).ToList();
            var keysByRecord = rated.ToDictionary(
                r => r,
                r => new HashSet<string>(MotiveTags.Parse(r.SelectedMotiveTags).Select(MotiveTags.GroupKey)));

            var result = new List<MotiveStat>();
            foreach (string key in keysByRecord.Values.SelectMany(k => k).Distinct())
            {
                var with    = rated.Where(r => keysByRecord[r].Contains(key)).ToList();
                var without = rated.Where(r => !keysByRecord[r].Contains(key)).ToList();
                if (with.Count < MinMotiveOccurrences) continue;

                var stat = new MotiveStat
                {
                    Key          = key,
                    DisplayName  = MotiveTags.DisplayName(key),
                    Count        = with.Count,
                    Pct          = 100f * with.Count / rated.Count,
                    MeanMoodWith = (float)with.Average(r => r.MoodLevel),
                    MostFrequentEmotion = with.GroupBy(r => r.EmotionType)
                                              .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key
                };
                if (without.Count > 0)
                {
                    stat.MeanMoodWithout = (float)without.Average(r => r.MoodLevel);
                    stat.Difference      = stat.MeanMoodWith - stat.MeanMoodWithout.Value;
                }
                result.Add(stat);
            }

            return result.OrderByDescending(m => m.Count).ThenBy(m => m.Key).ToList();
        }

        // ── §4.5 Patrones temporales ───────────────────────────────────

        private static void _patterns(TemporalPatterns patterns, SortedDictionary<DateTime, int> daily,
                                      List<EmotionRecord> user)
        {
            var sums = new int[7];
            foreach (var day in daily)
            {
                int index = ((int)day.Key.DayOfWeek + 6) % 7;
                sums[index] += day.Value;
                patterns.DayCheckInsByWeekday[index]++;
            }
            for (int i = 0; i < 7; i++)
                if (patterns.DayCheckInsByWeekday[i] >= MinWeekdayCheckIns)
                    patterns.MeanMoodByWeekday[i] = (float)sums[i] / patterns.DayCheckInsByWeekday[i];

            foreach (var record in user.Where(r => !r.IsMorningCheck))
                patterns.MomentRecordsByHour[record.Timestamp.Hour]++;
        }

        // ── §4.6 Minijuegos ────────────────────────────────────────────

        private static void _minigames(MinigameSummary summary, List<MinigameSession> sessions)
        {
            summary.Sessions      = sessions.Count;
            summary.ValidSessions = sessions.Count(s => s.IsValidForMoodEffect());
            summary.TotalMinutes  = sessions.Sum(s => s.DurationSeconds) / 60f;
            foreach (var s in sessions) summary.SessionsByHour[s.StartTime.Hour]++;

            foreach (var group in sessions.GroupBy(s => s.MinigameId))
            {
                var stat = new MinigameStat
                {
                    Type     = group.Key,
                    Sessions = group.Count(),
                    Minutes  = group.Sum(s => s.DurationSeconds) / 60f
                };
                foreach (var s in group)
                {
                    stat.EmotionBeforeCounts.TryGetValue(s.EmotionBefore, out int c);
                    stat.EmotionBeforeCounts[s.EmotionBefore] = c + 1;
                }

                var deltas = group.Select(s => s.MoodDelta()).Where(d => d.HasValue).Select(d => d.Value).ToList();
                stat.ValidSessions = deltas.Count;
                if (deltas.Count >= MinValidSessions)
                {
                    stat.MeanDelta   = (float)deltas.Average();
                    stat.PctImproved = 100f * deltas.Count(d => d > 0)  / deltas.Count;
                    stat.PctSame     = 100f * deltas.Count(d => d == 0) / deltas.Count;
                    stat.PctWorse    = 100f * deltas.Count(d => d < 0)  / deltas.Count;
                }
                summary.ByGame.Add(stat);
            }
            summary.ByGame = summary.ByGame.OrderByDescending(g => g.Sessions).ThenBy(g => g.Type).ToList();

            foreach (var group in sessions.Where(s => s.IsValidForMoodEffect()).GroupBy(s => s.EmotionBefore))
            {
                var deltas = group.Select(s => s.MoodDelta().Value).ToList();
                summary.ByEmotionBefore.Add(new EmotionEffectStat
                {
                    EmotionBefore = group.Key,
                    ValidSessions = deltas.Count,
                    MeanDelta     = deltas.Count >= MinValidSessions ? (float)deltas.Average() : (float?)null
                });
            }
            summary.ByEmotionBefore = summary.ByEmotionBefore.OrderBy(e => e.EmotionBefore).ToList();

            foreach (var s in sessions.Where(s => s.MinigameId == MinigameType.BreathJump).OrderBy(s => s.StartTime))
            {
                var m = s.Metrics;
                if (!m.TryGetValue("breaths_per_minute", out float bpm) || bpm <= 0f) continue;

                float perfect = _metric(m, "perfect_breaths");
                float landed  = perfect + _metric(m, "good_breaths") + _metric(m, "off_rhythm_breaths");
                summary.Breathing.Add(new BreathPoint
                {
                    Date              = s.StartTime,
                    BreathsPerMinute  = bpm,
                    ExhaleInhaleRatio = _metric(m, "exhale_inhale_ratio"),
                    BreathCv          = _metric(m, "breath_cv"),
                    PctPerfect        = landed > 0 ? 100f * perfect / landed : 0f
                });
            }
        }

        // ── §4.7 Diario ────────────────────────────────────────────────

        private static void _diary(DiarySummary diary, ReportInput input, int daysInRange)
        {
            var entries  = input.DiaryEntries ?? new List<DiaryEntry>();
            bool enabled = input.DiaryAnalysisEnabled && input.Lexicon != null;

            diary.Entries                 = entries.Count;
            diary.LanguageAnalysisEnabled = enabled;
            diary.EntriesPerWeek          = daysInRange > 0 ? entries.Count * 7f / daysInRange : 0f;

            var weeks = new SortedDictionary<DateTime, (int entries, DiaryLanguageCounts counts)>();
            foreach (var entry in entries)
            {
                var counts = DiaryLanguageAnalyzer.Analyze($"{entry.Title} {entry.Content}", enabled ? input.Lexicon : null);
                diary.TotalWords += counts.Words;

                DateTime week = WeekStart(entry.Date);
                weeks.TryGetValue(week, out var acc);
                acc.entries++;
                acc.counts.Add(counts);
                weeks[week] = acc;
            }

            if (entries.Count > 0) diary.WordsPerEntry = (float)diary.TotalWords / entries.Count;

            foreach (var week in weeks)
            {
                var c = week.Value.counts;
                bool enough = enabled && c.Words >= MinWeeklyWords;
                diary.Weeks.Add(new DiaryWeek
                {
                    WeekStart      = week.Key,
                    Entries        = week.Value.entries,
                    Words          = c.Words,
                    PctFirstPerson = enough ? c.PctFirstPerson : (float?)null,
                    PctNegative    = enough ? c.PctNegative    : (float?)null,
                    PctPositive    = enough ? c.PctPositive    : (float?)null
                });
            }
        }

        // ── §4.8 WHO-5 ─────────────────────────────────────────────────

        private static List<Who5Point> _who5(ReportInput input)
        {
            var result = new List<Who5Point>();
            DateTime from = input.From;
            DateTime to   = input.To;
            int? previousScore = null;

            foreach (var r in (input.Who5Responses ?? new List<ScaleResponse>())
                              .Where(r => r.Scale == ScaleType.Who5 && r.CompletedAt <= to)
                              .OrderBy(r => r.CompletedAt))
            {
                if (r.CompletedAt >= from)
                {
                    int? change = previousScore.HasValue ? r.Score - previousScore.Value : (int?)null;
                    result.Add(new Who5Point
                    {
                        CompletedAt         = r.CompletedAt,
                        AvailableSince      = r.AvailableSince,
                        Raw                 = r.RawScore,
                        Score               = r.Score,
                        ChangeVsPrevious    = change,
                        LowWellBeing        = r.Score <= 50,
                        ProbableDepression  = r.Score <= 28,
                        RelevantChange      = change.HasValue && Math.Abs(change.Value) >= 10,
                        PossiblyInattentive = r.DurationSeconds > 0 && r.DurationSeconds < InattentiveSeconds,
                        DurationSeconds     = r.DurationSeconds,
                        DelayDays           = Math.Max(0, (r.CompletedAt.Date - r.AvailableSince.Date).Days),
                        Answers             = r.Answers
                    });
                }
                previousScore = r.Score;
            }
            return result;
        }

        // ── §4.9 Adherencia ────────────────────────────────────────────

        private static void _adherence(AdherenceSummary adherence, ReportData data,
                                       SortedDictionary<DateTime, int> daily, ReportInput input)
        {
            adherence.DaysInRange   = data.DaysInRange;
            adherence.CurrentStreak = input.CurrentStreak;
            adherence.LongestStreak = input.LongestStreak;
            adherence.Who5Completed = data.Who5.Count;
            if (data.Who5.Count > 0) adherence.Who5MeanDelayDays = (float)data.Who5.Average(w => w.DelayDays);

            if (data.DaysInRange <= 0) return;

            var moodsBeforeGaps = new List<int>();
            DateTime? gapStart  = null;
            int gapDays         = 0;

            for (DateTime day = data.From; day <= data.To; day = day.AddDays(1))
            {
                if (daily.ContainsKey(day))
                {
                    adherence.DaysWithCheckIn++;
                    _closeGap(adherence, ref gapStart, ref gapDays, daily, moodsBeforeGaps);
                }
                else
                {
                    if (!gapStart.HasValue) gapStart = day;
                    gapDays++;
                }
            }
            _closeGap(adherence, ref gapStart, ref gapDays, daily, moodsBeforeGaps);

            adherence.Pct = 100f * adherence.DaysWithCheckIn / data.DaysInRange;
            if (moodsBeforeGaps.Count > 0) adherence.MeanMoodBeforeGaps = (float)moodsBeforeGaps.Average();
        }

        private static void _closeGap(AdherenceSummary adherence, ref DateTime? gapStart, ref int gapDays,
                                      SortedDictionary<DateTime, int> daily, List<int> moodsBeforeGaps)
        {
            if (gapStart.HasValue && gapDays >= MinGapDays)
            {
                adherence.Gaps.Add(new GapStat { Start = gapStart.Value, Days = gapDays });
                if (daily.TryGetValue(gapStart.Value.AddDays(-1), out int moodBefore))
                    moodsBeforeGaps.Add(moodBefore);
            }
            gapStart = null;
            gapDays  = 0;
        }

        // ── Estadística básica ─────────────────────────────────────────

        private static float _sampleStdDev(List<double> values)
        {
            double mean = values.Average();
            double sum  = values.Sum(v => (v - mean) * (v - mean));
            return (float)Math.Sqrt(sum / (values.Count - 1));
        }

        /// <summary>Pendiente de la recta de mínimos cuadrados; null si x no varía.</summary>
        private static float? _slope(List<double> x, List<double> y)
        {
            double mx = x.Average(), my = y.Average();
            double sxy = 0, sxx = 0;
            for (int i = 0; i < x.Count; i++)
            {
                sxy += (x[i] - mx) * (y[i] - my);
                sxx += (x[i] - mx) * (x[i] - mx);
            }
            return sxx > 0 ? (float)(sxy / sxx) : (float?)null;
        }

        private static float _metric(Dictionary<string, float> metrics, string key)
            => metrics.TryGetValue(key, out float value) ? value : 0f;
    }
}
