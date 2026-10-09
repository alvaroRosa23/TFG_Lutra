using UnityEngine;
using UnityEngine.UI;
using Lutra.Core.Architecture;
using Lutra.Core.Events;

namespace Lutra.UI.Theme
{
    /// <summary>
    /// Tiñe un Graphic (Image, RawImage, texto…) con el color del tema emocional activo, que ya incluye
    /// la paleta cultural del usuario. Sigue la transición de ThemeManager frame a frame.
    ///
    /// Dos usos con el arte de fondo de cada pantalla:
    ///   - Capa teñible: el equipo de diseño entrega una capa del fondo en blanco o tonos claros
    ///     (cielo, pared…) y este componente la tiñe. Strength = 1, Alpha = 1.
    ///   - Velo: una Image lisa a pantalla completa justo encima del arte (debajo del contenido,
    ///     sin Raycast Target) que deja ver el arte con un matiz del color. Strength = 1, Alpha ≈ 0,15-0,3.
    /// Strength &lt; 1 mezcla el color con blanco: un tinte suave sobre arte de color sin apagarlo.
    ///
    /// Hasta el primer ApplyTheme mantiene el color que tenga en el editor.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class ThemedGraphic : MonoBehaviour
    {
        private enum ColorSlot { Primary, Background }

        [Tooltip("Primary = color principal de la emoción (vivo); Background = color de fondo (oscuro).")]
        [SerializeField] private ColorSlot _slot = ColorSlot.Primary;

        [Tooltip("0 = blanco (sin tinte) … 1 = color del tema completo.")]
        [SerializeField, Range(0f, 1f)] private float _strength = 1f;

        [Tooltip("Opacidad del Graphic. 1 para teñir una capa del arte; 0,15-0,3 para un velo de color.")]
        [SerializeField, Range(0f, 1f)] private float _alpha = 1f;

        private Graphic _graphic;

        private ThemeManager _themeManager;
        private ThemeManager ThemeManagerService
        {
            get
            {
                // TryGet: un ThemedGraphic puede activarse antes de que GameManager registre el servicio
                if (_themeManager == null) ServiceLocator.TryGet(out _themeManager);
                return _themeManager;
            }
        }

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
        }

        private void OnEnable()
        {
            EventBus.OnThemeColorsChanged += _onThemeColorsChanged;

            // Pantallas que se activan después del cambio de tema: aplicar el color actual al momento
            var theme = ThemeManagerService;
            if (theme != null && theme.HasAppliedColors)
                _onThemeColorsChanged(theme.CurrentPrimary, theme.CurrentBackground);
        }

        private void OnDisable()
        {
            EventBus.OnThemeColorsChanged -= _onThemeColorsChanged;
        }

        private void _onThemeColorsChanged(Color primary, Color background)
        {
            if (_graphic == null) return;

            Color target = Color.Lerp(Color.white, _slot == ColorSlot.Primary ? primary : background, _strength);
            target.a = _alpha;
            _graphic.color = target;
        }
    }
}
