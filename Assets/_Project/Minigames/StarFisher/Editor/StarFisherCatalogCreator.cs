#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.Features.SafeZone;

namespace Lutra.Minigames.EditorTools
{
    /// <summary>
    /// Menú Lutra > StarFisher > Crear catálogo de ejemplo: crea las 30 estrellas normales
    /// (12 comunes, 8 poco comunes, 5 raras, 3 épicas, 2 legendarias), la estrella de racha
    /// (nº 31), el StarCatalog y el SafeZoneItem del telescopio. Los textos y el arte quedan
    /// por rellenar. No sobrescribe assets que ya existan.
    /// (Está dentro de LutraCore, por eso va entero en #if UNITY_EDITOR.)
    /// </summary>
    public static class StarFisherCatalogCreator
    {
        private const string Root          = "Assets/_Project/Core/Data/ScriptableObjects/StarFisher";
        private const string StarsFolder   = Root + "/Stars";
        private const string CatalogPath   = Root + "/StarCatalog.asset";
        private const string TelescopePath = "Assets/_Project/Core/Data/ScriptableObjects/SafeZoneItems/Telescope.asset";

        private static readonly (StarRarity rarity, int count, string idPrefix)[] Distribution =
        {
            (StarRarity.Common,    12, "common"),
            (StarRarity.Uncommon,   8, "uncommon"),
            (StarRarity.Rare,       5, "rare"),
            (StarRarity.Epic,       3, "epic"),
            (StarRarity.Legendary,  2, "legendary")
        };

        [MenuItem("Lutra/StarFisher/Crear catálogo de ejemplo")]
        public static void CreateSampleCatalog()
        {
            Directory.CreateDirectory(StarsFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<StarCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StarCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var stars = new List<StarDefinition>();
            foreach (var (rarity, count, idPrefix) in Distribution)
            {
                for (int i = 1; i <= count; i++)
                {
                    stars.Add(_loadOrCreateStar($"star_{idPrefix}_{i:00}",
                                                $"Estrella {rarity.ToDisplayName().ToLowerInvariant()} {i}",
                                                rarity, catalog.GetRarityColor(rarity), streakSpecial: false));
                }
            }

            stars.Add(_loadOrCreateStar("star_streak", "Estrella de la constancia", StarRarity.Legendary,
                                        new Color(1f, 0.6f, 0.9f), streakSpecial: true));

            catalog.stars = stars.ToArray();
            EditorUtility.SetDirty(catalog);

            _createTelescope(catalog.telescopeItemId);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;
            Debug.Log($"[StarFisherCatalogCreator] Catálogo listo en {CatalogPath} ({stars.Count} estrellas)");
        }

        private static StarDefinition _loadOrCreateStar(string starId, string displayName, StarRarity rarity,
                                                        Color tint, bool streakSpecial)
        {
            string path = $"{StarsFolder}/{starId}.asset";
            var star = AssetDatabase.LoadAssetAtPath<StarDefinition>(path);
            if (star != null) return star;

            star = ScriptableObject.CreateInstance<StarDefinition>();
            star.starId          = starId;
            star.displayName     = displayName;
            star.rarity          = rarity;
            star.isStreakSpecial = streakSpecial;
            star.tint            = tint;
            star.weight          = "Por definir";
            star.age             = "Por definir";
            star.description     = "Descripción, vida e historia por escribir.";
            star.phrase          = "Frase por escribir.";
            AssetDatabase.CreateAsset(star, path);
            return star;
        }

        private static void _createTelescope(string itemId)
        {
            if (AssetDatabase.LoadAssetAtPath<SafeZoneItem>(TelescopePath) != null) return;

            var telescope = ScriptableObject.CreateInstance<SafeZoneItem>();
            telescope.itemId            = itemId;
            telescope.displayName       = "Telescopio";
            telescope.coinCost          = 0;
            telescope.unlockDescription = "Completa la colección de estrellas";
            telescope.category          = ItemCategory.Furniture;
            telescope.placementType     = PlacementType.Furniture;
            telescope.isRewardOnly      = true;
            telescope.interaction       = SafeZoneItemInteraction.StarSky;
            telescope.interactionLabel  = "Mirar las estrellas";
            AssetDatabase.CreateAsset(telescope, TelescopePath);
        }
    }
}
#endif
