using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Resumen de la última partida terminada, ya persistida. Lo rellena MinigameLoader
    /// antes de navegar a PostMinigameScreen, que lo lee al recibir el foco.
    /// </summary>
    public class MinigameOutcome
    {
        /// <summary>Sesión guardada en SQLite (con Id asignado; se actualiza con la emoción post-juego).</summary>
        public MinigameSession Session { get; set; }

        public string DisplayName { get; set; }

        /// <summary>Mejor RelaxationScore (0-1) antes de esta partida; null si era la primera.</summary>
        public float? PreviousBestScore { get; set; }

        public bool IsNewRecord { get; set; }

        public int CoinsEarned { get; set; }

        /// <summary>Puntuación 0-100 tal y como se muestra en la UI.</summary>
        public static int ToDisplayScore(float relaxationScore)
            => UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(relaxationScore) * 100f);
    }
}
