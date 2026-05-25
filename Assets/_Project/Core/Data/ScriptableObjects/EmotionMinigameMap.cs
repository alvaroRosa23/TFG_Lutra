using System;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    /// <summary>
    /// Mapeo de emoción → etiquetas terapéuticas recomendadas.
    /// Configurar en el Inspector: un EntryMap por cada EmotionType.
    /// </summary>
    [CreateAssetMenu(menuName = "Lutra/Emotion Minigame Map", fileName = "EmotionMinigameMap")]
    public class EmotionMinigameMap : ScriptableObject
    {
        [Serializable]
        public struct EntryMap
        {
            public EmotionType  emotion;
            public MinigameTag[] recommendedTags;
        }

        [SerializeField] private EntryMap[] _entries;

        /// <summary>
        /// Devuelve las etiquetas recomendadas para la emoción dada.
        /// Devuelve array vacío si no hay entrada para esa emoción.
        /// </summary>
        public MinigameTag[] GetRecommendedTags(EmotionType emotion)
        {
            if (_entries == null) return Array.Empty<MinigameTag>();

            foreach (var entry in _entries)
                if (entry.emotion == emotion)
                    return entry.recommendedTags ?? Array.Empty<MinigameTag>();

            return Array.Empty<MinigameTag>();
        }
    }
}
