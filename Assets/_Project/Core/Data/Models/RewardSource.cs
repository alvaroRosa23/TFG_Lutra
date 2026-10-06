namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Origen de una recompensa. Se persiste como int en AppNotification.Source: no reordenar.
    /// </summary>
    public enum RewardSource
    {
        None             = 0,   // la notificación no es una recompensa
        CheckIn          = 1,
        StreakMilestone  = 2,
        RewardDefinition = 3,
        Diary            = 4,
        Minigame         = 5,
        StarCollection   = 6,
        Who5             = 7
    }
}
