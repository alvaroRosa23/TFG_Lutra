using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Events;

namespace Lutra.UI.Screens
{
    /// <summary>
    /// Gestiona la navegación entre pantallas y el historial de navegación.
    ///
    /// Flujo de transición:
    ///   AppStateMachine.TransitionTo(state) → UIScreen.OnExit (Hide) / UIScreen.OnEnter (Show + OnScreenFocused)
    ///   → OnStateChanged → ScreenManager._onAppStateChanged → NavigateTo (sincroniza _currentScreen)
    ///
    /// NavigateTo también puede llamarse directamente para navegar sin pasar por AppStateMachine.
    ///
    /// IMPORTANTE: NavigateTo no llama a AppStateMachine.TransitionTo para evitar recursión.
    /// Si quieres cambiar estado global, usa AppStateMachine.TransitionTo; el ScreenManager
    /// reaccionará automáticamente.
    /// </summary>
    public class ScreenManager : BaseService
    {
        [SerializeField] private UIScreen[] _screens;

        private readonly Dictionary<AppState, UIScreen> _screenMap         = new();
        private readonly Stack<AppState>                _navigationHistory = new();

        private AppState _currentState;
        private UIScreen _currentScreen;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Start()
        {
            if (AppStateMachine.Instance == null)
            {
                Debug.LogError("[ScreenManager] AppStateMachine.Instance es null en Start(). " +
                               "Asegúrate de que AppStateMachine se inicializa antes.");
                return;
            }

            // Construir mapa, registrar en AppStateMachine y ocultar todas las pantallas
            foreach (var screen in _screens)
            {
                if (screen == null) continue;
                _screenMap[screen.ScreenState] = screen;
                AppStateMachine.Instance.RegisterState(screen.ScreenState, screen);
                screen.Hide();
            }

            // Suscribirse a cambios de estado externos (p.ej. disparados desde GameManager)
            AppStateMachine.Instance.OnStateChanged += _onAppStateChanged;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (AppStateMachine.Instance != null)
                AppStateMachine.Instance.OnStateChanged -= _onAppStateChanged;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Navega a la pantalla correspondiente al estado indicado.
        /// Uso interno: llamado desde el callback de AppStateMachine.OnStateChanged.
        /// Para navegar externamente, usa AppStateMachine.Instance.TransitionTo().
        /// </summary>
        private void NavigateTo(AppState state, bool addToHistory = true)
        {
            if (!_screenMap.TryGetValue(state, out var nextScreen))
            {
                Debug.LogWarning($"[ScreenManager] Pantalla no registrada: {state}.");
                return;
            }

            if (addToHistory && _currentScreen != null)
                _navigationHistory.Push(_currentState);

            _currentScreen?.Hide();
            nextScreen.Show();

            if (_currentScreen != nextScreen)
                nextScreen.OnScreenFocused();

            _currentScreen = nextScreen;
            _currentState  = state;

            EventBus.EmitScreenChanged(state);
        }

        /// <summary>
        /// Vuelve a la pantalla anterior en el historial.
        /// No hace nada si el historial está vacío.
        /// </summary>
        public void NavigateBack()
        {
            if (_navigationHistory.Count == 0)
            {
                Debug.LogWarning("[ScreenManager] NavigateBack: historial vacío.");
                return;
            }

            NavigateTo(_navigationHistory.Pop(), addToHistory: false);
        }

        /// <summary>
        /// Busca y devuelve la pantalla del tipo genérico indicado.
        /// Devuelve null si no se encontró ninguna pantalla de ese tipo.
        /// </summary>
        public T GetScreen<T>() where T : UIScreen
        {
            foreach (var screen in _screens)
                if (screen is T typed)
                    return typed;

            return null;
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Callback de AppStateMachine.OnStateChanged.
        /// Sincroniza el tracker interno (_currentScreen) con el estado global.
        /// AppStateMachine ya llamó OnExit/OnEnter en las pantallas antes de disparar este evento,
        /// por lo que NavigateTo actúa como sincronización de estado, no como disparador de show/hide.
        /// </summary>
        private void _onAppStateChanged(AppState from, AppState to)
        {
            NavigateTo(to, addToHistory: false);
        }
    }
}
