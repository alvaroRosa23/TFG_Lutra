using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Acumula la puntuación y métricas de FruitNinja.
    ///
    /// Puntuación = % de elementos cortados sobre los resueltos (cortados + caídos) menos
    /// missPenaltyPoints por cada elemento caído. Durante la partida marca 100 mientras no se
    /// caiga nada y baja con cada caída. Solo una partida sin ninguna caída llega a 100:
    /// cualquier otra se limita a 99. Cada caída también rompe la racha.
    /// </summary>
    public class FruitNinjaMetricsTracker
    {
        private const float MaxScoreIfNotPerfect = 0.99f;

        private readonly FruitNinjaTuning _tuning;

        private int _spawned;
        private int _cut;
        private int _missed;
        private int _specialsCut;
        private int _streak;
        private int _bestStreak;
        private int _maxCombo;
        private int _bigCombos;

        public FruitNinjaMetricsTracker(FruitNinjaTuning tuning) => _tuning = tuning;

        public int  Streak    => _streak;
        public int  Missed    => _missed;
        public bool IsPerfect => _missed == 0 && _cut > 0;

        public void RegisterSpawn() => _spawned++;

        public void RegisterCut(SliceableKind kind)
        {
            _cut++;
            if (kind != SliceableKind.Normal) _specialsCut++;

            _streak++;
            if (_streak > _bestStreak) _bestStreak = _streak;
        }

        public void RegisterMiss()
        {
            _missed++;
            _streak = 0;
        }

        /// <summary>Combo cerrado (elementos cortados en un mismo trazo).</summary>
        public void RegisterCombo(int count)
        {
            if (count > _maxCombo) _maxCombo = count;
            if (count >= _tuning.bigComboThreshold) _bigCombos++;
        }

        /// <summary>0.0-1.0; 1.0 solo si no se ha caído ningún elemento.</summary>
        public float CalculateRelaxationScore()
        {
            int resolved = _cut + _missed;
            if (resolved == 0) return 0f;

            float score = (float)_cut / resolved - _missed * _tuning.missPenaltyPoints / 100f;
            score = Mathf.Clamp01(score);
            return _missed == 0 ? score : Mathf.Min(score, MaxScoreIfNotPerfect);
        }

        /// <summary>Monedas: 1 por cada 10 puntos mostrados (100 puntos = 10 monedas).</summary>
        public int CalculateCoinReward() => MinigameOutcome.ToDisplayScore(CalculateRelaxationScore()) / 10;

        public Dictionary<string, float> BuildMetrics()
        {
            int resolved = _cut + _missed;
            return new Dictionary<string, float>
            {
                ["objects_spawned"] = _spawned,
                ["objects_cut"]     = _cut,
                ["objects_missed"]  = _missed,
                ["specials_cut"]    = _specialsCut,
                ["accuracy"]        = resolved > 0 ? (float)_cut / resolved : 0f,
                ["best_streak"]     = _bestStreak,
                ["max_combo"]       = _maxCombo,
                ["big_combos"]      = _bigCombos
            };
        }
    }
}
