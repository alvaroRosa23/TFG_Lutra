namespace Lutra.Minigames
{
    /// <summary>Fases de un lanzamiento de StarFisher.</summary>
    public enum StarFisherPhase
    {
        Ready,      // esperando a que el jugador mantenga pulsado
        Charging,   // barra de fuerza oscilando
        Casting,    // el anzuelo vuela
        Waiting,    // espera (5-10 s); el anzuelo se guía con el dedo
        Bite,       // ha picado: 2 s para tocar
        Reeling,    // toques para tirar
        Caught,     // destello blanco
        Reveal,     // ficha de la estrella + botón Soltar
        Releasing,  // arrastrar hacia arriba para liberarla
        Released,   // destello blanco al liberarla y vuelta a la partida
        Escaped,    // mensaje de estrella escapada
        Finished    // mensaje final
    }

    /// <summary>Poses del astronauta.</summary>
    public enum AstronautPose { Cast, Wait, Reel }
}
