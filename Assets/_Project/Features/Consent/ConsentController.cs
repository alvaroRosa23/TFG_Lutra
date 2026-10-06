using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Systems;
using Lutra.UI.Components;

namespace Lutra.Features.Consent
{
    /// <summary>
    /// Controlador del consentimiento. Aceptar → guarda versión, fecha y preferencia del análisis
    /// del diario (ConsentGate) y continúa al destino pendiente. No aceptar → cierra sesión: sin
    /// consentimiento no se pueden registrar datos de bienestar.
    /// </summary>
    public class ConsentController : MonoBehaviour
    {
        [SerializeField] private ConsentView _view;

        private bool _saving;

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<ConsentView>();
        }

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnContinueRequested += _onContinueRequested;
            _view.OnDeclineRequested  += _onDeclineRequested;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnContinueRequested -= _onContinueRequested;
            _view.OnDeclineRequested  -= _onDeclineRequested;
        }

        public void OpenConsent()
        {
            _saving = false;
            _view?.Show(ConsentTexts.Title, ConsentTexts.Body,
                        ConsentTexts.RequiredCheck, ConsentTexts.DiaryAnalysisCheck);
        }

        private void _onContinueRequested(bool diaryAnalysis) => _ = _safeAccept(diaryAnalysis);

        private async Task _safeAccept(bool diaryAnalysis)
        {
            if (_saving) return;
            _saving = true;
            _view?.SetBusy(true);

            try
            {
                await ConsentGate.SaveConsent(diaryAnalysis);
                AppStateMachine.Instance.TransitionTo(ConsentGate.PendingTarget);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConsentController] _safeAccept: {ex.Message}");
                ToastNotification.ShowError("No se pudo guardar, inténtalo de nuevo");
                _view?.SetBusy(false);
            }
            finally
            {
                _saving = false;
            }
        }

        private void _onDeclineRequested()
        {
            if (_saving) return;
            ServiceLocator.Get<AuthManager>().Logout();
            ToastNotification.ShowInfo(ConsentTexts.DeclinedToast);
            AppStateMachine.Instance.TransitionTo(AppState.Login);
        }
    }
}
