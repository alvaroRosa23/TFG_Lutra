using UnityEngine;

namespace Lutra.Minigames
{
    /// <summary>
    /// Plataforma de BreathJump (prefab). El pivote del transform es el centro de la
    /// superficie superior. El SpriteRenderer va en un hijo (desplazado hacia abajo para que
    /// su borde superior coincida con el pivote) y es lo único que cambia de ancho: con Draw
    /// Mode Sliced/Tiled se ajusta su Size.x; con Simple se escala su transform en X (sprite
    /// de 1 unidad de ancho). Así la bandera de meta no se deforma.
    /// </summary>
    public class BreathJumpPlatform : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("Bandera / decoración de meta; se activa solo en la última plataforma")]
        [SerializeField] private GameObject _goalMarker;

        public int   Index   { get; private set; }
        public bool  IsGoal  { get; private set; }
        public float Width   { get; private set; }
        public float Top     => transform.position.y;
        public float CenterX => transform.position.x;
        public float Left    => CenterX - Width * 0.5f;
        public float Right   => CenterX + Width * 0.5f;

        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        public void Setup(int index, PlatformLayout layout)
        {
            Index  = index;
            IsGoal = layout.IsGoal;
            Width  = layout.Width;

            transform.position = new Vector3(layout.TopCenter.x, layout.TopCenter.y, 0f);

            if (_renderer != null)
            {
                if (_renderer.drawMode != SpriteDrawMode.Simple)
                {
                    _renderer.size = new Vector2(layout.Width, _renderer.size.y);
                }
                else
                {
                    var scale = _renderer.transform.localScale;
                    scale.x = layout.Width;
                    _renderer.transform.localScale = scale;
                }
            }

            if (_goalMarker != null) _goalMarker.SetActive(layout.IsGoal);
        }
    }
}
