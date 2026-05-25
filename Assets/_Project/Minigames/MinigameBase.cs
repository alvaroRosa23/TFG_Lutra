using System;
using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Clase base para todos los minijuegos de Lutra.
    /// Implementa el ciclo de vida de IMinigame y proporciona helpers para
    /// calcular duración y construir el MinigameResult final.
    ///
    /// Cada minijuego concreto:
    ///   - Override OnInitialize(), OnGameStarted(), OnGamePaused(), OnGameResumed()
    ///   - Implementa OnGameEnded(bool) donde calcula su RelaxationScore y métricas
    ///   - Llama a BuildResult() y lo asigna a _result antes de que EndGame termine
    /// </summary>
    public abstract class MinigameBase : MonoBehaviour, IMinigame
    {
        // ── IMinigame ──────────────────────────────────────────────────

        public abstract MinigameType Type { get; }
        public bool IsPlaying => _isPlaying;
        public event Action<MinigameResult> OnGameCompleted;

        // ── Estado protegido ───────────────────────────────────────────

        protected EmotionType   _emotionBefore;
        protected DateTime      _startTime;
        protected bool          _isPlaying;
        protected MinigameResult _result;

        // ── IMinigame — implementación pública ─────────────────────────

        public void Initialize(EmotionType emotionBefore)
        {
            _emotionBefore = emotionBefore;
            _result        = null;
            OnInitialize();
        }

        public void StartGame()
        {
            if (_isPlaying) return;
            _startTime = DateTime.Now;
            _isPlaying = true;
            OnGameStarted();
        }

        public void PauseGame()
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            OnGamePaused();
        }

        public void ResumeGame()
        {
            if (_isPlaying) return;
            _isPlaying = true;
            OnGameResumed();
        }

        public void EndGame(bool completedNaturally)
        {
            if (!_isPlaying && _result != null) return; // ya terminado

            _isPlaying = false;
            OnGameEnded(completedNaturally);

            // Si la subclase no construyó el resultado, generamos uno básico
            if (_result == null)
                _result = BuildResult(0f, completedNaturally);

            OnGameCompleted?.Invoke(_result);
        }

        // ── Hooks virtuales ────────────────────────────────────────────

        /// <summary>Llamado en Initialize(). Override para preparar el estado inicial.</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Llamado en StartGame(). Override para iniciar la lógica del juego.</summary>
        protected virtual void OnGameStarted() { }

        /// <summary>Llamado en PauseGame(). Override para pausar timers, animaciones, etc.</summary>
        protected virtual void OnGamePaused() { }

        /// <summary>Llamado en ResumeGame(). Override para reanudar timers, animaciones, etc.</summary>
        protected virtual void OnGameResumed() { }

        /// <summary>
        /// Llamado en EndGame(). Debe calcular RelaxationScore, métricas y asignar _result
        /// con BuildResult() antes de retornar.
        /// </summary>
        protected abstract void OnGameEnded(bool completedNaturally);

        // ── Helpers protegidos ─────────────────────────────────────────

        /// <summary>Devuelve los segundos transcurridos desde que empezó la partida.</summary>
        protected float CalculateDuration()
        {
            return (float)(DateTime.Now - _startTime).TotalSeconds;
        }

        /// <summary>
        /// Construye y devuelve un MinigameResult con los datos de la sesión actual.
        /// Llamar desde OnGameEnded() y asignar el retorno a _result.
        /// </summary>
        protected MinigameResult BuildResult(float relaxationScore, bool completedNaturally,
                                              Dictionary<string, float> metrics = null)
        {
            return new MinigameResult(Type, _emotionBefore)
            {
                StartTime          = _startTime,
                DurationSeconds    = Mathf.RoundToInt(CalculateDuration()),
                RelaxationScore    = Mathf.Clamp01(relaxationScore),
                CompletedNaturally = completedNaturally,
                Metrics            = metrics ?? new Dictionary<string, float>()
            };
        }

        // ── Unity lifecycle ────────────────────────────────────────────

        protected virtual void OnDestroy()
        {
            // Forzar cierre si la escena se descarga con la partida en curso
            if (_isPlaying)
                EndGame(completedNaturally: false);
        }
    }
}
