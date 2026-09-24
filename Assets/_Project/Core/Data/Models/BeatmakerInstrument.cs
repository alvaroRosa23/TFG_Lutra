namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Los 4 estilos de instrumento del minijuego Beatmaker, en el orden en que aparecen
    /// las filas del secuenciador. Cada estilo aporta 2 pistas (variaciones) al patrón
    /// (ver BeatmakerSoundPack.VariationsPerInstrument).
    /// </summary>
    public enum BeatmakerInstrument
    {
        Kick,
        Hat,
        Clap,
        Snare
    }
}
