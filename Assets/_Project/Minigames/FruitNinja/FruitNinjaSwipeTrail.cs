using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lutra.Minigames
{
    /// <summary>
    /// Estela del dedo en UI (LineRenderer no se ve en un Canvas Overlay): tira de quads que
    /// se estrecha hacia la cola y cuyos puntos desaparecen tras _lifetime segundos.
    /// Debe ser hijo a pantalla completa del contenedor del campo (mismas coordenadas: centro).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class FruitNinjaSwipeTrail : MaskableGraphic
    {
        [SerializeField] private float _lifetime = 0.16f;
        [SerializeField] private float _width    = 28f;
        [SerializeField] private float _minPointDistance = 6f;

        private struct TrailPoint
        {
            public Vector2 position;
            public float   time;
        }

        private readonly List<TrailPoint> _points = new List<TrailPoint>();

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void AddPoint(Vector2 centerRelativePosition)
        {
            if (_points.Count > 0 && Vector2.Distance(_points[_points.Count - 1].position, centerRelativePosition) < _minPointDistance)
                return;

            _points.Add(new TrailPoint { position = centerRelativePosition, time = Time.unscaledTime });
            SetVerticesDirty();
        }

        public void Clear()
        {
            if (_points.Count == 0) return;
            _points.Clear();
            SetVerticesDirty();
        }

        private void Update()
        {
            if (_points.Count == 0) return;

            float limit = Time.unscaledTime - _lifetime;
            int expired = 0;
            while (expired < _points.Count && _points[expired].time < limit) expired++;
            if (expired > 0) _points.RemoveRange(0, expired);

            SetVerticesDirty(); // el ancho y el alfa dependen de la edad de cada punto
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2) return;

            Vector2 center = rectTransform.rect.center;
            float now = Time.unscaledTime;
            var vertex = UIVertex.simpleVert;

            for (int i = 0; i < _points.Count; i++)
            {
                Vector2 previous = _points[Mathf.Max(0, i - 1)].position;
                Vector2 next     = _points[Mathf.Min(_points.Count - 1, i + 1)].position;
                Vector2 normal   = (next - previous).normalized;
                normal = new Vector2(-normal.y, normal.x);

                // Más fino en la cola (puntos antiguos) y en la punta más antigua de la tira
                float life  = 1f - Mathf.Clamp01((now - _points[i].time) / _lifetime);
                float taper = (float)i / (_points.Count - 1);
                float halfWidth = _width * 0.5f * life * Mathf.Lerp(0.2f, 1f, taper);

                Vector2 position = _points[i].position + center;
                var tint = color;
                tint.a *= life;
                vertex.color = tint;

                vertex.position = position + normal * halfWidth;
                vh.AddVert(vertex);
                vertex.position = position - normal * halfWidth;
                vh.AddVert(vertex);

                if (i == 0) continue;
                int start = (i - 1) * 2;
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start + 1, start + 3, start + 2);
            }
        }
    }
}
