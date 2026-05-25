using System;
using System.Collections.Generic;
using System.Linq;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Calcula ChartsData a partir de registros emocionales y sesiones de minijuegos.
    /// Clase estática: no tiene estado ni dependencias de Unity.
    /// </summary>
    public static class ChartsCalculator
    {
        /// <summary>
        /// Genera un ChartsData completo a partir de los datos en bruto del período.
        /// </summary>
        public static ChartsData Calculate(
            List<EmotionRecord>   records,
            List<MinigameSession> sessions)
        {
            var data = new ChartsData();

            if (records == null)  records  = new List<EmotionRecord>();
            if (sessions == null) sessions = new List<MinigameSession>();

            data.EmotionHistory        = records;
            data.TotalCheckIns         = records.Count;
            data.TotalMinigameSessions = sessions.Count;

            _calculateEmotionFrequency(data, records);
            _calculateDailyEmotionMap(data, records);
            _calculateFirstCheckIn(data, records);
            _calculateMinigameImpact(data, sessions);
            _calculateSessionStats(data, sessions);

            return data;
        }

        // ── Helpers de formato público ─────────────────────────────────

        /// <summary>
        /// Formatea segundos como duración legible.
        /// < 60  → "Xs"   | < 3600 → "Xm Ys"   | ≥ 3600 → "Xh Ym"
        /// </summary>
        public static string FormatDuration(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}s";

            if (seconds < 3600)
            {
                int m = seconds / 60;
                int s = seconds % 60;
                return $"{m}m {s:D2}s";
            }

            int h  = seconds / 3600;
            int rm = (seconds % 3600) / 60;
            return $"{h}h {rm}m";
        }

        /// <summary>Formatea días de racha: "1 día" o "X días".</summary>
        public static string FormatStreak(int days)
        {
            return days == 1 ? "1 día" : $"{days} días";
        }

        // ── Métodos de cálculo privados ────────────────────────────────

        private static void _calculateEmotionFrequency(ChartsData data, List<EmotionRecord> records)
        {
            var freq = new Dictionary<EmotionType, int>();

            foreach (var record in records)
            {
                freq.TryGetValue(record.EmotionType, out int count);
                freq[record.EmotionType] = count + 1;
            }

            data.EmotionFrequency = freq;

            // Emoción más frecuente
            if (freq.Count > 0)
            {
                data.MostFrequentEmotion = freq
                    .OrderByDescending(kv => kv.Value)
                    .First().Key;
            }
        }

        private static void _calculateDailyEmotionMap(ChartsData data, List<EmotionRecord> records)
        {
            // Para cada día, guardar la última emoción registrada
            var map = new Dictionary<DateTime, EmotionType>();

            foreach (var record in records.OrderBy(r => r.Timestamp))
                map[record.Timestamp.Date] = record.EmotionType;

            data.DailyEmotionMap = map;
        }

        private static void _calculateFirstCheckIn(ChartsData data, List<EmotionRecord> records)
        {
            if (records.Count == 0) return;
            data.FirstCheckInDate = records.Min(r => r.Timestamp);
        }

        private static void _calculateMinigameImpact(ChartsData data, List<MinigameSession> sessions)
        {
            // Agrupar por MinigameType y calcular promedio de (EmotionAfter - EmotionBefore)
            var grouped = new Dictionary<MinigameType, List<float>>();

            foreach (var session in sessions)
            {
                float delta = (int)session.EmotionAfter - (int)session.EmotionBefore;

                if (!grouped.ContainsKey(session.MinigameId))
                    grouped[session.MinigameId] = new List<float>();

                grouped[session.MinigameId].Add(delta);
            }

            var impact = new Dictionary<MinigameType, float>();
            foreach (var kv in grouped)
                impact[kv.Key] = kv.Value.Count > 0 ? kv.Value.Average() : 0f;

            data.MinigameImpact = impact;

            // Minijuego más beneficioso (mayor delta promedio)
            if (impact.Count > 0)
                data.MostBeneficialMinigame = impact.OrderByDescending(kv => kv.Value).First().Key;

            // Minijuego más jugado (mayor número de sesiones)
            if (grouped.Count > 0)
                data.MostPlayedMinigame = grouped.OrderByDescending(kv => kv.Value.Count).First().Key;
        }

        private static void _calculateSessionStats(ChartsData data, List<MinigameSession> sessions)
        {
            if (sessions.Count == 0)
            {
                data.AverageSessionDuration = 0f;
                return;
            }

            data.AverageSessionDuration = (float)sessions.Average(s => s.DurationSeconds);
        }
    }
}
