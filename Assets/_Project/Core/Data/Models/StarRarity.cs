namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Rareza de una estrella del minijuego StarFisher. El orden importa: se usa como índice
    /// en las tablas de probabilidades, toques y monedas (de más común a más rara).
    /// </summary>
    public enum StarRarity
    {
        Common    = 0,
        Uncommon  = 1,
        Rare      = 2,
        Epic      = 3,
        Legendary = 4
    }

    public static class StarRarityExtensions
    {
        public const int Count = 5;

        /// <summary>Nombre visible en español.</summary>
        public static string ToDisplayName(this StarRarity rarity)
        {
            switch (rarity)
            {
                case StarRarity.Common:    return "Común";
                case StarRarity.Uncommon:  return "Poco común";
                case StarRarity.Rare:      return "Rara";
                case StarRarity.Epic:      return "Épica";
                case StarRarity.Legendary: return "Legendaria";
                default:                   return rarity.ToString();
            }
        }
    }
}
