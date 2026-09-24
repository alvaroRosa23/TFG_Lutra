using System.Collections.Generic;

namespace Lutra.Minigames
{
    /// <summary>
    /// Acumula, mientras dura la partida, los tres factores del RelaxationScore de
    /// Beatmaker y los récords de sesión (max_layers, longest_groove, session_richness).
    /// Se alimenta llamando a Tick() cada frame con cuántos instrumentos suenan
    /// en el step actual del patrón.
    /// </summary>
    public class BeatmakerMetricsTracker
    {
        private float _totalTime;
        private float _timeWithSound;          // engagement: ≥1 instrumento activo
        private float _timeWithRichness2Plus;   // session_richness: ≥2 a la vez
        private float _timeWithDiversity3Plus;  // diversidad instrumental: ≥3 a la vez

        private int   _maxLayers;
        private float _currentGrooveStreak;
        private float _longestGroove;
        private bool  _usedLoop;

        public void Tick(float deltaSeconds, int activeInstrumentsAtCurrentStep)
        {
            if (deltaSeconds <= 0f) return;

            _totalTime += deltaSeconds;

            if (activeInstrumentsAtCurrentStep >= 1) _timeWithSound += deltaSeconds;
            if (activeInstrumentsAtCurrentStep >= 2) _timeWithRichness2Plus += deltaSeconds;
            if (activeInstrumentsAtCurrentStep >= 3) _timeWithDiversity3Plus += deltaSeconds;

            if (activeInstrumentsAtCurrentStep > _maxLayers)
                _maxLayers = activeInstrumentsAtCurrentStep;

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

        public void RegisterLoopUsed() => _usedLoop = true;

        /// <summary>0.0-1.0: 40% diversidad instrumental + 40% engagement + 20% uso de loop.</summary>
        public float CalculateRelaxationScore()
        {
            if (_totalTime <= 0f) return 0f;

            float diversity  = _timeWithDiversity3Plus / _totalTime;
            float engagement = _timeWithSound / _totalTime;
            float loopScore  = _usedLoop ? 1f : 0f;

            return diversity * 0.4f + engagement * 0.4f + loopScore * 0.2f;
        }

        public Dictionary<string, float> BuildMetrics()
        {
            return new Dictionary<string, float>
            {
                ["max_layers"]       = _maxLayers,
                ["longest_groove"]   = _longestGroove,
                ["session_richness"] = _totalTime > 0f ? _timeWithRichness2Plus / _totalTime : 0f
            };
        }
    }
}
