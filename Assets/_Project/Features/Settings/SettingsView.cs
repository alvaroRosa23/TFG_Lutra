using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Settings
{
    /// <summary>
    /// Vista de la pantalla de Ajustes.
    /// Gestiona todos los controles de UI y delega las acciones al SettingsController
    /// a través de eventos C#.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        // ── Referencias serializadas ───────────────────────────────────

        [Header("Notificaciones")]
        [SerializeField] private Toggle          _notificationsToggle;
        [SerializeField] private Slider          _reminderHourSlider;
        [SerializeField] private TextMeshProUGUI _reminderTimeLabel;

        [Header("Apariencia")]
        [SerializeField] private Toggle _darkModeToggle;

        [Header("Accesibilidad")]
        [SerializeField] private Toggle _hapticsToggle;

        [Header("Audio")]
        [SerializeField] private Slider          _musicVolumeSlider;
        [SerializeField] private Slider          _sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI _musicVolumeLabel;
        [SerializeField] private TextMeshProUGUI _sfxVolumeLabel;

        [Header("Accesibilidad - Daltonismo")]
        [SerializeField] private Button _colorblindNoneButton;
        [SerializeField] private Button _colorblindDeuterButton;
        [SerializeField] private Button _colorblindProtaButton;
        [SerializeField] private Button _colorblindTritaButton;

        [Header("Idioma")]
        [SerializeField] private Button _languageEsButton;
        [SerializeField] private Button _languageEnButton;

        [Header("Perfil")]
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TMP_InputField _surnameInput;
        [SerializeField] private TMP_InputField _dobDayInput;
        [SerializeField] private TMP_InputField _dobMonthInput;
        [SerializeField] private TMP_InputField _dobYearInput;
        [SerializeField] private Button         _saveProfileButton;

        [Header("Datos")]
        [SerializeField] private Button     _exportDataButton;
        [SerializeField] private Button     _deleteDataButton;
        [SerializeField] private GameObject _confirmDeletePanel;
        [SerializeField] private Button     _confirmDeleteYes;
        [SerializeField] private Button     _confirmDeleteNo;

        [Header("Navegación")]
        [SerializeField] private Button _backButton;

        [Header("Cuenta")]
        [SerializeField] private Button _changePasswordButton;
        [SerializeField] private Button _logoutButton;
        [SerializeField] private Button _deleteAccountButton;

        [Header("Panel cambio de contraseña")]
        [SerializeField] private GameObject     _changePasswordPanel;
        [SerializeField] private TMP_InputField _oldPasswordInput;
        [SerializeField] private TMP_InputField _newPasswordInput;
        [SerializeField] private TMP_InputField _newPasswordConfirmInput;
        [SerializeField] private Button         _confirmChangePasswordButton;
        [SerializeField] private Button         _cancelChangePasswordButton;

        [Header("Panel eliminar cuenta - primera confirmación")]
        [SerializeField] private GameObject _deleteAccountPanel1;
        [SerializeField] private Button     _deleteAccountConfirm1Yes;
        [SerializeField] private Button     _deleteAccountConfirm1No;

        [Header("Panel eliminar cuenta - segunda confirmación")]
        [SerializeField] private GameObject _deleteAccountPanel2;
        [SerializeField] private Button     _deleteAccountConfirm2Yes;
        [SerializeField] private Button     _deleteAccountConfirm2No;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI _feedbackLabel;

        // ── Eventos públicos ───────────────────────────────────────────

        /// <summary>notificationsEnabled, reminderHour, reminderMinute</summary>
        public event Action<bool, int, int>        OnNotificationSettingsChanged;

        /// <summary>darkMode, reduceAnimations, fontSize, highContrast</summary>
        public event Action<bool, bool, int, bool> OnAppearanceSettingsChanged;

        /// <summary>musicVolume [0-1], sfxVolume [0-1]</summary>
        public event Action<float, float>          OnAudioSettingsChanged;

        public event Action<ColorblindMode>        OnColorblindModeChanged;

        /// <summary>Código ISO del idioma seleccionado ("es", "en", …).</summary>
        public event Action<string>                OnLanguageChanged;

        /// <summary>name, surname, dob ("dd/MM/yyyy"), avatar</summary>
        public event Action<string, string, string, string> OnProfileSaved;

        /// <summary>oldPassword, newPassword</summary>
        public event Action<string, string>        OnChangePasswordRequested;

        public event Action                        OnDeleteAccountConfirmed;
        public event Action                        OnLogoutRequested;
        public event Action                        OnExportDataClicked;
        public event Action                        OnDeleteDataConfirmed;
        public event Action                        OnBackRequested;

        // ── Estado interno ─────────────────────────────────────────────

        private Coroutine     _feedbackCoroutine;
        private int           _currentFontSize     = 1;
        private ColorblindMode _activeColorblind   = ColorblindMode.None;
        private string        _activeLanguage      = "es";

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            // Notificaciones
            if (_reminderHourSlider != null)
            {
                _reminderHourSlider.minValue    = 0;
                _reminderHourSlider.maxValue    = 23;
                _reminderHourSlider.wholeNumbers = true;
                _reminderHourSlider.onValueChanged.AddListener(_onReminderHourChanged);
            }
            _notificationsToggle?.onValueChanged.AddListener(_onNotificationToggleChanged);

            // Apariencia
            _darkModeToggle?.onValueChanged.AddListener(_onAppearanceChanged);
            _hapticsToggle?.onValueChanged.AddListener(_onHapticsChanged);

            // Audio
            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.minValue     = 0f;
                _musicVolumeSlider.maxValue     = 1f;
                _musicVolumeSlider.wholeNumbers = false;
                _musicVolumeSlider.onValueChanged.AddListener(_onMusicVolumeChanged);
            }
            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.minValue     = 0f;
                _sfxVolumeSlider.maxValue     = 1f;
                _sfxVolumeSlider.wholeNumbers = false;
                _sfxVolumeSlider.onValueChanged.AddListener(_onSfxVolumeChanged);
            }

            // Daltonismo
            _colorblindNoneButton?.onClick.AddListener(  () => _onColorblindSelected(ColorblindMode.None));
            _colorblindDeuterButton?.onClick.AddListener(() => _onColorblindSelected(ColorblindMode.Deuteranopia));
            _colorblindProtaButton?.onClick.AddListener( () => _onColorblindSelected(ColorblindMode.Protanopia));
            _colorblindTritaButton?.onClick.AddListener( () => _onColorblindSelected(ColorblindMode.Tritanopia));

            // Idioma
            _languageEsButton?.onClick.AddListener(() => _onLanguageSelected("es"));
            _languageEnButton?.onClick.AddListener(() => _onLanguageSelected("en"));

            // Perfil
            if (_nameInput    != null) _nameInput.contentType    = TMP_InputField.ContentType.Name;
            if (_surnameInput != null) _surnameInput.contentType = TMP_InputField.ContentType.Name;
            _saveProfileButton?.onClick.AddListener(_onSaveProfileClicked);

            // Datos
            _exportDataButton?.onClick.AddListener(() => OnExportDataClicked?.Invoke());
            _deleteDataButton?.onClick.AddListener(ShowDeleteConfirmation);
            _confirmDeleteYes?.onClick.AddListener(_onConfirmDeleteYes);
            _confirmDeleteNo?.onClick.AddListener(_onConfirmDeleteNo);

            // Navegación
            _backButton?.onClick.AddListener(() => OnBackRequested?.Invoke());

            // Cuenta
            _changePasswordButton?.onClick.AddListener(_onChangePasswordButtonClicked);
            _logoutButton?.onClick.AddListener(() => OnLogoutRequested?.Invoke());
            _deleteAccountButton?.onClick.AddListener(_onDeleteAccountButtonClicked);

            // Panel de cambio de contraseña
            _confirmChangePasswordButton?.onClick.AddListener(_onConfirmChangePasswordClicked);
            _cancelChangePasswordButton?.onClick.AddListener(_onCancelChangePasswordClicked);

            // Panel de eliminación de cuenta
            _deleteAccountConfirm1Yes?.onClick.AddListener(_onDeleteAccountConfirm1Yes);
            _deleteAccountConfirm1No?.onClick.AddListener(_onDeleteAccountConfirm1No);
            _deleteAccountConfirm2Yes?.onClick.AddListener(_onDeleteAccountConfirm2Yes);
            _deleteAccountConfirm2No?.onClick.AddListener(_onDeleteAccountConfirm2No);

            // Ocultar paneles y feedback inicialmente
            if (_confirmDeletePanel    != null) _confirmDeletePanel.SetActive(false);
            if (_changePasswordPanel   != null) _changePasswordPanel.SetActive(false);
            if (_deleteAccountPanel1   != null) _deleteAccountPanel1.SetActive(false);
            if (_deleteAccountPanel2   != null) _deleteAccountPanel2.SetActive(false);
            if (_feedbackLabel         != null) _feedbackLabel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _reminderHourSlider?.onValueChanged.RemoveAllListeners();
            _notificationsToggle?.onValueChanged.RemoveAllListeners();
            _darkModeToggle?.onValueChanged.RemoveAllListeners();
            _hapticsToggle?.onValueChanged.RemoveAllListeners();
            _musicVolumeSlider?.onValueChanged.RemoveAllListeners();
            _sfxVolumeSlider?.onValueChanged.RemoveAllListeners();
            _colorblindNoneButton?.onClick.RemoveAllListeners();
            _colorblindDeuterButton?.onClick.RemoveAllListeners();
            _colorblindProtaButton?.onClick.RemoveAllListeners();
            _colorblindTritaButton?.onClick.RemoveAllListeners();
            _languageEsButton?.onClick.RemoveAllListeners();
            _languageEnButton?.onClick.RemoveAllListeners();
            _saveProfileButton?.onClick.RemoveAllListeners();
            _exportDataButton?.onClick.RemoveAllListeners();
            _deleteDataButton?.onClick.RemoveAllListeners();
            _confirmDeleteYes?.onClick.RemoveAllListeners();
            _confirmDeleteNo?.onClick.RemoveAllListeners();
            _backButton?.onClick.RemoveAllListeners();
            _changePasswordButton?.onClick.RemoveAllListeners();
            _logoutButton?.onClick.RemoveAllListeners();
            _deleteAccountButton?.onClick.RemoveAllListeners();
            _confirmChangePasswordButton?.onClick.RemoveAllListeners();
            _cancelChangePasswordButton?.onClick.RemoveAllListeners();
            _deleteAccountConfirm1Yes?.onClick.RemoveAllListeners();
            _deleteAccountConfirm1No?.onClick.RemoveAllListeners();
            _deleteAccountConfirm2Yes?.onClick.RemoveAllListeners();
            _deleteAccountConfirm2No?.onClick.RemoveAllListeners();

            OnNotificationSettingsChanged = null;
            OnAppearanceSettingsChanged   = null;
            OnAudioSettingsChanged        = null;
            OnColorblindModeChanged       = null;
            OnLanguageChanged             = null;
            OnProfileSaved                = null;
            OnChangePasswordRequested     = null;
            OnDeleteAccountConfirmed      = null;
            OnLogoutRequested             = null;
            OnExportDataClicked           = null;
            OnDeleteDataConfirmed         = null;
            OnBackRequested               = null;
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Rellena todos los controles con los valores del settings actual.</summary>
        public void LoadSettings(AppSettings settings)
        {
            if (settings == null) return;

            _currentFontSize = settings.FontSize;

            // Desactivar listeners temporalmente para evitar disparar eventos durante la carga
            _silentSetToggle(_notificationsToggle, settings.NotificationsEnabled);
            _silentSetToggle(_darkModeToggle,      settings.DarkMode);
            _silentSetToggle(_hapticsToggle,       settings.HapticsEnabled);

            if (_reminderHourSlider != null)
            {
                _reminderHourSlider.SetValueWithoutNotify(settings.ReminderHour);
                _updateReminderLabel(settings.ReminderHour, settings.ReminderMinute);
            }

            // Audio
            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
                _updateMusicVolumeLabel(settings.MusicVolume);
            }
            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.SetValueWithoutNotify(settings.SfxVolume);
                _updateSfxVolumeLabel(settings.SfxVolume);
            }

            // Daltonismo
            _activeColorblind = settings.Colorblind;
            _refreshColorblindButtons();

            // Idioma
            _activeLanguage = settings.Language;
            _refreshLanguageButtons();
        }

        /// <summary>Rellena los campos de perfil del usuario.</summary>
        public void LoadProfile(string name, string surname, DateTime dob, string avatar)
        {
            if (_nameInput    != null) _nameInput.SetTextWithoutNotify(name    ?? "");
            if (_surnameInput != null) _surnameInput.SetTextWithoutNotify(surname ?? "");

            if (dob != DateTime.MinValue)
            {
                if (_dobDayInput   != null) _dobDayInput.SetTextWithoutNotify(dob.Day.ToString());
                if (_dobMonthInput != null) _dobMonthInput.SetTextWithoutNotify(dob.Month.ToString());
                if (_dobYearInput  != null) _dobYearInput.SetTextWithoutNotify(dob.Year.ToString());
            }
            // TODO: selector de avatar no implementado aún
        }

        /// <summary>
        /// Muestra un mensaje de feedback temporal (3 segundos).
        /// Si isError es true, el texto se muestra en rojo.
        /// </summary>
        public void ShowFeedback(string message, bool isError = false)
        {
            if (_feedbackLabel == null) return;

            if (_feedbackCoroutine != null)
                StopCoroutine(_feedbackCoroutine);

            _feedbackLabel.text  = message;
            _feedbackLabel.color = isError ? Color.red : Color.white;
            _feedbackLabel.gameObject.SetActive(true);

            _feedbackCoroutine = StartCoroutine(_hideFeedbackAfterDelay(3f));
        }

        /// <summary>Muestra el panel de confirmación de borrado de datos locales.</summary>
        public void ShowDeleteConfirmation()
        {
            if (_confirmDeletePanel != null)
                _confirmDeletePanel.SetActive(true);
        }

        // ── Handlers de notificaciones ─────────────────────────────────

        private void _onReminderHourChanged(float value)
        {
            int hour = Mathf.RoundToInt(value);
            _updateReminderLabel(hour, 0);

            bool notifEnabled = _notificationsToggle != null && _notificationsToggle.isOn;
            OnNotificationSettingsChanged?.Invoke(notifEnabled, hour, 0);
        }

        private void _onNotificationToggleChanged(bool enabled)
        {
            int hour = _reminderHourSlider != null ? Mathf.RoundToInt(_reminderHourSlider.value) : 20;
            OnNotificationSettingsChanged?.Invoke(enabled, hour, 0);
        }

        // ── Handlers de apariencia ─────────────────────────────────────

        private void _onAppearanceChanged(bool _)
        {
            bool dark = _darkModeToggle != null && _darkModeToggle.isOn;
            OnAppearanceSettingsChanged?.Invoke(dark, false, _currentFontSize, false);
        }

        private void _onHapticsChanged(bool enabled)
        {
            bool dark = _darkModeToggle != null && _darkModeToggle.isOn;
            OnAppearanceSettingsChanged?.Invoke(dark, false, _currentFontSize, false);
        }

        // ── Handlers de audio ──────────────────────────────────────────

        private void _onMusicVolumeChanged(float value)
        {
            _updateMusicVolumeLabel(value);
            float sfx = _sfxVolumeSlider != null ? _sfxVolumeSlider.value : 1f;
            OnAudioSettingsChanged?.Invoke(value, sfx);
        }

        private void _onSfxVolumeChanged(float value)
        {
            _updateSfxVolumeLabel(value);
            float music = _musicVolumeSlider != null ? _musicVolumeSlider.value : 1f;
            OnAudioSettingsChanged?.Invoke(music, value);
        }

        // ── Handlers de daltonismo ─────────────────────────────────────

        private void _onColorblindSelected(ColorblindMode mode)
        {
            _activeColorblind = mode;
            _refreshColorblindButtons();
            OnColorblindModeChanged?.Invoke(mode);
        }

        // ── Handlers de idioma ─────────────────────────────────────────

        private void _onLanguageSelected(string languageCode)
        {
            _activeLanguage = languageCode;
            _refreshLanguageButtons();
            OnLanguageChanged?.Invoke(languageCode);
        }

        // ── Handlers de perfil ─────────────────────────────────────────

        private void _onSaveProfileClicked()
        {
            string name    = _nameInput    != null ? _nameInput.text    : "";
            string surname = _surnameInput != null ? _surnameInput.text : "";

            string dayStr   = _dobDayInput   != null ? _dobDayInput.text   : "";
            string monthStr = _dobMonthInput != null ? _dobMonthInput.text : "";
            string yearStr  = _dobYearInput  != null ? _dobYearInput.text  : "";

            if (!int.TryParse(dayStr, out int day) || day < 1 || day > 31)
            {
                ShowFeedback("El día debe estar entre 1 y 31.", isError: true);
                return;
            }
            if (!int.TryParse(monthStr, out int month) || month < 1 || month > 12)
            {
                ShowFeedback("El mes debe estar entre 1 y 12.", isError: true);
                return;
            }
            if (!int.TryParse(yearStr, out int year) || year < 1900 || year > DateTime.Today.Year)
            {
                ShowFeedback($"El año debe estar entre 1900 y {DateTime.Today.Year}.", isError: true);
                return;
            }
            if (!_isValidDate(day, month, year))
            {
                ShowFeedback("La fecha de nacimiento no es válida.", isError: true);
                return;
            }

            string dob = $"{day:D2}/{month:D2}/{year}";
            OnProfileSaved?.Invoke(name, surname, dob, "");
        }

        private static bool _isValidDate(int day, int month, int year)
        {
            try   { _ = new DateTime(year, month, day); return true; }
            catch { return false; }
        }

        // ── Handlers de datos ──────────────────────────────────────────

        private void _onConfirmDeleteYes()
        {
            if (_confirmDeletePanel != null)
                _confirmDeletePanel.SetActive(false);
            OnDeleteDataConfirmed?.Invoke();
        }

        private void _onConfirmDeleteNo()
        {
            if (_confirmDeletePanel != null)
                _confirmDeletePanel.SetActive(false);
        }

        // ── Handlers de cambio de contraseña ──────────────────────────

        private void _onChangePasswordButtonClicked()
        {
            if (_oldPasswordInput        != null) _oldPasswordInput.SetTextWithoutNotify("");
            if (_newPasswordInput        != null) _newPasswordInput.SetTextWithoutNotify("");
            if (_newPasswordConfirmInput != null) _newPasswordConfirmInput.SetTextWithoutNotify("");
            if (_changePasswordPanel     != null) _changePasswordPanel.SetActive(true);
        }

        private void _onConfirmChangePasswordClicked()
        {
            string oldPwd  = _oldPasswordInput        != null ? _oldPasswordInput.text        : "";
            string newPwd  = _newPasswordInput        != null ? _newPasswordInput.text        : "";
            string confirm = _newPasswordConfirmInput != null ? _newPasswordConfirmInput.text : "";

            if (newPwd.Length < 8)
            {
                ShowFeedback("La contraseña debe tener al menos 8 caracteres.", isError: true);
                return;
            }
            if (newPwd != confirm)
            {
                ShowFeedback("Las contraseñas nuevas no coinciden.", isError: true);
                return;
            }

            if (_changePasswordPanel != null)
                _changePasswordPanel.SetActive(false);

            OnChangePasswordRequested?.Invoke(oldPwd, newPwd);
        }

        private void _onCancelChangePasswordClicked()
        {
            if (_changePasswordPanel != null)
                _changePasswordPanel.SetActive(false);
        }

        // ── Handlers de eliminación de cuenta ─────────────────────────

        private void _onDeleteAccountButtonClicked()
        {
            if (_deleteAccountPanel1 != null)
                _deleteAccountPanel1.SetActive(true);
        }

        private void _onDeleteAccountConfirm1Yes()
        {
            if (_deleteAccountPanel1 != null) _deleteAccountPanel1.SetActive(false);
            if (_deleteAccountPanel2 != null) _deleteAccountPanel2.SetActive(true);
        }

        private void _onDeleteAccountConfirm1No()
        {
            if (_deleteAccountPanel1 != null)
                _deleteAccountPanel1.SetActive(false);
        }

        private void _onDeleteAccountConfirm2Yes()
        {
            if (_deleteAccountPanel2 != null)
                _deleteAccountPanel2.SetActive(false);
            OnDeleteAccountConfirmed?.Invoke();
        }

        private void _onDeleteAccountConfirm2No()
        {
            if (_deleteAccountPanel2 != null)
                _deleteAccountPanel2.SetActive(false);
        }

        // ── Helpers privados ───────────────────────────────────────────

        private void _updateReminderLabel(int hour, int minute)
        {
            if (_reminderTimeLabel != null)
                _reminderTimeLabel.text = $"{hour:D2}:{minute:D2}";
        }

        private void _updateMusicVolumeLabel(float value)
        {
            if (_musicVolumeLabel != null)
                _musicVolumeLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void _updateSfxVolumeLabel(float value)
        {
            if (_sfxVolumeLabel != null)
                _sfxVolumeLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        private void _refreshColorblindButtons()
        {
            _setButtonActive(_colorblindNoneButton,   _activeColorblind == ColorblindMode.None);
            _setButtonActive(_colorblindDeuterButton, _activeColorblind == ColorblindMode.Deuteranopia);
            _setButtonActive(_colorblindProtaButton,  _activeColorblind == ColorblindMode.Protanopia);
            _setButtonActive(_colorblindTritaButton,  _activeColorblind == ColorblindMode.Tritanopia);
        }

        private void _refreshLanguageButtons()
        {
            _setButtonActive(_languageEsButton, _activeLanguage == "es");
            _setButtonActive(_languageEnButton, _activeLanguage == "en");
        }

        private static void _setButtonActive(Button button, bool active)
        {
            if (button == null) return;
            var target = active ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            var colors = button.colors;
            colors.normalColor   = target;
            colors.selectedColor = target;
            button.colors = colors;
            // Unity no repinta el gráfico al cambiar colors a menos que ocurra una transición de estado.
            // CrossFadeColor con duration=0 fuerza la actualización visual inmediata.
            button.targetGraphic?.CrossFadeColor(target, 0f, true, true);
        }

        /// <summary>Establece el valor de un toggle sin disparar onValueChanged.</summary>
        private static void _silentSetToggle(Toggle toggle, bool value)
        {
            if (toggle == null) return;
            toggle.SetIsOnWithoutNotify(value);
        }

        private IEnumerator _hideFeedbackAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_feedbackLabel != null)
                _feedbackLabel.gameObject.SetActive(false);
        }
    }
}
