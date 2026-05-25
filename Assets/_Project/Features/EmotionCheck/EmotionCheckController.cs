using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Events;
using Lutra.Core.Systems;

namespace Lutra.Features.EmotionCheck
{
    /// <summary>
    /// Controlador del flujo de registro emocional.
    /// Coordina EmotionCheckView, DataRepository y StreakManager.
    /// </summary>
    public class EmotionCheckController : MonoBehaviour
    {
        [SerializeField] private EmotionCheckView _view;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private DataRepository _dataRepository;
        private AuthManager    _authManager;
        private StreakManager  _streakManager;

        private DataRepository DataRepo  => _dataRepository ??= ServiceLocator.Get<DataRepository>();
        private AuthManager    Auth      => _authManager    ??= ServiceLocator.Get<AuthManager>();
        private StreakManager  StreakMgr => _streakManager  ??= ServiceLocator.Get<StreakManager>();

        // ── Estado ─────────────────────────────────────────────────────

        private EmotionRecord _currentRecord;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (_view == null)
                _view = GetComponentInChildren<EmotionCheckView>();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Abre el check-in en modo "inicio del día".</summary>
        public async Task OpenDayCheck()
        {
            Debug.Log($"[EmotionCheckController] Open called. View: {_view != null}");
            _currentRecord  = new EmotionRecord
            {
                IsMorningCheck = true,
                Timestamp      = DateTime.Now
            };
            _view.SetTitle("¿Qué tal el día?");
            _view.SetDate(DateTime.Now);
            _view.ResetAll();
            var hobbies = await _getHobbiesFromProfile();
            _view.LoadUserHobbies(hobbies);
            _view.LoadEmotionTags();
            await Task.Yield();
            _view.ForceLayoutRefresh();
        }

        /// <summary>Abre el check-in en modo "momento actual".</summary>
        public async Task OpenMomentCheck()
        {
            Debug.Log($"[EmotionCheckController] Open called. View: {_view != null}");
            _currentRecord  = new EmotionRecord
            {
                IsMorningCheck = false,
                Timestamp      = DateTime.Now
            };
            _view.SetTitle("¿Cómo te sientes ahora?");
            _view.SetDate(DateTime.Now);
            _view.ResetAll();
            var hobbies = await _getHobbiesFromProfile();
            _view.LoadUserHobbies(hobbies);
            _view.LoadEmotionTags();
            await Task.Yield();
            _view.ForceLayoutRefresh();
        }

        /// <summary>
        /// Valida, persiste el registro y navega a MainMenu.
        /// Llamado desde el botón confirmar de EmotionCheckView.
        /// </summary>
        public async Task OnConfirmClicked()
        {
            try
            {
                if (_currentRecord == null) return;

                if (_view.GetSelectedMoodLevel() == 0)
                {
                    _view.ShowError("Selecciona cómo te sientes (las caritas)");
                    return;
                }

                if (!_view.IsEmotionSelected())
                {
                    _view.ShowError("Selecciona una emoción");
                    return;
                }

                _currentRecord.MoodLevel           = _view.GetSelectedMoodLevel();
                _currentRecord.EmotionType         = _view.GetSelectedEmotion();
                _currentRecord.IntensityLevel      = _currentRecord.MoodLevel;
                _currentRecord.SelectedEmotionTags = JsonConvert.SerializeObject(_view.GetSelectedEmotionTags());
                _currentRecord.SelectedMotiveTags  = JsonConvert.SerializeObject(_view.GetSelectedMotiveTags());
                _currentRecord.PhotoPath           = _view.GetPhotoPath();

                if (_currentRecord.IsMorningCheck)
                {
                    var existingDayRecord = await DataRepo.GetTodayEmotion();
                    if (existingDayRecord != null)
                    {
                        // Edición: actualizar sin volver a contabilizar racha
                        existingDayRecord.EmotionType         = _currentRecord.EmotionType;
                        existingDayRecord.IntensityLevel      = _currentRecord.IntensityLevel;
                        existingDayRecord.MoodLevel           = _currentRecord.MoodLevel;
                        existingDayRecord.SelectedEmotionTags = _currentRecord.SelectedEmotionTags;
                        existingDayRecord.SelectedMotiveTags  = _currentRecord.SelectedMotiveTags;
                        existingDayRecord.Timestamp           = DateTime.Now;
                        await DataRepo.UpdateEmotion(existingDayRecord);
                        _currentRecord = existingDayRecord;
                    }
                    else
                    {
                        // Nuevo check de día: RegisterCheckIn guarda Y actualiza racha
                        Debug.Log($"[EmotionCheckController] GetSelectedEmotion: " +
                                  $"{_view.GetSelectedEmotion()}, " +
                                  $"IsEmotionSelected: {_view.IsEmotionSelected()}");
                        Debug.Log($"[EmotionCheckController] _currentRecord.EmotionType antes de save: " +
                                  $"{_currentRecord.EmotionType}");
                        await StreakMgr.RegisterCheckIn(_currentRecord);
                    }
                }
                else
                {
                    // Momento: solo guardar, sin tocar racha
                    await DataRepo.SaveEmotion(_currentRecord);
                }

                try
                {
                    await ServiceLocator.Get<FirestoreManager>()
                        .SaveLastCheckIn(
                            ServiceLocator.Get<AuthManager>().CurrentUserId,
                            DateTime.Today,
                            _currentRecord.EmotionType);
                    Debug.Log("[EmotionCheckController] Check-in guardado en Firestore");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EmotionCheckController] " +
                                     $"No se pudo guardar check-in en Firestore: {ex.Message}");
                }

                EventBus.EmitEmotionRegistered(_currentRecord);

                AppStateMachine.Instance.TransitionTo(AppState.MainMenu);

                // Emitir después de la transición para que MainMenuScreen ya esté activo y suscrito
                EventBus.EmitCurrentEmotionChanged(_currentRecord.EmotionType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EmotionCheckController] OnConfirmClicked: {ex.Message}\n{ex.StackTrace}");
                _view.ShowError("Algo fue mal, inténtalo de nuevo");
            }
        }

        /// <summary>Cancela el flujo y vuelve a MainMenu.</summary>
        public void OnBackClicked()
        {
            AppStateMachine.Instance.TransitionTo(AppState.MainMenu);
        }

        // ── Métodos privados ───────────────────────────────────────────

        private async Task<List<HobbyType>> _getHobbiesFromProfile()
        {
            var profile = await DataRepo.GetUserProfile();
            if (profile == null) return new List<HobbyType>();
            return profile.Hobbies;
        }
    }
}
