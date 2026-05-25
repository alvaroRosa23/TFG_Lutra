using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;

namespace Lutra.UI.Screens
{
    /// <summary>
    /// Clase base de todas las pantallas de la app.
    /// Gestiona ciclo de vida (show/hide), fade del CanvasGroup y callbacks de foco.
    ///
    /// Implementa IAppState para que AppStateMachine pueda controlar la pantalla
    /// directamente: OnEnter → Show + OnScreenFocused; OnExit → Hide + OnScreenUnfocused.
    ///
    /// Uso desde ScreenManager:
    ///   NavigateTo(state) → Hide() pantalla anterior + Show() pantalla nueva.
    ///
    /// Animaciones: PlayEnterAnimation / PlayExitAnimation disponibles para uso manual.
    /// </summary>
    public abstract class UIScreen : MonoBehaviour, IAppState
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        // ── Propiedades ────────────────────────────────────────────────

        public bool IsVisible { get; private set; }

        /// <summary>Estado de la AppStateMachine que corresponde a esta pantalla.</summary>
        public abstract AppState ScreenState { get; }

        // ── Unity lifecycle ────────────────────────────────────────────

        protected virtual void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Activa el GameObject. Cuando se usa sin ScreenManager dispara PlayEnterAnimation()
        /// como fire-and-forget. ScreenManager llama a PlayEnterAnimation() por separado.
        /// </summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
            // Garantizar que el alpha sea visible; PlayExitAnimation lo puede haber dejado en 0
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
            IsVisible = true;
        }

        /// <summary>
        /// Desactiva el GameObject. Llamar siempre después de haber esperado PlayExitAnimation().
        /// </summary>
        public virtual void Hide()
        {
            IsVisible = false;
            gameObject.SetActive(false);
        }

        /// <summary>Animación de entrada. Debe ser esperada con await antes de considerar la pantalla lista.</summary>
        public abstract Task PlayEnterAnimation();

        /// <summary>Animación de salida. Debe ser esperada con await antes de llamar a Hide().</summary>
        public abstract Task PlayExitAnimation();

        /// <summary>Llamado por ScreenManager cuando esta pantalla pasa a ser la activa.</summary>
        public virtual void OnScreenFocused() { }

        /// <summary>Llamado por ScreenManager cuando esta pantalla deja de ser la activa.</summary>
        public virtual void OnScreenUnfocused() { }

        // ── IAppState ──────────────────────────────────────────────────

        /// <summary>AppStateMachine entra en este estado → muestra la pantalla y notifica el foco.</summary>
        public void OnEnter()
        {
            Show();
        }

        /// <summary>AppStateMachine sale de este estado → oculta la pantalla y notifica la pérdida de foco.</summary>
        public void OnExit()
        {
            Hide();
            OnScreenUnfocused();
        }

        /// <summary>Llamado cada frame por AppStateMachine mientras esta pantalla está activa. Override para lógica por frame.</summary>
        public virtual void OnUpdate() { }

        // ── Helpers protegidos ─────────────────────────────────────────

        /// <summary>
        /// Interpola el alpha del CanvasGroup de <paramref name="from"/> a <paramref name="to"/>
        /// en <paramref name="duration"/> segundos usando WaitForEndOfFrame por frame.
        /// </summary>
        protected Task FadeCanvasGroup(float from, float to, float duration)
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(_fadeCoroutine(from, to, duration, tcs));
            return tcs.Task;
        }

        // ── Métodos privados ───────────────────────────────────────────

        private IEnumerator _fadeCoroutine(float from, float to, float duration,
                                           TaskCompletionSource<bool> tcs)
        {
            if (_canvasGroup == null)
            {
                tcs.SetResult(true);
                yield break;
            }

            _canvasGroup.alpha = from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                yield return new WaitForEndOfFrame();
                elapsed           += Time.deltaTime;
                float t            = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.Lerp(from, to, t);
            }

            _canvasGroup.alpha = to;
            tcs.SetResult(true);
        }
    }
}
