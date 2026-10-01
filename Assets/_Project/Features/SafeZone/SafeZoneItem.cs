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
    /// Qué ocurre al usar un ítem colocado en la habitación (botón extra en su menú contextual).
    /// </summary>
    public enum SafeZoneItemInteraction
    {
        None,
        /// <summary>Telescopio: abre el cielo con las estrellas de la colección.</summary>
        StarSky
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

        [Tooltip("Solo se consigue como recompensa (p. ej. el telescopio): no sale en la tienda ni se puede vender.")]
        public bool isRewardOnly;

        [Header("Interacción")]
        [Tooltip("Acción del botón \"Usar\" en el menú del ítem colocado")]
        public SafeZoneItemInteraction interaction = SafeZoneItemInteraction.None;
        public string interactionLabel = "Mirar las estrellas";

        /// <summary>Se puede vender (tiene precio y no es una recompensa).</summary>
        public bool CanBeSold => coinCost > 0 && !isRewardOnly;
    }
}
