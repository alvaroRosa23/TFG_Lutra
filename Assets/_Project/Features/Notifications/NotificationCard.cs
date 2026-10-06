using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Notifications
{
    /// <summary>
    /// Tarjeta de una notificación. La misma clase sirve para las normales y las ancladas
    /// ("Importante"): las ancladas usan otro prefab con el botón de acción visible.
    /// </summary>
    public class NotificationCard : MonoBehaviour
    {
        [SerializeField] private Image           _icon;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _bodyLabel;
        [SerializeField] private TextMeshProUGUI _timeLabel;
        [Tooltip("Punto de no leída. Opcional.")]
        [SerializeField] private GameObject      _unreadDot;
        [Tooltip("Botón de acción (p. ej. \"Hacer ahora\"). Opcional; se oculta si no hay acción.")]
        [SerializeField] private Button          _actionButton;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        public Action<AppNotification> OnActionClicked;

        private AppNotification _notification;

        private void Awake()
        {
            _actionButton?.onClick.AddListener(() => OnActionClicked?.Invoke(_notification));
        }

        private void OnDestroy()
        {
            _actionButton?.onClick.RemoveAllListeners();
        }

        /// <param name="icon">null = mantener el icono del prefab.</param>
        /// <param name="actionText">null = sin botón de acción.</param>
        public void Setup(AppNotification notification, Sprite icon, string timeText, string actionText)
        {
            _notification = notification;

            if (_icon != null && icon != null) _icon.sprite = icon;
            if (_titleLabel != null) _titleLabel.text = notification.Title ?? string.Empty;
            if (_bodyLabel  != null) _bodyLabel.text  = notification.Body  ?? string.Empty;
            if (_timeLabel  != null) _timeLabel.text  = timeText ?? string.Empty;

            if (_unreadDot != null)
                _unreadDot.SetActive(!notification.IsRead && !notification.IsPinned);

            if (_actionButton != null)
                _actionButton.gameObject.SetActive(!string.IsNullOrEmpty(actionText));
            if (_actionLabel != null && !string.IsNullOrEmpty(actionText))
                _actionLabel.text = actionText;
        }
    }
}
