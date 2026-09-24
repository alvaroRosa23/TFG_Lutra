using UnityEngine;
using UnityEngine.UI;
using Lutra.Core.Architecture;
using Lutra.Core.Events;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Barra de navegación inferior con comportamiento especial en el botón central (índice 2).
    ///
    /// Botones 0, 1, 3, 4 navegan al AppState correspondiente en _navTargets.
    /// Botón central (índice 2):
    ///   - Si el estado actual es MainMenu → emite EventBus.EmitEmotionModalRequested()
    ///     para que MainMenuScreen abra el panel de selección Día/Momento.
    ///   - Si el estado actual NO es MainMenu → navega a MainMenu.
    ///
    /// La barra se oculta automáticamente en Onboarding y EmotionCheck.
    ///
    /// Setup en Inspector:
    ///   - _navButtons, _navTargets, _navButtonImages: mismo número de elementos, mismo índice.
    ///   - _centerButtonIcon: Image del botón central.
    ///   - _homeIcon / _emotionIcon: sprites para el cambio de estado.
    /// </summary>
    public class BottomNavBar : MonoBehaviour
    {
        private const int CenterIndex = 2;

        [Header("Navegación (mismo índice: botón ↔ destino ↔ imagen)")]
        [SerializeField] private Button[]   _navButtons;
        [SerializeField] private AppState[] _navTargets;
        [SerializeField] private Image[]    _navButtonImages;

        [Header("Botón central")]
        [SerializeField] private Image              _centerButtonIcon;
        [SerializeField] private Sprite             _homeIcon;
        [SerializeField] private Sprite             _emotionIcon;
        [SerializeField] private TMPro.TextMeshProUGUI _centerButtonLabel;

        [Header("Visibilidad")]
        [SerializeField] private GameObject _visualRoot;

        [Header("Colores de tab")]
        [SerializeField] private Color _activeColor   = Color.white;
        [SerializeField] private Color _inactiveColor = new Color(1f, 1f, 1f, 0.4f);

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_navButtons == null || _navTargets == null) return;

            int count = Mathf.Min(_navButtons.Length, _navTargets.Length);

            for (int i = 0; i < count; i++)
            {
                int index = i;

                if (_navButtons[index] == null) continue;

                if (index == CenterIndex)
                {
                    _navButtons[index].onClick.AddListener(_onCenterButtonClicked);
                }
                else
                {
                    AppState target = _navTargets[index]; // captura el valor antes del cierre
                    _navButtons[index].onClick.AddListener(() =>
                    {
                        Debug.Log($"[BottomNavBar] Navegando a {target}");
                        if (AppStateMachine.Instance != null)
                            AppStateMachine.Instance.TransitionTo(target);
                    });
                }
            }
        }

        private void Start()
        {
            // Estado visual inicial
            SetActiveTab(AppState.MainMenu);
            _onScreenChanged(AppState.MainMenu);
        }

        private void OnEnable()
        {
            EventBus.OnScreenChanged += SetActiveTab;
            EventBus.OnScreenChanged += _onScreenChanged;
        }

        private void OnDisable()
        {
            EventBus.OnScreenChanged -= SetActiveTab;
            EventBus.OnScreenChanged -= _onScreenChanged;
        }

        private void OnDestroy()
        {
            if (_navButtons == null) return;
            foreach (var btn in _navButtons)
                btn?.onClick.RemoveAllListeners();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Resalta visualmente la imagen del botón cuyo target coincide con <paramref name="state"/>
        /// y aplica el color inactivo al resto.
        /// </summary>
        public void SetActiveTab(AppState state)
        {
            if (_navButtonImages == null || _navTargets == null) return;

            int count = Mathf.Min(_navButtonImages.Length, _navTargets.Length);

            for (int i = 0; i < count; i++)
            {
                if (_navButtonImages[i] == null) continue;

                _navButtonImages[i].color = (_navTargets[i] == state) ? _activeColor : _inactiveColor;
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Oculta el contenido visual en los flujos sin navegación libre
        /// (EmotionCheck, Login, OnboardingProfile, Settings y la pantalla post-minijuego).
        /// Usa _visualRoot para no desactivar el GameObject raíz y preservar la suscripción al EventBus.
        /// En cualquier otro estado lo muestra y actualiza el icono del botón central.
        /// </summary>
        private void _onScreenChanged(AppState state)
        {
            bool hidden = state == AppState.EmotionCheck
                       || state == AppState.Login
                       || state == AppState.OnboardingProfile
                       || state == AppState.Register
                       || state == AppState.Settings
                       || state == AppState.MinigameActive;

            if (_visualRoot != null)
                _visualRoot.SetActive(!hidden);
            else
                gameObject.SetActive(!hidden);

            if (hidden) return;

            if (_centerButtonIcon != null)
                _centerButtonIcon.sprite = (state == AppState.MainMenu) ? _emotionIcon : _homeIcon;

            if (_centerButtonLabel != null)
                _centerButtonLabel.text = (state == AppState.MainMenu) ? "Qué tal?" : "Menú";
        }

        /// <summary>
        /// Comportamiento especial del botón central:
        /// en MainMenu abre el panel de selección Día/Momento vía EventBus;
        /// fuera de MainMenu navega a MainMenu.
        /// </summary>
        private void _onCenterButtonClicked()
        {
            if (AppStateMachine.Instance == null) return;

            if (AppStateMachine.Instance.CurrentState == AppState.MainMenu)
            {
                // Estamos en el menú principal: abrir panel Día/Momento
                EventBus.EmitEmotionModalRequested();
            }
            else
            {
                // Estamos en otra sección: volver al menú principal
                AppStateMachine.Instance.TransitionTo(AppState.MainMenu);
            }
        }
    }
}
