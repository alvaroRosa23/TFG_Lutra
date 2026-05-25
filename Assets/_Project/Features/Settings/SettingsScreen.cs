using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Settings
{
    /// <summary>
    /// Pantalla de ajustes de la aplicación.
    /// Delega toda la lógica en SettingsController.
    /// </summary>
    public class SettingsScreen : UIScreen
    {
        [SerializeField] private SettingsController _controller;

        public override AppState ScreenState => AppState.Settings;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            if (_controller != null)
                _controller.OpenSettings();
            else
                Debug.LogWarning("[SettingsScreen] _controller no asignado en Inspector.");
        }
    }
}
