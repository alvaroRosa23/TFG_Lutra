namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Nivel de ánimo general representado por las 5 caritas del check-in emocional.
    /// Se persiste como int en EmotionRecord.MoodLevel.
    /// </summary>
    public enum MoodLevel
    {
        VeryBad  = 1,
        Bad      = 2,
        Neutral  = 3,
        Good     = 4,
        VeryGood = 5
    }
}
