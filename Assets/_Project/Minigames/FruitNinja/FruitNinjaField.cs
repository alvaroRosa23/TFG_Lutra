using System;
using System.Collections.Generic;
using UnityEngine;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.ScriptableObjects;

namespace Lutra.Minigames
{
    /// <summary>
    /// Campo de juego de FruitNinja: lanza los elementos a partir de un SpawnRequest, los mueve
    /// (Tick, con el tiempo del mundo), detecta los cortes de un trazo y los que se caen, y
    /// recicla las instancias (pool).
    ///
    /// Coordenadas: respecto al centro de _root (el contenedor que hace zoom), en unidades del
    /// Canvas. _root debe ocupar toda la pantalla. Si no hay definiciones asignadas se usan
    /// círculos generados de colores.
    /// </summary>
    public class FruitNinjaField : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [Tooltip("Opcional: si se deja vacío se crean elementos desde cero")]
        [SerializeField] private SliceableView _prefab;

        [Header("Aspecto (opcional)")]
        [SerializeField] private SliceableDefinition[] _definitions;
        [SerializeField] private Color[] _fallbackColors =
        {
            new Color(0.95f, 0.45f, 0.45f), new Color(0.98f, 0.75f, 0.35f), new Color(0.55f, 0.85f, 0.5f),
            new Color(0.45f, 0.7f, 0.95f),  new Color(0.75f, 0.55f, 0.95f)
        };

        [Header("Color del aro de cada especial")]
        [SerializeField] private Color _burstColor      = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color _rainColor       = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color _crossfireColor  = new Color(0.75f, 0.35f, 1f);
        [SerializeField] private Color _slowMotionColor = new Color(0.3f, 0.95f, 0.85f);

        private const int GeneratedSpriteSize = 128;

        /// <summary>Un elemento sin cortar ha salido de la pantalla (se recicla justo después).</summary>
        public event Action<SliceableView> OnMissed;

        private readonly List<SliceableView>  _active = new List<SliceableView>();
        private readonly Stack<SliceableView> _pool   = new Stack<SliceableView>();
        private readonly List<SliceableDefinition> _normalDefinitions = new List<SliceableDefinition>();

        private FruitNinjaTuning _tuning;
        private Texture2D _circleTexture, _ringTexture;
        private Sprite    _circleSprite, _ringSprite;

        /// <summary>Elementos en pantalla que aún no se han cortado.</summary>
        public int UnslicedCount
        {
            get
            {
                int count = 0;
                foreach (var item in _active) if (!item.IsSliced) count++;
                return count;
            }
        }

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_root == null) _root = (RectTransform)transform;

            _circleSprite = _createGeneratedSprite("SliceableCircle", ring: false, out _circleTexture);
            _ringSprite   = _createGeneratedSprite("SliceableRing",   ring: true,  out _ringTexture);

            if (_definitions != null)
                foreach (var definition in _definitions)
                    if (definition != null && definition.kind == SliceableKind.Normal) _normalDefinitions.Add(definition);
        }

        private void OnDestroy()
        {
            OnMissed = null;
            if (_circleSprite != null)  Destroy(_circleSprite);
            if (_ringSprite != null)    Destroy(_ringSprite);
            if (_circleTexture != null) Destroy(_circleTexture);
            if (_ringTexture != null)   Destroy(_ringTexture);
        }

        // ── API pública ────────────────────────────────────────────────

        public void Setup(FruitNinjaTuning tuning)
        {
            _tuning = tuning;
            Clear();
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) _recycle(i);
        }

        public void Spawn(SpawnRequest request)
        {
            if (_tuning == null) return;

            var item = _pool.Count > 0 ? _pool.Pop() : _createItem();
            item.gameObject.SetActive(true);

            var definition = _pickDefinition(request.kind);
            float diameter = _tuning.baseDiameter * request.sizeScale * (definition != null ? definition.sizeMultiplier : 1f);
            Sprite sprite = definition != null && definition.sprite != null ? definition.sprite : _circleSprite;
            Color  color  = definition != null ? definition.color : _randomFallbackColor();
            item.Setup(sprite, color, diameter, request.kind, _markerColor(request.kind));

            _computeLaunch(request, diameter * 0.5f, out var position, out var velocity, out float gravity);
            item.Launch(position, velocity, gravity, request.spin);

            _active.Add(item);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            var rect = _root.rect;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                item.Tick(deltaTime);

                if (item.IsSliced)
                {
                    if (item.IsFinished || _isOutside(item, rect)) _recycle(i);
                    continue;
                }

                if (_isOutside(item, rect))
                {
                    OnMissed?.Invoke(item);
                    _recycle(i);
                }
            }
        }

        /// <summary>Corta todo lo que cruce el segmento from→to; devuelve los cortados en results.</summary>
        public void Slice(Vector2 from, Vector2 to, List<SliceableView> results)
        {
            results.Clear();
            if (_tuning == null) return;

            Vector2 direction = to - from;
            foreach (var item in _active)
            {
                if (item.IsSliced) continue;
                if (_distanceToSegment(item.Position, from, to) > item.Radius * _tuning.cutRadiusScale) continue;

                item.Slice(direction);
                results.Add(item);
            }
        }

        // ── Helpers privados ───────────────────────────────────────────

        /// <summary>
        /// Trayectoria balística que llega a la altura apexFraction y vuelve a la altura de
        /// salida en flightTime (gravedad y velocidad vertical derivadas, como en BreathJump).
        /// </summary>
        private void _computeLaunch(SpawnRequest request, float radius, out Vector2 position, out Vector2 velocity, out float gravity)
        {
            var rect = _root.rect;
            float halfW = rect.width * 0.5f;
            float halfH = rect.height * 0.5f;
            float time  = Mathf.Max(0.5f, request.flightTime);

            switch (request.origin)
            {
                case SpawnOrigin.Top:
                {
                    // Cae desde arriba y cruza la pantalla en el 75 % del tiempo de vuelo
                    float fallTime = time * 0.75f;
                    position = new Vector2(request.lane * halfW * _tuning.spawnWidthFraction, halfH + radius);
                    gravity  = 2f * (rect.height + 2f * radius) / (fallTime * fallTime);
                    velocity = new Vector2(request.drift * rect.width * 0.08f / fallTime, 0f);
                    return;
                }

                case SpawnOrigin.Left:
                case SpawnOrigin.Right:
                {
                    float side = request.origin == SpawnOrigin.Left ? -1f : 1f;
                    position = new Vector2(side * (halfW + radius), -halfH + rect.height * Mathf.Lerp(0.2f, 0.45f, request.lane));
                    float rise = Mathf.Max(radius, -halfH + rect.height * request.apexFraction - position.y);
                    gravity  = 8f * rise / (time * time);
                    float targetX = -side * rect.width * (0.1f + 0.15f * Mathf.Abs(request.drift));
                    velocity = new Vector2((targetX - position.x) / time, 4f * rise / time);
                    return;
                }

                default:
                {
                    position = new Vector2(request.lane * halfW * _tuning.spawnWidthFraction, -halfH - radius);
                    float rise = -halfH + rect.height * request.apexFraction - position.y;
                    gravity  = 8f * rise / (time * time);
                    float targetX = Mathf.Clamp(-0.4f * position.x + request.drift * halfW * 0.3f, -halfW * 0.8f, halfW * 0.8f);
                    velocity = new Vector2((targetX - position.x) / time, 4f * rise / time);
                    return;
                }
            }
        }

        /// <summary>Fuera = bajando por debajo del borde inferior o alejándose por un lateral.</summary>
        private static bool _isOutside(SliceableView item, Rect rect)
        {
            Vector2 p = item.Position;
            Vector2 v = item.Velocity;
            float r = item.Radius;
            float halfW = rect.width * 0.5f;
            float halfH = rect.height * 0.5f;

            return (p.y < -halfH - r && v.y < 0f)
                || (p.x < -halfW - r && v.x < 0f)
                || (p.x >  halfW + r && v.x > 0f);
        }

        private static float _distanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + ab * t);
        }

        private SliceableDefinition _pickDefinition(SliceableKind kind)
        {
            if (kind == SliceableKind.Normal)
                return _normalDefinitions.Count > 0 ? _normalDefinitions[UnityEngine.Random.Range(0, _normalDefinitions.Count)] : null;

            if (_definitions != null)
                foreach (var definition in _definitions)
                    if (definition != null && definition.kind == kind) return definition;

            return null;
        }

        private Color _randomFallbackColor()
        {
            if (_fallbackColors == null || _fallbackColors.Length == 0) return Color.white;
            return _fallbackColors[UnityEngine.Random.Range(0, _fallbackColors.Length)];
        }

        private Color _markerColor(SliceableKind kind)
        {
            switch (kind)
            {
                case SliceableKind.Burst:      return _burstColor;
                case SliceableKind.Rain:       return _rainColor;
                case SliceableKind.Crossfire:  return _crossfireColor;
                case SliceableKind.SlowMotion: return _slowMotionColor;
                default:                       return Color.clear;
            }
        }

        private SliceableView _createItem()
        {
            SliceableView item;
            if (_prefab != null)
            {
                item = Instantiate(_prefab, _root, false);
            }
            else
            {
                var go = new GameObject("Sliceable", typeof(RectTransform));
                go.transform.SetParent(_root, false);
                item = go.AddComponent<SliceableView>();
            }

            item.Build(_circleSprite, _ringSprite);
            return item;
        }

        private void _recycle(int index)
        {
            var item = _active[index];
            _active.RemoveAt(index);
            item.gameObject.SetActive(false);
            _pool.Push(item);
        }

        /// <summary>Círculo relleno (o aro) blanco con borde suavizado, para teñir con el color.</summary>
        private static Sprite _createGeneratedSprite(string spriteName, bool ring, out Texture2D texture)
        {
            int size = GeneratedSpriteSize;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name     = spriteName,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            float edge = 1.5f / half;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 1f - Mathf.InverseLerp(1f - edge, 1f, distance);
                    if (ring) alpha *= Mathf.InverseLerp(0.8f - edge, 0.8f, distance);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
