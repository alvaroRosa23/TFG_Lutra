using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Minigames
{
    /// <summary>
    /// Transporte del secuenciador de Beatmaker. Igual que en FL Studio, cada step (un
    /// cuadrado del secuenciador) es una semicorchea que dura 60 / (bpm * 4) s; 8 steps = 2 beats.
    ///
    /// Todo el audio se programa con AudioSource.PlayScheduled sobre AudioSettings.dspTime con
    /// un margen de anticipación (_scheduleAhead): los steps, el metrónomo y el loop suenan en el
    /// sample exacto, sin depender de cuándo se ejecute Update (a 30 fps un frame son 33 ms, que
    /// a más de 90 BPM ya se nota como "arrastre"). El tiempo de cada step se calcula desde el
    /// arranque (inicio + n * duración), así que no acumula deriva.
    ///
    /// El loop instrumental ocupa N vueltas del patrón (N = duración del clip / duración de la
    /// vuelta, redondeado) y se reprograma al empezar cada bloque de N vueltas. Si el clip no
    /// dura exactamente N vueltas (loop grabado a otro tempo o mal recortado), con
    /// _fitLoopsToTempo se ajusta su pitch para que dure justo eso: así no se desfasa respecto a
    /// la rejilla ni se corta antes de que la línea del loop llegue al final.
    /// </summary>
    public class BeatmakerAudioEngine : MonoBehaviour
    {
        [Header("Fuentes plantilla (mixer y volumen; si se dejan vacías se crean en Awake)")]
        [FormerlySerializedAs("_beatSource")] [SerializeField] private AudioSource _stepSource;
        [SerializeField] private AudioSource _loopSource;
        [SerializeField] private AudioSource _metronomeSource;

        [Header("Metrónomo (si se dejan vacíos se genera un 'tic' sintético)")]
        [SerializeField] private AudioClip _metronomeClip;
        [SerializeField] private AudioClip _metronomeAccentClip;
        [SerializeField, Range(0f, 1f)] private float _metronomeVolume = 0.8f;

        [Header("Programación de audio")]
        [Tooltip("Segundos de antelación con que se programan los sonidos. Debe superar el peor frame (móvil a 30 fps ≈ 0,033 s)")]
        [SerializeField, Range(0.04f, 0.3f)] private float _scheduleAhead = 0.1f;
        [Tooltip("Voces simultáneas para steps y metrónomo (si se agotan se corta la cola más antigua)")]
        [SerializeField, Range(8, 64)] private int _voiceCount = 32;
        [Tooltip("Ajusta el pitch de cada loop para que dure exactamente sus N vueltas del patrón")]
        [SerializeField] private bool _fitLoopsToTempo = true;

        private const int    DefaultBpm        = 90;
        private const double StartDelay        = 0.05;  // margen para programar el primer step con precisión
        private const float  LoopFitWarning    = 0.01f; // avisa si un loop se desvía más de un 1 % del tempo

        /// <summary>Disparado justo cuando un step pasa a sonar (para métricas).</summary>
        public event Action<int> OnStepTriggered;

        private BeatmakerSoundPack    _pack;
        private BeatmakerPatternState _pattern;
        private double _secondsPerStep;

        private bool   _running;
        private bool   _paused;
        private double _pauseDsp;
        private double _transportStartDsp; // dspTime del step 1 de la primera vuelta
        private long   _nextStepNumber;    // siguiente step sin programar, contando desde el arranque

        // Steps ya programados pendientes de sonar (para OnStepTriggered)
        private readonly Queue<(int step, double time)> _pendingSteps = new Queue<(int, double)>();

        // Voces para steps y metrónomo
        private AudioSource[] _voices;
        private double[]      _voiceStart;
        private double[]      _voiceEnd;

        // Loop: dos fuentes alternas para enlazar bloques sin huecos
        private readonly AudioSource[] _loopSources = new AudioSource[2];
        private int    _currentLoopSource;
        private double _nextLoopBlockDsp = double.MaxValue; // inicio del próximo bloque sin programar

        private int  _activeLoop = -1;
        private bool _metronomeEnabled;

        private AudioClip _generatedClick;
        private AudioClip _generatedAccent;
        private readonly HashSet<AudioClip> _warnedLoops = new HashSet<AudioClip>();

        public bool IsRunning => _running;

        /// <summary>Posición (0-1) dentro del ciclo de 8 steps; 0 si el transporte está parado.</summary>
        public float CycleProgress
        {
            get
            {
                if (!_running || _cycleDuration <= 0.0) return 0f;

                double cycles = _elapsed / _cycleDuration;
                return Mathf.Clamp((float)(cycles - Math.Floor(cycles)), 0f, 0.9999f);
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
                var clip = _activeLoopClip;
                if (!_running || clip == null || _cycleDuration <= 0.0) return 0f;

                double blocks = _elapsed / _loopBlockDuration(clip);
                return Mathf.Clamp((float)(blocks - Math.Floor(blocks)), 0f, 0.9999f);
            }
        }

        private double _cycleDuration => _secondsPerStep * BeatmakerPatternState.StepCount;

        /// <summary>Segundos desde el step 1 de la primera vuelta (congelado en pausa, nunca negativo).</summary>
        private double _elapsed
        {
            get
            {
                double now = _paused ? _pauseDsp : AudioSettings.dspTime;
                return Math.Max(0.0, now - _transportStartDsp);
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _stepSource      = _ensureSource(_stepSource);
            _loopSource      = _ensureSource(_loopSource);
            _metronomeSource = _ensureSource(_metronomeSource);

            _loopSources[0] = _loopSource;
            _loopSources[1] = _createSourceLike(_loopSource);

            _voices     = new AudioSource[_voiceCount];
            _voiceStart = new double[_voiceCount];
            _voiceEnd   = new double[_voiceCount];
            for (int i = 0; i < _voiceCount; i++)
                _voices[i] = _createSourceLike(_stepSource);

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

            double now = AudioSettings.dspTime;
            _notifyPlayedSteps(now);

            // Tras un parón largo (carga, frame muy lento) no se recuperan los steps perdidos:
            // se salta al primero que aún no ha pasado para evitar una ráfaga de golpes.
            if (_stepTime(_nextStepNumber) < now - _secondsPerStep)
                _nextStepNumber = _firstStepAfter(now);

            double horizon = now + _scheduleAhead;
            while (_stepTime(_nextStepNumber) < horizon)
            {
                _scheduleStep(_nextStepNumber);
                _nextStepNumber++;
            }

            _scheduleLoopBlocks(now, horizon);
        }

        // ── API pública ────────────────────────────────────────────────

        public void Setup(BeatmakerSoundPack pack, BeatmakerPatternState pattern)
        {
            _pattern = pattern;
            _applyPack(pack);
        }

        /// <summary>
        /// Cambia de pack en caliente. Si el transporte está sonando, se reinicia desde
        /// el step 1 con el nuevo tempo para que la línea y el loop sigan sincronizados.
        /// </summary>
        public void SetPack(BeatmakerSoundPack pack)
        {
            _applyPack(pack);

            if (!_running) return;

            bool wasPaused = _paused;
            StartTransport();
            if (wasPaused) Pause();
        }

        /// <summary>Arranca el patrón desde el step 1.</summary>
        public void StartTransport()
        {
            _cancelScheduled(AudioSettings.dspTime);

            _transportStartDsp = AudioSettings.dspTime + StartDelay;
            _nextStepNumber    = 0;
            _running = true;
            _paused  = false;

            // El primer bloque del loop empieza con el primer step
            _nextLoopBlockDsp = _activeLoopClip != null ? _transportStartDsp : double.MaxValue;
        }

        public void StopTransport()
        {
            _cancelScheduled(AudioSettings.dspTime);
            _running = false;
            _paused  = false;
        }

        public void Pause()
        {
            if (!_running || _paused) return;

            double now = AudioSettings.dspTime;
            _paused   = true;
            _pauseDsp = now;

            // Lo ya programado para después de la pausa se cancela y se reprograma al reanudar
            _cancelScheduled(now);
            _nextStepNumber = _firstStepAfter(now);
        }

        public void Resume()
        {
            if (!_running || !_paused) return;

            // Desplaza el reloj lo que duró la pausa: el patrón sigue donde se quedó.
            _transportStartDsp += AudioSettings.dspTime - _pauseDsp;
            _paused = false;

            _startLoopInCurrentBlock();
        }

        /// <summary>
        /// Cambia el loop activo (-1 = ninguno). Si el transporte está sonando, el nuevo loop
        /// entra en la posición que le toca dentro de su bloque de N vueltas para seguir
        /// alineado con la línea de steps.
        /// </summary>
        public void SetActiveLoop(int loopIndex)
        {
            _activeLoop = loopIndex;
            _stopLoopSources();

            // En pausa o parado: se programará al reanudar / arrancar.
            if (_activeLoop < 0 || !_running || _paused) return;

            _startLoopInCurrentBlock();
        }

        public void SetMetronomeEnabled(bool enabled) => _metronomeEnabled = enabled;

        // ── Programación de steps ──────────────────────────────────────

        private double _stepTime(long stepNumber) => _transportStartDsp + stepNumber * _secondsPerStep;

        /// <summary>Primer step cuyo instante es posterior a time (los anteriores ya sonaron).</summary>
        private long _firstStepAfter(double time)
        {
            if (time < _transportStartDsp) return 0;
            return (long)Math.Floor((time - _transportStartDsp) / _secondsPerStep) + 1;
        }

        private void _scheduleStep(long stepNumber)
        {
            int    step = (int)(stepNumber % BeatmakerPatternState.StepCount);
            double time = _stepTime(stepNumber);

            if (_pack != null)
            {
                for (int track = 0; track < BeatmakerPatternState.TrackCount; track++)
                {
                    if (!_pattern.IsStepActive(track, step)) continue;

                    var clip = _pack.GetClip(BeatmakerPatternState.InstrumentOf(track),
                                             BeatmakerPatternState.VariationOf(track));
                    _playVoice(clip, time, _stepSource, 1f);
                }
            }

            if (_metronomeEnabled && step % BeatmakerSoundPack.StepsPerBeat == 0)
                _playVoice(step == 0 ? _metronomeAccentClip : _metronomeClip, time, _metronomeSource, _metronomeVolume);

            _pendingSteps.Enqueue((step, time));
        }

        private void _notifyPlayedSteps(double now)
        {
            while (_pendingSteps.Count > 0 && _pendingSteps.Peek().time <= now)
                OnStepTriggered?.Invoke(_pendingSteps.Dequeue().step);
        }

        /// <summary>Programa un clip en una voz libre (o roba la que antes termine).</summary>
        private void _playVoice(AudioClip clip, double time, AudioSource template, float volumeScale)
        {
            if (clip == null || _voices == null) return;

            double now = AudioSettings.dspTime;
            int chosen = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                // Libre = ya terminó de sonar (no basta con que termine antes de 'time':
                // PlayScheduled sobre una fuente que suena la corta en el acto).
                if (_voiceEnd[i] <= now) { chosen = i; break; }
                if (_voiceEnd[i] < _voiceEnd[chosen]) chosen = i;
            }

            var voice = _voices[chosen];
            voice.outputAudioMixerGroup = template.outputAudioMixerGroup;
            voice.volume = template.volume * volumeScale;
            voice.clip   = clip;
            voice.PlayScheduled(time);

            _voiceStart[chosen] = time;
            _voiceEnd[chosen]   = time + clip.length;
        }

        /// <summary>Cancela los sonidos programados después de 'time' (los que ya suenan terminan su cola).</summary>
        private void _cancelScheduled(double time)
        {
            if (_voices != null)
            {
                for (int i = 0; i < _voices.Length; i++)
                {
                    if (_voiceStart[i] <= time) continue;
                    _voices[i].Stop();
                    _voiceStart[i] = 0.0;
                    _voiceEnd[i]   = 0.0;
                }
            }

            _pendingSteps.Clear();
            _stopLoopSources();
        }

        // ── Loop instrumental ──────────────────────────────────────────

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

        private double _loopBlockDuration(AudioClip clip) => _loopCycles(clip) * _cycleDuration;

        /// <summary>Pitch que hace durar el clip exactamente sus N vueltas (1 si no se ajusta).</summary>
        private float _loopPitch(AudioClip clip)
        {
            if (!_fitLoopsToTempo || clip == null) return 1f;

            float pitch = (float)(clip.length / _loopBlockDuration(clip));
            if (Mathf.Abs(pitch - 1f) > LoopFitWarning && _warnedLoops.Add(clip))
            {
                double realBpm = (_pack != null ? _pack.bpm : DefaultBpm) / pitch;
                Debug.LogWarning($"[BeatmakerAudioEngine] El loop '{clip.name}' ({clip.length:F3} s) no cuadra con " +
                                 $"{_loopCycles(clip)} vueltas a {(_pack != null ? _pack.bpm : DefaultBpm)} BPM " +
                                 $"(parece ir a ~{realBpm:F1} BPM). Se ajusta su pitch x{pitch:F3}; " +
                                 "conviene reexportarlo al tempo del pack.");
            }
            return pitch;
        }

        /// <summary>Arranca el loop a mitad de su bloque actual (al activarlo o al reanudar).</summary>
        private void _startLoopInCurrentBlock()
        {
            var clip = _activeLoopClip;
            if (clip == null) { _nextLoopBlockDsp = double.MaxValue; return; }

            double start = AudioSettings.dspTime + StartDelay;
            if (start <= _transportStartDsp)
            {
                // El transporte aún no ha empezado: el loop entra con el primer step
                _nextLoopBlockDsp = _transportStartDsp;
                return;
            }

            double blockDuration = _loopBlockDuration(clip);
            double blockStart = _transportStartDsp
                              + Math.Floor((start - _transportStartDsp) / blockDuration) * blockDuration;

            _playLoopScheduled(clip, start, start - blockStart);
            _nextLoopBlockDsp = blockStart + blockDuration;
        }

        /// <summary>Programa el inicio de cada bloque de N vueltas que caiga dentro del horizonte.</summary>
        private void _scheduleLoopBlocks(double now, double horizon)
        {
            var clip = _activeLoopClip;
            if (clip == null || _nextLoopBlockDsp == double.MaxValue) return;

            double blockDuration = _loopBlockDuration(clip);
            if (_nextLoopBlockDsp < now) // parón largo: se retoma en el bloque en curso
            {
                _startLoopInCurrentBlock();
                return;
            }

            while (_nextLoopBlockDsp < horizon)
            {
                _playLoopScheduled(clip, _nextLoopBlockDsp, 0.0);
                _nextLoopBlockDsp += blockDuration;
            }
        }

        /// <summary>
        /// Programa el clip en la fuente de loop libre a partir de 'offsetSeconds' (tiempo real
        /// dentro del bloque) y hace que la fuente anterior termine justo en ese instante.
        /// </summary>
        private void _playLoopScheduled(AudioClip clip, double dspStart, double offsetSeconds)
        {
            var previous = _loopSources[_currentLoopSource];
            _currentLoopSource = 1 - _currentLoopSource;
            var source = _loopSources[_currentLoopSource];
            if (source == null) return;

            float pitch = _loopPitch(clip);
            source.Stop();
            source.clip  = clip;
            source.loop  = !_fitLoopsToTempo; // sin ajuste, rellena si el clip es más corto que el bloque
            source.pitch = pitch;
            source.timeSamples = Mathf.Clamp((int)(offsetSeconds * pitch * clip.frequency), 0, clip.samples - 1);
            source.PlayScheduled(dspStart);

            if (previous != null && previous != source) previous.SetScheduledEndTime(dspStart);
        }

        private void _stopLoopSources()
        {
            foreach (var source in _loopSources)
                if (source != null) source.Stop();

            _nextLoopBlockDsp = double.MaxValue;
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _applyPack(BeatmakerSoundPack pack)
        {
            _pack = pack;
            _secondsPerStep = pack != null && pack.bpm > 0
                ? pack.SecondsPerStep
                : 60.0 / (DefaultBpm * BeatmakerSoundPack.StepsPerBeat);

            _preload(pack);
        }

        /// <summary>
        /// Carga los datos de todos los clips del pack al elegirlo: con "Preload Audio Data"
        /// desactivado, el primer golpe de cada sonido llegaría tarde mientras se carga.
        /// </summary>
        private static void _preload(BeatmakerSoundPack pack)
        {
            if (pack == null) return;

            foreach (BeatmakerInstrument instrument in Enum.GetValues(typeof(BeatmakerInstrument)))
                for (int v = 0; v < BeatmakerSoundPack.VariationsPerInstrument; v++)
                    _preload(pack.GetClip(instrument, v));

            if (pack.loopClips != null)
                foreach (var clip in pack.loopClips) _preload(clip);
        }

        private static void _preload(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        private AudioSource _ensureSource(AudioSource source)
        {
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private AudioSource _createSourceLike(AudioSource template)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake           = false;
            source.outputAudioMixerGroup = template.outputAudioMixerGroup;
            source.volume                = template.volume;
            source.priority              = template.priority;
            source.spatialBlend          = template.spatialBlend;
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
