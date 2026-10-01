using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;

namespace Lutra.Minigames
{
    /// <summary>
    /// Decide cuándo y cómo se lanzan los elementos de FruitNinja (lógica pura, sin escena).
    ///
    /// Oleadas desde abajo cuyo ritmo baja con la partida: Descarga → Transición → Calma
    /// (intervalo, tamaño de oleada, tiempo de vuelo y tamaño se interpolan con la "calma", 0-1).
    /// Cada oleada puede incluir un especial (probabilidad que baja a 0 en Calma). Al cortar un
    /// especial, TriggerSpecial lanza su efecto: Burst encola un grupo junto; Rain y Crossfire
    /// sustituyen a las oleadas durante unos segundos lanzando desde arriba o desde los lados.
    ///
    /// El reloj de fase (elapsed) es tiempo real de partida; los intervalos avanzan con el tiempo
    /// del mundo (worldDeltaTime), así la cámara lenta también ralentiza las oleadas.
    /// </summary>
    public class FruitNinjaSpawnPlanner
    {
        private struct QueuedSpawn
        {
            public float        delay;
            public SpawnRequest request;
        }

        private readonly FruitNinjaTuning   _tuning;
        private readonly System.Random      _random;
        private readonly List<QueuedSpawn>  _queue = new List<QueuedSpawn>();

        private float _waveTimer;
        private float _sinceSpecial;

        private SliceableKind _effect = SliceableKind.Normal;
        private float _effectLeft;
        private float _effectTimer;
        private bool  _crossfireFromLeft;

        public FruitNinjaPhase Phase { get; private set; } = FruitNinjaPhase.Release;

        public FruitNinjaSpawnPlanner(FruitNinjaTuning tuning, int seed)
        {
            _tuning = tuning;
            _random = new System.Random(seed);
            _waveTimer = tuning.firstWaveDelay;
        }

        /// <summary>0 = Descarga, 1 = Calma (suavizado durante la Transición).</summary>
        public float Calmness(float elapsed)
            => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_tuning.releaseEndSeconds, _tuning.calmStartSeconds, elapsed));

        /// <summary>Se sigue lanzando mientras quede tiempo para que lo lanzado caiga antes del final.</summary>
        public bool IsSpawning(float elapsed) => elapsed < _tuning.durationSeconds - _tuning.LastSpawnMargin;

        /// <summary>Avanza el planificador y añade a output los lanzamientos de este frame.</summary>
        public void Tick(float elapsed, float worldDeltaTime, List<SpawnRequest> output)
        {
            Phase = elapsed < _tuning.releaseEndSeconds ? FruitNinjaPhase.Release
                  : elapsed < _tuning.calmStartSeconds  ? FruitNinjaPhase.Transition
                  : FruitNinjaPhase.Calm;

            _sinceSpecial += worldDeltaTime;
            _flushQueue(worldDeltaTime, output);

            if (!IsSpawning(elapsed))
            {
                _effectLeft = 0f;
                return;
            }

            float calm = Calmness(elapsed);

            if (_effectLeft > 0f)
            {
                _tickEffect(worldDeltaTime, calm, output);
                return;
            }

            _waveTimer -= worldDeltaTime;
            if (_waveTimer > 0f) return;

            _queueWave(calm);
            _waveTimer = Mathf.Lerp(_range(_tuning.releaseWaveInterval), _range(_tuning.calmWaveInterval), calm);
        }

        /// <summary>Efecto de un especial recién cortado (SlowMotion lo gestiona el controlador).</summary>
        public void TriggerSpecial(SliceableKind kind, float elapsed)
        {
            if (!IsSpawning(elapsed)) return;

            float calm = Calmness(elapsed);
            switch (kind)
            {
                case SliceableKind.Burst:
                    _queueBurst(calm);
                    break;

                case SliceableKind.Rain:
                    _startEffect(kind, _tuning.rainSeconds);
                    break;

                case SliceableKind.Crossfire:
                    _startEffect(kind, _tuning.crossfireSeconds);
                    _crossfireFromLeft = _random.NextDouble() < 0.5;
                    break;
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _flushQueue(float deltaTime, List<SpawnRequest> output)
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                var queued = _queue[i];
                queued.delay -= deltaTime;
                if (queued.delay > 0f)
                {
                    _queue[i] = queued;
                    continue;
                }

                output.Add(queued.request);
                _queue.RemoveAt(i);
            }
        }

        private void _queueWave(float calm)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(_rangeInclusive(_tuning.releaseWaveSize),
                                                                 _rangeInclusive(_tuning.calmWaveSize), calm)));

            int specialIndex = -1;
            if (_sinceSpecial >= _tuning.minSecondsBetweenSpecials &&
                _tuning.enabledSpecials != null && _tuning.enabledSpecials.Length > 0 &&
                _random.NextDouble() < _tuning.specialChancePerWave * (1f - calm))
            {
                specialIndex = _random.Next(count);
                _sinceSpecial = 0f;
            }

            for (int i = 0; i < count; i++)
            {
                var request = _bottomRequest(calm, _range(-1f, 1f));
                if (i == specialIndex)
                    request.kind = _tuning.enabledSpecials[_random.Next(_tuning.enabledSpecials.Length)];

                _enqueue(request, i * _tuning.waveStagger);
            }
        }

        /// <summary>Grupo de elementos casi juntos: pensado para un combo grande de un solo trazo.</summary>
        private void _queueBurst(float calm)
        {
            float center = _range(-0.5f, 0.5f);
            float flight = Mathf.Lerp(_tuning.releaseFlightTime, _tuning.calmFlightTime, calm);

            for (int i = 0; i < _tuning.burstCount; i++)
            {
                var request = _bottomRequest(calm, Mathf.Clamp(center + _range(-0.18f, 0.18f), -1f, 1f));
                request.flightTime   = flight;
                request.apexFraction = _tuning.apexHeightRange.y - _range(0f, 0.08f);
                request.drift        = _range(-0.2f, 0.2f);
                _enqueue(request, i * 0.04f);
            }
        }

        private void _startEffect(SliceableKind kind, float seconds)
        {
            _effect      = kind;
            _effectLeft  = seconds;
            _effectTimer = 0f;
        }

        private void _tickEffect(float deltaTime, float calm, List<SpawnRequest> output)
        {
            _effectLeft  -= deltaTime;
            _effectTimer -= deltaTime;
            if (_effectTimer > 0f) return;

            var request = _bottomRequest(calm, 0f);
            switch (_effect)
            {
                case SliceableKind.Rain:
                    request.origin = SpawnOrigin.Top;
                    request.lane   = _range(-1f, 1f);
                    _effectTimer   = _tuning.rainInterval;
                    break;

                case SliceableKind.Crossfire:
                    request.origin       = _crossfireFromLeft ? SpawnOrigin.Left : SpawnOrigin.Right;
                    request.lane         = _range(0f, 1f);
                    request.apexFraction = _range(_tuning.sideApexRange);
                    _crossfireFromLeft   = !_crossfireFromLeft;
                    _effectTimer         = _tuning.crossfireInterval;
                    break;

                default:
                    _effectLeft = 0f;
                    return;
            }

            output.Add(request);

            // Al terminar el efecto, la siguiente oleada normal llega con un respiro
            if (_effectLeft <= 0f) _waveTimer = Mathf.Max(_waveTimer, 0.8f);
        }

        private SpawnRequest _bottomRequest(float calm, float lane)
        {
            float flight = Mathf.Lerp(_tuning.releaseFlightTime, _tuning.calmFlightTime, calm);
            return new SpawnRequest
            {
                origin       = SpawnOrigin.Bottom,
                kind         = SliceableKind.Normal,
                flightTime   = flight * _range(0.92f, 1.08f),
                sizeScale    = Mathf.Lerp(_tuning.releaseSizeScale, _tuning.calmSizeScale, calm),
                lane         = lane,
                apexFraction = _range(_tuning.apexHeightRange),
                drift        = _range(-1f, 1f),
                spin         = _range(-_tuning.maxSpin, _tuning.maxSpin) * Mathf.Lerp(1f, 0.4f, calm)
            };
        }

        private void _enqueue(SpawnRequest request, float delay)
            => _queue.Add(new QueuedSpawn { delay = delay, request = request });

        private float _range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
        private float _range(Vector2 range) => _range(range.x, range.y);
        private int   _rangeInclusive(Vector2Int range) => _random.Next(range.x, range.y + 1);
    }
}
