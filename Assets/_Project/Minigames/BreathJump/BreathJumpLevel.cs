using System.Collections.Generic;
using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>
    /// Instancia las plataformas que genera BreathJumpLevelGenerator alrededor del jugador
    /// y recicla las que quedan lejos (pool), así nunca hay más de unas pocas en escena.
    /// </summary>
    public class BreathJumpLevel : MonoBehaviour
    {
        [SerializeField] private BreathJumpPlatform _platformPrefab;
        [SerializeField] private Transform _platformsRoot;

        [Tooltip("Plataformas que se mantienen instanciadas por delante / por detrás")]
        [SerializeField] private int _keepAhead  = 3;
        [SerializeField] private int _keepBehind = 2;

        private readonly Dictionary<int, BreathJumpPlatform> _active = new Dictionary<int, BreathJumpPlatform>();
        private readonly Stack<BreathJumpPlatform> _pool = new Stack<BreathJumpPlatform>();
        private readonly List<int> _toRecycle = new List<int>();

        private BreathJumpLevelGenerator _generator;

        public int GoalIndex => _generator != null ? _generator.GoalIndex : 0;

        private void Awake()
        {
            if (_platformsRoot == null) _platformsRoot = transform;
        }

        /// <summary>Genera un recorrido nuevo y coloca las primeras plataformas.</summary>
        public void Build(BreathJumpTuning tuning, int seed)
        {
            foreach (var platform in _active.Values) _recycle(platform);
            _active.Clear();

            _generator = new BreathJumpLevelGenerator(tuning, seed);
            EnsureAround(0);
        }

        /// <summary>Plataforma del índice indicado (la instancia si hace falta).</summary>
        public BreathJumpPlatform GetPlatform(int index)
        {
            if (_generator == null || _platformPrefab == null) return null;

            index = Mathf.Clamp(index, 0, GoalIndex);
            if (_active.TryGetValue(index, out var platform)) return platform;

            platform = _pool.Count > 0 ? _pool.Pop() : Instantiate(_platformPrefab, _platformsRoot, false);
            platform.gameObject.SetActive(true);
            platform.name = $"Platform_{index}";
            platform.Setup(index, _generator.Get(index));
            _active.Add(index, platform);
            return platform;
        }

        /// <summary>Deja instanciadas solo las plataformas cercanas al índice actual.</summary>
        public void EnsureAround(int currentIndex)
        {
            if (_generator == null) return;

            int from = Mathf.Max(0, currentIndex - _keepBehind);
            int to   = Mathf.Min(GoalIndex, currentIndex + _keepAhead);

            _toRecycle.Clear();
            foreach (var index in _active.Keys)
                if (index < from || index > to) _toRecycle.Add(index);

            foreach (var index in _toRecycle)
            {
                _recycle(_active[index]);
                _active.Remove(index);
            }

            for (int i = from; i <= to; i++) GetPlatform(i);
        }

        private void _recycle(BreathJumpPlatform platform)
        {
            if (platform == null) return;
            platform.gameObject.SetActive(false);
            _pool.Push(platform);
        }
    }
}
