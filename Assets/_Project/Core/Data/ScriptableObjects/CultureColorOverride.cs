using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    [CreateAssetMenu(menuName = "Lutra/Culture Color Override")]
    public class CultureColorOverride : ScriptableObject
    {
        public CultureType         cultureType;
        public EmotionColorEntry[] emotionOverrides;

        /// <summary>
        /// Devuelve los colores asociados a la emoción indicada para esta cultura.
        /// Retorna false si no hay override definido para esa emoción (usar colores base del EmotionTheme).
        /// </summary>
        public bool TryGetColors(EmotionType emotion, out EmotionColorEntry entry)
        {
            if (emotionOverrides != null)
            {
                foreach (var e in emotionOverrides)
                {
                    if (e != null && e.emotionType == emotion)
                    {
                        entry = e;
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }
    }
}
