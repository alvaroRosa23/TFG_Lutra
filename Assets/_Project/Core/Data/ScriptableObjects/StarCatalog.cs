using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    /// <summary>
    /// Catálogo de estrellas de StarFisher (30 normales + la especial de racha) y colores de
    /// cada rareza. Lo usan el minijuego, el libro de colección y el telescopio de SafeZone.
    /// Crear desde Assets > Create > Lutra > Star Catalog (o con Lutra > StarFisher > Crear catálogo de ejemplo).
    /// </summary>
    [CreateAssetMenu(menuName = "Lutra/Star Catalog", fileName = "StarCatalog")]
    public class StarCatalog : ScriptableObject
    {
        public StarDefinition[] stars;

        [Tooltip("Color de cada rareza: Común, Poco común, Rara, Épica, Legendaria")]
        public Color[] rarityColors =
        {
            new Color(0.92f, 0.92f, 0.95f),
            new Color(0.35f, 0.85f, 0.45f),
            new Color(0.30f, 0.60f, 1.00f),
            new Color(0.70f, 0.40f, 1.00f),
            new Color(1.00f, 0.80f, 0.25f)
        };

        [Tooltip("itemId del SafeZoneItem que se regala al completar la colección")]
        public string telescopeItemId = "telescope";

        public Color GetRarityColor(StarRarity rarity)
        {
            int index = (int)rarity;
            return rarityColors != null && index < rarityColors.Length ? rarityColors[index] : Color.white;
        }

        public StarDefinition Find(string starId)
        {
            if (stars == null || string.IsNullOrEmpty(starId)) return null;
            foreach (var star in stars)
                if (star != null && star.starId == starId) return star;
            return null;
        }

        /// <summary>Estrellas del sorteo normal (las que cuentan para completar la colección).</summary>
        public List<StarDefinition> GetRegularStars()
        {
            var result = new List<StarDefinition>();
            if (stars == null) return result;
            foreach (var star in stars)
                if (star != null && !star.isStreakSpecial) result.Add(star);
            return result;
        }

        /// <summary>La estrella de racha; null si no está en el catálogo.</summary>
        public StarDefinition GetStreakStar()
        {
            if (stars == null) return null;
            foreach (var star in stars)
                if (star != null && star.isStreakSpecial) return star;
            return null;
        }
    }
}
