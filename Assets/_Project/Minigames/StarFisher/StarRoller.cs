using System;
using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Minigames
{
    /// <summary>
    /// Sorteo de StarFisher (lógica pura). Primero la rareza, con pesos base que suben para las
    /// rarezas altas según la bonificación del lanzamiento; después la estrella dentro de esa
    /// rareza, con más peso para las no descubiertas.
    ///
    /// bonificación = distancia × distanceBonus + (clavado ? perfectCastBonus : 0) + zonas × glowBonus
    /// peso(rareza r) = pesoBase(r) × (1 + bonificación × r × rarityBoostStrength)
    /// </summary>
    public class StarRoller
    {
        private readonly StarFisherTuning _tuning;
        private readonly System.Random    _random;

        public StarRoller(StarFisherTuning tuning, int seed)
        {
            _tuning = tuning;
            _random = new System.Random(seed);
        }

        /// <param name="power">Fuerza del lanzamiento 0-1 (distancia)</param>
        /// <param name="glowFactor">Tiempo en zonas brillantes normalizado 0-1</param>
        public float CalculateBonus(float power, bool perfect, float glowFactor)
        {
            return Mathf.Clamp01(power) * _tuning.distanceBonus
                 + (perfect ? _tuning.perfectCastBonus : 0f)
                 + Mathf.Clamp01(glowFactor) * _tuning.glowBonus;
        }

        /// <summary>Probabilidad (0-1) de cada rareza con una bonificación dada.</summary>
        public float[] GetChances(float bonus)
        {
            var chances = new float[StarRarityExtensions.Count];
            float total = 0f;

            for (int r = 0; r < chances.Length; r++)
            {
                float baseWeight = _tuning.rarityWeights != null && r < _tuning.rarityWeights.Length
                    ? Mathf.Max(0f, _tuning.rarityWeights[r])
                    : 0f;
                chances[r] = baseWeight * (1f + bonus * r * _tuning.rarityBoostStrength);
                total += chances[r];
            }

            for (int r = 0; r < chances.Length; r++)
                chances[r] = total > 0f ? chances[r] / total : (r == 0 ? 1f : 0f);

            return chances;
        }

        public StarRarity RollRarity(float bonus)
        {
            var chances = GetChances(bonus);
            double roll = _random.NextDouble();

            for (int r = 0; r < chances.Length; r++)
            {
                roll -= chances[r];
                if (roll < 0) return (StarRarity)r;
            }
            return StarRarity.Common;
        }

        /// <summary>
        /// Elige una estrella de la rareza pedida. Si no hay ninguna de esa rareza en el catálogo
        /// se busca en la más cercana (primero hacia abajo). Devuelve null si no hay estrellas.
        /// </summary>
        public StarDefinition PickStar(StarRarity rarity, IReadOnlyList<StarDefinition> stars,
                                       Func<string, bool> isDiscovered)
        {
            if (stars == null || stars.Count == 0) return null;

            for (int distance = 0; distance < StarRarityExtensions.Count; distance++)
            {
                var star = _pickFromRarity((int)rarity - distance, stars, isDiscovered)
                        ?? _pickFromRarity((int)rarity + distance, stars, isDiscovered);
                if (star != null) return star;
            }
            return null;
        }

        public float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private StarDefinition _pickFromRarity(int rarity, IReadOnlyList<StarDefinition> stars,
                                               Func<string, bool> isDiscovered)
        {
            if (rarity < 0 || rarity >= StarRarityExtensions.Count) return null;

            float total = 0f;
            foreach (var star in stars)
                if (star != null && (int)star.rarity == rarity) total += _weight(star, isDiscovered);

            if (total <= 0f) return null;

            double roll = _random.NextDouble() * total;
            StarDefinition last = null;
            foreach (var star in stars)
            {
                if (star == null || (int)star.rarity != rarity) continue;
                last = star;
                roll -= _weight(star, isDiscovered);
                if (roll < 0) return star;
            }
            return last;
        }

        private float _weight(StarDefinition star, Func<string, bool> isDiscovered)
            => isDiscovered != null && !isDiscovered(star.starId) ? Mathf.Max(1f, _tuning.undiscoveredWeight) : 1f;
    }
}
