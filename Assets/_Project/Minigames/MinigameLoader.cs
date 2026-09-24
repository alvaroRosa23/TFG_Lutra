using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Data.ScriptableObjects;
using Lutra.Core.Events;
using Lutra.UI.Screens;

namespace Lutra.Minigames
{
    /// <summary>
    /// Gestiona la carga y descarga aditiva de escenas de minijuegos.
    /// Se registra como servicio para que MinigamesController pueda usarlo.
    ///
    /// Setup: asignar una MinigameSceneData por cada MinigameType en _minigameScenes.
    /// </summary>
    public class MinigameLoader : BaseService
    {
        [SerializeField] private MinigameSceneData[]  _minigameScenes;
        [SerializeField] private MinigameDefinition[] _minigameDefinitions;

        // ── Estado ─────────────────────────────────────────────────────

        private IMinigame  _currentMinigame;
        private string     _currentSceneName;
        private EmotionType _emotionBefore;

        // Referencia guardada para poder desuscribir el handler
        private Action<MinigameResult> _gameCompletedHandler;

        /// <summary>Última partida terminada y guardada; la lee PostMinigameScreen al recibir el foco.</summary>
        public MinigameOutcome LastOutcome { get; private set; }

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake() { }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Carga la escena del minijuego de forma aditiva, inicializa el componente
        /// IMinigame encontrado en ella y arranca la partida.
        /// </summary>
        public async Task LoadMinigame(MinigameType type, EmotionType emotion)
        {
            var sceneData = _findSceneData(type);
            if (sceneData == null)
            {
                Debug.LogError($"[MinigameLoader] No hay escena configurada para: {type}");
                return;
            }

            // Descargar minijuego anterior si existe
            if (_currentMinigame != null)
                await UnloadCurrentMinigame();

            _emotionBefore    = emotion;
            _currentSceneName = sceneData.sceneName;

            // Cargar escena de forma aditiva
            await _loadSceneAsync(_currentSceneName);

            // Buscar componente IMinigame en la escena recién cargada
            _currentMinigame = _findMinigameInScene(_currentSceneName);
            if (_currentMinigame == null)
            {
                Debug.LogError($"[MinigameLoader] No se encontró IMinigame en la escena: {_currentSceneName}");
                return;
            }

            // Suscribir handler antes de iniciar
            _gameCompletedHandler = result =>
            {
                _ = _onMinigameFinished(result).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Debug.LogError($"[MinigameLoader] Error en _onMinigameFinished: {t.Exception?.GetBaseException().Message}");
                }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            };
            _currentMinigame.OnGameCompleted += _gameCompletedHandler;

            _currentMinigame.Initialize(emotion);
            _currentMinigame.StartGame();

            EventBus.EmitMinigameStarted(type);
        }

        /// <summary>Definición (nombre, logo, tiempo...) de un minijuego; null si no está configurada.</summary>
        public MinigameDefinition GetDefinition(MinigameType type)
        {
            if (_minigameDefinitions == null) return null;
            foreach (var def in _minigameDefinitions)
                if (def != null && def.minigameType == type) return def;
            return null;
        }

        /// <summary>
        /// Descarga la escena del minijuego activo y limpia el estado interno.
        /// </summary>
        public async Task UnloadCurrentMinigame()
        {
            if (_currentMinigame == null) return;

            if (_gameCompletedHandler != null)
            {
                _currentMinigame.OnGameCompleted -= _gameCompletedHandler;
                _gameCompletedHandler = null;
            }

            _currentMinigame = null;

            if (!string.IsNullOrEmpty(_currentSceneName))
            {
                await _unloadSceneAsync(_currentSceneName);
                _currentSceneName = null;
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Callback disparado por el minijuego al terminar.
        /// Persiste la sesión, comprueba el récord, emite eventos y navega a la pantalla post-juego.
        /// </summary>
        private async Task _onMinigameFinished(MinigameResult result)
        {
            try
            {
                var repo = ServiceLocator.Get<DataRepository>();

                // Récord previo: se consulta ANTES de guardar para poder comparar
                float? previousBest = await repo.GetBestRelaxationScore(result.Type);

                // Construir y persistir sesión
                var session = new MinigameSession(result.Type, result.EmotionBefore)
                {
                    StartTime       = result.StartTime,
                    DurationSeconds = result.DurationSeconds,
                    EmotionAfter    = result.EmotionAfter,
                    RelaxationScore = result.RelaxationScore,
                    Metrics         = result.Metrics
                };

                await repo.SaveMinigameSession(session);

                EventBus.EmitMinigameCompleted(session);

                // Monedas por completar minijuego: Mathf.Max(3, estimatedTimeSeconds / 30)
                int coinReward = _calculateMinigameCoinReward(result.Type);
                if (coinReward > 0)
                {
                    await repo.AddCoins(coinReward);
                    var profile = await repo.GetUserProfile();
                    EventBus.EmitCoinsChanged(profile?.Coins ?? 0);
                    Debug.Log($"[MinigameLoader] Monedas por minijuego ({result.Type}): +{coinReward}");
                }

                int newScore = MinigameOutcome.ToDisplayScore(result.RelaxationScore);
                LastOutcome = new MinigameOutcome
                {
                    Session           = session,
                    DisplayName       = GetDefinition(result.Type)?.displayName ?? result.Type.ToString(),
                    PreviousBestScore = previousBest,
                    IsNewRecord       = newScore > 0 &&
                                        (!previousBest.HasValue || newScore > MinigameOutcome.ToDisplayScore(previousBest.Value)),
                    CoinsEarned       = coinReward
                };

                // Descargar escena del minijuego
                await UnloadCurrentMinigame();

                // Navegar a pantalla post-juego
                AppStateMachine.Instance.TransitionTo(AppState.MinigameActive);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MinigameLoader] _onMinigameFinished: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private int _calculateMinigameCoinReward(MinigameType type)
        {
            var def = GetDefinition(type);
            return def != null ? Mathf.Max(3, def.estimatedTimeSeconds / 30) : 3;
        }

        private MinigameSceneData _findSceneData(MinigameType type)
        {
            if (_minigameScenes == null) return null;
            foreach (var data in _minigameScenes)
                if (data != null && data.type == type) return data;
            return null;
        }

        private IMinigame _findMinigameInScene(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            foreach (var go in scene.GetRootGameObjects())
            {
                var minigame = go.GetComponentInChildren<MinigameBase>(includeInactive: true);
                if (minigame != null) return minigame;
            }
            return null;
        }

        // ── Helpers de escena (Task desde AsyncOperation) ──────────────

        private Task _loadSceneAsync(string sceneName)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op  = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            op.completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }

        private Task _unloadSceneAsync(string sceneName)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op  = SceneManager.UnloadSceneAsync(sceneName);
            if (op == null) { tcs.SetResult(true); return tcs.Task; }
            op.completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }

        // ── Clase interna ──────────────────────────────────────────────

        [Serializable]
        public class MinigameSceneData
        {
            public MinigameType type;
            public string       sceneName;
        }
    }
}
