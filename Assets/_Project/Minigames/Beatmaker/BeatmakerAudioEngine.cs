using System;
using UnityEngine;
using UnityEngine.Serialization;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Minigames
{
    /// <summary>
    /// Transporte del secuenciador de Beatmaker. El reloj se basa en AudioSettings.dspTime
    /// (no en Time.deltaTime, que acumula deriva): igual que en FL Studio, cada step (un
    /// cuadrado del secuenciador) es una semicorchea y se programa exactamente
    /// 60 / (bpm * 4) segundos después del anterior. 8 steps = 2 beats.
    ///
    /// El transporte solo corre mientras el controlador lo mantiene arrancado (hay al menos
    /// un step activo). El loop instrumental ocupa N vueltas del patrón según la duración de
    /// su clip (redondeada) y se reinicia en el step 1 de cada N-ésima vuelta. El metrónomo
    /// suena en cada beat (steps 1 y 5), con acento en el step 1, si está activado.
    /// </summary>
    public class BeatmakerAudioEngine : MonoBehaviour
    {
        [Header("Fuentes (si se dejan vacías se crean en Awake)")]
        [FormerlySerializedAs("_beatSource")] [SerializeField] private AudioSource _stepSource;
        [SerializeField] private AudioSource _loopSource;
        [SerializeField] private AudioSource _metronomeSource;

        [Header("Metrónomo (si se dejan vacíos se genera un 'tic' sintético)")]
        [SerializeField] private AudioClip _metronomeClip;
        [SerializeField] private AudioClip _metronomeAccentClip;
        [SerializeField, Range(0f, 1f)] private float _metronomeVolume = 0.8f;

        private const int DefaultBpm = 90;

        /// <summary>Disparado justo cuando un step pasa a sonar (para métricas).</summary>
        public event Action<int> OnStepTriggered;

        private BeatmakerSoundPack    _pack;
        private BeatmakerPatternState _pattern;
        private double _secondsPerStep;

        private double _cycleStartDsp;   // dspTime en que empezó (o debía empezar) el step 1 del ciclo actual
        private int    _cycleCount;      // vueltas completas desde que arrancó el transporte (-1 antes del primer step 1)
        private double _nextStepDspTime;
        private int    _nextStepIndex;
        private bool   _running;
        private bool   _paused;
        private double _pauseDsp;
        private bool   _loopWasPlaying;

        private int  _activeLoop = -1;
        private bool _metronomeEnabled;

        private AudioClip _generatedClick;
        private AudioClip _generatedAccent;

        public bool IsRunning => _running;

        /// <summary>Posición (0-1) dentro del ciclo de 8 steps; 0 si el transporte está parado.</summary>
        public float CycleProgress
        {
            get
            {
                if (!_running) return 0f;

                double now = _paused ? _pauseDsp : AudioSettings.dspTime;
                double progress = (now - _cycleStartDsp) / _cycleDuration;
                return Mathf.Clamp((float)progress, 0f, 0.9999f);
            }
        }

        /// <summary>
        /// Posición (0-1) dentro del loop activo, que dura N vueltas del patrón.
        /// 0 si no hay transporte o loop con clip.
        /// </summary>
        public float LoopProgress
        {
            get
            {
                if (!_running || _activeLoopClip == null) return 0f;

                int cycles = _loopCycles(_activeLoopClip);
                return Mathf.Clamp((_loopCycleIndex(cycles) + CycleProgress) / cycles, 0f, 0.9999f);
            }
        }

        private double _cycleDuration => _secondsPerStep * BeatmakerPatternState.StepCount;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _stepSource      = _ensureSource(_stepSource);
            _loopSource      = _ensureSource(_loopSource);
            _metronomeSource = _ensureSource(_metronomeSource);

            if (_metronomeClip == null)
                _metronomeClip = _generatedClick = _createClick("MetronomeClick", 1000f);
            if (_metronomeAccentClip == null)
                _metronomeAccentClip = _generatedAccent = _createClick("MetronomeAccent", 1600f);
        }

        private void OnDestroy()
        {
            OnStepTriggered = null;
            if (_generatedClick != null) Destroy(_generatedClick);
            if (_generatedAccent != null) Destroy(_generatedAccent);
        }

        private void Update()
        {
            if (!_running || _paused || _pattern == null) return;
            if (AudioSettings.dspTime < _nextStepDspTime) return;

            _triggerStep(_nextStepIndex);

            _nextStepIndex    = (_nextStepIndex + 1) % BeatmakerPatternState.StepCount;
            _nextStepDspTime += _secondsPerStep;
        }

        // ── API pública ────────────────────────────────────────────────

        public void Setup(BeatmakerSoundPack pack, BeatmakerPatternState pattern)
        {
            _pattern = pattern;
            _applyPackTiming(pack);
        }

        /// <summary>
        /// Cambia de pack en caliente. Si el transporte está sonando, se reinicia desde
        /// el step 1 con el nuevo tempo para que la línea y el loop sigan sincronizados.
        /// </summary>
        public void SetPack(BeatmakerSoundPack pack)
        {
            _applyPackTiming(pack);

            if (!_running) return;

            bool wasPaused = _paused;
            StartTransport();
            if (wasPaused) Pause();
        }

        /// <summary>Arranca el patrón desde el step 1.</summary>
        public void StartTransport()
        {
            _stopLoopSource();

            double now = AudioSettings.dspTime;
            _nextStepIndex   = 0;
            _nextStepDspTime = now;
            _cycleStartDsp   = now;
            _cycleCount      = -1;
            _running = true;
            _paused  = false;
        }

        public void StopTransport()
        {
            _running = false;
            _paused  = false;
            _stopLoopSource();
        }

        public void Pause()
        {
            if (!_running || _paused) return;

            _paused   = true;
            _pauseDsp = AudioSettings.dspTime;
            _loopWasPlaying = _loopSource != null && _loopSource.isPlaying;
            if (_loopWasPlaying) _loopSource.Pause();
        }

        public void Resume()
        {
            if (!_running || !_paused) return;

            // Desplaza el reloj lo que duró la pausa: evita una ráfaga de steps "atrasados".
            double shift = AudioSettings.dspTime - _pauseDsp;
            _cycleStartDsp   += shift;
            _nextStepDspTime += shift;
            _paused = false;

            if (_loopWasPlaying && _loopSource != null) _loopSource.UnPause();
        }

        /// <summary>
        /// Cambia el loop activo (-1 = ninguno). Si el transporte está sonando, el nuevo loop
        /// entra en la posición que le toca (vuelta actual dentro de sus N vueltas + posición
        /// en el ciclo) para seguir alineado con la línea de steps.
        /// </summary>
        public void SetActiveLoop(int loopIndex)
        {
            _activeLoop = loopIndex;

            if (_activeLoop < 0 || !_running)
            {
                _stopLoopSource();
                return;
            }

            // En pausa: se retomará al volver al step 1.
            if (_paused)
            {
                _stopLoopSource();
                _loopWasPlaying = false;
                return;
            }

            var clip = _activeLoopClip;
            if (clip == null) { _stopLoopSource(); return; }

            double offset = _loopCycleIndex(_loopCycles(clip)) * _cycleDuration
                          + (AudioSettings.dspTime - _cycleStartDsp);
            _playLoopFrom(offset);
        }

        public void SetMetronomeEnabled(bool enabled) => _metronomeEnabled = enabled;

        // ── Helpers privados ───────────────────────────────────────────

        private void _applyPackTiming(BeatmakerSoundPack pack)
        {
            _pack = pack;

            if (pack != null && pack.bpm > 0)
            {
                _secondsPerStep = pack.SecondsPerStep;
            }
            else
            {
                _secondsPerStep = 60.0 / (DefaultBpm * BeatmakerSoundPack.StepsPerBeat);
            }
        }

        private void _triggerStep(int step)
        {
            if (step == 0)
            {
                // Vuelta al step 1: el ciclo empieza en el instante programado (no en "ahora").
                // El loop solo se reinicia al empezar cada bloque de N vueltas, compensando el
                // retraso de frame.
                _cycleStartDsp = _nextStepDspTime;
                _cycleCount++;

                var loopClip = _activeLoopClip;
                if (loopClip != null && _loopCycleIndex(_loopCycles(loopClip)) == 0)
                    _playLoopFrom(AudioSettings.dspTime - _cycleStartDsp);
            }

            if (_pack != null && _stepSource != null)
            {
                for (int track = 0; track < BeatmakerPatternState.TrackCount; track++)
                {
                    if (!_pattern.IsStepActive(track, step)) continue;

                    var clip = _pack.GetClip(BeatmakerPatternState.InstrumentOf(track),
                                             BeatmakerPatternState.VariationOf(track));
                    if (clip != null) _stepSource.PlayOneShot(clip);
                }
            }

            if (_metronomeEnabled && step % BeatmakerSoundPack.StepsPerBeat == 0)
                _playMetronome(isAccent: step == 0);

            OnStepTriggered?.Invoke(step);
        }

        private void _playMetronome(bool isAccent)
        {
            if (_metronomeSource == null) return;

            var clip = isAccent ? _metronomeAccentClip : _metronomeClip;
            if (clip != null) _metronomeSource.PlayOneShot(clip, _metronomeVolume);
        }

        private AudioClip _activeLoopClip
        {
            get
            {
                if (_pack == null || _pack.loopClips == null || _activeLoop < 0 || _activeLoop >= _pack.loopClips.Length) return null;
                return _pack.loopClips[_activeLoop];
            }
        }

        /// <summary>Cuántas vueltas del patrón ocupa un clip de loop (mínimo 1, redondeado).</summary>
        private int _loopCycles(AudioClip clip)
        {
            if (clip == null || _cycleDuration <= 0.0) return 1;
            return Math.Max(1, (int)Math.Round(clip.length / _cycleDuration));
        }

        /// <summary>En qué vuelta (0..cycles-1) del loop estamos, contando desde que arrancó el transporte.</summary>
        private int _loopCycleIndex(int cycles) => Math.Max(0, _cycleCount) % cycles;

        private void _playLoopFrom(double offsetSeconds)
        {
            if (_loopSource == null) return;

            var clip = _activeLoopClip;
            if (clip == null) { _stopLoopSource(); return; }

            _loopSource.clip = clip;
            _loopSource.loop = true;
            _loopSource.Play();
            _loopSource.time = (float)(Math.Max(0.0, offsetSeconds) % clip.length);
        }

        private void _stopLoopSource()
        {
            if (_loopSource == null) return;
            _loopSource.Stop();
        }

        private AudioSource _ensureSource(AudioSource source)
        {
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        /// <summary>"Tic" corto de metrónomo: seno con caída exponencial rápida.</summary>
        private static AudioClip _createClick(string clipName, float frequency)
        {
            int sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
            int length = Mathf.CeilToInt(sampleRate * 0.05f);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 90f);
            }

            var clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
