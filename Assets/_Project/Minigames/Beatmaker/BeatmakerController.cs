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

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null) _view = GetComponentInChildren<BeatmakerView>(true);
            if (_audioEngine == null) _audioEngine = GetComponentInChildren<BeatmakerAudioEngine>(true);
        }

        private void OnEnable()
        {
            if (_view != null)
            {
                _view.OnStepToggled      += _onStepToggled;
                _view.OnLoopToggled      += _onLoopToggled;
                _view.OnPackSelected     += _onPackSelected;
                _view.OnMetronomeToggled += _onMetronomeToggled;
                _view.OnExitRequested    += _onExitRequested;
            }

            if (_audioEngine != null)
                _audioEngine.OnStepTriggered += _onStepTriggered;
        }

        private void OnDisable()
        {
            if (_view != null)
            {
                _view.OnStepToggled      -= _onStepToggled;
                _view.OnLoopToggled      -= _onLoopToggled;
                _view.OnPackSelected     -= _onPackSelected;
                _view.OnMetronomeToggled -= _onMetronomeToggled;
                _view.OnExitRequested    -= _onExitRequested;
            }

            if (_audioEngine != null)
                _audioEngine.OnStepTriggered -= _onStepTriggered;
        }

        private void Update()
        {
            if (_pattern == null) return;

            bool running = _audioEngine != null && _audioEngine.IsRunning;
            _view?.UpdatePlayheads(running,
                                   running ? _audioEngine.CycleProgress : 0f,
                                   _pattern.ActiveLoopIndex,
                                   running ? _audioEngine.LoopProgress : 0f);

            if (!_isPlaying || _metrics == null) return;

            int layers = running ? _pattern.ActiveInstrumentCountAt(_lastTriggeredStep) : 0;
            _metrics.Tick(Time.deltaTime, layers);
            _view?.SetScore(Mathf.RoundToInt(_metrics.CalculateRelaxationScore() * 100f));
        }

        // ── MinigameBase hooks ─────────────────────────────────────────

        protected override void OnInitialize()
        {
            _pattern = new BeatmakerPatternState();
            _metrics = new BeatmakerMetricsTracker();
            _activePackIndex = _pickSoundPackIndex();
            _lastTriggeredStep = -1;
            _metronomeEnabled = false;

            _audioEngine?.Setup(_activePack, _pattern);
            _audioEngine?.SetActiveLoop(-1);
            _audioEngine?.SetMetronomeEnabled(false);
            _view?.Initialize(_soundPacks, _activePackIndex);
        }

        // El transporte no arranca hasta que el usuario activa el primer step.
        protected override void OnGameStarted() => _syncTransport();

        protected override void OnGamePaused() => _audioEngine?.Pause();

        protected override void OnGameResumed()
        {
            _audioEngine?.Resume();
            _syncTransport(); // por si se activaron/desactivaron steps durante la pausa
        }

        protected override void OnGameEnded(bool completedNaturally)
        {
            _audioEngine?.StopTransport();

            float score = _metrics?.CalculateRelaxationScore() ?? 0f;
            Dictionary<string, float> metrics = _metrics?.BuildMetrics() ?? new Dictionary<string, float>();

            _result = BuildResult(score, completedNaturally, metrics);
        }

        // ── Handlers de vista / motor de audio ─────────────────────────

        private void _onStepTriggered(int step) => _lastTriggeredStep = step;

        private void _onStepToggled(int track, int step)
        {
            if (_pattern == null) return;

            bool active = _pattern.ToggleStep(track, step);
            _view?.SetStepState(track, step, active);
            _syncTransport();
        }

        private void _onLoopToggled(int loopIndex)
        {
            if (_pattern == null) return;

            int previous = _pattern.ActiveLoopIndex;
            int nowActive = _pattern.SetActiveLoop(loopIndex);

            if (previous >= 0 && previous != nowActive)
                _view?.SetLoopState(previous, false);

            _audioEngine?.SetActiveLoop(nowActive);
            if (nowActive >= 0) _metrics?.RegisterLoopUsed();

            _view?.SetLoopState(loopIndex, nowActive == loopIndex);
        }

        private void _onPackSelected(int packIndex)
        {
            if (_soundPacks == null || packIndex < 0 || packIndex >= _soundPacks.Length) return;
            if (packIndex == _activePackIndex) return;

            _activePackIndex = packIndex;
            _lastTriggeredStep = -1;
            _audioEngine?.SetPack(_activePack);
            _view?.ApplyPack(_activePack);
        }

        private void _onMetronomeToggled()
        {
            _metronomeEnabled = !_metronomeEnabled;
            _audioEngine?.SetMetronomeEnabled(_metronomeEnabled);
            _view?.SetMetronomeState(_metronomeEnabled);
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
            if (_audioEngine == null || _pattern == null) return;

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
