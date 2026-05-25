using System;
using System.Collections.Generic;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Datos calculados para los informes. Generado por ChartsCalculator
    /// a partir de EmotionRecords y MinigameSessions del período seleccionado.
    /// </summary>
    public class ChartsData
    {
        // ── Historial en bruto ─────────────────────────────────────────

        public List<EmotionRecord>   EmotionHistory  { get; set; } = new();

        // ── Frecuencia emocional ───────────────────────────────────────

        public Dictionary<EmotionType, int>   EmotionFrequency      { get; set; } = new();
        public EmotionType                    MostFrequentEmotion   { get; set; }

        // ── Impacto de minijuegos ──────────────────────────────────────

        /// <summary>Promedio de mejora emocional (diferencia de enum int After-Before) por minijuego.</summary>
        public Dictionary<MinigameType, float> MinigameImpact        { get; set; } = new();
        public MinigameType                    MostBeneficialMinigame { get; set; }
        public MinigameType                    MostPlayedMinigame     { get; set; }

        // ── Métricas globales ──────────────────────────────────────────

        public int      CurrentStreak          { get; set; }
        public int      LongestStreak          { get; set; }
        public int      TotalCheckIns          { get; set; }
        public int      TotalMinigameSessions  { get; set; }
        public float    AverageSessionDuration { get; set; }

        // ── Temporal ───────────────────────────────────────────────────

        public DateTime FirstCheckInDate { get; set; }

        /// <summary>Emoción predominante de cada día (última registrada). Usada para el heatmap.</summary>
        public Dictionary<DateTime, EmotionType> DailyEmotionMap { get; set; } = new();
    }
}
