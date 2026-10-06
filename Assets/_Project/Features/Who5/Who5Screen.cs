using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Who5
{
    /// <summary>
    /// Pantalla del cuestionario WHO-5 (AppState.Who5). Se abre desde la notificación anclada
    /// del centro de notificaciones. Delega toda la lógica en Who5Controller.
    /// </summary>
    public class Who5Screen : UIScreen
    {
        [SerializeField] private Who5Controller _controller;

        public override AppState ScreenState => AppState.Who5;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            if (_controller != null)
                _controller.OpenQuestionnaire();
            else
                Debug.LogWarning("[Who5Screen] _controller no asignado en Inspector.");
        }

        /// <summary>Al salir sin enviar se descartan las respuestas (la notificación sigue anclada).</summary>
        public override void OnScreenUnfocused()
        {
            if (_controller != null) _controller.DiscardAnswers();
        }
    }
}
