using UnityEngine;

namespace Lutra.Features.Diary
{
    /// <summary>
    /// ScriptableObject con los prompts de escritura que se sugieren al usuario
    /// al abrir el editor de diario. Configurable desde el Inspector.
    /// Crear desde Assets > Create > Lutra > Diary Prompts.
    /// </summary>
    [CreateAssetMenu(fileName = "DiaryPrompts", menuName = "Lutra/Diary Prompts")]
    public class DiaryEntryPromptData : ScriptableObject
    {
        [TextArea(1, 2)]
        [SerializeField] private string[] _prompts =
        {
            "¿Qué te ha sorprendido hoy?",
            "¿Qué te ha costado más?",
            "¿De qué te sientes orgulloso/a hoy?",
            "¿Qué persona ha marcado tu día?",
            "¿Qué harías diferente?",
            "¿Qué emoción ha dominado tu día?",
            "¿Qué te ha hecho sonreír?",
            "¿Qué aprendiste hoy?",
            "¿Qué necesitas soltar?",
            "¿Qué te gustaría recordar de hoy?"
        };

        /// <summary>
        /// Devuelve un prompt aleatorio del array.
        /// Devuelve string vacío si el array está vacío.
        /// </summary>
        public string GetRandomPrompt()
        {
            if (_prompts == null || _prompts.Length == 0)
                return string.Empty;

            return _prompts[Random.Range(0, _prompts.Length)];
        }
    }
}
