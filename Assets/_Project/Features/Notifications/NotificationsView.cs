using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Notifications
{
    /// <summary>
    /// Vista del centro de notificaciones. Sin lógica de negocio: pinta lo que le pasa
    /// NotificationsController y avisa de las acciones del usuario con eventos.
    ///
    /// Estructura: cabecera con "atrás" · sección "Importante" (ancladas) · lista agrupada por
    /// fecha (Hoy, Ayer, Esta semana, fecha) · "Cargar más" · estado vacío.
    /// </summary>
    public class NotificationsView : MonoBehaviour
    {
        [Header("Cabecera")]
        [SerializeField] private Button _backButton;

        [Header("Importante (ancladas)")]
        [Tooltip("Raíz de la sección, incluido su título. Se oculta si no hay ancladas.")]
        [SerializeField] private GameObject _pinnedSection;
        [SerializeField] private Transform  _pinnedContainer;
        [Tooltip("Prefab con NotificationCard y botón de acción.")]
        [SerializeField] private GameObject _pinnedCardPrefab;

        [Header("Lista")]
        [SerializeField] private Transform  _listContainer;
        [Tooltip("Prefab con NotificationCard.")]
        [SerializeField] private GameObject _cardPrefab;
        [Tooltip("Prefab de cabecera de grupo con un TextMeshProUGUI (\"Hoy\", \"Ayer\"…).")]
        [SerializeField] private GameObject _groupHeaderPrefab;
        [SerializeField] private Button     _loadMoreButton;
        [SerializeField] private GameObject _emptyState;

        [Header("Iconos (opcionales; vacío = icono del prefab)")]
        [SerializeField] private Sprite _checkInIcon;
        [SerializeField] private Sprite _streakIcon;
        [SerializeField] private Sprite _rewardIcon;
        [SerializeField] private Sprite _diaryIcon;
        [SerializeField] private Sprite _minigameIcon;
        [SerializeField] private Sprite _starIcon;
        [SerializeField] private Sprite _who5Icon;
        [SerializeField] private Sprite _supportIcon;
        [SerializeField] private Sprite _weeklySummaryIcon;

        // ── Eventos ────────────────────────────────────────────────────

        public event Action                  OnBackRequested;
        public event Action                  OnLoadMoreRequested;
        public event Action<AppNotification> OnActionRequested;

        // ── Estado ─────────────────────────────────────────────────────

        private string _lastGroupLabel;
        private bool   _listHasItems;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _backButton?.onClick.AddListener(() => OnBackRequested?.Invoke());
            _loadMoreButton?.onClick.AddListener(() => OnLoadMoreRequested?.Invoke());
        }

        private void OnDestroy()
        {
            _backButton?.onClick.RemoveAllListeners();
            _loadMoreButton?.onClick.RemoveAllListeners();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Pinta la sección "Importante". Se oculta si no hay ancladas.</summary>
        public void ShowPinned(List<AppNotification> pinned, Func<AppNotification, string> actionTextFor)
        {
            _clearContainer(_pinnedContainer);

            bool hasPinned = pinned != null && pinned.Count > 0;
            if (_pinnedSection != null) _pinnedSection.SetActive(hasPinned);
            if (!hasPinned || _pinnedCardPrefab == null || _pinnedContainer == null) return;

            DateTime today = DateTime.Today;
            foreach (var notification in pinned)
            {
                var card = Instantiate(_pinnedCardPrefab, _pinnedContainer, false).GetComponent<NotificationCard>();
                if (card == null) continue;

                card.Setup(notification, _iconFor(notification),
                           NotificationTimeFormatter.TimeText(notification.CreatedAt, today),
                           actionTextFor?.Invoke(notification));
                card.OnActionClicked = n => OnActionRequested?.Invoke(n);
            }

            _rebuildLayout(_pinnedContainer);
        }

        /// <summary>Vacía la lista antes de cargar la primera página.</summary>
        public void ClearList()
        {
            _clearContainer(_listContainer);
            _lastGroupLabel = null;
            _listHasItems   = false;
            SetLoadMoreVisible(false);
            if (_emptyState != null) _emptyState.SetActive(false);
        }

        /// <summary>Añade una página al final de la lista, con cabeceras de grupo cuando cambia el día.</summary>
        /// <param name="actionTextFor">Texto del botón de acción de cada tarjeta, o null si no tiene
        /// (solo se ve si el prefab tiene botón; p. ej. "Ver recursos" en las de apoyo).</param>
        public void AppendPage(List<AppNotification> page, bool hasMore, Func<AppNotification, string> actionTextFor = null)
        {
            if (_listContainer != null && _cardPrefab != null && page != null)
            {
                DateTime today = DateTime.Today;
                foreach (var notification in page)
                {
                    string group = NotificationTimeFormatter.GroupLabel(notification.CreatedAt, today);
                    if (group != _lastGroupLabel)
                    {
                        _addGroupHeader(group);
                        _lastGroupLabel = group;
                    }

                    var card = Instantiate(_cardPrefab, _listContainer, false).GetComponent<NotificationCard>();
                    if (card != null)
                    {
                        card.Setup(notification, _iconFor(notification),
                                   NotificationTimeFormatter.TimeText(notification.CreatedAt, today),
                                   actionTextFor?.Invoke(notification));
                        card.OnActionClicked = n => OnActionRequested?.Invoke(n);
                    }
                    _listHasItems = true;
                }
            }

            SetLoadMoreVisible(hasMore);
            _rebuildLayout(_listContainer);
        }

        /// <summary>Estado vacío si no hay ninguna notificación (ni ancladas ni en la lista).</summary>
        public void RefreshEmptyState(bool hasPinned)
        {
            if (_emptyState != null) _emptyState.SetActive(!hasPinned && !_listHasItems);
        }

        public void SetLoadMoreVisible(bool visible)
        {
            if (_loadMoreButton == null) return;
            _loadMoreButton.gameObject.SetActive(visible);
            // El botón está al final del contenedor: mantenerlo siempre como último hijo
            if (visible && _loadMoreButton.transform.parent == _listContainer)
                _loadMoreButton.transform.SetAsLastSibling();
        }

        // ── Métodos privados ───────────────────────────────────────────

        private void _addGroupHeader(string label)
        {
            if (_groupHeaderPrefab == null) return;
            var header = Instantiate(_groupHeaderPrefab, _listContainer, false);
            var text   = header.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

        private Sprite _iconFor(AppNotification notification)
        {
            switch (notification.Type)
            {
                case NotificationType.Who5Available: return _who5Icon;
                case NotificationType.Support:       return _supportIcon;
                case NotificationType.WeeklySummary: return _weeklySummaryIcon;
            }

            switch (notification.Source)
            {
                case RewardSource.CheckIn:          return _checkInIcon;
                case RewardSource.StreakMilestone:  return _streakIcon;
                case RewardSource.Diary:            return _diaryIcon;
                case RewardSource.Minigame:         return _minigameIcon;
                case RewardSource.StarCollection:   return _starIcon;
                case RewardSource.Who5:             return _who5Icon;
                default:                            return _rewardIcon;
            }
        }

        /// <summary>
        /// Patrón obligatorio de CLAUDE.md. Si "Cargar más" está dentro del contenedor no se destruye.
        /// </summary>
        private void _clearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (_loadMoreButton != null && child == _loadMoreButton.transform) continue;
                child.gameObject.SetActive(false);  // síncrono; LayoutGroup ignora inactivos inmediatamente
                Destroy(child.gameObject);
            }
        }

        private static void _rebuildLayout(Transform container)
        {
            if (container is RectTransform rt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                if (rt.parent is RectTransform parentRt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }
        }
    }
}
