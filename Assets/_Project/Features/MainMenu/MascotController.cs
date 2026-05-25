using System.Collections;
using UnityEngine;
using Lutra.Core.Data.Models;
using Lutra.Core.Events;

namespace Lutra.Features.MainMenu
{
    /// <summary>
    /// Controla el búho animado del menú principal.
    /// Recibe cambios de emoción vía EventBus y lanza variaciones de idle aleatorias
    /// para dar vida a la mascota sin intervención manual.
    ///
    /// Requiere un Animator con los parámetros: Emotion (int), Intensity (int),
    /// IdleVariant (int), Reaction (trigger).
    /// </summary>
    public class MascotController : MonoBehaviour
    {
        // ── Hashes cacheados (evitan búsqueda por string en runtime) ───

        private static readonly int EmotionParam      = Animator.StringToHash("Emotion");
        private static readonly int IntensityParam    = Animator.StringToHash("Intensity");
        private static readonly int IdleVariantParam  = Animator.StringToHash("IdleVariant");
        private static readonly int ReactionTrigger   = Animator.StringToHash("Reaction");

        // ── Referencias ────────────────────────────────────────────────

        [SerializeField] private Animator _animator;

        // ── Estado interno ─────────────────────────────────────────────

        private Coroutine _idleCoroutine;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            // Fallback: buscar Animator en el mismo GameObject
            if (_animator == null)
                _animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            EventBus.OnCurrentEmotionChanged += _onEmotionChanged;
            _idleCoroutine = StartCoroutine(_randomIdleVariations());
        }

        private void OnDisable()
        {
            EventBus.OnCurrentEmotionChanged -= _onEmotionChanged;

            if (_idleCoroutine != null)
            {
                StopCoroutine(_idleCoroutine);
                _idleCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_idleCoroutine != null)
                {
                    StopCoroutine(_idleCoroutine);
                    _idleCoroutine = null;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MascotController] Error al limpiar coroutine en OnDestroy: {ex.Message}");
            }
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Actualiza la animación del búho según la emoción e intensidad indicadas.
        /// intensity: 1 (muy baja) – 5 (muy alta).
        /// </summary>
        public void SetEmotion(EmotionType emotion, int intensity = 3)
        {
            if (_animator == null) return;

            _animator.SetInteger(EmotionParam,   (int)emotion);
            _animator.SetInteger(IntensityParam, Mathf.Clamp(intensity, 1, 5));
        }

        /// <summary>
        /// Lanza una animación de reacción puntual (p.ej. "Happy", "Surprised", "Wave").
        /// El nombre debe coincidir con un estado accesible desde el trigger Reaction
        /// en el Animator Controller.
        /// </summary>
        public void PlayReaction(string reactionType)
        {
            if (_animator == null) return;

            // El parámetro reactionType se pasa como string hash al Animator
            // para seleccionar el sub-estado correcto antes de activar el trigger
            _animator.SetInteger(ReactionTrigger, Animator.StringToHash(reactionType));
            _animator.SetTrigger(ReactionTrigger);
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Cada 5-12 segundos aleatoriza IdleVariant (0-2) para que el búho
        /// realice micro-animaciones de idle variadas de forma orgánica.
        /// </summary>
        private IEnumerator _randomIdleVariations()
        {
            while (true)
            {
                float waitTime = Random.Range(5f, 12f);
                yield return new WaitForSeconds(waitTime);

                if (_animator != null)
                    _animator.SetInteger(IdleVariantParam, Random.Range(0, 3));
            }
        }

        /// <summary>Callback del EventBus: actualiza la emoción con intensidad media por defecto.</summary>
        private void _onEmotionChanged(EmotionType emotion)
        {
            SetEmotion(emotion);
        }
    }
}
