using System;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Models;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Systems;
using Lutra.UI.Theme;

namespace Lutra.Features.Auth
{
    /// <summary>
    /// Controlador de la pantalla de login.
    /// Orquesta la lógica de autenticación entre LoginView y AuthManager,
    /// y decide el estado de navegación tras un login correcto.
    /// </summary>
    public class LoginController : MonoBehaviour
    {
        // ── Referencias ────────────────────────────────────────────────

        [SerializeField] private LoginView _view;

        // ── Servicios (lazy) ───────────────────────────────────────────

        private AuthManager _authManager;
        private ThemeManager _themeManager;

        private AuthManager  AuthManagerService  => _authManager  ??= ServiceLocator.Get<AuthManager>();
        private ThemeManager ThemeManagerService => _themeManager ??= ServiceLocator.Get<ThemeManager>();

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _view.OnLoginClicked          = () => _ = OnLoginClicked();
            _view.OnRegisterClicked       = () => _ = OnRegisterClicked();
            _view.OnForgotPasswordClicked = () => _ = OnForgotPasswordClicked();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>Inicializa la vista al entrar en la pantalla de login.</summary>
        public async Task OpenLogin()
        {
            _view.ClearError();
            _view.SetLoading(false);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Valida los campos, llama a Firebase y navega al estado correcto según el perfil.
        /// </summary>
        public async Task OnLoginClicked()
        {
            try
            {
                string email    = _view.GetEmail();
                string password = _view.GetPassword();

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    _view.ShowError("Rellena todos los campos");
                    return;
                }

                _view.SetLoading(true);
                var (success, error) = await AuthManagerService.LoginWithEmail(email, password);
                _view.SetLoading(false);

                if (!success)
                {
                    _view.ShowError(error);
                    return;
                }

                await _navigateAfterAuth();
            }
            catch (Exception ex)
            {
                _view.SetLoading(false);
                _view.ShowError("Error inesperado");
                Debug.LogError($"[LoginController] OnLoginClicked: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>Navega a la pantalla de registro.</summary>
        public Task OnRegisterClicked()
        {
            AppStateMachine.Instance.TransitionTo(AppState.Register);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Envía un email de recuperación de contraseña al email introducido en el campo.
        /// </summary>
        public async Task OnForgotPasswordClicked()
        {
            try
            {
                string email = _view.GetEmail();

                if (string.IsNullOrWhiteSpace(email))
                {
                    _view.ShowError("Introduce tu email primero");
                    return;
                }

                _view.SetLoading(true);
                var (success, error) = await AuthManagerService.SendPasswordResetEmail(email);
                _view.SetLoading(false);

                if (success)
                    _view.ShowError("Email de recuperación enviado", isSuccess: true);
                else
                    _view.ShowError(error);
            }
            catch (Exception ex)
            {
                _view.SetLoading(false);
                _view.ShowError("Error inesperado");
                Debug.LogError($"[LoginController] OnForgotPasswordClicked: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ── Restauración de datos ──────────────────────────────────────

        private async Task _restoreInventoryFromFirestore(DataRepository repo, int userId)
        {
            try
            {
                var firestoreManager = ServiceLocator.Get<FirestoreManager>();
                var itemIds = await firestoreManager
                    .GetInventoryItemIds(AuthManagerService.CurrentUserId);

                int restored = 0;
                foreach (var itemId in itemIds)
                {
                    bool already = await repo.IsItemUnlocked(userId, itemId);
                    if (!already)
                    {
                        await repo.UnlockItem(userId, itemId);
                        restored++;
                    }
                }

                if (restored > 0)
                    Debug.Log($"[LoginController] Ítems de inventario restaurados desde Firestore: {restored}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoginController] No se pudo restaurar inventario desde Firestore: {ex.Message}");
            }
        }

        private async Task _restoreDiaryFromFirestore(DataRepository repo)
        {
            try
            {
                var firestoreManager = ServiceLocator.Get<FirestoreManager>();
                var entries = await firestoreManager
                    .GetDiaryEntries(AuthManagerService.CurrentUserId);

                int restored = 0;
                foreach (var entry in entries)
                {
                    var existing = await repo.GetDiaryEntryByDate(entry.Date);
                    if (existing == null)
                    {
                        await repo.SaveDiaryEntry(entry);
                        restored++;
                    }
                }

                if (restored > 0)
                    Debug.Log($"[LoginController] Entradas de diario restauradas desde Firestore: {restored}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoginController] No se pudo restaurar diario desde Firestore: {ex.Message}");
            }
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Tras un login correcto decide el siguiente estado:
        ///   - Sin perfil local → intenta restaurar desde Firestore.
        ///   - Sin perfil en ningún lado → OnboardingProfile.
        ///   - Con perfil y check-in hecho → MainMenu.
        ///   - Con perfil y sin check-in → EmotionCheck.
        /// </summary>
        private async Task _navigateAfterAuth()
        {
            var repo    = ServiceLocator.Get<DataRepository>();
            var profile = await repo.GetUserProfile();

            // Si el perfil local pertenece a otra cuenta, descartar datos locales
            if (profile != null &&
                !string.IsNullOrEmpty(profile.FirebaseUserId) &&
                profile.FirebaseUserId != AuthManagerService.CurrentUserId)
            {
                Debug.Log("[LoginController] Perfil local de otra cuenta → limpiando datos locales.");
                await repo.DeleteAllData();
                profile = null;
            }

            // Sin perfil local, intentar restaurar desde Firestore
            if (profile == null)
            {
                try
                {
                    Debug.Log("[LoginController] Sin perfil local, buscando en Firestore...");
                    var firestoreManager = ServiceLocator.Get<FirestoreManager>();
                    var firestoreProfile = await firestoreManager
                        .GetUserProfile(AuthManagerService.CurrentUserId);

                    if (firestoreProfile != null)
                    {
                        await repo.SaveUserProfile(firestoreProfile);
                        profile = firestoreProfile;
                        Debug.Log("[LoginController] Perfil restaurado desde Firestore.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LoginController] No se pudo consultar Firestore: {ex.Message}");
                }
            }

            if (profile == null)
            {
                Debug.Log("[LoginController] Login correcto, sin perfil → OnboardingProfile.");
                AppStateMachine.Instance.TransitionTo(AppState.OnboardingProfile);
                return;
            }

            // Aplicar paleta cultural del perfil del usuario
            ThemeManagerService.SetActiveCulture(profile.Culture);

            await _restoreDiaryFromFirestore(repo);
            await _restoreInventoryFromFirestore(repo, profile.Id);

            var streakManager   = ServiceLocator.Get<StreakManager>();
            bool checkedInToday = await streakManager.HasCheckedInToday();

            if (!checkedInToday)
            {
                try
                {
                    var lastCheckIn = await ServiceLocator
                        .Get<FirestoreManager>()
                        .GetLastCheckIn(AuthManagerService.CurrentUserId);

                    if (lastCheckIn.date.HasValue &&
                        lastCheckIn.date.Value.Date == DateTime.Today)
                    {
                        checkedInToday = true;
                        Debug.Log("[LoginController] Check-in de hoy restaurado desde Firestore");

                        ThemeManagerService.ApplyTheme(lastCheckIn.emotion);

                        // Crear registro local placeholder si no existe
                        var localRecord = await repo.GetTodayEmotion();
                        if (localRecord == null)
                        {
                            var placeholderRecord = new EmotionRecord
                            {
                                Timestamp      = DateTime.Today.AddHours(0),
                                EmotionType    = lastCheckIn.emotion,
                                IntensityLevel = 3,
                                IsMorningCheck = true,
                                Source         = RecordSource.RestoredFirestore
                            };
                            await repo.SaveEmotion(placeholderRecord);
                            Debug.Log("[LoginController] Registro placeholder creado en SQLite.");
                        }

                        var checkInHistory = await ServiceLocator
                            .Get<FirestoreManager>()
                            .GetCheckInHistory(AuthManagerService.CurrentUserId);

                        foreach (var historyDate in checkInHistory)
                        {
                            if (historyDate.Date == DateTime.Today)
                            {
                                Debug.Log("[LoginController] Saltando hoy en historial — ya tiene placeholder con emoción correcta");
                                continue;
                            }

                            var existing = await repo
                                .GetEmotionsForPeriod(
                                    historyDate,
                                    historyDate.AddDays(1).AddSeconds(-1));

                            if (existing.Count == 0)
                            {
                                var historyRecord = new EmotionRecord
                                {
                                    Timestamp      = historyDate.AddHours(12),
                                    EmotionType    = EmotionType.Calm,
                                    IntensityLevel = 3,
                                    IsMorningCheck = true,
                                    Source         = RecordSource.RestoredFirestoreHistory
                                };
                                await repo.SaveEmotion(historyRecord);
                            }
                        }

                        Debug.Log($"[LoginController] Historial restaurado: {checkInHistory.Count} días desde Firestore");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LoginController] " +
                                     $"No se pudo consultar check-in Firestore: {ex.Message}");
                }
            }

            if (checkedInToday)
            {
                Debug.Log($"[LoginController] {profile.Name} ya hizo check-in → MainMenu.");
                AppStateMachine.Instance.TransitionTo(AppState.MainMenu);
            }
            else
            {
                Debug.Log($"[LoginController] Bienvenido de nuevo, {profile.Name} → EmotionCheck.");
                AppStateMachine.Instance.TransitionTo(AppState.EmotionCheck);
            }
        }
    }
}
