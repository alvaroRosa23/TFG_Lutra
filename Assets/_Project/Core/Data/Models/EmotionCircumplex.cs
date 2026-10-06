namespace Lutra.Core.Data.Models
{
    public enum Valence { Unpleasant, Mixed, Pleasant }

    public enum Arousal { Low, High }

    /// <summary>Cuadrantes del modelo circumplejo; Nostalgia (valencia mixta) va aparte.</summary>
    public enum AffectQuadrant
    {
        Tension,     // desagradable + activación alta
        LowMood,     // desagradable + activación baja ("decaimiento")
        Calm,        // agradable + activación baja
        Enthusiasm,  // agradable + activación alta
        Mixed        // valencia mixta (Nostalgia)
    }

    /// <summary>
    /// Valencia y activación derivadas de la emoción elegida según el modelo circumplejo del afecto
    /// (Russell, 1980). Es una aproximación teórica, no una medida: Lutra no pregunta la activación.
    /// Ver docs/METRICS.md §3.1.
    /// </summary>
    public static class EmotionCircumplex
    {
        public static Valence ValenceOf(this EmotionType emotion)
        {
            switch (emotion)
            {
                case EmotionType.Anxiety:
                case EmotionType.Overwhelm:
                case EmotionType.Frustration:
                case EmotionType.Sadness:    return Valence.Unpleasant;
                case EmotionType.Nostalgia:  return Valence.Mixed;
                default:                     return Valence.Pleasant;   // Calm, Energy, Joy
            }
        }

        public static Arousal ArousalOf(this EmotionType emotion)
        {
            switch (emotion)
            {
                case EmotionType.Anxiety:
                case EmotionType.Overwhelm:
                case EmotionType.Frustration:
                case EmotionType.Energy:
                case EmotionType.Joy:        return Arousal.High;
                default:                     return Arousal.Low;        // Sadness, Nostalgia, Calm
            }
        }

        public static AffectQuadrant QuadrantOf(this EmotionType emotion)
        {
            Valence valence = emotion.ValenceOf();
            if (valence == Valence.Mixed) return AffectQuadrant.Mixed;

            bool high = emotion.ArousalOf() == Arousal.High;
            return valence == Valence.Unpleasant
                ? (high ? AffectQuadrant.Tension    : AffectQuadrant.LowMood)
                : (high ? AffectQuadrant.Enthusiasm : AffectQuadrant.Calm);
        }

        /// <summary>Nombre visible en español.</summary>
        public static string ToDisplayName(this AffectQuadrant quadrant)
        {
            switch (quadrant)
            {
                case AffectQuadrant.Tension:    return "Tensión";
                case AffectQuadrant.LowMood:    return "Decaimiento";
                case AffectQuadrant.Calm:       return "Calma";
                case AffectQuadrant.Enthusiasm: return "Entusiasmo";
                default:                        return "Mixta";
            }
        }
    }
}
