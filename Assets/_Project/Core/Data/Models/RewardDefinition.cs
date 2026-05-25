using UnityEngine;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Definición de una recompensa. Configura cuándo se otorga,
    /// qué entrega (monedas o ítem) y si se puede repetir.
    ///
    /// Crear instancias en Assets/_Project/Core/Data/ScriptableObjects/Rewards/
    /// vía menú "Lutra/Reward Definition".
    /// </summary>
    [CreateAssetMenu(menuName = "Lutra/Reward Definition")]
    public class RewardDefinition : ScriptableObject
    {
        [Header("Identificación")]
        public string     rewardId;
        public string     displayName;
        [TextArea(2, 4)]
        public string     description;
        public RewardType type;
        public Sprite     icon;

        [Header("Entrega")]
        [Tooltip("Monedas que otorga. 0 si la recompensa es un ítem.")]
        public int    coinValue;

        [Tooltip("itemId del SafeZoneItem que se desbloquea. Vacío si la recompensa es solo monedas.")]
        public string itemId;

        [Header("Condiciones de desbloqueo")]
        [Tooltip("Días de racha necesarios para activar esta recompensa. 0 = no es recompensa de racha.")]
        public int  requiredStreakDays;

        [Tooltip("Número exacto de check-ins necesarios. 0 = no requiere check-ins.")]
        public int  requiredCheckIns;

        [Tooltip("Si es false, la recompensa solo se otorga una vez por perfil.")]
        public bool isRepeatable;
    }
}
