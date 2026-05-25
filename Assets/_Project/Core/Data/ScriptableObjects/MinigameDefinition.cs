using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    /// <summary>
    /// Datos de configuración de un minijuego. Crear un asset por minijuego
    /// y añadirlo al array _allMinigames del MinigamesController.
    /// </summary>
    [CreateAssetMenu(menuName = "Lutra/Minigame Definition", fileName = "MinigameDefinition")]
    public class MinigameDefinition : ScriptableObject
    {
        public MinigameType minigameType;
        public string       displayName;
        [TextArea]
        public string       description;
        public Sprite       logo;
        public Sprite       previewImage;
        public MinigameTag[] tags;
        public int          estimatedTimeSeconds;
        public bool         isAvailable;
    }
}
