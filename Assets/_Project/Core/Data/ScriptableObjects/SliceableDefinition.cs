using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    /// <summary>
    /// Aspecto de un elemento cortable del minijuego FruitNinja (la temática está por decidir).
    /// Asignar los assets en FruitNinjaField: los Normal se eligen al azar y, para cada tipo
    /// especial, el primero que coincida con su kind. Si no hay ninguno, se genera un círculo
    /// de color en tiempo de ejecución.
    /// </summary>
    [CreateAssetMenu(menuName = "Lutra/Sliceable Definition", fileName = "SliceableDefinition")]
    public class SliceableDefinition : ScriptableObject
    {
        public string        id;
        public SliceableKind kind = SliceableKind.Normal;

        [Tooltip("Si se deja vacío se usa un círculo generado")]
        public Sprite sprite;
        public Color  color = Color.white;

        [Range(0.5f, 2f)]
        [Tooltip("Multiplicador sobre el diámetro base (FruitNinjaTuning.baseDiameter)")]
        public float sizeMultiplier = 1f;
    }
}
