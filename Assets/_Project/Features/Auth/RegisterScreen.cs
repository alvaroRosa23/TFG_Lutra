using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Pantalla de creación de cuenta.
    /// Se activa cuando AppStateMachine entra en <see cref="AppState.Register"/>.
    /// </summary>
    public class RegisterScreen : UIScreen
    {
        [SerializeField] private RegisterController _registerController;

        public override AppState ScreenState => AppState.Register;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.4f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            if (_registerController != null)
                _ = _registerController.OpenRegister();
            else
                Debug.LogWarning("[RegisterScreen] RegisterController no asignado en el Inspector.");
        }
    }
}
