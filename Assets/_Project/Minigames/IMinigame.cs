using System;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Contrato que debe implementar cada minijuego de Lutra.
    /// MinigameLoader usa esta interfaz para operar sobre cualquier minijuego
    /// sin acoplarse a la implementación concreta.
    /// </summary>
    public interface IMinigame
    {
        /// <summary>Identificador del minijuego.</summary>
        MinigameType Type { get; }

        /// <summary>True mientras la partida está en curso (no pausada ni terminada).</summary>
        bool IsPlaying { get; }

        /// <summary>Disparado al finalizar la partida con el resultado completo.</summary>
        event Action<MinigameResult> OnGameCompleted;

        /// <summary>Configura el minijuego con la emoción del usuario antes de jugarlo.</summary>
        void Initialize(EmotionType emotionBefore);

        /// <summary>Inicia la partida. Llamar siempre después de Initialize().</summary>
        void StartGame();

        /// <summary>Pausa la partida sin terminarla.</summary>
        void PauseGame();

        /// <summary>Reanuda una partida pausada.</summary>
        void ResumeGame();

        /// <summary>
        /// Finaliza la partida.
        /// <paramref name="completedNaturally"/> es false si el usuario salió antes de terminar.
        /// </summary>
        void EndGame(bool completedNaturally);
    }
}
