using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.UI.Screens;

namespace Lutra.Features.Diary
{
    /// <summary>
    /// Pantalla del diario emocional con calendario y editor de entradas.
    /// Delega toda la lógica en DiaryController.
    /// </summary>
    public class DiaryScreen : UIScreen
    {
        [SerializeField] private DiaryController _diaryController;

        // ── UIScreen ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.Diary;

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        // ── UIScreen callbacks ─────────────────────────────────────────

        public override void OnScreenFocused()
        {
            if (_diaryController != null)
                _ = _diaryController.OpenDiary();
            else
                Debug.LogWarning("[DiaryScreen] _diaryController no asignado en Inspector.");
        }
    }
}
