using System;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Minijuego BreathJump: la nutria avanza por plataformas procedurales siguiendo el
    /// ritmo de respiración 4-6. Mantener pulsado = inspirar (carga el salto), soltar =
    /// espirar (salta y planea). Mantener en el aire corrige la trayectoria, pero esa
    /// respiración ya no cuenta como perfecta. Sin game over: al caer se reaparece.
    ///
    /// Cada respiración se valora al empezar la siguiente inspiración (entonces ya se conoce
    /// cuánto duró la espiración). Tras breathsToComplete saltos aparece la meta; al llegar
    /// y terminar de espirar, la partida acaba (PostMinigameScreen). Salir antes es un
    /// abandono: no se guarda y se vuelve a la lista de minijuegos.
    /// </summary>
    public class BreathJumpController : MinigameBase
    {
        [SerializeField] private BreathJumpView      _view;
        [SerializeField] private BreathJumpPlayer    _player;
        [SerializeField] private BreathJumpLevel     _level;
        [SerializeField] private BreathJumpCameraRig _cameraRig;
        [SerializeField] private BreathJumpTuning    _tuning = new BreathJumpTuning();

        private const float FinishDelaySeconds = 1.5f; // para que se vea la última valoración

        public override MinigameType Type => MinigameType.BreathJump;

        private BreathJumpMetricsTracker _metrics;
        private Transform _playerTransform;
        private bool _hasDependencies;

        private int _currentIndex;

        // Inspiración en curso
        private bool  _inhaling;
        private float _inhaleElapsed;

        // Espiración: desde que se suelta hasta la siguiente inspiración
        private bool  _exhaling;
        private float _exhaleElapsed;
        private float _breathAtRelease;

        // Salto en vuelo y respiración aterrizada pendiente de valorar
        private bool  _jumpCorrected;
        private bool  _hasPendingBreath;
        private float _pendingInhale;
        private bool  _pendingCorrected;

        private bool  _finishing;
        private float _finishDelay;

        private float _charge => Mathf.Clamp01(_inhaleElapsed / _tuning.inhaleSeconds);

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)      _view      = GetComponentInChildren<BreathJumpView>(true);
            if (_player == null)    _player    = GetComponentInChildren<BreathJumpPlayer>(true);
            if (_level == null)     _level     = GetComponentInChildren<BreathJumpLevel>(true);
            if (_cameraRig == null) _cameraRig = GetComponentInChildren<BreathJumpCameraRig>(true);

            _hasDependencies = _view != null && _player != null && _level != null && _cameraRig != null;
            if (!_hasDependencies)
            {
                Debug.LogError("[BreathJumpController] Falta asignar View, Player, Level o CameraRig");
                return;
            }

            _playerTransform = _player.transform;
        }

        private void OnEnable()
        {
            if (!_hasDependencies) return;

            _view.OnHoldStarted   += _onHoldStarted;
            _view.OnHoldEnded     += _onHoldEnded;
            _view.OnExitRequested += _onExitRequested;
            _view.OnExitConfirmed += _onExitConfirmed;
            _view.OnExitCancelled += _onExitCancelled;
            _player.OnLanded      += _onPlayerLanded;
            _player.OnFell        += _onPlayerFell;
        }

        private void OnDisable()
        {
            if (!_hasDependencies) return;

            _view.OnHoldStarted   -= _onHoldStarted;
            _view.OnHoldEnded     -= _onHoldEnded;
            _view.OnExitRequested -= _onExitRequested;
            _view.OnExitConfirmed -= _onExitConfirmed;
            _view.OnExitCancelled -= _onExitCancelled;
            _player.OnLanded      -= _onPlayerLanded;
            _player.OnFell        -= _onPlayerFell;
        }

        private void Update()
        {
            if (!_isPlaying || _metrics == null) return;

            float deltaTime = Time.deltaTime;

            if (_inhaling)
            {
                _inhaleElapsed += deltaTime;
                _player.SetCharge(_charge);
            }

            if (_exhaling) _exhaleElapsed += deltaTime;

            _player.Tick(deltaTime);

            if (_finishing)
            {
                _tickFinish(deltaTime);
                if (!_isPlaying) return;
            }

            float breath = _breathLevel();
            _cameraRig.Tick(deltaTime, _playerTransform.position, breath);
            _view.SetBreath(breath);
            _view.SetGuidePhase(_guidePhase());
            _view.Tick(deltaTime);
        }

        // ── MinigameBase hooks ─────────────────────────────────────────

        protected override void OnInitialize()
        {
            if (!_hasDependencies) return;

            _metrics = new BreathJumpMetricsTracker(_tuning);
            _resetBreathState();
            _currentIndex = 0;
            _finishing    = false;
            _finishDelay  = FinishDelaySeconds;

            _player.Setup(_tuning);
            _level.Build(_tuning, Environment.TickCount);
            _player.PlaceOn(_level.GetPlatform(0));
            _cameraRig.SnapTo(_playerTransform.position);
            _view.Initialize(_level.GoalIndex);
        }

        protected override void OnGamePaused()
        {
            if (!_hasDependencies) return;

            _cancelInhale();
            _player.SetSteering(false);
        }

        protected override void OnGameEnded(bool completedNaturally)
        {
            if (_hasDependencies) _cancelInhale();

            // Un abandono no se guarda (MinigameLoader lo descarta): puntuación y monedas a 0
            float score = completedNaturally && _metrics != null ? _metrics.CalculateRelaxationScore() : 0f;
            _result = BuildResult(score, completedNaturally, _metrics?.BuildMetrics());
            _result.CoinReward = completedNaturally && _metrics != null ? _metrics.CalculateCoinReward() : 0;
        }

        // ── Handlers de entrada ────────────────────────────────────────

        private void _onHoldStarted()
        {
            if (!_isPlaying || _finishing) return;

            // En el aire: corregir trayectoria (la respiración deja de poder ser perfecta)
            if (!_player.IsGrounded)
            {
                _player.SetSteering(true);
                _jumpCorrected = true;
                return;
            }

            // En la plataforma: empieza una inspiración y se valora la respiración anterior
            if (_hasPendingBreath)
            {
                _hasPendingBreath = false;
                _showBreathResult(_metrics.RegisterBreath(_pendingInhale, _exhaleElapsed, _pendingCorrected));
            }

            _exhaling      = false;
            _inhaling      = true;
            _inhaleElapsed = 0f;
        }

        private void _onHoldEnded()
        {
            if (!_isPlaying) return;

            if (!_inhaling)
            {
                _player.SetSteering(false);
                return;
            }

            _inhaling = false;

            // Toque accidental: no hay salto
            if (_inhaleElapsed < _tuning.minInhaleToJump)
            {
                _player.SetCharge(0f);
                return;
            }

            _pendingInhale   = _inhaleElapsed;
            _jumpCorrected   = false;
            _breathAtRelease = _charge;
            _player.Jump(_level.GetPlatform(_currentIndex + 1), _charge);

            _exhaling      = true;
            _exhaleElapsed = 0f;
        }

        // ── Handlers de la nutria ──────────────────────────────────────

        private void _onPlayerLanded(BreathJumpPlatform platform)
        {
            // Salto demasiado corto: ha vuelto a caer en la plataforma de origen
            if (platform.Index <= _currentIndex)
            {
                _registerMiss();
                return;
            }

            _currentIndex = platform.Index;
            _level.EnsureAround(_currentIndex);
            _view.SetProgress(_currentIndex, _level.GoalIndex);

            _hasPendingBreath = true;
            _pendingCorrected = _jumpCorrected;

            if (platform.IsGoal) _finishing = true;
        }

        private void _onPlayerFell()
        {
            _registerMiss();

            _currentIndex = Mathf.Max(0, _currentIndex - _tuning.respawnPlatformsBack);
            _level.EnsureAround(_currentIndex);
            _player.PlaceOn(_level.GetPlatform(_currentIndex));
            _view.SetProgress(_currentIndex, _level.GoalIndex);
        }

        // ── Handlers de salida ─────────────────────────────────────────

        private void _onExitRequested()
        {
            if (!_isPlaying) return;

            PauseGame();
            _view.ShowExitConfirm(true);
        }

        // Salir antes de la meta es un abandono: vuelve a la lista de minijuegos sin guardar.
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

        /// <summary>Tras llegar a la meta: termina la espiración, valora la última respiración y cierra.</summary>
        private void _tickFinish(float deltaTime)
        {
            if (_hasPendingBreath)
            {
                if (_exhaleElapsed < _tuning.exhaleSeconds) return;

                _hasPendingBreath = false;
                _exhaling = false;
                _showBreathResult(_metrics.RegisterBreath(_pendingInhale, _exhaleElapsed, _pendingCorrected));
                return;
            }

            _finishDelay -= deltaTime;
            if (_finishDelay <= 0f) EndGame(completedNaturally: true);
        }

        private void _registerMiss()
        {
            _showBreathResult(_metrics.RegisterMiss());
            _hasPendingBreath = false;
        }

        private void _showBreathResult(BreathQuality quality)
        {
            _view.ShowFeedback(quality);
            _view.SetScore(MinigameOutcome.ToDisplayScore(_metrics.CalculateRelaxationScore()));
        }

        private void _cancelInhale()
        {
            if (!_inhaling) return;

            _inhaling = false;
            _player.SetCharge(0f);
        }

        private void _resetBreathState()
        {
            _inhaling         = false;
            _inhaleElapsed    = 0f;
            _exhaling         = false;
            _exhaleElapsed    = 0f;
            _breathAtRelease  = 0f;
            _jumpCorrected    = false;
            _hasPendingBreath = false;
            _pendingInhale    = 0f;
            _pendingCorrected = false;
        }

        /// <summary>0-1: sube con la inspiración y baja durante la espiración.</summary>
        private float _breathLevel()
        {
            if (_inhaling) return _charge;
            if (_exhaling) return _breathAtRelease * (1f - Mathf.Clamp01(_exhaleElapsed / _tuning.exhaleSeconds));
            return 0f;
        }

        private BreathGuidePhase _guidePhase()
        {
            if (_finishing) return BreathGuidePhase.Goal;
            if (_inhaling)
                return _inhaleElapsed >= _tuning.inhaleSeconds ? BreathGuidePhase.Release : BreathGuidePhase.Inhale;
            if (!_player.IsGrounded || (_exhaling && _exhaleElapsed < _tuning.exhaleSeconds))
                return BreathGuidePhase.Exhale;
            return BreathGuidePhase.Ready;
        }
    }
}
