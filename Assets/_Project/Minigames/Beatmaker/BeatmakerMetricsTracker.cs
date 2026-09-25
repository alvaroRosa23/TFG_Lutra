using System.Collections.Generic;
using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>
    /// Acumula, mientras dura la partida, la puntuación de Beatmaker (0-100) y los récords
    /// de sesión (max_layers, longest_groove, session_richness).
    /// Se alimenta llamando a Tick() cada frame con cuántas notas hay puestas en la rejilla
    /// y cuántos instrumentos suenan en el step actual del patrón, y a RegisterInteraction()
    /// cada vez que el usuario toca algún control.
    ///
    /// Puntuación:
    ///   - Tiempo: con al menos una nota puesta se ganan 2,5 puntos cada 30 s, de forma continua.
    ///   - Bonus: cada 30 s acumulados con más de 5 notas puestas, +2,5 puntos.
    ///     Jugando con bonus todo el rato se llega a 100 en 10 minutos (solo tiempo: 20 min).
    ///   - Inactividad: cada 30 s acumulados sin ninguna nota puesta, -2 puntos.
    ///   - Límite suave: tras 2 minutos sin tocar ningún control, ni el tiempo ni el bonus
    ///     suman (evita dejar el móvil sonando para farmear monedas).
    /// La puntuación se limita a [0, 100] en cada paso, así la penalización nunca deja
    /// "deuda" que se coma los puntos que se ganen después.
    /// </summary>
    public class BeatmakerMetricsTracker
    {
        public const float MaxScore             = 100f;
        public const float IntervalSeconds      = 30f;
        public const float TimePoints           = 2.5f;  // por cada 30 s con notas
        public const int   BonusNoteThreshold   = 5;     // bonus con MÁS de 5 notas
        public const float BonusPoints          = 2.5f;
        public const float InactivityPenalty    = 2f;
        public const float IdleLimitSeconds     = 120f;  // sin tocar nada: deja de sumar

        private const float TimePointsPerSecond = TimePoints / IntervalSeconds;

        private float _score;
        private float _bonusTimer;      // tiempo acumulado con > BonusNoteThreshold notas
        private float _inactivityTimer; // tiempo acumulado sin ninguna nota
        private float _idleTimer;       // tiempo desde la última interacción

        private float _totalTime;
        private float _timeWithRichness2Plus;   // session_richness: ≥2 a la vez
        private int   _maxLayers;
        private float _currentGrooveStreak;
        private float _longestGroove;
        private int   _maxNotes;

        /// <summary>Puntuación actual (0-100).</summary>
        public float Score => _score;

        public void Tick(float deltaSeconds, int activeNotes, int activeInstrumentsAtCurrentStep)
        {
            if (deltaSeconds <= 0f) return;

            _tickScore(deltaSeconds, activeNotes);
            _tickRecords(deltaSeconds, activeNotes, activeInstrumentsAtCurrentStep);
        }

        /// <summary>El usuario ha tocado algún control: reinicia el límite suave.</summary>
        public void RegisterInteraction() => _idleTimer = 0f;

        /// <summary>0.0-1.0: puntuación / 100.</summary>
        public float CalculateRelaxationScore() => _score / MaxScore;

        /// <summary>
        /// Monedas de la partida: 1 por cada 10 puntos mostrados (100 puntos = 10 monedas).
        /// Usa la misma puntuación redondeada que ve el usuario.
        /// </summary>
        public int CalculateCoinReward() => MinigameOutcome.ToDisplayScore(CalculateRelaxationScore()) / 10;

        public Dictionary<string, float> BuildMetrics()
        {
            return new Dictionary<string, float>
            {
                ["max_layers"]       = _maxLayers,
                ["longest_groove"]   = _longestGroove,
                ["session_richness"] = _totalTime > 0f ? _timeWithRichness2Plus / _totalTime : 0f,
                ["max_notes"]        = _maxNotes
            };
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _tickScore(float deltaSeconds, int activeNotes)
        {
            _idleTimer += deltaSeconds;

            if (activeNotes <= 0)
            {
                _bonusTimer = 0f;
                _inactivityTimer += deltaSeconds;
                while (_inactivityTimer >= IntervalSeconds)
                {
                    _inactivityTimer -= IntervalSeconds;
                    _addScore(-InactivityPenalty);
                }
                return;
            }

            _inactivityTimer = 0f;
            if (_idleTimer >= IdleLimitSeconds) return;

            _addScore(TimePointsPerSecond * deltaSeconds);

            if (activeNotes > BonusNoteThreshold)
            {
                _bonusTimer += deltaSeconds;
                while (_bonusTimer >= IntervalSeconds)
                {
                    _bonusTimer -= IntervalSeconds;
                    _addScore(BonusPoints);
                }
            }
        }

        private void _addScore(float delta) => _score = Mathf.Clamp(_score + delta, 0f, MaxScore);

        private void _tickRecords(float deltaSeconds, int activeNotes, int activeInstrumentsAtCurrentStep)
        {
            _totalTime += deltaSeconds;

            if (activeInstrumentsAtCurrentStep >= 2) _timeWithRichness2Plus += deltaSeconds;

            if (activeInstrumentsAtCurrentStep > _maxLayers)
                _maxLayers = activeInstrumentsAtCurrentStep;

            if (activeNotes > _maxNotes)
                _maxNotes = activeNotes;

            if (activeInstrumentsAtCurrentStep >= 4)
            {
                _currentGrooveStreak += deltaSeconds;
                if (_currentGrooveStreak > _longestGroove)
                    _longestGroove = _currentGrooveStreak;
            }
            else
            {
                _currentGrooveStreak = 0f;
            }
        }
    }
}
