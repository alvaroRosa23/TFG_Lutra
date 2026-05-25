using UnityEngine;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Modelo de configuración de la aplicación. No es MonoBehaviour.
    /// Se persiste en PlayerPrefs y se gestiona a través de SettingsManager.
    /// </summary>
    public class AppSettings
    {
        // ── Notificaciones ─────────────────────────────────────────────

        public bool NotificationsEnabled { get; set; } = true;
        public int  ReminderHour         { get; set; } = 20;
        public int  ReminderMinute       { get; set; } = 0;

        // ── Apariencia ─────────────────────────────────────────────────

        public bool DarkMode          { get; set; } = false;
        public bool ReduceAnimations  { get; set; } = false;

        /// <summary>Tamaño de fuente: 0 = pequeño, 1 = normal, 2 = grande.</summary>
        public int  FontSize          { get; set; } = 1;
        public bool HighContrast      { get; set; } = false;

        // ── Audio ──────────────────────────────────────────────────────

        /// <summary>Volumen de la música ambiente [0, 1].</summary>
        public float MusicVolume { get; set; } = 1f;

        /// <summary>Volumen de efectos de sonido [0, 1].</summary>
        public float SfxVolume   { get; set; } = 1f;

        // ── Accesibilidad / Daltonismo ─────────────────────────────────

        /// <summary>Modo de daltonismo activo.</summary>
        public ColorblindMode Colorblind { get; set; } = ColorblindMode.None;

        // ── Accesibilidad / Sistema ────────────────────────────────────

        /// <summary>Código ISO del idioma de la interfaz (ej. "es", "en").</summary>
        public string Language        { get; set; } = "es";
        public bool   HapticsEnabled  { get; set; } = true;
        public bool   FaceIDEnabled   { get; set; } = false;

        // ── Keys de PlayerPrefs ────────────────────────────────────────

        private const string K_NOTIF_ENABLED    = "settings_notif_enabled";
        private const string K_REMINDER_HOUR    = "settings_reminder_hour";
        private const string K_REMINDER_MINUTE  = "settings_reminder_minute";
        private const string K_DARK_MODE        = "settings_dark_mode";
        private const string K_REDUCE_ANIM      = "settings_reduce_animations";
        private const string K_FONT_SIZE        = "settings_font_size";
        private const string K_HIGH_CONTRAST    = "settings_high_contrast";
        private const string K_LANGUAGE         = "settings_language";
        private const string K_HAPTICS          = "settings_haptics";
        private const string K_FACEID           = "settings_faceid";
        private const string K_MUSIC_VOLUME     = "settings_music_volume";
        private const string K_SFX_VOLUME       = "settings_sfx_volume";
        private const string K_COLORBLIND       = "settings_colorblind";

        // ── Persistencia ───────────────────────────────────────────────

        /// <summary>
        /// Carga la configuración desde PlayerPrefs.
        /// Devuelve valores por defecto para las keys no encontradas.
        /// </summary>
        public static AppSettings LoadFromPlayerPrefs()
        {
            return new AppSettings
            {
                NotificationsEnabled = PlayerPrefs.GetInt(K_NOTIF_ENABLED,   1) == 1,
                ReminderHour         = PlayerPrefs.GetInt(K_REMINDER_HOUR,   20),
                ReminderMinute       = PlayerPrefs.GetInt(K_REMINDER_MINUTE, 0),
                DarkMode             = PlayerPrefs.GetInt(K_DARK_MODE,       0) == 1,
                ReduceAnimations     = PlayerPrefs.GetInt(K_REDUCE_ANIM,     0) == 1,
                FontSize             = PlayerPrefs.GetInt(K_FONT_SIZE,       1),
                HighContrast         = PlayerPrefs.GetInt(K_HIGH_CONTRAST,   0) == 1,
                Language             = PlayerPrefs.GetString(K_LANGUAGE,     "es"),
                HapticsEnabled       = PlayerPrefs.GetInt(K_HAPTICS,         1) == 1,
                FaceIDEnabled        = PlayerPrefs.GetInt(K_FACEID,          0) == 1,
                MusicVolume          = PlayerPrefs.GetFloat(K_MUSIC_VOLUME,  1f),
                SfxVolume            = PlayerPrefs.GetFloat(K_SFX_VOLUME,    1f),
                Colorblind           = (ColorblindMode)PlayerPrefs.GetInt(K_COLORBLIND, 0)
            };
        }

        /// <summary>Escribe todos los campos en PlayerPrefs y llama Save().</summary>
        public void SaveToPlayerPrefs()
        {
            PlayerPrefs.SetInt(K_NOTIF_ENABLED,   NotificationsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(K_REMINDER_HOUR,   ReminderHour);
            PlayerPrefs.SetInt(K_REMINDER_MINUTE, ReminderMinute);
            PlayerPrefs.SetInt(K_DARK_MODE,       DarkMode ? 1 : 0);
            PlayerPrefs.SetInt(K_REDUCE_ANIM,     ReduceAnimations ? 1 : 0);
            PlayerPrefs.SetInt(K_FONT_SIZE,       FontSize);
            PlayerPrefs.SetInt(K_HIGH_CONTRAST,   HighContrast ? 1 : 0);
            PlayerPrefs.SetString(K_LANGUAGE,     Language);
            PlayerPrefs.SetInt(K_HAPTICS,         HapticsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(K_FACEID,          FaceIDEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(K_MUSIC_VOLUME,  MusicVolume);
            PlayerPrefs.SetFloat(K_SFX_VOLUME,    SfxVolume);
            PlayerPrefs.SetInt(K_COLORBLIND,      (int)Colorblind);
            PlayerPrefs.Save();
        }

        /// <summary>Elimina todas las keys de settings de PlayerPrefs.</summary>
        public static void DeleteAll()
        {
            PlayerPrefs.DeleteKey(K_NOTIF_ENABLED);
            PlayerPrefs.DeleteKey(K_REMINDER_HOUR);
            PlayerPrefs.DeleteKey(K_REMINDER_MINUTE);
            PlayerPrefs.DeleteKey(K_DARK_MODE);
            PlayerPrefs.DeleteKey(K_REDUCE_ANIM);
            PlayerPrefs.DeleteKey(K_FONT_SIZE);
            PlayerPrefs.DeleteKey(K_HIGH_CONTRAST);
            PlayerPrefs.DeleteKey(K_LANGUAGE);
            PlayerPrefs.DeleteKey(K_HAPTICS);
            PlayerPrefs.DeleteKey(K_FACEID);
            PlayerPrefs.DeleteKey(K_MUSIC_VOLUME);
            PlayerPrefs.DeleteKey(K_SFX_VOLUME);
            PlayerPrefs.DeleteKey(K_COLORBLIND);
            PlayerPrefs.Save();
        }
    }
}
