using System;
using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;

namespace Lutra.Features.EmotionCheck
{
    /// <summary>
    /// Recomienda minijuegos terapéuticos según la emoción registrada.
    /// Los mappings se configuran en el Inspector para que el equipo de diseño
    /// pueda ajustar las recomendaciones sin tocar código.
    ///
    /// Setup: asignar un MinigameRecommendationData por cada EmotionType en _mappings.
    /// </summary>
    public class EmotionRecommender : BaseService
    {
        [SerializeField] private MinigameRecommendationData[] _mappings;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake() { }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la lista de minijuegos recomendados para la emoción indicada,
        /// primero los primarios y luego los secundarios.
        /// Devuelve lista vacía si no hay mapping configurado para esa emoción.
        /// </summary>
        public List<MinigameType> GetRecommendations(EmotionType emotion)
        {
            var result = new List<MinigameType>();

            if (_mappings == null) return result;

            foreach (var mapping in _mappings)
            {
                if (mapping.emotion != emotion) continue;

                if (mapping.primaryRecommendations != null)
                    result.AddRange(mapping.primaryRecommendations);

                if (mapping.secondaryRecommendations != null)
                    result.AddRange(mapping.secondaryRecommendations);

                break;
            }

            return result;
        }

        /// <summary>
        /// Devuelve el minijuego más recomendado para la emoción indicada.
        /// Si no hay recomendaciones devuelve el valor por defecto del enum (Unpacking).
        /// </summary>
        public MinigameType GetTopRecommendation(EmotionType emotion)
        {
            var recommendations = GetRecommendations(emotion);
            return recommendations.Count > 0 ? recommendations[0] : default;
        }

        // ── Clase interna ──────────────────────────────────────────────

        /// <summary>
        /// Asocia una emoción con sus minijuegos recomendados.
        /// Configurable desde el Inspector del componente EmotionRecommender.
        /// </summary>
        [Serializable]
        public class MinigameRecommendationData
        {
            public EmotionType   emotion;

            [Tooltip("Minijuegos más adecuados para esta emoción (se muestran primero).")]
            public MinigameType[] primaryRecommendations;

            [Tooltip("Minijuegos alternativos si el usuario prefiere más opciones.")]
            public MinigameType[] secondaryRecommendations;
        }
    }
}
