using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Minigames
{
    /// <summary>
    /// Minijuego Beatmaker: secuenciador estilo FL Studio de 8 pistas (Kick/Hat/Clap/Snare,
    /// 2 variaciones cada uno) x 8 steps + loops instrumentales exclusivos, metrónomo
    /// conmutable y selector de pack. El transporte solo corre mientras haya algún step
    /// activo. Sin condición de fin: el usuario sale cuando quiere y eso cuenta como partida
    /// completada con normalidad.
    /// </summary>
    public class BeatmakerController : MinigameBase
    {
        [SerializeField] private BeatmakerView _view;
        [SerializeField] private BeatmakerAudioEngine _audioEngine;
        [SerializeField] private BeatmakerSoundPack[] _soundPacks;

        public override MinigameType Type => MinigameType.Beatmaker;

        private BeatmakerPatternState   _pattern;
        private BeatmakerMetricsTracker _metrics;
        private int  _activePackIndex = -1;
        private int  _lastTriggeredStep = -1;
        private bool _metronomeEnabled;

        // Vista y motor son obligatorios: si falta alguno el controlador no hace nada
        // (así el resto del código no necesita comprobar null en cada llamada).
        private bool _hasDependencies;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null) _view = GetComponentInChildren<BeatmakerView>(true);
            if (_audioEngine == null) _audioEngine = GetComponentInChildren<BeatmakerAudioEngine>(true);

            _hasDependencies = _view != null && _audioEngine != null;
            if (!_hasDependencies)
                Debug.LogError("[BeatmakerController] Falta asignar BeatmakerView o BeatmakerAudioEngine");
        }

        private void OnEnable()
        {
            if (!_hasDependencies) return;

            _view.OnStepToggled      += _onStepToggled;
            _view.OnLoopToggled      += _onLoopToggled;
            _view.OnPackSelected     += _onPackSelected;
            _view.OnMetronomeToggled += _onMetronomeToggled;
            _view.OnExitRequested    += _onExitRequested;
            _audioEngine.OnStepTriggered += _onStepTriggered;
        }

        private void OnDisable()
        {
            if (!_hasDependencies) return;

            _view.OnStepToggled      -= _onStepToggled;
            _view.OnLoopToggled      -= _onLoopToggled;
            _view.OnPackSelected     -= _onPackSelected;
            _view.OnMetronomeToggled -= _onMetronomeToggled;
            _view.OnExitRequested    -= _onExitRequested;
            _audioEngine.OnStepTriggered -= _onStepTriggered;
        }

        private void Update()
        {
            if (_pattern == null) return;

            bool running = _audioEngine.IsRunning;
            _view.UpdatePlayheads(running,
                                  running ? _audioEngine.CycleProgress : 0f,
                                  _pattern.ActiveLoopIndex,
                                  running ? _audioEngine.LoopProgress : 0f);

            if (!_isPlaying) return;

            int layers = running ? _pattern.ActiveInstrumentCountAt(_lastTriggeredStep) : 0;
            _metrics.Tick(Time.deltaTime, _pattern.ActiveStepCount, layers);
            _view.SetScore(MinigameOutcome.ToDisplayScore(_metrics.CalculateRelaxationScore()));
        }

        // ── MinigameBase hooks ─────────────────────────────────────────

        protected override void OnInitialize()
        {
            if (!_hasDependencies) return;

            _pattern = new BeatmakerPatternState();
            _metrics = new BeatmakerMetricsTracker();
            _activePackIndex = _pickSoundPackIndex();
            _lastTriggeredStep = -1;
            _metronomeEnabled = false;

            _audioEngine.Setup(_activePack, _pattern);
            _audioEngine.SetActiveLoop(-1);
            _audioEngine.SetMetronomeEnabled(false);
            _view.Initialize(_soundPacks, _activePackIndex);
        }

        // El transporte no arranca hasta que el usuario activa el primer step.
        protected override void OnGameStarted() => _syncTransport();

        protected override void OnGamePaused()
        {
            if (_hasDependencies) _audioEngine.Pause();
        }

        protected override void OnGameResumed()
        {
            if (!_hasDependencies) return;

            _audioEngine.Resume();
            _syncTransport(); // por si se activaron/desactivaron steps durante la pausa
        }

        protected override void OnGameEnded(bool completedNaturally)
        {
            if (_hasDependencies) _audioEngine.StopTransport();

            float score = _metrics?.CalculateRelaxationScore() ?? 0f;
            Dictionary<string, float> metrics = _metrics?.BuildMetrics() ?? new Dictionary<string, float>();

            _result = BuildResult(score, completedNaturally, metrics);
            _result.CoinReward = _metrics?.CalculateCoinReward() ?? 0;
        }

        // ── Handlers de vista / motor de audio ─────────────────────────

        private void _onStepTriggered(int step) => _lastTriggeredStep = step;

        private void _onStepToggled(int track, int step)
        {
            if (_pattern == null) return;

            _metrics.RegisterInteraction();
            bool active = _pattern.ToggleStep(track, step);
            _view.SetStepState(track, step, active);
            _syncTransport();
        }

        private void _onLoopToggled(int loopIndex)
        {
            if (_pattern == null) return;

            _metrics.RegisterInteraction();
            int previous = _pattern.ActiveLoopIndex;
            int nowActive = _pattern.SetActiveLoop(loopIndex);

            if (previous >= 0 && previous != nowActive)
                _view.SetLoopState(previous, false);

            _audioEngine.SetActiveLoop(nowActive);
            _view.SetLoopState(loopIndex, nowActive == loopIndex);
        }

        private void _onPackSelected(int packIndex)
        {
            if (_pattern == null) return;
            if (_soundPacks == null || packIndex < 0 || packIndex >= _soundPacks.Length) return;
            if (packIndex == _activePackIndex) return;

            _metrics.RegisterInteraction();
            _activePackIndex = packIndex;
            _lastTriggeredStep = -1;
            _audioEngine.SetPack(_activePack);
            _view.ApplyPack(_activePack);
        }

        private void _onMetronomeToggled()
        {
            if (_pattern == null) return;

            _metrics.RegisterInteraction();
            _metronomeEnabled = !_metronomeEnabled;
            _audioEngine.SetMetronomeEnabled(_metronomeEnabled);
            _view.SetMetronomeState(_metronomeEnabled);
        }

        // El usuario sale cuando quiere: salir es una finalización normal, no un abandono.
        private void _onExitRequested() => EndGame(completedNaturally: true);

        // ── Helpers privados ───────────────────────────────────────────

        private BeatmakerSoundPack _activePack =>
            _soundPacks != null && _activePackIndex >= 0 && _activePackIndex < _soundPacks.Length
                ? _soundPacks[_activePackIndex]
                : null;

        /// <summary>
        /// Arranca el transporte (desde el step 1) al activar el primer step y lo para al
        /// desactivar el último. En pausa solo se permite parar, nunca arrancar.
        /// </summary>
        private void _syncTransport()
        {
            if (!_hasDependencies || _pattern == null) return;

            bool hasSteps = _pattern.HasAnyStepActive();

            if (hasSteps && !_audioEngine.IsRunning && _isPlaying)
            {
                _lastTriggeredStep = -1;
                _audioEngine.StartTransport();
            }
            else if (!hasSteps && _audioEngine.IsRunning)
            {
                _lastTriggeredStep = -1;
                _audioEngine.StopTransport();
            }
        }

        private int _pickSoundPackIndex()
        {
            if (_soundPacks == null || _soundPacks.Length == 0) return -1;
            return Random.Range(0, _soundPacks.Length);
        }
    }
}
