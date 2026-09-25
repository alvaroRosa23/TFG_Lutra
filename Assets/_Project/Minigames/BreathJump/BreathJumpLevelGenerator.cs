using System.Collections.Generic;
using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>Posición (centro de la superficie superior) y ancho de una plataforma.</summary>
    public readonly struct PlatformLayout
    {
        public readonly Vector2 TopCenter;
        public readonly float   Width;
        public readonly bool    IsGoal;

        public PlatformLayout(Vector2 topCenter, float width, bool isGoal)
        {
            TopCenter = topCenter;
            Width     = width;
            IsGoal    = isGoal;
        }
    }

    /// <summary>
    /// Genera de forma procedural (y determinista para una semilla) el recorrido de
    /// BreathJump: avanza hacia la derecha subiendo suavemente. La plataforma
    /// breathsToComplete es la meta. Los layouts se generan bajo demanda y se cachean.
    /// </summary>
    public class BreathJumpLevelGenerator
    {
        private readonly BreathJumpTuning     _tuning;
        private readonly System.Random        _rng;
        private readonly List<PlatformLayout> _layouts = new List<PlatformLayout>();

        public BreathJumpLevelGenerator(BreathJumpTuning tuning, int seed)
        {
            _tuning = tuning;
            _rng    = new System.Random(seed);
        }

        /// <summary>Índice de la plataforma meta.</summary>
        public int GoalIndex => Mathf.Max(1, _tuning.breathsToComplete);

        public PlatformLayout Get(int index)
        {
            index = Mathf.Clamp(index, 0, GoalIndex);
            while (_layouts.Count <= index) _generateNext();
            return _layouts[index];
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _generateNext()
        {
            int index = _layouts.Count;

            if (index == 0)
            {
                _layouts.Add(new PlatformLayout(Vector2.zero, _tuning.startPlatformWidth, false));
                return;
            }

            var  previous = _layouts[index - 1];
            bool isGoal   = index == GoalIndex;

            float width = isGoal ? _tuning.goalPlatformWidth : _range(_tuning.widthRange);
            float minGap = previous.Width * 0.5f + width * 0.5f + _tuning.minEdgeGap;
            float gap   = Mathf.Max(_range(_tuning.gapRange), minGap);
            float rise  = _range(_tuning.riseRange);

            _layouts.Add(new PlatformLayout(previous.TopCenter + new Vector2(gap, rise), width, isGoal));
        }

        private float _range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)_rng.NextDouble());
    }
}
