using UnityEngine;
using UnityEngine.UI;

namespace Lutra.Minigames
{
    /// <summary>
    /// Sedal en UI (LineRenderer no se ve en un Canvas Overlay): curva entre la punta de la caña y
    /// el anzuelo que cuelga más o menos según _sag (0 = tenso). Se redibuja cada frame porque
    /// sus extremos se mueven. Puede ir en cualquier sitio del mismo Canvas (convierte las
    /// posiciones de mundo a su espacio local).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class StarFisherLine : MaskableGraphic
    {
        [SerializeField] private RectTransform _from;
        [SerializeField] private RectTransform _to;
        [SerializeField] private float _width    = 4f;
        [SerializeField] private int   _segments = 24;
        [Tooltip("Caída máxima del sedal en unidades del Canvas cuando está flojo")]
        [SerializeField] private float _maxSag   = 160f;

        private float _sag = 0.5f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>0 = tenso, 1 = muy flojo.</summary>
        public void SetSlack(float slack) => _sag = Mathf.Clamp01(slack);

        public void SetVisible(bool visible) => enabled = visible;

        private void LateUpdate()
        {
            if (_from != null && _to != null) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_from == null || _to == null) return;

            Vector2 start = rectTransform.InverseTransformPoint(_from.position);
            Vector2 end   = rectTransform.InverseTransformPoint(_to.position);

            // Punto de control: el medio, hundido hacia abajo según lo flojo que esté
            float sag = _sag * Mathf.Min(_maxSag, Vector2.Distance(start, end) * 0.4f);
            Vector2 control = (start + end) * 0.5f + Vector2.down * sag;

            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            int segments = Mathf.Max(2, _segments);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector2 point   = _bezier(start, control, end, t);
                Vector2 tangent = (_bezier(start, control, end, Mathf.Min(1f, t + 0.01f))
                                 - _bezier(start, control, end, Mathf.Max(0f, t - 0.01f))).normalized;
                Vector2 normal  = new Vector2(-tangent.y, tangent.x) * (_width * 0.5f);

                vertex.position = point + normal;
                vh.AddVert(vertex);
                vertex.position = point - normal;
                vh.AddVert(vertex);

                if (i == 0) continue;
                int index = (i - 1) * 2;
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index + 1, index + 3, index + 2);
            }
        }

        private static Vector2 _bezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }
    }
}
