using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Core.Data.ScriptableObjects
{
    /// <summary>
    /// Define la identidad visual y sonora de una emoción.
    /// Crear un asset por cada valor de EmotionType desde
    /// Assets > Create > Lutra > Emotion Theme.
    /// </summary>
    [CreateAssetMenu(fileName = "EmotionTheme", menuName = "Lutra/Emotion Theme")]
    public class EmotionTheme : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("Emoción a la que corresponde este tema.")]
        public EmotionType emotionType;

        [Tooltip("Nombre localizado que se muestra al usuario.")]
        public string displayName;

        [Header("Colores")]
        public Color primaryColor    = Color.white;
        public Color backgroundColor = Color.white;

        [Header("Mascota")]
        [Tooltip("Sprite de expresión facial del búho para esta emoción.")]
        public Sprite mascotExpression;

        [Tooltip("Override del Animator para las animaciones específicas de esta emoción.")]
        public AnimatorOverrideController mascotAnimatorOverride;

        [Header("Ambiente")]
        [Tooltip("Música/sonido ambiental en loop para esta emoción.")]
        public AudioClip ambientLoop;

        [Range(0f, 1f)]
        [Tooltip("Volumen del loop ambiental.")]
        public float ambientVolume = 0.3f;

        [Tooltip("Sistema de partículas que acompaña al fondo dinámico.")]
        public GameObject ambientParticlesPrefab;

        [Header("Transición")]
        [Tooltip("Duración en segundos del fade/blend al cambiar al tema de esta emoción.")]
        public float transitionDuration = 0.8f;
    }
}
