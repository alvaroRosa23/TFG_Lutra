# Sistemas — Documentación detallada

## Core/Architecture

### GameManager
Singleton, DontDestroyOnLoad, inicialización async de todos los servicios.
- Inspector: campo `_authManager` (AuthManager), campo `_firestoreManager` (FirestoreManager), array `_allServices` en este orden: `StreakManager, NotificationManager, SettingsManager, ThemeManager, ScreenManager, RewardSystem, EmotionRecommender, MinigameLoader`
- Restaura perfil desde Firestore si no hay uno local
- Llama a `ThemeManager.SetActiveCulture(profile.Culture)` tras confirmar perfil, antes de cualquier `ApplyTheme`
- Restaura check-in de hoy desde Firestore (crea placeholder en SQLite) y aplica el tema
- Restaura historial de check-ins como placeholders en SQLite (tipo `restored_from_firestore_history`)

### AppStateMachine
Estados, `TransitionTo`, `OnStateChanged(AppState prev, AppState next)`, historial con `Stack<AppState>`, propiedad `PreviousState`.

### ServiceLocator
`Register`, `Get`, `Unregister`, `RegisterByType`, `UnregisterByType`, `Clear`.

### BaseService
Clase base de servicios; auto-registra/desregistra en ServiceLocator.

### EventBus — eventos disponibles

Todos los eventos tienen un método de emisión estático `Emit*`. Suscribir siempre en `OnEnable`, desuscribir en `OnDisable`.

Cada `Emit*` itera los suscriptores individualmente con try/catch: una excepción en un suscriptor no cancela los demás.

```csharp
// Emociones
OnEmotionRegistered(EmotionRecord)          EmitEmotionRegistered(record)
OnCurrentEmotionChanged(EmotionType)        EmitCurrentEmotionChanged(emotion)

// Racha
OnStreakUpdated(int days)                   EmitStreakUpdated(days)
OnStreakBroken()                            EmitStreakBroken()

// Minijuegos
OnMinigameStarted(MinigameType)             EmitMinigameStarted(type)
OnMinigameCompleted(MinigameSession)        EmitMinigameCompleted(session)   // ⚠ tipo MinigameSession, no MinigameResult

// Navegación
OnScreenChanged(AppState)                   EmitScreenChanged(state)

// Diario
OnDiaryEntrySaved(DiaryEntry)               EmitDiaryEntrySaved(entry)

// Economía
OnCoinsChanged(int)                         EmitCoinsChanged(newTotal)

// Informes
OnChartPeriodChanged(ChartPeriod)           EmitChartPeriodChanged(period)

// Ajustes
OnSettingsChanged(AppSettings)              EmitSettingsChanged(settings)    // ⚠ tiene parámetro

// Recompensas
OnRewardEarned(string itemId, RewardType)   EmitRewardEarned(itemId, type)
OnRewardUnlocked(RewardDefinition)          EmitRewardUnlocked(reward)

// Modal de emoción
OnEmotionModalRequested()                   EmitEmotionModalRequested()      // BottomNavBar → MainMenuScreen abre panel Día/Momento
```

`ClearAllListeners()` elimina todos los suscriptores. Llamar al reiniciar la app o en tests.

---

## Core/Systems

### AuthManager
Firebase Auth: `RegisterWithEmail`, `LoginWithEmail`, `SendPasswordResetEmail`, `Logout`, `RefreshCurrentUser`; propiedades `IsLoggedIn`, `CurrentUserId`, `CurrentEmail`.
- Validación de contraseña: mínimo 8 caracteres, una mayúscula, un número
- Mensajes de error traducidos al español en `_getFirebaseErrorMessage`

### FirestoreManager
Backup remoto:
- `SaveUserProfile(UserProfile)`, `GetUserProfile(userId)` — documento principal del usuario
- `SaveLastCheckIn(userId, date, EmotionType)` — actualiza `lastCheckInDate`, `lastEmotionType`, `checkInHistory`
- `GetLastCheckIn(userId)` → `(DateTime? date, EmotionType emotion)`
- `GetCheckInHistory(userId)` → `List<DateTime>` — historial de hasta 60 días
- `SaveDiaryEntry(userId, DiaryEntry)` — escribe/sobreescribe subcollección `diary/{yyyy-MM-dd}`
- `GetDiaryEntries(userId)` → `List<DiaryEntry>` — recupera todas las entradas del diario

### Firebase — estructura de datos

**Proyecto**: Lutra | **Auth**: email/contraseña | **Firestore**: colección `users`

Documento `users/{uid}`:
```
name, surname, email, dateOfBirth (yyyy-MM-dd), culture (int),
hobbiesJson, coins, creationDate (yyyy-MM-dd),
lastCheckInDate (yyyy-MM-dd), lastEmotionType (int),
checkInHistory: ["yyyy-MM-dd", ...]   // array, máx 60 entradas, orden ascendente
```

Subcollección `users/{uid}/diary/{yyyy-MM-dd}`:
```
date (string "yyyy-MM-dd"), title, content, mood (nombre enum EmotionType), timestamp (ISO 8601)
```
AudioPath e ImagePath **no** se sincronizan (rutas locales del dispositivo).

**Reglas de seguridad**: solo el usuario autenticado puede leer/escribir su propio documento y subcollecciones.
**SDK instalado**: `FirebaseAuth.unitypackage`, `FirebaseAnalytics.unitypackage`, `FirebaseFirestore.unitypackage`.

### StreakManager
`GetCurrentStreak`, `GetLongestStreak`, `RegisterCheckIn`, `HasCheckedInToday`.
- `RegisterCheckIn` detecta rotura, emite `OnStreakUpdated` y `OnStreakBroken`, revisa hitos 7/14/30 días → `EmitRewardEarned("streak_reward_{n}d", RewardType.RoomDecoration)`

### RewardSystem
`CheckAndGrantRewards`, evalúa rachas y check-ins contra `RewardDefinition[]`.

### NotificationManager
Recordatorio diario, streak warning, condicionado por `#if` de plataforma.

### StreakWarningChecker
Escucha EventBus, programa aviso si racha ≥ 3 sin check-in.

### SettingsManager
Preferencias de usuario: notificaciones, apariencia, exportación y borrado de datos.

---

## Core/Data/Persistence

### DatabaseManager
`SQLiteAsyncConnection`, `async Task Initialize`.

### DataRepository
Emociones, diario, minijuegos, perfil, rachas, monedas, ítems desbloqueados.
- `GetTodayEmotion()` — registro más reciente del día de hoy
- `GetLastEmotion()` — filtra por `Source == RecordSource.User` primero; fallback al primer registro disponible
- `UpdateEmotion(EmotionRecord)` — actualiza un registro existente en SQLite
- `AddCoins(int)` — UPDATE atómico en SQL (`Coins = Coins + ?`) sin ciclo read-modify-write
- `SpendCoins(int)` — lee perfil para comprobar saldo; el UPDATE final es condicional (`AND Coins >= ?`)
- `GetCurrentStreak()` / `GetLongestStreak()` / `GetAllEmotionDates()` — usan helper `_getDistinctDates()` compartido; deduplicación en memoria con LINQ (sqlite-net-pcl almacena DateTime como ticks, incompatibles con `date()` de SQLite)
- `GetEmotionDatesDebug()` — método de diagnóstico temporal (no eliminar hasta build final)

---

## UI/Theme

### ColorblindFeature
`ScriptableRendererFeature` en `Assets/_Project/UI/Theme/ColorblindFeature.cs`.
- Shader: `Assets/Shaders/Colorblind.shader` (URP, usa `Blit.hlsl`)
- Propiedad estática `CurrentMode` (escribe `SettingsManager` en `Awake` y en `UpdateColorblindMode`)
- Aplica una matriz 3×3 de corrección de color via producto escalar en el fragment shader
- Usa un RT temporal para evitar leer y escribir en el mismo render target
- `renderPassEvent = AfterRenderingPostProcessing`
- **Requiere** estar añadido manualmente al `Renderer2D.asset` en el Inspector de Unity:
  Project → Settings/Renderer2D → Add Renderer Feature → Colorblind Feature → asignar shader `Lutra/Colorblind`

Modos y matrices de corrección (daltonización):
| Modo | RowR | RowG | RowB |
|---|---|---|---|
| Deuteranopia | (0.625, 0.375, 0) | (0.700, 0.300, 0) | (0, 0.300, 0.700) |
| Protanopia   | (0.567, 0.433, 0) | (0.558, 0.442, 0) | (0, 0.242, 0.758) |
| Tritanopia   | (0.950, 0.050, 0) | (0, 0.433, 0.567) | (0, 0.475, 0.525) |

### ThemeManager
`ApplyTheme(emotion, animate=true)`, `GetTheme(EmotionType)`, `GetCurrentTheme()`, `SetActiveCulture(CultureType)`.
- Crossfade de color de cámara y `_backgroundImage` durante `transitionDuration` segundos (SmoothStep)
- Crossfade de audio entre `_audioSourceA` y `_audioSourceB`
- Instancia y destruye `ambientParticlesPrefab` en `transform`
- Cambia sprite de `_mascotImage` al final de la transición
- Eventos: `OnThemeTransition(EmotionTheme, float progress)`, `OnThemeApplied(EmotionTheme)`
- Emite `EventBus.EmitCurrentEmotionChanged` al completar la transición

**Paletas culturales**: Inspector tiene array `_cultureOverrides` (`CultureColorOverride[]`). `SetActiveCulture` selecciona el override activo. `ApplyTheme` llama a `_resolveColors` antes de animar: si hay override con entrada para esa emoción usa sus colores, si no usa los del `EmotionTheme` base. Solo se sobreescriben `primaryColor` y `backgroundColor`; sprite, audio, partículas y duración vienen siempre del `EmotionTheme`.

`SetActiveCulture` debe llamarse en los tres puntos de entrada con sesión activa:
1. `GameManager.StartApp`
2. `LoginController._navigateAfterAuth`
3. `OnboardingProfileController._finishOnboarding`

---

## UI/Screens

### UIScreen
Base abstracta con `FadeCanvasGroup`, `Show/Hide`, `OnScreenFocused/Unfocused`.
- `Show()` fuerza `_canvasGroup.alpha = 1f` antes de marcar `IsVisible = true` — necesario porque `PlayExitAnimation()` deja el alpha en 0 y Unity no lo resetea al reactivar el GameObject.

### ScreenManager
`NavigateTo`, `NavigateBack`, historial de pantallas.

### MainMenuScreen
Saludo, barra de racha semanal (lunes-domingo), panel Día/Momento animado.
- Panel bottom-sheet: Y=-220 → 0 en 0.25s SmoothStep (abrir), 0 → -220 en 0.2s (cerrar)
- `OnScreenFocused` aplica tema usando `GetLastEmotion()` (no solo la de hoy); default `EmotionType.Calm`
- Guard `_lastAppliedEmotion` para evitar bucle al recibir `OnCurrentEmotionChanged`
- `_logoutButton` — botón de debug (limpiar SQLite + logout + `PlayerPrefs.DeleteAll()`)

### ChartsScreen
Delega en `ChartsController.OpenCharts()`.

---

## UI/Components

| Componente | Descripción |
|---|---|
| `BottomNavBar` | 5 tabs; botón central especial (abre panel Día/Momento en MainMenu, vuelve a MainMenu desde otros estados) |
| `SafeAreaHandler` | Adapta `RectTransform` al safe area del dispositivo en cada frame |
| `ToastNotification` | Singleton; `ShowError(string)`, `ShowSuccess(string)` |
| `ToggleColorChanger` | Cambia color de fondo de un Toggle según su estado |
| `GreetingAnimator` | Fade in del texto de saludo sobre el color del label |
| `StreakIconController` | Icono de día con estados: completado/hoy-pendiente (pulso naranja)/inactivo |
| `StreakIconPrefabSetup` | `SetState(bool completed, bool todayPending)` y `SetMissed()` |

---

## Prefabs creados

| Prefab | Componentes clave | Uso |
|---|---|---|
| `EmotionTagPrefab` | `Button` + `EmotionTagButton` + `LayoutElement` | Tags de emoción y motivo en `EmotionCheckView` |
| `StreakIconPrefab` | `Image` + `TextMeshProUGUI` + `StreakIconPrefabSetup` | Iconos de días en la barra de racha de `MainMenuScreen` |
| `DiaryEntryCardPrefab` | `Button` + `DiaryEntryCard` + TMP labels + `Image` (punto de color) | Tarjetas de entrada en `DiaryView` |
| `MinigameCardPrefab` | `Button` + `MinigameCard` + `Image` (logo) + TMP labels + tags container | Cards en `MinigamesView` (sección todos y recomendados) |
