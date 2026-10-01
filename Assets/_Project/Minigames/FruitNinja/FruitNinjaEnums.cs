using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>Fase de la partida: el ritmo baja de Descarga a Calma.</summary>
    public enum FruitNinjaPhase
    {
        Release,     // descarga: muchos elementos y rápidos
        Transition,  // el ritmo baja poco a poco
        Calm         // pocos elementos, grandes y lentos
    }

    /// <summary>Borde de la pantalla por el que entra un elemento.</summary>
    public enum SpawnOrigin
    {
        Bottom,
        Top,
        Left,
        Right
    }

    /// <summary>
    /// Petición de lanzamiento que genera FruitNinjaSpawnPlanner y ejecuta FruitNinjaField.
    /// Toda la aleatoriedad va aquí, así el campo es determinista a partir de la petición.
    /// </summary>
    public struct SpawnRequest
    {
        public SpawnOrigin   origin;
        public SliceableKind kind;
        public float flightTime;     // s: subir y volver a la altura de salida (caída completa si viene de arriba)
        public float sizeScale;      // multiplicador del diámetro base
        public float lane;           // Bottom/Top: posición horizontal (-1..1); Left/Right: altura de salida (0..1)
        public float apexFraction;   // altura máxima como fracción de la pantalla (desde abajo)
        public float drift;          // -1..1: desvío horizontal de la trayectoria
        public float spin;           // grados/s
    }
}
