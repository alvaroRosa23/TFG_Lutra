using System;
using System.Collections.Generic;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Datos de entrada de ReportCalculator. Los rellena ReportDataLoader desde DataRepository;
    /// en los tests se construyen a mano.
    /// </summary>
    public class ReportInput
    {
        public DateTime From;
        public DateTime To;
        /// <summary>Hoy (inyectable para los tests). Si To es hoy y aún no hay check-in, hoy no cuenta como día sin registro.</summary>
        public DateTime Today = DateTime.Today;
        /// <summary>Alta del perfil: el periodo efectivo no empieza antes.</summary>
        public DateTime? ProfileCreatedAt;

        /// <summary>Registros del periodo, incluidos los placeholders (se excluyen y se cuentan).</summary>
        public List<EmotionRecord>   Records         = new List<EmotionRecord>();
        /// <summary>Registros del periodo anterior de igual duración (para comparar).</summary>
        public List<EmotionRecord>   PreviousRecords = new List<EmotionRecord>();
        public List<MinigameSession> Sessions        = new List<MinigameSession>();
        public List<DiaryEntry>      DiaryEntries    = new List<DiaryEntry>();
        /// <summary>Envíos del WHO-5 hasta To (también anteriores al periodo, para calcular el cambio).</summary>
        public List<ScaleResponse>   Who5Responses   = new List<ScaleResponse>();
        /// <summary>Notificaciones de tipo Support del periodo (activaciones del protocolo de apoyo).</summary>
        public List<AppNotification> SupportNotifications = new List<AppNotification>();

        public bool         DiaryAnalysisEnabled = true;
        public DiaryLexicon Lexicon;

        public int? CurrentStreak;
        public int? LongestStreak;
    }

    /// <summary>
    /// Resultado de ReportCalculator: todas las métricas de docs/METRICS.md §4 para un periodo.
    /// Lo usan la pantalla de Estadísticas (lenguaje sencillo) y el informe profesional (valores
    /// técnicos). null = no hay datos suficientes (ver los mínimos en ReportCalculator).
    /// </summary>
    public class ReportData
    {
        public DateTime From;            // inicio efectivo (no antes del alta)
        public DateTime To;              // fin efectivo
        public int      DaysInRange;

        public int TotalUserRecords;
        public int DayCheckIns;
        public int MomentRecords;
        /// <summary>Placeholders restaurados excluidos de todos los cálculos (se indica en la metodología).</summary>
        public int ExcludedPlaceholders;

        public MoodSummary       Mood       = new MoodSummary();
        public DynamicsSummary   Dynamics   = new DynamicsSummary();
        public EmotionProfile    Emotions   = new EmotionProfile();
        public List<MotiveStat>  Motives    = new List<MotiveStat>();
        public TemporalPatterns  Patterns   = new TemporalPatterns();
        public MinigameSummary   Minigames  = new MinigameSummary();
        public DiarySummary      Diary      = new DiarySummary();
        public List<Who5Point>   Who5       = new List<Who5Point>();
        public AdherenceSummary  Adherence  = new AdherenceSummary();
        public List<SupportActivation> SupportActivations = new List<SupportActivation>();
    }

    // ── §4.1 Estado de ánimo ─────────────────────────────────────────

    public class DailyMood
    {
        public DateTime Date;
        public int      Mood;
        /// <summary>Media de los días con registro dentro de los 7 últimos (incluido este).</summary>
        public float    MovingAverage7;
    }

    public class MoodSummary
    {
        public List<DailyMood> Daily = new List<DailyMood>();
        public int    N;                    // días con check-in de Día
        public float? Mean;                 // ≥ 3 días
        public float? StdDev;               // ≥ 7 días (muestral)
        public float? SlopePerWeek;         // ≥ 7 días (puntos de ánimo por semana)
        public float? PreviousMean;         // periodo anterior, ≥ 3 días
        public float? ChangeVsPrevious;     // Mean − PreviousMean
        public float? PctGoodDays;          // % días con ánimo ≥ 4 (≥ 3 días)
        public float? PctBadDays;           // % días con ánimo ≤ 2 (≥ 3 días)
        public int    IntradayPairs;        // registros de Momento con check-in de Día el mismo día
        public float? IntradayDelta;        // media de (Momento − Día), ≥ 5 pares
    }

    // ── §4.2 Dinámica emocional ──────────────────────────────────────

    public class DynamicsSummary
    {
        public int    ConsecutivePairs;     // pares de días consecutivos con check-in de Día
        public float? Mssd;                 // ≥ 14 pares
        public float? Inertia;              // autocorrelación lag-1, ≥ 14 pares
        public int    PreviousConsecutivePairs;
        public float? PreviousMssd;
        public float? PreviousInertia;
    }

    // ── §4.3 Perfil emocional ────────────────────────────────────────

    public class EmotionProfile
    {
        public int Total;
        public Dictionary<EmotionType, int>    Counts         = new Dictionary<EmotionType, int>();
        public Dictionary<AffectQuadrant, int> QuadrantCounts = new Dictionary<AffectQuadrant, int>();
        public EmotionType? MostFrequent;
        public int    DistinctEmotions;
        public float? PctPleasant;          // ≥ 5 registros
        public float? PctUnpleasant;
        public float? PctMixed;
        public float? Emodiversity;         // 0-1, ≥ 10 registros
    }

    // ── §4.4 Motivos ─────────────────────────────────────────────────

    public class MotiveStat
    {
        public string Key;                  // MotiveTags.GroupKey
        public string DisplayName;
        public int    Count;
        public float  Pct;                  // % de registros con este motivo
        public float  MeanMoodWith;
        public float? MeanMoodWithout;
        public float? Difference;           // con − sin
        public EmotionType MostFrequentEmotion;
    }

    // ── §4.5 Patrones temporales ─────────────────────────────────────

    public class TemporalPatterns
    {
        /// <summary>Índice 0 = lunes … 6 = domingo. null con menos de 2 check-ins ese día de la semana.</summary>
        public float?[] MeanMoodByWeekday  = new float?[7];
        public int[]    DayCheckInsByWeekday = new int[7];
        /// <summary>Registros de Momento por hora del día (0-23).</summary>
        public int[]    MomentRecordsByHour = new int[24];
    }

    // ── §4.6 Minijuegos ──────────────────────────────────────────────

    public class MinigameStat
    {
        public MinigameType Type;
        public int    Sessions;
        public float  Minutes;
        public int    ValidSessions;        // IsValidForMoodEffect
        public float? MeanDelta;            // ≥ 3 válidas
        public float? PctImproved;
        public float? PctSame;
        public float? PctWorse;
        public Dictionary<EmotionType, int> EmotionBeforeCounts = new Dictionary<EmotionType, int>();
    }

    public class EmotionEffectStat
    {
        public EmotionType EmotionBefore;
        public int    ValidSessions;
        public float? MeanDelta;            // ≥ 3 válidas
    }

    public class BreathPoint
    {
        public DateTime Date;
        public float    BreathsPerMinute;
        public float    ExhaleInhaleRatio;
        public float    BreathCv;
        public float    PctPerfect;
    }

    public class MinigameSummary
    {
        public int    Sessions;
        public int    ValidSessions;
        public float  TotalMinutes;
        public List<MinigameStat>      ByGame          = new List<MinigameStat>();
        public List<EmotionEffectStat> ByEmotionBefore = new List<EmotionEffectStat>();
        public List<BreathPoint>       Breathing       = new List<BreathPoint>();
        public int[] SessionsByHour = new int[24];
    }

    // ── §4.7 Diario ──────────────────────────────────────────────────

    public class DiaryWeek
    {
        public DateTime WeekStart;          // lunes
        public int      Entries;
        public int      Words;
        public float?   PctFirstPerson;     // ≥ 50 palabras en la semana y análisis activado
        public float?   PctNegative;
        public float?   PctPositive;
    }

    public class DiarySummary
    {
        public int    Entries;
        public int    TotalWords;
        public float? WordsPerEntry;
        public float  EntriesPerWeek;
        public bool   LanguageAnalysisEnabled;
        public List<DiaryWeek> Weeks = new List<DiaryWeek>();
    }

    // ── §4.8 WHO-5 ───────────────────────────────────────────────────

    public class Who5Point
    {
        public DateTime  CompletedAt;
        public DateTime  AvailableSince;
        public int       Raw;
        public int       Score;
        public int?      ChangeVsPrevious;
        public bool      LowWellBeing;          // ≤ 50
        public bool      ProbableDepression;    // ≤ 28
        public bool      RelevantChange;        // |cambio| ≥ 10
        public bool      PossiblyInattentive;   // respondido en < 10 s
        public float     DurationSeconds;
        public int       DelayDays;             // días entre disponible y completado
        public List<int> Answers = new List<int>();
    }

    // ── §4.9 Adherencia ──────────────────────────────────────────────

    public class GapStat
    {
        public DateTime Start;
        public int      Days;
    }

    public class AdherenceSummary
    {
        public int    DaysInRange;
        public int    DaysWithCheckIn;
        public float? Pct;
        public List<GapStat> Gaps = new List<GapStat>();   // ≥ 2 días seguidos sin check-in de Día
        public float? MeanMoodBeforeGaps;
        public int?   CurrentStreak;
        public int?   LongestStreak;
        public int    Who5Completed;
        public float? Who5MeanDelayDays;
    }

    // ── Protocolo de apoyo ───────────────────────────────────────────

    public class SupportActivation
    {
        public DateTime At;
        public string   Trigger;
    }
}
