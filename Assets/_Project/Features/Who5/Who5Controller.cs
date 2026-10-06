using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.Core.Systems;
using Lutra.UI.Components;

namespace Lutra.Features.Who5
{
    /// <summary>
    /// Controlador del cuestionario WHO-5 (docs/PROFESSIONAL_REPORT.md §3.3).
    ///
    /// Introducción → 5 ítems (uno por pantalla) → Enviar. Al enviar: guarda el ScaleResponse,
    /// resuelve la notificación anclada, da 20 monedas (independientes de las respuestas) y
    /// programa el aviso de la próxima aplicación. Si se sale antes de enviar, las respuestas
    /// se descartan y la notificación sigue anclada.
    /// </summary>
    public class Who5Controller : MonoBehaviour
    {
        [SerializeField] private Who5View _view;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private DataRepository _repo;
        private DataRepository Repo => _repo ??= ServiceLocator.Get<DataRepository>();

        private NotificationCenter _center;
        private NotificationCenter Center => _center ??= ServiceLocator.Get<NotificationCenter>();

        private static readonly CultureInfo _es = new CultureInfo("es-ES");

        // ── Estado ─────────────────────────────────────────────────────

        private readonly int?[] _answers = new int?[Who5Questionnaire.ItemCount];   // índice de opción por ítem
        private int       _currentItem;
        private DateTime? _firstItemShownAt;
        private DateTime  _availableSince;
        private string    _notificationRemoteId;
        private bool      _submitting;
        private bool      _submitted;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<Who5View>();
        }

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnCloseRequested    += _onCloseRequested;
            _view.OnStartRequested    += _onStartRequested;
            _view.OnOptionSelected    += _onOptionSelected;
            _view.OnPreviousRequested += _onPreviousRequested;
            _view.OnNextRequested     += _onNextRequested;
            _view.OnDoneRequested     += _onDoneRequested;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnCloseRequested    -= _onCloseRequested;
            _view.OnStartRequested    -= _onStartRequested;
            _view.OnOptionSelected    -= _onOptionSelected;
            _view.OnPreviousRequested -= _onPreviousRequested;
            _view.OnNextRequested     -= _onNextRequested;
            _view.OnDoneRequested     -= _onDoneRequested;
        }

        // ── API pública ────────────────────────────────────────────────

        public void OpenQuestionnaire() => _ = _safeOpen();

        /// <summary>Descarta las respuestas a medias (al salir de la pantalla sin enviar).</summary>
        public void DiscardAnswers()
        {
            if (_submitting) return;
            for (int i = 0; i < _answers.Length; i++) _answers[i] = null;
            _currentItem      = 0;
            _firstItemShownAt = null;
        }

        // ── Handlers ───────────────────────────────────────────────────

        private void _onStartRequested() => _showItem(0);

        private void _onOptionSelected(int optionIndex)
        {
            if (_submitting || optionIndex < 0 || optionIndex >= Who5Questionnaire.Options.Length) return;
            _answers[_currentItem] = optionIndex;
            _view.SetSelectedOption(optionIndex);
        }

        private void _onPreviousRequested()
        {
            if (_submitting || _currentItem == 0) return;
            _showItem(_currentItem - 1);
        }

        private void _onNextRequested()
        {
            if (_submitting || !_answers[_currentItem].HasValue) return;

            if (_currentItem < Who5Questionnaire.ItemCount - 1)
                _showItem(_currentItem + 1);
            else
                _ = _safeSubmit();
        }

        private void _onCloseRequested()
        {
            if (_submitting) return;
            DiscardAnswers();
            AppStateMachine.Instance.TransitionTo(AppState.Notifications);
        }

        private void _onDoneRequested() => AppStateMachine.Instance.TransitionTo(AppState.Notifications);

        // ── Métodos privados ───────────────────────────────────────────

        private async Task _safeOpen()
        {
            if (_view == null) return;

            DiscardAnswers();
            _submitted = false;
            _view.ShowIntro(Who5Questionnaire.Instructions);

            try
            {
                // Notificación anclada pendiente: de ella sale la fecha de disponibilidad
                var pending = (await Center.GetPinned())
                    .Where(n => n.Type == NotificationType.Who5Available)
                    .OrderBy(n => n.CreatedAt)
                    .FirstOrDefault();

                _notificationRemoteId = pending?.RemoteId;
                _availableSince = pending != null
                    ? (Who5Questionnaire.TryParseAvailableSince(pending.RemoteId, out var since) ? since : pending.CreatedAt.Date)
                    : DateTime.Today;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Who5Controller] _safeOpen: {ex.Message}");
                _notificationRemoteId = null;
                _availableSince       = DateTime.Today;
            }
        }

        private void _showItem(int index)
        {
            _currentItem = index;
            if (_firstItemShownAt == null) _firstItemShownAt = DateTime.Now;

            _view.ShowQuestion(
                index,
                Who5Questionnaire.ItemCount,
                Who5Questionnaire.ItemPrefix,
                Who5Questionnaire.Items[index],
                Who5Questionnaire.Options.Select(o => o.label).ToArray(),
                _answers[index],
                index == Who5Questionnaire.ItemCount - 1);
        }

        private async Task _safeSubmit()
        {
            if (_submitting || _submitted) return;
            if (_answers.Any(a => !a.HasValue)) return;

            _submitting = true;
            _view.SetSubmitting(true);

            try
            {
                var values = _answers.Select(a => Who5Questionnaire.Options[a.Value].value).ToList();
                int raw    = Who5Questionnaire.RawScore(values);
                int score  = Who5Questionnaire.ToIndex(raw);
                DateTime now = DateTime.Now;

                await Repo.SaveScaleResponse(new ScaleResponse
                {
                    Scale           = ScaleType.Who5,
                    AvailableSince  = _availableSince,
                    CompletedAt     = now,
                    Answers         = values,
                    RawScore        = raw,
                    Score           = score,
                    DurationSeconds = _firstItemShownAt.HasValue ? (float)(now - _firstItemShownAt.Value).TotalSeconds : 0f
                });
                _submitted = true;

                // El envío ya está guardado: cada paso siguiente falla por separado sin bloquear la pantalla
                await _safeStep("resolver la notificación", async () =>
                {
                    if (!string.IsNullOrEmpty(_notificationRemoteId))
                        await Center.ResolvePinned(_notificationRemoteId);
                });
                await _safeStep("dar la recompensa", _grantReward);
                await _safeStep("programar el próximo aviso", () =>
                {
                    Who5Scheduler.ScheduleNextReminder(now);
                    return Task.CompletedTask;
                });

                // Protocolo de apoyo si el índice es ≤ 28 (el diálogo se abre sobre el agradecimiento)
                await _safeStep("comprobar el protocolo de apoyo", () => SupportProtocol.CheckWho5Score(score));

                DateTime next = Who5Questionnaire.NextAvailableDate(now);
                _view.ShowThanks(score, Who5Questionnaire.CoinReward,
                                 $"Volverá a estar disponible el {next.ToString("d 'de' MMMM", _es)}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Who5Controller] _safeSubmit: {ex.Message}");
                ToastNotification.ShowError("No se pudo guardar el cuestionario, inténtalo de nuevo");
                if (!_submitted)
                {
                    _view.SetSubmitting(false);
                    _view.SetSelectedOption(_answers[_currentItem]);
                }
            }
            finally
            {
                _submitting = false;
            }
        }

        private static async Task _safeStep(string what, Func<Task> step)
        {
            try { await step(); }
            catch (Exception ex) { Debug.LogError($"[Who5Controller] No se pudo {what}: {ex.Message}"); }
        }

        private async Task _grantReward()
        {
            await Repo.AddCoins(Who5Questionnaire.CoinReward);
            var profile = await Repo.GetUserProfile();
            EventBus.EmitCoinsChanged(profile?.Coins ?? 0);

            EventBus.EmitRewardGranted(new RewardGrant
            {
                Source = RewardSource.Who5,
                Coins  = Who5Questionnaire.CoinReward,
                Title  = "Cuestionario de bienestar completado",
                Body   = $"+{Who5Questionnaire.CoinReward} monedas"
            });
        }
    }
}
