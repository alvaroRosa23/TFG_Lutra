using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

namespace Lutra.UI.Components
{
    /// <summary>
    /// Anima la aparición del texto de saludo con un fade in sobre el color del TextMeshPro.
    /// No requiere CanvasGroup adicional: opera directamente sobre el alpha del color del label.
    /// </summary>
    public class GreetingAnimator : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private float           _fadeInDuration = 0.5f;

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Escribe <paramref name="text"/> en el label y lo hace aparecer
        /// con un fade in de <see cref="_fadeInDuration"/> segundos.
        /// Devuelve un Task awaitable.
        /// </summary>
        public Task ShowGreeting(string text)
        {
            if (_label == null)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(_fadeIn(text, tcs));
            return tcs.Task;
        }

        // ── Métodos privados ───────────────────────────────────────────

        private IEnumerator _fadeIn(string text, TaskCompletionSource<bool> tcs)
        {
            // Empezar con alpha 0
            Color c = _label.color;
            c.a = 0f;
            _label.color = c;
            _label.text  = text;

            float elapsed = 0f;

            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                c = _label.color;
                c.a = Mathf.Clamp01(elapsed / _fadeInDuration);
                _label.color = c;
                yield return null;
            }

            // Garantizar alpha final exacto
            c = _label.color;
            c.a = 1f;
            _label.color = c;

            tcs.SetResult(true);
        }
    }
}
