using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Systems;
using Lutra.UI.Components;

namespace Lutra.Features.Notifications
{
    /// <summary>
    /// Controlador del centro de notificaciones. Carga ancladas y la primera página, marca como
    /// leídas las no ancladas (los puntos de no leída se ven durante esta visita) y pagina con
    /// "Cargar más". Las ancladas no se marcan: siguen pendientes hasta que se resuelven.
    /// </summary>
    public class NotificationsController : MonoBehaviour
    {
        [SerializeField] private NotificationsView _view;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private NotificationCenter _center;
        private NotificationCenter Center => _center ??= ServiceLocator.Get<NotificationCenter>();

        // ── Estado ─────────────────────────────────────────────────────

        private int  _loadedCount;
        private bool _loading;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<NotificationsView>();
        }

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnBackRequested         += _onBackRequested;
            _view.OnLoadMoreRequested     += _onLoadMoreRequested;
            _view.OnActionRequested += _onActionRequested;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnBackRequested         -= _onBackRequested;
            _view.OnLoadMoreRequested     -= _onLoadMoreRequested;
            _view.OnActionRequested -= _onActionRequested;
        }

        // ── API pública ────────────────────────────────────────────────

        public void OpenNotifications() => _ = _safeOpen();

        // ── Handlers ───────────────────────────────────────────────────

        private void _onBackRequested() => AppStateMachine.Instance.TransitionTo(AppState.MainMenu);

        private void _onLoadMoreRequested() => _ = _safeLoadPage();

        private void _onActionRequested(AppNotification notification)
        {
            if (notification == null) return;

            switch (notification.Type)
            {
                case NotificationType.Who5Available:
                    AppStateMachine.Instance.TransitionTo(AppState.Who5);
                    break;

                case NotificationType.Support:
                    SupportDialog.ShowResources();
                    break;

                default:
                    Debug.LogWarning($"[NotificationsController] Acción sin implementar para {notification.Type}");
                    break;
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        private async Task _safeOpen()
        {
            if (_view == null) return;

            try
            {
                var pinned = await Center.GetPinned();
                _view.ShowPinned(pinned, _actionTextFor);

                _view.ClearList();
                _loadedCount = 0;
                await _loadPage();
                _view.RefreshEmptyState(pinned.Count > 0);

                await Center.MarkAllRead();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NotificationsController] _safeOpen: {ex.Message}");
                ToastNotification.ShowError("No se pudieron cargar las notificaciones");
            }
        }

        private async Task _safeLoadPage()
        {
            try { await _loadPage(); }
            catch (Exception ex)
            {
                Debug.LogError($"[NotificationsController] _safeLoadPage: {ex.Message}");
                ToastNotification.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        /// <summary>Pide una notificación de más para saber si queda otra página.</summary>
        private async Task _loadPage()
        {
            if (_loading) return;
            _loading = true;

            try
            {
                int pageSize = NotificationCenter.PageSize;
                var page     = await Center.GetPage(_loadedCount, pageSize + 1);

                bool hasMore = page.Count > pageSize;
                if (hasMore) page.RemoveAt(page.Count - 1);

                _loadedCount += page.Count;
                _view.AppendPage(page, hasMore, _actionTextFor);
            }
            finally
            {
                _loading = false;
            }
        }

        private static string _actionTextFor(AppNotification notification)
        {
            switch (notification.Type)
            {
                case NotificationType.Who5Available: return "Hacer ahora";
                case NotificationType.Support:       return "Ver recursos";
                default:                             return null;
            }
        }
    }
}
