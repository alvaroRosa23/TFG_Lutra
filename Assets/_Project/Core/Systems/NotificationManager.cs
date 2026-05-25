using System;
using UnityEngine;
using Lutra.Core.Architecture;

// Notificaciones reales requieren el paquete "Mobile Notifications"
// (com.unity.mobile.notifications) instalado vía Package Manager.
// En editor se usa solo Debug.Log.
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#elif UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace Lutra.Core.Systems
{
    /// <summary>
    /// Gestiona las notificaciones locales de la aplicación.
    /// - Recordatorio diario configurable (hora).
    /// - Aviso de racha en peligro (programado a las 21:00).
    ///
    /// Las preferencias de hora se persisten en PlayerPrefs para
    /// reprogramar el recordatorio en cada arranque de la app.
    /// </summary>
    public class NotificationManager : BaseService
    {
        private const string DAILY_REMINDER_ID = "owlet_daily_reminder";
        private const string STREAK_WARNING_ID  = "owlet_streak_warning";

        [SerializeField] private int _defaultReminderHour   = 20;
        [SerializeField] private int _defaultReminderMinute = 0;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _initializeChannel();
        }

        private void Start()
        {
            LoadSavedPreferences();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Programa el recordatorio diario a la hora indicada.
        /// Cancela el anterior antes de crear el nuevo.
        /// </summary>
        public void ScheduleDailyReminder(int hour, int minute)
        {
            _cancelNotification(DAILY_REMINDER_ID);

            var  fireTime = _getNextOccurrence(hour, minute);
            _scheduleNotification(
                DAILY_REMINDER_ID,
                "¿Cómo estás hoy?",
                "Lutra te espera para tu check-in diario 🦉",
                fireTime);

            PlayerPrefs.SetInt("notif_hour",   hour);
            PlayerPrefs.SetInt("notif_minute", minute);
            PlayerPrefs.Save();

            Debug.Log($"[NotificationManager] Recordatorio diario → {fireTime:HH:mm dd/MM/yyyy}");
        }

        /// <summary>Cancela el recordatorio diario y elimina las preferencias guardadas.</summary>
        public void CancelDailyReminder()
        {
            _cancelNotification(DAILY_REMINDER_ID);
            PlayerPrefs.DeleteKey("notif_hour");
            PlayerPrefs.DeleteKey("notif_minute");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Programa un aviso de racha en peligro para las 21:00 del día actual.
        /// Solo si la racha es >= 3 días.
        /// </summary>
        public void ScheduleStreakWarning(int currentStreak)
        {
            if (currentStreak < 3) return;

            var fireTime = _getNextOccurrence(21, 0);
            _scheduleNotification(
                STREAK_WARNING_ID,
                $"¡Tu racha de {currentStreak} días está en peligro!",
                "Entra a Lutra antes de medianoche para mantenerla 🔥",
                fireTime);

            Debug.Log($"[NotificationManager] Aviso de racha ({currentStreak}d) → {fireTime:HH:mm}");
        }

        /// <summary>Cancela el aviso de racha en peligro.</summary>
        public void CancelStreakWarning()
        {
            _cancelNotification(STREAK_WARNING_ID);
        }

        /// <summary>
        /// Lee las preferencias de PlayerPrefs y reprograma el recordatorio guardado.
        /// Llamar al arrancar la app para restaurar notificaciones tras reinicio del dispositivo.
        /// </summary>
        public void LoadSavedPreferences()
        {
            if (!PlayerPrefs.HasKey("notif_hour")) return;

            int hour   = PlayerPrefs.GetInt("notif_hour",   _defaultReminderHour);
            int minute = PlayerPrefs.GetInt("notif_minute", _defaultReminderMinute);

            ScheduleDailyReminder(hour, minute);
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Devuelve la próxima ocurrencia de la hora indicada.
        /// Si ya pasó hoy, devuelve mañana a esa misma hora.
        /// </summary>
        private DateTime _getNextOccurrence(int hour, int minute)
        {
            var now       = DateTime.Now;
            var candidate = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);

            if (candidate <= now)
                candidate = candidate.AddDays(1);

            return candidate;
        }

        private void _scheduleNotification(string id, string title, string body, DateTime fireTime)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var notification = new AndroidNotification
            {
                Title    = title,
                Text     = body,
                FireTime = fireTime,
                SmallIcon = "default",
                LargeIcon = "default"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(
                notification, "owlet_channel", _idToInt(id));
#elif UNITY_IOS && !UNITY_EDITOR
            var trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = fireTime - DateTime.Now,
                Repeats      = false
            };
            var notification = new iOSNotification
            {
                Identifier       = id,
                Title            = title,
                Body             = body,
                ShowInForeground = false,
                Trigger          = trigger
            };
            iOSNotificationCenter.ScheduleNotification(notification);
#else
            Debug.Log($"[NotificationManager] [EDITOR] Programaría '{id}': \"{title}\" a las {fireTime:HH:mm dd/MM/yyyy}");
#endif
        }

        private void _cancelNotification(string id)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.CancelNotification(_idToInt(id));
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveScheduledNotification(id);
#else
            Debug.Log($"[NotificationManager] [EDITOR] Cancelaría notificación '{id}'");
#endif
        }

        /// <summary>
        /// Crea el canal de notificaciones de Android (solo en dispositivo).
        /// Debe llamarse antes de enviar la primera notificación.
        /// </summary>
        private void _initializeChannel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var channel = new AndroidNotificationChannel
            {
                Id          = "owlet_channel",
                Name        = "Recordatorios Lutra",
                Description = "Notificaciones de check-in y racha",
                Importance  = Importance.Default
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
#endif
        }

        /// <summary>Convierte un string ID en un int estable para AndroidNotificationCenter.</summary>
        private static int _idToInt(string id) => Math.Abs(id.GetHashCode()) % 100_000;
    }
}
