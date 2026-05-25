using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Data.Persistence;
using Lutra.Core.Systems;
using Lutra.Core.Data.Models;
using Lutra.UI.Theme;

namespace Lutra.Core.Architecture
{
    /// <summary>
    /// Punto de entrada de la aplicación. Registra todos los servicios en el ServiceLocator
    /// en un orden determinista, espera la inicialización async de la base de datos
    /// y lanza el flujo de navegación.
    ///
    /// Debe existir exactamente un GameManager en la escena de arranque.
    /// AppStateMachine debe estar en el mismo GameObject o asignada en Inspector.
    ///
    /// Rellenar _allServices en el Inspector arrastrando los BaseService en orden:
    ///   StreakManager, NotificationManager, SettingsManager, ThemeManager,
    ///   ScreenManager, RewardSystem, EmotionRecommender, MinigameLoader.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────

        public static GameManager Instance { get; private set; }

        // ── Referencias ────────────────────────────────────────────────

        [Header("Arquitectura")]
        [Tooltip("AppStateMachine del mismo GameObject o asignada en Inspector.")]
        [SerializeField] private AppStateMachine _appStateMachine;

        [Header("Autenticación")]
        [Tooltip("AuthManager del mismo GameObject. Se inicializa antes que la BD.")]
        [SerializeField] private AuthManager _authManager;

        [Tooltip("FirestoreManager del mismo GameObject. Se inicializa tras Firebase Auth.")]
        [SerializeField] private FirestoreManager _firestoreManager;

        [Header("Servicios MonoBehaviour (en orden de dependencia)")]
        [Tooltip("Arrastrar en orden: StreakManager, NotificationManager, SettingsManager, " +
                 "ThemeManager, ScreenManager, RewardSystem, EmotionRecommender, MinigameLoader.")]
        [SerializeField] private BaseService[] _allServices;

        // ── Servicios no-MonoBehaviour (instanciados en código) ────────

        private DatabaseManager _databaseManager;
        private DataRepository  _dataRepository;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_appStateMachine == null)
                _appStateMachine = GetComponent<AppStateMachine>();
        }

        private async void Start()
        {
            try
            {
                await _registerServices();
                await StartApp();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Error crítico en inicialización: {e.Message}\n{e.StackTrace}");
            }
        }

        private void OnDestroy()
        {
            _databaseManager?.Close();
            ServiceLocator.Clear();
        }

        // ── API pública ────────────────────────────────────────────────

        /// <summary>
        /// Decide el flujo inicial:
        /// - Sin perfil guardado → Onboarding (primera ejecución).
        /// - Con perfil y check-in ya hecho hoy → MainMenu directamente.
        /// - Con perfil y sin check-in hoy → EmotionCheck.
        /// </summary>
        public async Task StartApp()
        {
            var profile = await _dataRepository.GetUserProfile();

            // 1. Sin sesión de Firebase → pantalla de login/registro
            if (!_authManager.IsLoggedIn)
            {
                Debug.Log("[GameManager] Sin sesión activa → Login.");
                _appStateMachine.TransitionTo(AppState.Login);
                return;
            }

            // 2. Verificar que la cuenta sigue siendo válida en Firebase
            try
            {
                await _authManager.RefreshCurrentUser();
            }
            catch
            {
                Debug.Log("[GameManager] Token inválido → limpiando sesión → Login.");
                _authManager.Logout();
                _appStateMachine.TransitionTo(AppState.Login);
                return;
            }

            // 3. Sin perfil local: intentar restaurar desde Firestore
            if (profile == null)
            {
                try
                {
                    UserProfile firestoreProfile = await _firestoreManager
                        .GetUserProfile(_authManager.CurrentUserId);

                    if (firestoreProfile != null)
                    {
                        await _dataRepository.SaveUserProfile(firestoreProfile);
                        profile = firestoreProfile;
                        Debug.Log("[GameManager] Perfil restaurado desde Firestore.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GameManager] No se pudo consultar Firestore: {ex.Message}");
                }
            }

            // 4. Sigue sin perfil local → completar onboarding
            if (profile == null)
            {
                Debug.Log("[GameManager] Sin perfil → OnboardingProfile.");
                _appStateMachine.TransitionTo(AppState.OnboardingProfile);
                return;
            }

            // 5. Sesión y perfil listos → activar paleta cultural y comprobar check-in del día
            ServiceLocator.Get<ThemeManager>().SetActiveCulture(profile.Culture);
            var streakManager   = ServiceLocator.Get<StreakManager>();
            bool checkedInToday = await streakManager.HasCheckedInToday();

            if (!checkedInToday)
            {
                try
                {
                    var lastCheckIn = await _firestoreManager
                        .GetLastCheckIn(_authManager.CurrentUserId);

                    if (lastCheckIn.date.HasValue &&
                        lastCheckIn.date.Value.Date == System.DateTime.Today)
                    {
                        checkedInToday = true;
                        Debug.Log("[GameManager] Check-in de hoy restaurado desde Firestore");

                        ServiceLocator.Get<ThemeManager>().ApplyTheme(lastCheckIn.emotion);

                        // Crear registro local placeholder si no existe
                        var localRecord = await _dataRepository.GetTodayEmotion();
                        if (localRecord == null)
                        {
                            var placeholderRecord = new EmotionRecord
                            {
                                Timestamp      = System.DateTime.Today.AddHours(0),
                                EmotionType    = lastCheckIn.emotion,
                                IntensityLevel = 3,
                                IsMorningCheck = true,
                                Source         = RecordSource.RestoredFirestore
                            };
                            await _dataRepository.SaveEmotion(placeholderRecord);
                            Debug.Log("[GameManager] Registro placeholder creado en SQLite.");
                        }

                        var checkInHistory = await _firestoreManager
                            .GetCheckInHistory(_authManager.CurrentUserId);

                        foreach (var historyDate in checkInHistory)
                        {
                            if (historyDate.Date == System.DateTime.Today)
                            {
                                Debug.Log("[GameManager] Saltando hoy en historial — ya tiene placeholder con emoción correcta");
                                continue;
                            }

                            var existing = await _dataRepository
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
                                await _dataRepository.SaveEmotion(historyRecord);
                            }
                        }

                        Debug.Log($"[GameManager] Historial restaurado: {checkInHistory.Count} días desde Firestore");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GameManager] No se pudo consultar check-in Firestore: {ex.Message}");
                }
            }

            if (checkedInToday)
                _appStateMachine.TransitionTo(AppState.MainMenu);
            else
                _appStateMachine.TransitionTo(AppState.EmotionCheck);
        }

        // ── Métodos privados ───────────────────────────────────────────

        /// <summary>
        /// Registra e inicializa todos los servicios en orden determinista.
        /// Los no-MonoBehaviour (BD y repositorio) se crean primero;
        /// los MonoBehaviour se auto-registran llamando a RegisterSelf().
        /// </summary>
        private async Task _registerServices()
        {
            // 1. BD: crear, registrar e inicializar (abre conexión y crea tablas)
            _databaseManager = new DatabaseManager();
            ServiceLocator.Register(_databaseManager);
            await _databaseManager.Initialize();

            // 2. Repositorio: crear, registrar y resolver la conexión ya abierta
            _dataRepository = new DataRepository();
            ServiceLocator.Register(_dataRepository);
            _dataRepository.Initialize();

            // 3+. Servicios MonoBehaviour en el orden definido en el Inspector
            //     AuthManager debe estar en este array (primer elemento recomendado)
            if (_allServices != null)
                foreach (var service in _allServices)
                    if (service != null) service.RegisterSelf();

            // 4. Inicializar Firebase Auth (requiere que AuthManager ya esté registrado)
            bool firebaseReady = await _authManager.InitializeAsync();
            if (!firebaseReady)
                Debug.LogError("[GameManager] Firebase no disponible");

            // 5. Inicializar Firestore (requiere Firebase Auth inicializado)
            if (_firestoreManager != null)
            {
                ServiceLocator.Register(_firestoreManager);
                await _firestoreManager.InitializeAsync();
            }
            else
            {
                Debug.LogWarning("[GameManager] FirestoreManager no asignado en el Inspector.");
            }

            Debug.Log("[GameManager] Servicios registrados en ServiceLocator.");
        }
    }
}
