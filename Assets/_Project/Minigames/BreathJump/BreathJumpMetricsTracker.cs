using System.Collections.Generic;
using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>
    /// Valora cada respiración y acumula la puntuación y métricas de BreathJump.
    ///
    /// Puntuación: suma de los puntos de cada intento (perfecta 1, buena 0,6, fuera de
    /// ritmo 0,25, caída 0) dividida entre max(intentos, breathsToComplete). Durante la
    /// partida sube desde 0 (lo que llevas sobre el total); al llegar a la meta equivale a la
    /// media por intento. Las caídas cuentan como intento, así que diluyen la nota.
    /// Solo una partida con TODAS las respiraciones perfectas y sin caídas llega a 100:
    /// cualquier otra se limita a 99.
    /// </summary>
    public class BreathJumpMetricsTracker
    {
        private const float PerfectPoints        = 1f;
        private const float GoodPoints           = 0.6f;
        private const float OffRhythmPoints      = 0.25f;
        private const float MaxScoreIfNotPerfect = 0.99f;

        private readonly BreathJumpTuning _tuning;

        private int   _perfect;
        private int   _good;
        private int   _offRhythm;
        private int   _missed;
        private int   _corrections;
        private float _earned;
        private float _inhaleSum;
        private float _exhaleSum;
        private float _cycleSum;        // suma de (inspiración + espiración) de cada respiración
        private float _cycleSquaredSum; // para la desviación típica del ciclo (regularidad)
        private int   _perfectStreak;
        private int   _bestPerfectStreak;

        public BreathJumpMetricsTracker(BreathJumpTuning tuning) => _tuning = tuning;

        private int _landed   => _perfect + _good + _offRhythm;
        private int _attempts => _landed + _missed;

        /// <summary>Registra una respiración completa (inspiración + salto aterrizado + espiración).</summary>
        public BreathQuality RegisterBreath(float inhaleSeconds, float exhaleSeconds, bool corrected)
        {
            var quality = _evaluate(inhaleSeconds, exhaleSeconds, corrected);

            _inhaleSum += inhaleSeconds;
            _exhaleSum += exhaleSeconds;

            float cycle = inhaleSeconds + exhaleSeconds;
            _cycleSum        += cycle;
            _cycleSquaredSum += cycle * cycle;
            if (corrected) _corrections++;

            switch (quality)
            {
                case BreathQuality.Perfect:
                    _perfect++;
                    _earned += PerfectPoints;
                    _perfectStreak++;
                    if (_perfectStreak > _bestPerfectStreak) _bestPerfectStreak = _perfectStreak;
                    break;
                case BreathQuality.Good:
                    _good++;
                    _earned += GoodPoints;
                    _perfectStreak = 0;
                    break;
                default:
                    _offRhythm++;
                    _earned += OffRhythmPoints;
                    _perfectStreak = 0;
                    break;
            }

            return quality;
        }

        /// <summary>Registra un salto que no llegó a la plataforma.</summary>
        public BreathQuality RegisterMiss()
        {
            _missed++;
            _perfectStreak = 0;
            return BreathQuality.Missed;
        }

        /// <summary>0.0-1.0; 1.0 solo si todas las respiraciones fueron perfectas.</summary>
        public float CalculateRelaxationScore()
        {
            int denominator = Mathf.Max(_attempts, _tuning.breathsToComplete);
            if (denominator <= 0) return 0f;

            float score = _earned / denominator;
            return _perfect == _attempts ? score : Mathf.Min(score, MaxScoreIfNotPerfect);
        }

        /// <summary>Monedas: 1 por cada 10 puntos mostrados (100 puntos = 10 monedas).</summary>
        public int CalculateCoinReward() => MinigameOutcome.ToDisplayScore(CalculateRelaxationScore()) / 10;

        public Dictionary<string, float> BuildMetrics()
        {
            return new Dictionary<string, float>
            {
                ["perfect_breaths"]     = _perfect,
                ["good_breaths"]        = _good,
                ["off_rhythm_breaths"]  = _offRhythm,
                ["missed_jumps"]        = _missed,
                ["air_corrections"]     = _corrections,
                ["avg_inhale"]          = _landed > 0 ? _inhaleSum / _landed : 0f,
                ["avg_exhale"]          = _landed > 0 ? _exhaleSum / _landed : 0f,
                ["best_perfect_streak"] = _bestPerfectStreak,
                ["breaths_per_minute"]  = _breathsPerMinute(),
                ["exhale_inhale_ratio"] = _inhaleSum > 0f ? _exhaleSum / _inhaleSum : 0f,
                ["breath_cv"]           = _cycleCoefficientOfVariation()
            };
        }

        // ── Helpers privados ───────────────────────────────────────────

        /// <summary>
        /// Frecuencia respiratoria según el ciclo medio (inspiración + espiración). Con el ritmo
        /// guía por defecto (4 s + 6 s) el objetivo es 6 respiraciones/min (ver docs/METRICS.md §4.6).
        /// </summary>
        private float _breathsPerMinute()
        {
            if (_landed == 0) return 0f;
            float meanCycle = _cycleSum / _landed;
            return meanCycle > 0f ? 60f / meanCycle : 0f;
        }

        /// <summary>
        /// Coeficiente de variación del ciclo (desviación típica muestral / media).
        /// Cuanto menor, más regular la respiración. 0 si hay menos de 2 respiraciones.
        /// </summary>
        private float _cycleCoefficientOfVariation()
        {
            if (_landed < 2) return 0f;

            float mean     = _cycleSum / _landed;
            float variance = (_cycleSquaredSum - _landed * mean * mean) / (_landed - 1);
            if (mean <= 0f || variance <= 0f) return 0f;

            return Mathf.Sqrt(variance) / mean;
        }

        private BreathQuality _evaluate(float inhale, float exhale, bool corrected)
        {
            float inhaleError = Mathf.Abs(inhale - _tuning.inhaleSeconds);
            float exhaleError = Mathf.Abs(exhale - _tuning.exhaleSeconds);

            if (!corrected &&
                inhaleError <= _tuning.perfectInhaleTolerance &&
                exhaleError <= _tuning.perfectExhaleTolerance)
                return BreathQuality.Perfect;

            if (inhaleError <= _tuning.goodInhaleTolerance &&
                exhaleError <= _tuning.goodExhaleTolerance)
                return BreathQuality.Good;

            return BreathQuality.OffRhythm;
        }
    }
}
