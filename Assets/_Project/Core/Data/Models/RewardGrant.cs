namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Recompensa recién entregada (en memoria, no SQLite). Quien entrega monedas o ítems la emite
    /// con EventBus.EmitRewardGranted justo después de guardarlos, y NotificationCenter la convierte
    /// en una notificación. La reconciliación con Firestore no emite nada: no es una recompensa nueva.
    /// </summary>
    public class RewardGrant
    {
        public RewardSource Source    { get; set; }
        public int          Coins     { get; set; }
        public string       ItemId    { get; set; }   // nullable
        public string       Title     { get; set; }   // "¡Racha de 7 días!"
        public string       Body      { get; set; }   // "+20 monedas"
        public string       SourceRef { get; set; }   // nullable: MinigameType, rewardId…
    }
}
