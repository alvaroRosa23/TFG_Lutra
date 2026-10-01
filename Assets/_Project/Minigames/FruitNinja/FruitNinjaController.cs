using System;
using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Minijuego FruitNinja (nombre en clave; la temática de lo que se corta está por decidir):
    /// partida de 2 minutos en la que se cortan elementos deslizando el dedo. El ritmo baja de
    /// Descarga a Calma. Sin bombas ni game over: cada elemento que cae sin cortar resta un poco
    /// y rompe la racha; solo cortándolo todo se llega a 100.
    ///
    /// Combos: elementos cortados en un mismo trazo (se cierra al levantar el dedo o tras
    /// comboWindow sin cortar). Un combo de más de 5 hace un zoom rápido con un breve congelado
    /// (con enfriamiento para que no se repita sin parar). Los especiales lanzan efectos:
    /// ráfaga, lluvia desde arriba, elementos desde los lados o cámara lenta.
    ///
    /// Al acabar el tiempo se espera a que caigan los últimos elementos y termina la partida
    /// (PostMinigameScreen). Salir antes es un abandono: no se guarda.
    /// </summary>
    public class FruitNinjaController : MinigameBase
    {
        [SerializeField] private FruitNinjaView        _view;
        [SerializeField] private FruitNinjaField       _field;
        [SerializeField] private FruitNinjaSwipeInput  _input;
        [SerializeField] private FruitNinjaSwipeTrail  _trail;
        [SerializeField] private FruitNinjaTuning      _tuning = new FruitNinjaTuning();

        private const float FinishDelaySeconds = 1.5f;

        public override MinigameType Type => MinigameType.FruitNinja;

        private readonly List<SpawnRequest>  _spawnBuffer = new List<SpawnRequest>();
        private readonly List<SliceableView> _sliceBuffer = new List<SliceableView>();

        private FruitNinjaSpawnPlanner   _planner;
        private FruitNinjaMetricsTracker _metrics;
        private bool _hasDependencies;

        private float _elapsed;
        private FruitNinjaPhase _shownPhase;

        // Tiempo del mundo
        private float _slowMotionLeft;
        private float _hitStopLeft;

        // Combo del trazo actual
        private int     _comboCount;
        private float   _comboIdle;
        private Vector2 _comboPositionSum;
        private float   _bigComboCooldown;

        // Final
        private bool  _timeUp;
        private bool  _finishing;
        private float _finishDelay;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)  _view  = GetComponentInChildren<FruitNinjaView>(true);
            if (_field == null) _field = GetComponentInChildren<FruitNinjaField>(true);
            if (_input == null) _input = GetComponentInChildren<FruitNinjaSwipeInput>(true);
            if (_trail == null) _trail = GetComponentInChildren<FruitNinjaSwipeTrail>(true);

            _hasDependencies = _view != null && _field != null && _input != null;
            if (!_hasDependencies)
                Debug.LogError("[FruitNinjaController] Falta asignar View, Field o SwipeInput");
        }

        private void OnEnable()
        {
            if (!_hasDependencies) return;

            _input.OnSwipeStarted += _onSwipeStarted;
            _input.OnSwipeMoved   += _onSwipeMoved;
            _input.OnSwipeEnded   += _onSwipeEnded;
            _field.OnMissed       += _onMissed;
            _view.OnExitRequested += _onExitRequested;
            _view.OnExitConfirmed += _onExitConfirmed;
            _view.OnExitCancelled += _onExitCancelled;
        }

        private void OnDisable()
        {
            if (!_hasDependencies) return;

            _input.OnSwipeStarted -= _onSwipeStarted;
            _input.OnSwipeMoved   -= _onSwipeMoved;
            _input.OnSwipeEnded   -= _onSwipeEnded;
            _field.OnMissed       -= _onMissed;
            _view.OnExitRequested -= _onExitRequested;
            _view.OnExitConfirmed -= _onExitConfirmed;
            _view.OnExitCancelled -= _onExitCancelled;
        }

        private void Update()
        {
            if (!_isPlaying || _metrics == null) return;

            float deltaTime = Time.deltaTime;
            _elapsed += deltaTime;

            float worldDeltaTime = deltaTime * _worldTimeScale();
            _slowMotionLeft   = Mathf.Max(0f, _slowMotionLeft - deltaTime);
            _hitStopLeft      = Mathf.Max(0f, _hitStopLeft - deltaTime);
            _bigComboCooldown = Mathf.Max(0f, _bigComboCooldown - deltaTime);

            if (!_timeUp) _tickSpawning(worldDeltaTime);
            _field.Tick(worldDeltaTime);

            if (_comboCount > 0)
            {
                _comboIdle += deltaTime;
                if (_comboIdle >= _tuning.comboWindow) _closeCombo();
            }

            _view.SetTimeLeft(_tuning.durationSeconds - _elapsed);
            _view.Tick(deltaTime);

            _tickEnd(deltaTime);
        }

        // ── MinigameBase hooks ─────────────────────────────────────────

        protected override void OnInitialize()
        {
            if (!_hasDependencies) return;

            _planner = new FruitNinjaSpawnPlanner(_tuning, Environment.TickCount);
            _metrics = new FruitNinjaMetricsTracker(_tuning);

            _elapsed = 0f;
            _slowMotionLeft = _hitStopLeft = _bigComboCooldown = 0f;
            _comboCount = 0;
            _timeUp = _finishing = false;
            _finishDelay = FinishDelaySeconds;
            _shownPhase = FruitNinjaPhase.Release;

            _field.Setup(_tuning);
            if (_trail != null) _trail.Clear();
            _view.Initialize(_tuning.durationSeconds);
        }

        protected override void OnGameStarted()
        {
            if (_hasDependencies) _view.ShowPhase(FruitNinjaPhase.Release);
        }

        protected override void OnGamePaused()
        {
            if (!_hasDependencies) return;

            _closeCombo();
            if (_trail != null) _trail.Clear();
        }

        protected override void OnGameEnded(bool completedNaturally)
        {
            if (_hasDependencies) _field.Clear();

            // Un abandono no se guarda (MinigameLoader lo descarta): puntuación y monedas a 0
            float score = completedNaturally && _metrics != null ? _metrics.CalculateRelaxationScore() : 0f;
            _result = BuildResult(score, completedNaturally, _metrics?.BuildMetrics());
            _result.CoinReward = completedNaturally && _metrics != null ? _metrics.CalculateCoinReward() : 0;
        }

        // ── Handlers de entrada ────────────────────────────────────────

        private void _onSwipeStarted(Vector2 position)
        {
            if (!_isPlaying || _finishing) return;

            _closeCombo();
            if (_trail != null)
            {
                _trail.Clear();
                _trail.AddPoint(position);
            }
        }

        private void _onSwipeMoved(Vector2 from, Vector2 to, float speed)
        {
            if (!_isPlaying || _finishing) return;

            if (_trail != null) _trail.AddPoint(to);
            if (speed < _tuning.minSwipeSpeed) return;

            _field.Slice(from, to, _sliceBuffer);
            foreach (var item in _sliceBuffer) _registerCut(item);
        }

        private void _onSwipeEnded() => _closeCombo();

        // ── Handlers del campo ─────────────────────────────────────────

        private void _onMissed(SliceableView item)
        {
            if (_metrics == null || _finishing) return;

            _metrics.RegisterMiss();
            _view.FlashMiss();
            _refreshScore();
        }

        // ── Handlers de salida ─────────────────────────────────────────

        private void _onExitRequested()
        {
            if (!_isPlaying) return;

            PauseGame();
            _view.ShowExitConfirm(true);
        }

        // Salir antes de terminar es un abandono: vuelve a la lista de minijuegos sin guardar.
        private void _onExitConfirmed()
        {
            _view.ShowExitConfirm(false);
            EndGame(completedNaturally: false);
        }

        private void _onExitCancelled()
        {
            _view.ShowExitConfirm(false);
            ResumeGame();
        }

        // ── Helpers privados ───────────────────────────────────────────

        private float _worldTimeScale()
        {
            float scale = _slowMotionLeft > 0f ? _tuning.slowMotionScale : 1f;
            if (_hitStopLeft > 0f) scale *= _tuning.hitStopTimeScale;
            return scale;
        }

        private void _tickSpawning(float worldDeltaTime)
        {
            _spawnBuffer.Clear();
            _planner.Tick(_elapsed, worldDeltaTime, _spawnBuffer);

            foreach (var request in _spawnBuffer)
            {
                _field.Spawn(request);
                _metrics.RegisterSpawn();
            }

            if (_planner.Phase != _shownPhase)
            {
                _shownPhase = _planner.Phase;
                _view.ShowPhase(_shownPhase);
            }
        }

        private void _registerCut(SliceableView item)
        {
            _metrics.RegisterCut(item.Kind);
            _refreshScore();

            _comboCount++;
            _comboIdle = 0f;
            _comboPositionSum += item.Position;
            if (_comboCount >= 3) _view.ShowCombo(_comboCount);

            if (_comboCount == _tuning.bigComboThreshold && _bigComboCooldown <= 0f)
            {
                _view.PunchZoom(_comboPositionSum / _comboCount);
                _hitStopLeft      = _tuning.hitStopSeconds;
                _bigComboCooldown = _tuning.bigComboCooldown;
            }

            if (item.Kind == SliceableKind.Normal) return;

            _view.ShowSpecial(item.Kind);
            if (item.Kind == SliceableKind.SlowMotion)
                _slowMotionLeft = _tuning.slowMotionSeconds;
            else
                _planner.TriggerSpecial(item.Kind, _elapsed);
        }

        private void _closeCombo()
        {
            if (_comboCount > 0) _metrics?.RegisterCombo(_comboCount);

            _comboCount = 0;
            _comboIdle  = 0f;
            _comboPositionSum = Vector2.zero;
        }

        private void _refreshScore()
        {
            _view.SetScore(MinigameOutcome.ToDisplayScore(_metrics.CalculateRelaxationScore()));
            _view.SetStreak(_metrics.Streak);
        }

        /// <summary>Se acaba el tiempo → espera a que no quede nada en el aire → cierra tras una pausa.</summary>
        private void _tickEnd(float deltaTime)
        {
            if (!_timeUp && _elapsed >= _tuning.durationSeconds)
            {
                _timeUp = true;
                _view.ShowMessage("¡Tiempo!");
            }

            if (!_timeUp) return;

            if (!_finishing)
            {
                bool allResolved = _field.UnslicedCount == 0;
                if (!allResolved && _elapsed < _tuning.durationSeconds + _tuning.endGraceSeconds) return;

                _finishing = true;
                _closeCombo();
                if (_metrics.IsPerfect) _view.ShowMessage("¡Perfecto! No se ha caído nada");
                return;
            }

            _finishDelay -= deltaTime;
            if (_finishDelay <= 0f) EndGame(completedNaturally: true);
        }
    }
}
