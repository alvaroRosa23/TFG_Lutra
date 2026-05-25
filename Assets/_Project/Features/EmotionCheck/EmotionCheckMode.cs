namespace Lutra.Features.EmotionCheck
{
    /// <summary>
    /// Tipo de registro emocional que el usuario va a realizar.
    ///   Day    → check-in del día      (IsMorningCheck = true)
    ///   Moment → registro del momento  (IsMorningCheck = false)
    /// </summary>
    public enum EmotionCheckMode
    {
        Day,
        Moment
    }
}
