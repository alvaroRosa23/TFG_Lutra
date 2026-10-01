using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    /// <summary>
    /// Una estrella del minijuego StarFisher: arte, rareza y ficha (peso, edad, historia y frase).
    /// Crear desde Assets > Create > Lutra > Star Definition y añadirla al StarCatalog.
    /// El starId se guarda en SQLite y Firestore: no cambiarlo una vez publicado.
    /// </summary>
    [CreateAssetMenu(menuName = "Lutra/Star Definition", fileName = "Star")]
    public class StarDefinition : ScriptableObject
    {
        [Header("Identidad")]
        public string     starId;
        public string     displayName;
        public StarRarity rarity = StarRarity.Common;

        [Tooltip("Solo la estrella de racha (nº 31): no entra en el sorteo normal ni cuenta para completar la colección")]
        public bool isStreakSpecial;

        [Header("Arte")]
        [Tooltip("Si se deja vacío se usa una estrella generada con el color de abajo")]
        public Sprite sprite;
        public Color  tint = Color.white;

        [Header("Ficha")]
        [Tooltip("Texto libre, p. ej. \"3,2 masas solares\"")]
        public string weight;
        [Tooltip("Texto libre, p. ej. \"4.600 millones de años\"")]
        public string age;
        [TextArea(3, 8)]
        [Tooltip("Descripción, vida e historia de la estrella")]
        public string description;
        [TextArea(2, 4)]
        [Tooltip("Frase con la que el jugador puede identificarse")]
        public string phrase;
    }
}
