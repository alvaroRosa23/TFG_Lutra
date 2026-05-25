using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.Core.Systems;
using Lutra.Features.EmotionCheck;
using Lutra.Features.Charts;
using Lutra.Features.MainMenu;
using Lutra.UI.Components;
using Lutra.UI.Theme;

namespace Lutra.UI.Screens
{
    /// <summary>
    /// Pantalla del menú principal.
    /// Muestra saludo personalizado, barra de racha de 7 días, mascota
    /// y botón de check-in emocional. Reacciona a eventos de racha y emoción vía EventBus.
    ///
    /// El botón central de BottomNavBar emite <see cref="EventBus.EmitEmotionModalRequested"/>,
    /// que abre el panel DayMoment para que el usuario elija entre check-in del día o del momento.
    /// </summary>
    public class MainMenuScreen : UIScreen
    {
        // ── AppState ───────────────────────────────────────────────────

        public override AppState ScreenState => AppState.MainMenu;

        // ── Referencias serializadas ───────────────────────────────────

        [Header("Fondo y mascota")]
        [SerializeField] private Image            _backgroundImage;
        [SerializeField] private Image            _mascotImage;

        [Header("Saludo")]
        [SerializeField] private TextMeshProUGUI  _greetingLabel;

        [Header("Racha")]
        [SerializeField] private GameObject       _streakRow;
        [SerializeField] private TextMeshProUGUI  _streakLabel;
        [SerializeField] private TextMeshProUGUI  _streakDaysLabel;
        [SerializeField] private Transform        _streakIconsContainer;
        [SerializeField] private GameObject       _streakIconPrefab;

        [Header("Panel Día / Momento")]
        [SerializeField] private GameObject       _dayMomentPanel;
        [SerializeField] private RectTransform    _dayMomentPanelRect;
        [SerializeField] private Button           _btnDia;
        [SerializeField] private Button           _btnMomento;

        [Header("Mascota (controlador)")]
        [SerializeField] private MascotController _mascotController;

        [Header("Navegación")]
        [SerializeField] private Button _settingsButton;

        [Header("Debug / Dev")]
        [SerializeField] private Button _logoutButton;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private DataRepository _dataRepository;
        private StreakManager  _streakManager;
        private ThemeManager   _themeManager;

        private DataRepository Repository          => _dataRepository ??= ServiceLocator.Get<DataRepository>();
        private StreakManager  StreakManagerService => _streakManager  ??= ServiceLocator.Get<StreakManager>();
        private ThemeManager   ThemeManagerService => _themeManager   ??= ServiceLocator.Get<ThemeManager>();

        // ── Guard anti-bucle para ApplyTheme ──────────────────────────

        private EmotionType? _lastAppliedEmotion;

        // ── Estado panel Día/Momento ───────────────────────────────────

        private bool      _isPanelOpen    = false;
        private Coroutine _panelCoroutine;

        // ── Unity lifecycle (Awake) ────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();

            if (_dayMomentPanel != null)
                _dayMomentPanel.SetActive(false);

            if (_btnDia != null)
                _btnDia.onClick.AddListener(_onDiaSelected);

            if (_btnMomento != null)
                _btnMomento.onClick.AddListener(_onMomentoSelected);

            _settingsButton?.onClick.AddListener(_onSettingsClicked);
            _logoutButton?.onClick.AddListener(_onLogoutClicked);
        }

        // ── UIScreen overrides ─────────────────────────────────────────

        public override Task PlayEnterAnimation() => FadeCanvasGroup(0f, 1f, 0.3f);
        public override Task PlayExitAnimation()  => FadeCanvasGroup(1f, 0f, 0.2f);

        public override void OnScreenFocused()
        {
            _ = _safeOnScreenFocused();
        }

        private async Task _safeOnScreenFocused()
        {
            Debug.Log("[MainMenuScreen] _safeOnScreenFocused START");
            try
            {
                // Perfil y saludo
                var profile = await Repository.GetUserProfile();
                _setGreeting(profile?.Name ?? "");

                // Racha
                int streak = await StreakManagerService.GetCurrentStreak();
                if (_streakDaysLabel != null)
                    _streakDaysLabel.text = ChartsCalculator.FormatStreak(streak);
                await _refreshStreakIcons();

                // Tema por última emoción registrada (o Calm por defecto)
                var lastEmotion = await Repository.GetLastEmotion();
                var emotionToApply = lastEmotion?.EmotionType ?? EmotionType.Calm;
                Debug.Log($"[MainMenuScreen] OnScreenFocused - " +
                          $"LastEmotion: {lastEmotion?.EmotionType}, " +
                          $"EmotionToApply: {emotionToApply}, " +
                          $"LastRecord Notes: {lastEmotion?.Notes}");
                _lastAppliedEmotion = emotionToApply;
                ThemeManagerService.ApplyTheme(emotionToApply);
            }
            catch (Exception ex)
            { Debug.LogError($"[MainMenuScreen] OnScreenFocused: {ex.Message}"); }
        }

        // ── Unity lifecycle ────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.OnStreakUpdated         += _onStreakUpdated;
            EventBus.OnCurrentEmotionChanged += _onCurrentEmotionChanged;
            EventBus.OnEmotionModalRequested += OpenDayMomentPanel;
        }

        private void OnDisable()
        {
            EventBus.OnStreakUpdated         -= _onStreakUpdated;
            EventBus.OnCurrentEmotionChanged -= _onCurrentEmotionChanged;
            EventBus.OnEmotionModalRequested -= OpenDayMomentPanel;
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Actualiza el saludo según la hora del día y el nombre del perfil.
        /// </summary>
        private void _setGreeting(string name)
        {
            if (_greetingLabel == null) return;
            if (string.IsNullOrWhiteSpace(name)) name = "Jugador";

            int hour = DateTime.Now.Hour;
            string greeting = hour switch
            {
                >= 6  and < 12 => $"Buenos días, {name}",
                >= 12 and < 20 => $"Buenas tardes, {name}",
                _              => $"Buenas noches, {name}"
            };

            _greetingLabel.text = greeting;
        }

        /// <summary>
        /// Destruye los iconos existentes e instancia 7 representando los días de la semana actual
        /// (lunes a domingo). Reglas de color:
        ///   Antes del perfil o futuro → gris    SetState(false, false)
        ///   Hoy con registro          → verde   SetState(true, false)
        ///   Hoy sin registro          → naranja SetState(false, true)
        ///   Pasado con registro       → verde   SetState(true, false)
        ///   Pasado sin registro       → rojo    SetMissed()
        /// </summary>
        private async Task _refreshStreakIcons()
        {
            if (_streakIconsContainer == null || _streakIconPrefab == null) return;

            var profile = await Repository.GetUserProfile();
            var dates   = await Repository.GetAllEmotionDates();

            // Calcular el lunes de la semana actual (DayOfWeek.Sunday = 0)
            int dayOfWeek      = (int)DateTime.Today.DayOfWeek;
            int daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
            DateTime monday    = DateTime.Today.AddDays(-daysFromMonday);

            // Destruir iconos anteriores
            for (int i = _streakIconsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _streakIconsContainer.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            DateTime profileCreated = profile?.CreationDate.Date ?? DateTime.Today;

            // i=0 → lunes … i=6 → domingo
            for (int i = 0; i < 7; i++)
            {
                DateTime day       = monday.AddDays(i);
                bool isToday       = day.Date == DateTime.Today;
                bool isFuture      = day.Date > DateTime.Today;
                bool hasRecord     = dates != null && dates.Any(d => d.Date == day.Date);
                bool beforeProfile = day.Date < profileCreated;

                var go    = Instantiate(_streakIconPrefab, _streakIconsContainer, false);
                var setup = go.GetComponent<StreakIconPrefabSetup>();

                if (setup != null)
                {
                    if (beforeProfile || isFuture)
                        setup.SetState(false, false);       // gris: antes del perfil o futuro
                    else if (isToday && hasRecord)
                        setup.SetState(true, false);        // verde: hoy registrado
                    else if (isToday && !hasRecord)
                        setup.SetState(false, true);        // naranja: hoy pendiente
                    else if (hasRecord)
                        setup.SetState(true, false);        // verde: pasado con registro
                    else
                        setup.SetMissed();                  // rojo: pasado sin registro
                }
                else
                {
                    // Fallback sin StreakIconPrefabSetup: colorear Image directamente
                    var img = go.GetComponent<Image>();
                    if (img != null)
                        img.color = hasRecord ? new Color(0.31f, 0.79f, 0.63f) : new Color(0.4f, 0.4f, 0.4f);
                }
            }

            if (_streakIconsContainer is RectTransform streakRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(streakRt);
        }

        // ── Callbacks EventBus ─────────────────────────────────────────

        private void _onStreakUpdated(int streak)
            => _ = _safeStreakUpdated(streak);

        private async Task _safeStreakUpdated(int streak)
        {
            try
            {
                if (_streakDaysLabel != null)
                    _streakDaysLabel.text = ChartsCalculator.FormatStreak(streak);
                await _refreshStreakIcons();
            }
            catch (Exception ex)
            { Debug.LogError($"[MainMenuScreen] _onStreakUpdated: {ex.Message}"); }
        }

        private void _onCurrentEmotionChanged(EmotionType emotion)
        {
            Debug.Log($"[MainMenuScreen] _onCurrentEmotionChanged: {emotion}");
            // Guard anti-bucle: ApplyTheme emite OnCurrentEmotionChanged al terminar
            if (_lastAppliedEmotion == emotion) return;
            _lastAppliedEmotion = emotion;
            ThemeManagerService.ApplyTheme(emotion);
        }

        // ── Panel Día / Momento ────────────────────────────────────────

        /// <summary>
        /// Muestra el panel bottom-sheet de selección de tipo de registro.
        /// El sheet entra desde Y = -220 hasta Y = 0 en 0.25 s (SmoothStep).
        /// </summary>
        public void OpenDayMomentPanel()
        {
            // Toggle: si ya está abierto, cerrarlo
            if (_isPanelOpen) { _ = CloseDayMomentPanel(); return; }

            if (_dayMomentPanel == null || _dayMomentPanelRect == null) return;
            if (_panelCoroutine != null) StopCoroutine(_panelCoroutine);

            _isPanelOpen = true;

            // Posicionar en el inicio antes de activar para evitar flash
            var pos = _dayMomentPanelRect.anchoredPosition;
            pos.y = -220f;
            _dayMomentPanelRect.anchoredPosition = pos;

            _dayMomentPanel.SetActive(true);
            _panelCoroutine = StartCoroutine(_animateOpen());
        }

        /// <summary>
        /// Oculta el panel bottom-sheet.
        /// El sheet sale hacia Y = -220 en 0.2 s (SmoothStep).
        /// Al terminar desactiva <see cref="_dayMomentPanel"/>.
        /// Devuelve un Task que se completa al finalizar la animación.
        /// </summary>
        public Task CloseDayMomentPanel()
        {
            if (_dayMomentPanel == null || !_dayMomentPanel.activeSelf)
                return Task.CompletedTask;

            if (_panelCoroutine != null) StopCoroutine(_panelCoroutine);

            var tcs = new TaskCompletionSource<bool>();
            _panelCoroutine = StartCoroutine(_animateClose(tcs));
            return tcs.Task;
        }

        private IEnumerator _animateOpen()
        {
            const float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float t   = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                var pos   = _dayMomentPanelRect.anchoredPosition;
                pos.y     = Mathf.Lerp(-220f, 0f, t);
                _dayMomentPanelRect.anchoredPosition = pos;
            }

            // Garantizar valor final exacto
            var finalPos = _dayMomentPanelRect.anchoredPosition;
            finalPos.y   = 0f;
            _dayMomentPanelRect.anchoredPosition = finalPos;

            _panelCoroutine = null;
        }

        private IEnumerator _animateClose(TaskCompletionSource<bool> tcs)
        {
            const float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float t   = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                var pos   = _dayMomentPanelRect.anchoredPosition;
                pos.y     = Mathf.Lerp(0f, -220f, t);
                _dayMomentPanelRect.anchoredPosition = pos;
            }

            // Garantizar valor final exacto y desactivar
            var finalPos = _dayMomentPanelRect.anchoredPosition;
            finalPos.y   = -220f;
            _dayMomentPanelRect.anchoredPosition = finalPos;

            _dayMomentPanel.SetActive(false);
            _isPanelOpen    = false;
            _panelCoroutine = null;
            tcs.SetResult(true);
        }

        // ── Callbacks botones del panel ────────────────────────────────

        /// <summary>
        /// El usuario quiere hacer el check-in del día.
        /// Espera a que termine la animación de cierre y navega a EmotionCheck en modo Day.
        /// </summary>
        private void _onDiaSelected()
            => _ = _safeDiaSelected();

        private async Task _safeDiaSelected()
        {
            try
            {
                await CloseDayMomentPanel();
                EmotionCheckScreen.PendingMode = EmotionCheckMode.Day;
                AppStateMachine.Instance.TransitionTo(AppState.EmotionCheck);
            }
            catch (Exception ex)
            { Debug.LogError($"[MainMenuScreen] _onDiaSelected: {ex.Message}"); }
        }

        /// <summary>
        /// El usuario quiere registrar cómo se siente en este momento.
        /// Espera a que termine la animación de cierre y navega a EmotionCheck en modo Moment.
        /// </summary>
        private void _onMomentoSelected()
            => _ = _safeMomentoSelected();

        private async Task _safeMomentoSelected()
        {
            try
            {
                await CloseDayMomentPanel();
                EmotionCheckScreen.PendingMode = EmotionCheckMode.Moment;
                AppStateMachine.Instance.TransitionTo(AppState.EmotionCheck);
            }
            catch (Exception ex)
            { Debug.LogError($"[MainMenuScreen] _onMomentoSelected: {ex.Message}"); }
        }

        // ── Ajustes ────────────────────────────────────────────────────

        private void _onSettingsClicked()
            => AppStateMachine.Instance.TransitionTo(AppState.Settings);

        // ── Logout / borrado de sesión ─────────────────────────────────

        private void _onLogoutClicked()
            => _ = _safeLogout();

        private async Task _safeLogout()
        {
            try
            {
                ServiceLocator.Get<AuthManager>().Logout();
                AppStateMachine.Instance.TransitionTo(AppState.Login);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            { Debug.LogError($"[MainMenuScreen] _onLogoutClicked: {ex.Message}"); }
        }

        private void OnDestroy()
        {
            _settingsButton?.onClick.RemoveAllListeners();
            _logoutButton?.onClick.RemoveListener(_onLogoutClicked);
            _btnDia?.onClick.RemoveAllListeners();
            _btnMomento?.onClick.RemoveAllListeners();
        }
    }
}
