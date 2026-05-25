using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.UI.Components;
using Lutra.UI.Screens;

namespace Lutra.Features.Minigames
{
    /// <summary>
    /// Pantalla de selección de minijuegos.
    /// Delega toda la lógica en MinigamesController.
    /// </summary>
    public class MinigamesScreen : UIScreen
    {
        [SerializeField] private MinigamesController _minigamesController;

        // ── UIScreen ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.Minigames;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        // ── UIScreen callbacks ─────────────────────────────────────────

        public override void OnScreenFocused()
        {
            _ = _loadAndOpen();
        }

        // ── Métodos privados ───────────────────────────────────────────

        private async Task _loadAndOpen()
        {
            try
            {
                await _minigamesController.OpenMinigames();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MinigamesScreen] Error al abrir minijuegos: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }
    }
}
