namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Emociones disponibles en la aplicación.
    /// Cada valor se corresponde con un EmotionTheme ScriptableObject.
    /// </summary>
    public enum EmotionType
    {
        Anxiety     = 0,
        Overwhelm   = 1,
        Frustration = 2,
        Sadness     = 3,
        Nostalgia   = 4,
        Calm        = 5,
        Energy      = 6,
        Joy         = 7
    }

    public static class EmotionTypeExtensions
    {
        /// <summary>Nombre visible en español.</summary>
        public static string ToDisplayName(this EmotionType emotion)
        {
            switch (emotion)
            {
                case EmotionType.Anxiety:     return "Ansiedad";
                case EmotionType.Overwhelm:   return "Agobio";
                case EmotionType.Frustration: return "Frustración";
                case EmotionType.Sadness:     return "Tristeza";
                case EmotionType.Nostalgia:   return "Nostalgia";
                case EmotionType.Calm:        return "Calma";
                case EmotionType.Energy:      return "Energía";
                case EmotionType.Joy:         return "Alegría";
                default:                      return emotion.ToString();
            }
        }
    }
}
