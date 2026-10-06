using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Notifications
{
    /// <summary>
    /// Pantalla del centro de notificaciones (AppState.Notifications).
    /// Delega toda la lógica en NotificationsController.
    /// </summary>
    public class NotificationsScreen : UIScreen
    {
        [SerializeField] private NotificationsController _controller;

        public override AppState ScreenState => AppState.Notifications;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            if (_controller != null)
                _controller.OpenNotifications();
            else
                Debug.LogWarning("[NotificationsScreen] _controller no asignado en Inspector.");
        }
    }
}
