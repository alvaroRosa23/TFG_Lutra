using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Consent
{
    /// <summary>
    /// Pantalla de consentimiento de datos de salud (AppState.Consent). Se llega a ella con
    /// ConsentGate.ContinueTo. Delega toda la lógica en ConsentController.
    /// </summary>
    public class ConsentScreen : UIScreen
    {
        [SerializeField] private ConsentController _controller;

        public override AppState ScreenState => AppState.Consent;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            if (_controller != null)
                _controller.OpenConsent();
            else
                Debug.LogWarning("[ConsentScreen] _controller no asignado en Inspector.");
        }
    }
}
