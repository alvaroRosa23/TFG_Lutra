using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Pantalla de login y registro de cuenta.
    /// Se activa cuando AppStateMachine entra en <see cref="AppState.Login"/>.
    /// </summary>
    public class LoginScreen : UIScreen
    {
        [SerializeField] private LoginController _loginController;

        public override AppState ScreenState => AppState.Login;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.4f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            Debug.Log("[LoginScreen] Pantalla de login activa");

            if (_loginController != null)
                _ = _loginController.OpenLogin();
            else
                Debug.LogWarning("[LoginScreen] LoginController no asignado en el Inspector.");
        }
    }
}
