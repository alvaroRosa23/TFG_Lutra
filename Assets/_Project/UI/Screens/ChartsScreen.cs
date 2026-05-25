using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Features.Charts;

namespace Lutra.UI.Screens
{
    /// <summary>
    /// Pantalla de gráficas y métricas emocionales.
    /// Delega toda la lógica en ChartsController.
    /// </summary>
    public class ChartsScreen : UIScreen
    {
        [SerializeField] private ChartsController _chartsController;

        // ── UIScreen ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.Charts;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        // ── UIScreen callbacks ─────────────────────────────────────────

        public override void OnScreenFocused()
        {
            if (_chartsController != null)
                _ = _chartsController.OpenCharts();
            else
                Debug.LogWarning("[ChartsScreen] _chartsController no asignado en Inspector.");
        }
    }
}
