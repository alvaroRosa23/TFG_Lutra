using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Puntuación y métricas de StarFisher.
    /// Puntuación = estrellas pescadas / lanzamientos (5 de 5 = 100). Una estrella que se escapa
    /// (al picar o durante la recogida) resta su parte.
    /// Monedas = suma por rareza (Común y Poco común 1, Rara 3, Épica y Legendaria 6) hasta un tope de 10.
    /// </summary>
    public class StarFisherScoring
    {
        private readonly StarFisherTuning _tuning;

        private int   _caught;
        private int   _escapedAtBite;
        private int   _escapedWhileReeling;
        private int   _newStars;
        private int   _perfectCasts;
        private int   _coins;
        private int   _bestRarity = -1;
        private float _glowSeconds;
        private bool  _streakStarCaught;

        public StarFisherScoring(StarFisherTuning tuning) => _tuning = tuning;

        public int Caught => _caught;
        public int Lost   => _escapedAtBite + _escapedWhileReeling;

        public void RegisterPerfectCast()        => _perfectCasts++;
        public void AddGlowSeconds(float seconds) => _glowSeconds += seconds;

        public void RegisterCatch(StarRarity rarity, bool isNew, bool isStreakStar)
        {
            _caught++;
            if (isNew) _newStars++;
            if (isStreakStar) _streakStarCaught = true;
            _bestRarity = Mathf.Max(_bestRarity, (int)rarity);
            _coins += _tuning.GetCoins((int)rarity);
        }

        public void RegisterEscape(bool atBite)
        {
            if (atBite) _escapedAtBite++;
            else        _escapedWhileReeling++;
        }

        public float CalculateRelaxationScore()
            => _tuning.casts > 0 ? Mathf.Clamp01((float)_caught / _tuning.casts) : 0f;

        public int CalculateCoinReward() => Mathf.Min(_coins, _tuning.maxCoins);

        public Dictionary<string, float> BuildMetrics() => new Dictionary<string, float>
        {
            ["stars_caught"]          = _caught,
            ["stars_escaped_bite"]    = _escapedAtBite,
            ["stars_escaped_reeling"] = _escapedWhileReeling,
            ["new_stars"]             = _newStars,
            ["perfect_casts"]         = _perfectCasts,
            ["best_rarity"]           = _bestRarity,
            ["glow_seconds"]          = _glowSeconds,
            ["streak_star"]           = _streakStarCaught ? 1f : 0f
        };
    }
}
