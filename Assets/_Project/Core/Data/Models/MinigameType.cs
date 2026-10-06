namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Identificadores de los minijuegos terapéuticos disponibles.
    /// Se usan para cargar la escena correspondiente y para registrar sesiones.
    /// </summary>
    public enum MinigameType
    {
        Unpacking,
        FruitNinja,
        Beatmaker,
        SandCastle,
        FluidSim,
        BreathJump,
        Puzzle,
        StarFisher
    }

    public static class MinigameTypeExtensions
    {
        /// <summary>
        /// Nombre visible en español. Para pantallas con acceso a MinigameDefinition
        /// es preferible su displayName; esto sirve donde no hay definición (estadísticas, informe).
        /// </summary>
        public static string ToDisplayName(this MinigameType type)
        {
            switch (type)
            {
                case MinigameType.Unpacking:  return "Unpacking";
                case MinigameType.FruitNinja: return "Fruit Ninja";
                case MinigameType.Beatmaker:  return "Beatmaker";
                case MinigameType.SandCastle: return "Castillo de Arena";
                case MinigameType.FluidSim:   return "Fluid Simulator";
                case MinigameType.BreathJump: return "Breath Jump";
                case MinigameType.Puzzle:     return "Puzzle";
                case MinigameType.StarFisher: return "Pescador de Estrellas";
                default:                      return type.ToString();
            }
        }
    }
}
