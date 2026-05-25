namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Categorías de recompensas que puede ganar el usuario.
    /// Usadas por RewardSystem para clasificar los ítems desbloqueables.
    /// </summary>
    public enum RewardType
    {
        /// <summary>Elemento cosmético general.</summary>
        Cosmetic,

        /// <summary>Objeto decorativo para la Zona Segura.</summary>
        RoomDecoration,

        /// <summary>Accesorio visual para la mascota búho.</summary>
        MascotAccessory,

        /// <summary>Animación especial de la mascota.</summary>
        SpecialAnimation
    }
}
