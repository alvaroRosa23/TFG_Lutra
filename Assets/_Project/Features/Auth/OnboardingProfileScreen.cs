using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Pantalla de creación de perfil tras el registro en Firebase.
    /// Se activa cuando AppStateMachine entra en <see cref="AppState.OnboardingProfile"/>.
    /// </summary>
    public class OnboardingProfileScreen : UIScreen
    {
        [SerializeField] private OnboardingProfileController _controller;

        public override AppState ScreenState => AppState.OnboardingProfile;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.4f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            if (_controller != null)
                _ = _controller.OpenOnboarding();
            else
                Debug.LogWarning("[OnboardingProfileScreen] OnboardingProfileController no asignado en el Inspector.");
        }
    }
}
