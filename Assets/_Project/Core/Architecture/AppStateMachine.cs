using System;
using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Events;
using Lutra.UI.Screens;

namespace Lutra.Core.Architecture
{
    /// <summary>
    /// Estados globales de la aplicación.
    /// </summary>
    public enum AppState
    {
        Splash,
        Login,              // pantalla de login / registro con Firebase
        Register,           // pantalla de creación de nueva cuenta
        OnboardingProfile,  // completar datos de perfil tras el registro
        EmotionCheck,
        MainMenu,
        Diary,
        SafeZone,
        Minigames,
        MinigameActive,
        Charts,
        Settings
    }

    /// <summary>
    /// Máquina de estados global de la aplicación.
    /// Gestiona las transiciones entre secciones principales y notifica cambios vía EventBus.
    ///
    /// Añadir un estado:
    ///   1. Crear clase que implemente IAppState.
    ///   2. Registrarla con RegisterState() antes de llamar a TransitionTo().
    /// </summary>
    public class AppStateMachine : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────

        public static AppStateMachine Instance { get; private set; }

        // ── Estado ─────────────────────────────────────────────────────

        /// <summary>Estado actualmente activo.</summary>
        public AppState CurrentState { get; private set; }

        /// <summary>Estado inmediatamente anterior al actual. Igual a CurrentState si no hubo transición previa.</summary>
        public AppState PreviousState => _stateHistory.Count > 0 ? _stateHistory.Peek() : CurrentState;

        /// <summary>
        /// Disparado justo después de cada transición.
        /// Parámetros: estado anterior, estado nuevo.
        /// </summary>
        public event Action<AppState, AppState> OnStateChanged;

        private Dictionary<AppState, IAppState> _states;
        private IAppState _activeState;
        private readonly Stack<AppState> _stateHistory = new Stack<AppState>();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            // Patrón Singleton: destruir duplicados entre cargas de escena
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_states == null)
                _states = new Dictionary<AppState, IAppState>();
        }

        private void Update()
        {
            _activeState?.OnUpdate();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Registra la implementación de un estado. Llamar durante la inicialización.
        /// </summary>
        public void RegisterState(AppState key, IAppState state)
        {
            _states[key] = state;
        }

        /// <summary>
        /// Transiciona al estado indicado.
        /// Llama a OnExit() del estado actual y a OnEnter() del nuevo.
        /// No hace nada si el estado solicitado es el mismo que el activo.
        /// </summary>
        public void TransitionTo(AppState newState)
        {
            if (newState == CurrentState && _activeState != null)
            {
                var screen = _activeState as UIScreen;
                if (screen != null && !screen.IsVisible)
                {
                    _activeState.OnEnter();
                    return;
                }
                return;
            }

            var previousState = CurrentState;
            _stateHistory.Push(previousState);

            if (!_states.TryGetValue(newState, out var nextState))
            {
                // Estado no registrado: cambiar _currentState sin invocar hooks OnExit/OnEnter
                Debug.LogWarning($"[AppStateMachine] Estado no registrado: {newState}. " +
                                 "Cambiando estado sin hooks (registra el handler con RegisterState()).");
                CurrentState = newState;
                OnStateChanged?.Invoke(previousState, CurrentState);
                EventBus.EmitScreenChanged(CurrentState);
                return;
            }

            // Salir del estado actual
            _activeState?.OnExit();

            // Entrar en el nuevo estado
            CurrentState = newState;
            _activeState = nextState;
            _activeState.OnEnter();

            // Notificar listeners
            OnStateChanged?.Invoke(previousState, CurrentState);
            EventBus.EmitScreenChanged(CurrentState);

            Debug.Log($"[AppStateMachine] {previousState} → {CurrentState}");
        }
    }
}
