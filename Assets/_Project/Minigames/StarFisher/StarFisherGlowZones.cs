using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Lutra.Features.StarCollection;

namespace Lutra.Minigames
{
    /// <summary>
    /// Zonas brillantes de la espera: aparecen en sitios aleatorios de la zona segura, laten y se
    /// apagan. Si el anzuelo está dentro de alguna, sube la probabilidad de rareza.
    /// Las imágenes se crean en _container (hijo del contenedor de la escena, detrás del anzuelo).
    /// </summary>
    public class StarFisherGlowZones : MonoBehaviour
    {
        [SerializeField] private RectTransform _space;
        [SerializeField] private RectTransform _safeZone;
        [SerializeField] private RectTransform _container;
        [Tooltip("Si se deja vacío se usa un brillo radial generado")]
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Color  _color = new Color(1f, 0.9f, 0.55f, 0.55f);
        [Tooltip("Tamaño visual respecto al radio que cuenta para la bonificación")]
        [SerializeField] private float  _visualScale = 2.4f;

        private const float FadeSeconds = 0.6f;

        private class Zone
        {
            public Image   Image;
            public Vector2 Center;
            public float   Age;
            public float   Lifetime;
        }

        private readonly List<Zone> _zones = new List<Zone>();
        private readonly List<Image> _pool = new List<Image>();

        private StarFisherTuning _tuning;
        private StarRoller       _random;
        private bool             _spawning;
        private float            _nextSpawn;

        private void Awake()
        {
            if (_container == null) _container = (RectTransform)transform;
            if (_space == null)     _space = _container;
        }

        public void Setup(StarFisherTuning tuning, StarRoller random)
        {
            _tuning = tuning;
            _random = random;
        }

        public void Begin()
        {
            _spawning  = true;
            _nextSpawn = 0.4f;
        }

        /// <summary>Deja de crear zonas; las que hay se apagan poco a poco.</summary>
        public void Stop()
        {
            _spawning = false;
            foreach (var zone in _zones)
                zone.Lifetime = Mathf.Min(zone.Lifetime, zone.Age + FadeSeconds);
        }

        public void Clear()
        {
            _spawning = false;
            foreach (var zone in _zones) _recycle(zone.Image);
            _zones.Clear();
        }

        /// <summary>True si el punto (coordenadas de _space) está dentro de alguna zona visible.</summary>
        public bool Contains(Vector2 spacePosition)
        {
            if (_tuning == null) return false;
            foreach (var zone in _zones)
                if (_alpha(zone) > 0.5f && Vector2.Distance(zone.Center, spacePosition) <= _tuning.glowZoneRadius)
                    return true;
            return false;
        }

        public void Tick(float deltaTime)
        {
            if (_tuning == null) return;

            if (_spawning)
            {
                _nextSpawn -= deltaTime;
                if (_nextSpawn <= 0f && _zones.Count < _tuning.maxGlowZones)
                {
                    _spawn();
                    _nextSpawn = _random.Range(_tuning.glowZoneSpawnInterval.x, _tuning.glowZoneSpawnInterval.y);
                }
            }

            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var zone = _zones[i];
                zone.Age += deltaTime;
                if (zone.Age >= zone.Lifetime)
                {
                    _recycle(zone.Image);
                    _zones.RemoveAt(i);
                    continue;
                }

                float pulse = 1f + 0.06f * Mathf.Sin(zone.Age * 4f);
                zone.Image.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                var color = _color;
                color.a *= _alpha(zone);
                zone.Image.color = color;
            }
        }

        // ── Privado ────────────────────────────────────────────────────

        private void _spawn()
        {
            var image = _take();
            var zone = new Zone
            {
                Image    = image,
                Center   = _randomPointInSafeZone(),
                Lifetime = _random.Range(_tuning.glowZoneLifetime.x, _tuning.glowZoneLifetime.y)
            };

            float size = _tuning.glowZoneRadius * 2f * _visualScale;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.position  = _space.TransformPoint(zone.Center);
            image.color = Color.clear;
            _zones.Add(zone);
        }

        private static float _alpha(Zone zone)
        {
            float fadeIn  = Mathf.Clamp01(zone.Age / FadeSeconds);
            float fadeOut = Mathf.Clamp01((zone.Lifetime - zone.Age) / FadeSeconds);
            return Mathf.Min(fadeIn, fadeOut);
        }

        private Vector2 _randomPointInSafeZone()
        {
            if (_safeZone == null) return Vector2.zero;

            var corners = new Vector3[4];
            _safeZone.GetWorldCorners(corners);
            Vector2 min = _space.InverseTransformPoint(corners[0]);
            Vector2 max = _space.InverseTransformPoint(corners[2]);
            return new Vector2(_random.Range(min.x, max.x), _random.Range(min.y, max.y));
        }

        private Image _take()
        {
            Image image;
            if (_pool.Count > 0)
            {
                image = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                var go = new GameObject("GlowZone", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_container, false);
                image = go.GetComponent<Image>();
                image.sprite = _sprite != null ? _sprite : StarSpriteFactory.Glow;
                image.raycastTarget = false;
            }

            image.gameObject.SetActive(true);
            return image;
        }

        private void _recycle(Image image)
        {
            if (image == null) return;
            image.gameObject.SetActive(false);
            _pool.Add(image);
        }
    }
}
