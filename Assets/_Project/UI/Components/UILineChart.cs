using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Gráfico de línea para la UI (sin librerías): dibuja su propia malla dentro de su RectTransform.
    ///   - Serie principal: puntos (con color propio opcional) unidos por líneas; la línea se corta
    ///     si entre dos puntos hay un hueco mayor que maxGap en X (p. ej. días sin registro).
    ///   - Serie secundaria opcional: línea continua sin puntos (p. ej. media de 7 días).
    ///   - Líneas guía horizontales opcionales (p. ej. en cada nivel de ánimo 1-5).
    /// Las coordenadas son de datos: SetData recibe el rango X/Y y el componente las escala.
    /// Uso: añadir a un GameObject de UI (con CanvasRenderer) dentro de un Canvas.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILineChart : MaskableGraphic
    {
        [Header("Estilo")]
        [SerializeField] private float _lineWidth      = 4f;
        [SerializeField] private float _secondaryWidth = 3f;
        [SerializeField] private float _pointSize      = 12f;
        [SerializeField] private float _gridWidth      = 1.5f;
        [SerializeField] private Color _lineColor      = new Color(0.40f, 0.60f, 1.00f, 1f);
        [SerializeField] private Color _secondaryColor = new Color(0.40f, 0.60f, 1.00f, 0.45f);
        [SerializeField] private Color _pointColor     = new Color(0.25f, 0.45f, 0.95f, 1f);
        [SerializeField] private Color _gridColor      = new Color(0.5f, 0.5f, 0.5f, 0.25f);
        [Tooltip("Margen interior en píxeles para que los puntos de los bordes no se corten.")]
        [SerializeField] private float _padding        = 10f;

        private readonly List<Vector2> _primary     = new List<Vector2>();
        private readonly List<Color>   _pointColors = new List<Color>();
        private readonly List<Vector2> _secondary   = new List<Vector2>();
        private readonly List<float>   _gridLines   = new List<float>();
        private Rect  _range = new Rect(0, 0, 1, 1);
        private float _maxGap = float.MaxValue;
        private float _secondaryMaxGap = float.MaxValue;

        /// <param name="range">Rango de datos: x = xMin, y = yMin, width = xMax − xMin, height = yMax − yMin.</param>
        /// <param name="pointColors">Color de cada punto principal (mismo índice); null = color por defecto.</param>
        /// <param name="maxGap">Distancia máxima en X para unir dos puntos principales con línea.</param>
        /// <param name="secondaryMaxGap">Lo mismo para la serie secundaria (p. ej. no unir la media a través de semanas sin datos).</param>
        public void SetData(IList<Vector2> primary, Rect range, IList<Color> pointColors = null,
                            IList<Vector2> secondary = null, IList<float> gridLines = null,
                            float maxGap = float.MaxValue, float secondaryMaxGap = float.MaxValue)
        {
            _primary.Clear();
            _pointColors.Clear();
            _secondary.Clear();
            _gridLines.Clear();

            if (primary != null)    _primary.AddRange(primary);
            if (pointColors != null) _pointColors.AddRange(pointColors);
            if (secondary != null)  _secondary.AddRange(secondary);
            if (gridLines != null)  _gridLines.AddRange(gridLines);

            _range  = new Rect(range.x, range.y, Mathf.Max(range.width, 0.0001f), Mathf.Max(range.height, 0.0001f));
            _maxGap = maxGap;
            _secondaryMaxGap = secondaryMaxGap;
            SetVerticesDirty();
        }

        public void Clear() => SetData(null, _range);

        // ── Malla ──────────────────────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;

            foreach (float y in _gridLines)
            {
                Vector2 a = _toLocal(new Vector2(_range.xMin, y), rect);
                Vector2 b = _toLocal(new Vector2(_range.xMax, y), rect);
                a.x = rect.xMin; b.x = rect.xMax;
                _addSegment(vh, a, b, _gridWidth, _gridColor);
            }

            for (int i = 1; i < _secondary.Count; i++)
            {
                if (_secondary[i].x - _secondary[i - 1].x > _secondaryMaxGap) continue;
                _addSegment(vh, _toLocal(_secondary[i - 1], rect), _toLocal(_secondary[i], rect), _secondaryWidth, _secondaryColor);
            }

            for (int i = 1; i < _primary.Count; i++)
            {
                if (_primary[i].x - _primary[i - 1].x > _maxGap) continue;
                _addSegment(vh, _toLocal(_primary[i - 1], rect), _toLocal(_primary[i], rect), _lineWidth, _lineColor);
            }

            for (int i = 0; i < _primary.Count; i++)
            {
                Color c = i < _pointColors.Count ? _pointColors[i] : _pointColor;
                _addSquare(vh, _toLocal(_primary[i], rect), _pointSize, c);
            }
        }

        private Vector2 _toLocal(Vector2 data, Rect rect)
        {
            float w = Mathf.Max(rect.width  - 2f * _padding, 1f);
            float h = Mathf.Max(rect.height - 2f * _padding, 1f);
            float nx = (data.x - _range.xMin) / _range.width;
            float ny = (data.y - _range.yMin) / _range.height;
            return new Vector2(rect.xMin + _padding + nx * w, rect.yMin + _padding + ny * h);
        }

        private static void _addSegment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
            _addQuad(vh, a - n, a + n, b + n, b - n, color);
        }

        private static void _addSquare(VertexHelper vh, Vector2 center, float size, Color color)
        {
            float h = size * 0.5f;
            _addQuad(vh, center + new Vector2(-h, -h), center + new Vector2(-h, h),
                         center + new Vector2(h, h),   center + new Vector2(h, -h), color);
        }

        private static void _addQuad(VertexHelper vh, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
        {
            int start = vh.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = v0; vh.AddVert(vertex);
            vertex.position = v1; vh.AddVert(vertex);
            vertex.position = v2; vh.AddVert(vertex);
            vertex.position = v3; vh.AddVert(vertex);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }
}
