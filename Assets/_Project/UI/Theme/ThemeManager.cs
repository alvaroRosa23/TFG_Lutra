using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.Core.Events;

namespace Lutra.UI.Theme
{
    /// <summary>
    /// Aplica el EmotionTheme activo a la cámara, audio ambiental y partículas.
    /// Se registra como servicio para que cualquier sistema pueda consultarlo.
    ///
    /// Setup: asignar los 8 EmotionTheme assets en el array _themes desde el Inspector.
    /// Requiere dos AudioSource hijos (para crossfade) nombrados AmbientA y AmbientB.
    /// </summary>
    public class ThemeManager : BaseService
    {
        // ── Configuración ──────────────────────────────────────────────

        [Header("Temas emocionales")]
        [SerializeField] private EmotionTheme[] _themes;

        [Header("Paletas culturales")]
        [SerializeField] private CultureColorOverride[] _cultureOverrides;

        [Header("UI References")]
        [SerializeField] private Image _mascotImage;
        [SerializeField] private Image _backgroundImage;

        [Header("Audio (crossfade)")]
        [SerializeField] private AudioSource _audioSourceA;
        [SerializeField] private AudioSource _audioSourceB;

        // ── Estado interno ─────────────────────────────────────────────

        private EmotionTheme         _currentTheme;
        private Coroutine            _transitionCoroutine;
        private bool                 _isSourceAActive = true;
        private GameObject           _currentParticlesInstance;
        private Camera               _mainCamera;
        private CultureColorOverride _activeCultureOverride;

        // ── Eventos públicos ───────────────────────────────────────────

        /// <summary>Disparado cada frame durante la transición. Parámetros: tema destino, progreso 0→1.</summary>
        public event Action<EmotionTheme, float> OnThemeTransition;

        /// <summary>Disparado cuando la transición ha completado.</summary>
        public event Action<EmotionTheme> OnThemeApplied;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            OnThemeTransition = null;
            OnThemeApplied    = null;

            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Aplica el tema correspondiente a la emoción indicada.
        /// Si animate = false lo aplica instantáneamente.
        /// </summary>
        public void ApplyTheme(EmotionType emotion, bool animate = true)
        {
            var theme = GetTheme(emotion);
            Debug.Log($"[ThemeManager] ApplyTheme({emotion}). " +
                      $"Theme found: {theme != null}, " +
                      $"Themes count: {_themes?.Length ?? 0}, " +
                      $"BackgroundImage: {_backgroundImage != null}, " +
                      $"Camera: {_mainCamera != null}");

            if (theme == null)
            {
                Debug.LogWarning($"[ThemeManager] No se encontró tema para: {emotion}");
                return;
            }

            // Resolver colores: override cultural si existe, colores base del tema si no
            _resolveColors(theme, out var primary, out var background);

            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            if (animate)
                _transitionCoroutine = StartCoroutine(_transitionToTheme(theme, primary, background));
            else
                _applyInstant(theme, primary, background);
        }

        /// <summary>
        /// Activa la paleta de colores correspondiente a la cultura del usuario.
        /// Las siguientes llamadas a ApplyTheme usarán esos colores en lugar de los del EmotionTheme base.
        /// </summary>
        public void SetActiveCulture(CultureType culture)
        {
            _activeCultureOverride = null;

            if (_cultureOverrides == null) return;

            foreach (var override_ in _cultureOverrides)
            {
                if (override_ != null && override_.cultureType == culture)
                {
                    _activeCultureOverride = override_;
                    Debug.Log($"[ThemeManager] Paleta cultural activada: {culture}");
                    return;
                }
            }

            Debug.Log($"[ThemeManager] Sin paleta cultural configurada para: {culture}. Se usarán colores base.");
        }

        /// <summary>Devuelve el tema actualmente aplicado.</summary>
        public EmotionTheme GetCurrentTheme() => _currentTheme;

        /// <summary>Devuelve el tema asociado a una emoción. Puede ser null si no está configurado.</summary>
        public EmotionTheme GetTheme(EmotionType emotion)
        {
            if (_themes == null) return null;

            foreach (var theme in _themes)
                if (theme != null && theme.emotionType == emotion)
                    return theme;

            return null;
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Devuelve los colores efectivos para el tema: override cultural si existe, colores base si no.
        /// Solo afecta a primaryColor y backgroundColor; el resto del tema no varía.
        /// </summary>
        private void _resolveColors(EmotionTheme theme, out Color primary, out Color background)
        {
            if (_activeCultureOverride != null &&
                _activeCultureOverride.TryGetColors(theme.emotionType, out var entry))
            {
                primary    = entry.primaryColor;
                background = entry.backgroundColor;
            }
            else
            {
                primary    = theme.primaryColor;
                background = theme.backgroundColor;
            }
        }

        /// <summary>
        /// Aplica el tema sin animación: color de cámara, audio y partículas instantáneos.
        /// Los parámetros de color ya tienen aplicado el override cultural si corresponde.
        /// </summary>
        private void _applyInstant(EmotionTheme theme, Color primary, Color background)
        {
            if (_mainCamera != null)
                _mainCamera.backgroundColor = background;

            if (_backgroundImage != null)
                _backgroundImage.color = primary;

            if (theme.mascotExpression != null && _mascotImage != null)
                _mascotImage.sprite = theme.mascotExpression;

            _swapAudioInstant(theme);
            _swapParticles(theme);

            _currentTheme = theme;
            OnThemeApplied?.Invoke(theme);
            EventBus.EmitCurrentEmotionChanged(theme.emotionType);
        }

        /// <summary>
        /// Interpola el color de fondo de la cámara con SmoothStep,
        /// hace crossfade del audio ambiental y gestiona las partículas.
        /// Los parámetros de color ya tienen aplicado el override cultural si corresponde.
        /// </summary>
        private IEnumerator _transitionToTheme(EmotionTheme theme, Color primary, Color background)
        {
            var fromColor = _mainCamera != null ? _mainCamera.backgroundColor : background;
            var toColor   = background;
            var fromBg    = _backgroundImage != null ? _backgroundImage.color : primary;
            var toBg      = primary;
            float elapsed  = 0f;
            float duration = theme.transitionDuration;

            // Iniciar crossfade de audio en paralelo
            StartCoroutine(_crossfadeAudio(theme, duration));

            // Cambiar partículas al inicio (fade-out se puede extender si se quiere)
            _swapParticles(theme);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t        = Mathf.Clamp01(elapsed / duration);
                float smoothT  = Mathf.SmoothStep(0f, 1f, t);

                if (_mainCamera != null)
                    _mainCamera.backgroundColor = Color.Lerp(fromColor, toColor, smoothT);

                if (_backgroundImage != null)
                    _backgroundImage.color = Color.Lerp(fromBg, toBg, smoothT);

                OnThemeTransition?.Invoke(theme, smoothT);
                yield return null;
            }

            // Garantizar valor final exacto
            if (_mainCamera != null)
                _mainCamera.backgroundColor = toColor;

            if (_backgroundImage != null)
                _backgroundImage.color = toBg;

            if (theme.mascotExpression != null && _mascotImage != null)
                _mascotImage.sprite = theme.mascotExpression;

            _currentTheme        = theme;
            _transitionCoroutine = null;

            OnThemeApplied?.Invoke(theme);
        }

        /// <summary>Crossfade entre los dos AudioSources en el tiempo de transición indicado.</summary>
        private IEnumerator _crossfadeAudio(EmotionTheme theme, float duration)
        {
            var incoming = _isSourceAActive ? _audioSourceB : _audioSourceA;
            var outgoing = _isSourceAActive ? _audioSourceA : _audioSourceB;

            // Preparar incoming
            incoming.clip   = theme.ambientLoop;
            incoming.volume = 0f;
            incoming.loop   = true;
            if (theme.ambientLoop != null) incoming.Play();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed         += Time.deltaTime;
                float t          = Mathf.Clamp01(elapsed / duration);
                incoming.volume  = Mathf.Lerp(0f, theme.ambientVolume, t);
                outgoing.volume  = Mathf.Lerp(outgoing.volume, 0f, t);
                yield return null;
            }

            outgoing.Stop();
            outgoing.clip   = null;
            incoming.volume = theme.ambientVolume;
            _isSourceAActive = !_isSourceAActive;
        }

        /// <summary>Aplica el audio del tema de forma instantánea (sin crossfade).</summary>
        private void _swapAudioInstant(EmotionTheme theme)
        {
            _audioSourceA.Stop();
            _audioSourceB.Stop();

            var active = _isSourceAActive ? _audioSourceA : _audioSourceB;
            active.clip   = theme.ambientLoop;
            active.volume = theme.ambientVolume;
            active.loop   = true;
            if (theme.ambientLoop != null) active.Play();
        }

        /// <summary>Destruye las partículas actuales e instancia las del nuevo tema.</summary>
        private void _swapParticles(EmotionTheme theme)
        {
            if (_currentParticlesInstance != null)
                Destroy(_currentParticlesInstance);

            if (theme.ambientParticlesPrefab != null)
                _currentParticlesInstance = Instantiate(theme.ambientParticlesPrefab, transform, false);
        }
    }
}
