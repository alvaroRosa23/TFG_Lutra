namespace Lutra.Minigames
{
    /// <summary>Valoración de cada respiración (un salto).</summary>
    public enum BreathQuality
    {
        Perfect,    // ritmo 4-6 dentro del margen perfecto y sin corregir en el aire
        Good,       // dentro del margen amplio (puede haber corregido en el aire)
        OffRhythm,  // aterrizó, pero lejos del ritmo
        Missed      // no llegó a la plataforma
    }

    /// <summary>Indicación que muestra el círculo guía.</summary>
    public enum BreathGuidePhase
    {
        Ready,      // mantén pulsado e inspira
        Inhale,     // inspirando
        Release,    // inspiración completa: suelta y espira
        Exhale,     // espirando (en el aire o ya en la plataforma)
        Goal        // meta alcanzada
    }
}
