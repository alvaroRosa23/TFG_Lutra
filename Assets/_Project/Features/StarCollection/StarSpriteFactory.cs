using UnityEngine;

namespace Lutra.Features.StarCollection
{
    /// <summary>
    /// Sprites generados en tiempo de ejecución para poder probar StarFisher, el libro y el
    /// telescopio sin arte: estrella de 5 puntas, brillo radial y viñeta de bordes. Se crean una
    /// sola vez y se comparten.
    /// </summary>
    public static class StarSpriteFactory
    {
        private const int Size = 128;

        private static Sprite _star, _glow, _vignette;

        public static Sprite Star     => _star     != null ? _star     : _star     = _create("GeneratedStar", _starAlpha);
        public static Sprite Glow     => _glow     != null ? _glow     : _glow     = _create("GeneratedGlow", _glowAlpha);
        public static Sprite Vignette => _vignette != null ? _vignette : _vignette = _create("GeneratedVignette", _vignetteAlpha);

        /// <summary>Sprite de la estrella o, si no tiene arte, la estrella generada.</summary>
        public static Sprite Or(Sprite sprite) => sprite != null ? sprite : Star;

        private static Sprite _create(string spriteName, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = spriteName, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Coordenadas -1..1 con el centro en el medio del píxel
                float u = (x + 0.5f) / Size * 2f - 1f;
                float v = (y + 0.5f) / Size * 2f - 1f;
                byte a = (byte)(Mathf.Clamp01(alpha(u, v)) * 255f);
                pixels[y * Size + x] = new Color32(255, 255, 255, a);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>Estrella de 5 puntas con el borde suavizado.</summary>
        private static float _starAlpha(float u, float v)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float angle  = Mathf.Atan2(v, u) - Mathf.PI / 2f;
            // Radio del contorno: alterna entre punta (1) y valle (0.45) cada 36°
            float sector = Mathf.Repeat(angle, 2f * Mathf.PI / 5f) / (2f * Mathf.PI / 5f);
            float toValley = 1f - Mathf.Abs(sector * 2f - 1f); // 0 en la punta, 1 en el valle
            float edge   = Mathf.Lerp(0.95f, 0.42f, toValley);
            return (edge - radius) * Size * 0.25f + 0.5f;
        }

        private static float _glowAlpha(float u, float v)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float t = Mathf.Clamp01(1f - radius);
            return t * t;
        }

        /// <summary>Transparente en el centro y opaco hacia los bordes.</summary>
        private static float _vignetteAlpha(float u, float v)
        {
            float edge = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, edge));
        }
    }
}
