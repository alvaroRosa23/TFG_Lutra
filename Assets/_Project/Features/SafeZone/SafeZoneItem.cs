using UnityEngine;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Categorías de items de la Zona Segura.
    /// </summary>
    public enum ItemCategory
    {
        Furniture,
        Plant,
        Decoration,
        WallItem,
        MascotAccessory
    }

    /// <summary>
    /// Define un item desbloqueable de la Zona Segura.
    /// Crear desde Assets > Create > Lutra > SafeZone Item.
    /// </summary>
    [CreateAssetMenu(fileName = "SafeZoneItem", menuName = "Lutra/SafeZone Item")]
    public class SafeZoneItem : ScriptableObject
    {
        [Header("Identidad")]
        public string itemId;
        public string displayName;

        [Header("Visual")]
        public Sprite     previewSprite;
        public GameObject prefab;

        [Header("Economía")]
        [Tooltip("Coste en monedas. Se ignora si requiredStreakDays > 0.")]
        public int coinCost;

        [Tooltip("Días de racha necesarios para desbloquearlo gratuitamente. 0 = solo monedas.")]
        public int requiredStreakDays;

        [Tooltip("Texto explicativo del método de desbloqueo mostrado en la tienda.")]
        [TextArea(1, 2)]
        public string unlockDescription;

        [Header("Clasificación")]
        public ItemCategory  category;
        public PlacementType placementType;

        [Tooltip("Si es true el item está disponible desde el inicio sin coste.")]
        public bool isUnlockedByDefault;
    }
}
