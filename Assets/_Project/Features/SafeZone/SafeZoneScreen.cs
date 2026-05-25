using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.SafeZone
{
    /// <summary>
    /// Pantalla de la Zona Segura: habitación decorable del usuario.
    /// Delega toda la lógica en SafeZoneController.
    /// </summary>
    public class SafeZoneScreen : UIScreen
    {
        [SerializeField] private SafeZoneController _safeZoneController;

        // ── UIScreen ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.SafeZone;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        // ── UIScreen callbacks ─────────────────────────────────────────

        public override void OnScreenFocused()
        {
            if (_safeZoneController != null)
                _ = _safeZoneController.OpenSafeZone();
            else
                Debug.LogWarning("[SafeZoneScreen] _safeZoneController no asignado en Inspector.");
        }
    }
}
