using System;
using System.Collections.Generic;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Resultado de una sesión de minijuego. Contiene métricas de rendimiento
    /// y datos necesarios para persistir la MinigameSession y mostrarlo en PostMinigameScreen.
    /// Se genera al finalizar la partida y lo emite MinigameBase a través de OnGameCompleted.
    /// </summary>
    public class MinigameResult
    {
        public MinigameType Type              { get; set; }
        public int          DurationSeconds   { get; set; }
        public float        RelaxationScore   { get; set; }   // 0.0 – 1.0
        public bool         CompletedNaturally { get; set; }
        public EmotionType  EmotionBefore     { get; set; }
        public EmotionType  EmotionAfter      { get; set; }

        /// <summary>Métricas específicas del minijuego (p.ej. "objectsUnpacked", "fruitsSliced").</summary>
        public Dictionary<string, float> Metrics { get; set; } = new();

        public DateTime StartTime { get; set; }

        public MinigameResult(MinigameType type, EmotionType emotionBefore)
        {
            Type         = type;
            EmotionBefore = emotionBefore;
            EmotionAfter  = emotionBefore; // valor por defecto hasta que el usuario lo actualice
            StartTime    = DateTime.Now;
        }
    }
}
