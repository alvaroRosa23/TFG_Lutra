# Sistemas — Documentación detallada

## Core/Architecture

### GameManager
Singleton, DontDestroyOnLoad, inicialización async de todos los servicios.
- Inspector: campo `_authManager` (AuthManager), campo `_firestoreManager` (FirestoreManager), array `_allServices` en este orden: `StreakManager, NotificationManager, SettingsManager, ThemeManager, ScreenManager, RewardSystem, EmotionRecommender, MinigameLoader`
- Restaura perfil desde Firestore si no hay uno local (y descarta un perfil local de otra cuenta)
- Llama a `ThemeManager.SetActiveCulture(profile.Culture)` tras confirmar perfil, antes de cualquier `ApplyTheme`
- Lanza `CloudSync.SyncAllAsync`: la espera si el perfil se acaba de restaurar o no hay check-in local hoy (pudo hacerse en otro dispositivo); si no, en segundo plano

### AppStateMachine
Estados, `TransitionTo`, `OnStateChanged(AppState prev, AppState next)`, historial con `Stack<AppState>`, propiedad `PreviousState`.

### ServiceLocator
`Register`, `Get`, `TryGet` (no lanza si no está registrado; para servicios opcionales), `Unregister`, `RegisterByType`, `UnregisterByType`, `Clear`.

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

// Barra de navegación (ocultarla dentro de una pantalla, p. ej. editor del diario)
OnNavBarVisibilityRequested(bool)           EmitNavBarVisibilityRequested(visible)

// Economía
OnCoinsChanged(int)                         EmitCoinsChanged(newTotal)

// Informes
OnChartPeriodChanged(ChartPeriod)           EmitChartPeriodChanged(period)

// Ajustes
OnSettingsChanged(AppSettings)              EmitSettingsChanged(settings)    // ⚠ tiene parámetro

// Recompensas
OnRewardEarned(string itemId, RewardType)   EmitRewardEarned(itemId, type)
OnRewardUnlocked(RewardDefinition)          EmitRewardUnlocked(reward)
OnRewardGranted(RewardGrant)                EmitRewardGranted(grant)         // toda recompensa nueva → centro de notificaciones

// Centro de notificaciones
OnNotificationsChanged(bool needsAttention) EmitNotificationsChanged(value)  // "!" del menú principal

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
Solo lee y escribe documentos; la sincronización la coordina `CloudSync`. Los `Save*` propagan excepciones (CloudSync las registra); los `Get*` devuelven null si falla la lectura.
- `SaveUserProfile(profile, includeCoins=false)` / `GetUserProfile(userId)` — datos del perfil, `avatar` y `preferencesJson` (mascota, recompensa diaria del diario). Las monedas solo con `includeCoins` (al crear la cuenta)
- `GetUserState(userId)` → `RemoteUserState`: lee de una vez monedas, `syncVersion`, preferencias, inventario, vendidos, colocaciones y último check-in
- `SaveLastCheckIn` — `lastCheckInDate`, `lastEmotionType` y `checkInHistory` (ArrayUnion)
- `SaveEmotion` / `GetEmotions` — `emotions/{remoteId}` con todos los campos (menos la foto, que es un archivo local)
- `SaveDiaryEntry` / `GetDiaryEntries` — `diary/{remoteId}` (las entradas antiguas tienen la fecha `yyyy-MM-dd` como id). `GetDiaryEntries` devuelve `RemoteDiary` (entradas + ids borrados)
- `DeleteDiaryEntry` — sustituye el documento por un marcador `{ deleted: true, deletedAt }` sin contenido
- `SaveMinigameSession` / `GetMinigameSessions` — `minigameSessions/{remoteId}` (récords y gráficas)
- `AddInventoryItem` / `RemoveInventoryItem` — `inventoryItems` con ArrayUnion/ArrayRemove; vender apunta el ítem en `inventoryRemoved` y borra su colocación
- `SetInventoryPlacement` — mapa `inventoryPlacements` (índice < 0 borra la clave)
- `IncrementCoins(userId, delta)` — incremento atómico; `SaveCoins(userId, coins)` escribe el total y `syncVersion`
- `SaveStarEntry` / `GetStarCollection` — `stars/{starId}`
- `DeleteUserData` borra el documento y las subcolecciones `diary`, `emotions`, `minigameSessions` y `stars`

### CloudSync (estático)
Sincronización SQLite ↔ Firestore. **Ninguna feature sube nada a mano**:
- **Subidas automáticas**: cada escritura de `DataRepository` llama a `CloudSync.Push*` (fire-and-forget; sin conexión Firestore las encola). `sync: false` solo para guardar datos que vienen de Firestore.
- **Reconciliación** (`SyncAllAsync(repo)`, idempotente) al iniciar sesión (`LoginController`) y al abrir la app (`GameManager`):
  - Diario, emociones, partidas, estrellas, inventario: unión por id estable (`RemoteId` GUID / `starId` / `itemId`). Lo que falta en local se descarga y lo que falta en remoto se sube.
  - Monedas: manda Firestore (cada variación se sube como `Increment`). La primera vez (`syncVersion` < 2) manda el saldo local.
  - Colocaciones y preferencias: mandan las remotas; las locales que no están en remoto se suben.
  - Ítems vendidos en otro dispositivo (`inventoryRemoved`) se quitan también en local.
  - Cuestionarios (WHO-5): unión por `RemoteId` (no se editan).
  - Notificaciones: unión por `RemoteId`; si existe en ambos lados, leída y resuelta ganan. Al terminar actualiza la "!" (`NotificationCenter.RefreshAttention`).
  - Diario: las entradas con marcador de borrado en Firestore se borran en local (y no se vuelven a subir); si una entrada existe en ambos lados con distinto título/contenido/emoción, manda Firestore (edición en otro dispositivo).
  - Check-ins antiguos (solo fecha en `checkInHistory`): placeholders para los días sin registro, que se borran cuando llega el registro real.

### Firebase — estructura de datos

**Proyecto**: Lutra | **Auth**: email/contraseña | **Firestore**: colección `users`

Documento `users/{uid}`:
```
name, surname, email, dateOfBirth (yyyy-MM-dd), culture (int),
hobbiesJson, coins, creationDate (yyyy-MM-dd),
lastCheckInDate (yyyy-MM-dd), lastEmotionType (int),
checkInHistory: ["yyyy-MM-dd", ...]   // array, máx 60 entradas, orden ascendente
inventoryItems: ["itemId", ...]        // ítems comprados / ganados de SafeZone
inventoryPlacements: { itemId: int }   // índice del PlacementPoint de cada ítem colocado
```

Además en el documento: `avatar`, `preferencesJson`, `inventoryRemoved` (ítems vendidos), `syncVersion`.

Subcollección `users/{uid}/emotions/{RemoteId}` (check-ins; los placeholders no se suben):
```
timestamp (ISO 8601), emotionType (int), intensity (int), isMorningCheck (bool), notes,
moodLevel (int 1-5), emotionTags (JSON), motiveTags (JSON), songTitle, songArtist
```

Subcollección `users/{uid}/minigameSessions/{RemoteId}`:
```
startTime (ISO 8601), durationSeconds, minigameId (int), emotionBefore (int), emotionAfter (int),
relaxationScore (0-1), metricsJson, moodBefore (int|null), moodBeforeAt (ISO 8601|null), moodAfter (int|null)
```

Subcollección `users/{uid}/stars/{starId}` (colección de StarFisher):
```
timesCaught (int), firstCaughtAt (ISO 8601), lastCaughtAt (ISO 8601)
```

Subcollección `users/{uid}/diary/{RemoteId}` (GUID; "yyyy-MM-dd" en entradas antiguas):
```
date (string "yyyy-MM-dd"), title, content, mood (nombre enum EmotionType), timestamp (ISO 8601)
```

Subcollección `users/{uid}/notifications/{RemoteId}` (centro de notificaciones; campos en `docs/NOTIFICATION_CENTER.md` §2).

Subcollección `users/{uid}/scaleResponses/{RemoteId}` (WHO-5; campos en `docs/PROFESSIONAL_REPORT.md` §3.6).

Las subcolecciones nuevas necesitan permiso en las reglas. `DeleteUserData` borra `diary`, `emotions`, `minigameSessions`, `stars`, `notifications` y `scaleResponses`.
Entrada borrada: el documento queda solo con `deleted (bool true), deletedAt (ISO 8601)`.
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
Además `ScheduleWho5Reminder(fecha)`: aviso de que el WHO-5 vuelve a estar disponible, a la hora del recordatorio diario.
Son notificaciones **push del sistema operativo**. La bandeja dentro de la app es otro sistema, `NotificationCenter` (planificado, `docs/NOTIFICATION_CENTER.md`).

### NotificationCenter
Bandeja de notificaciones dentro de la app (≠ `NotificationManager`). La crea `GameManager` por código (`AddComponent`) si no está en la escena.
Convierte cada `OnRewardGranted` en una `AppNotification`, gestiona las ancladas (`CreatePinned` / `ResolvePinned`), publica el resumen semanal (`CheckWeeklySummary`) y emite `OnNotificationsChanged`. Detalle en `docs/NOTIFICATION_CENTER.md`.

### Who5Scheduler (estático)
`CheckAvailability()` (idempotente): si toca el WHO-5 crea su notificación anclada `who5-{fecha}` en `NotificationCenter`. `ScheduleNextReminder(completedAt)` programa el push del siguiente. Lo llama `MainMenuScreen.OnScreenFocused`. Calendario en `Who5Questionnaire`.

### ConsentGate (estático)
Consentimiento de datos de salud (`docs/PROFESSIONAL_REPORT.md` §6.2). `ContinueTo(destino)` sustituye a la navegación a MainMenu / EmotionCheck tras el arranque, el login y el onboarding: si el perfil no tiene la versión actual (`CurrentVersion`), pasa antes por `AppState.Consent`. `SaveConsent(diaryAnalysis)`, `SetDiaryAnalysis(bool)`, `HasConsent(profile)`, `IsDiaryAnalysisEnabled(profile)`. Guarda en `UserProfile.Preferences` (`consentVersion`, `consentDate`, `diaryLanguageAnalysis`).

### SupportProtocol / SupportRules (estáticos)
Protocolo de apoyo (`docs/PROFESSIONAL_REPORT.md` §6.3). `SupportRules`: reglas puras (3 días seguidos con ánimo ≤ 2, WHO-5 ≤ 28, espera de 7 días). `SupportProtocol.CheckLowMoodStreak()` (tras un check-in de Día) y `CheckWho5Score(score)` (tras el WHO-5): si toca y no está en espera, `NotificationCenter.AddSupport(disparador)` + `SupportDialog.ShowAlert()`.

### StreakWarningChecker
Escucha EventBus, programa aviso si racha ≥ 3 sin check-in.

### SettingsManager
Preferencias de usuario: notificaciones, apariencia, exportación y borrado de datos.
- `ExportUserData()` → `Task<string>`: serializa todos los datos del usuario (perfil, registros, diario, partidas, cuestionarios, notificaciones) a `temporaryCachePath/lutra_datos.json` y devuelve la ruta; `SettingsController` la comparte con `FileSharer` (portabilidad, RGPD art. 20).

---

## Core/Data/Persistence

### DatabaseManager
`SQLiteAsyncConnection`, `async Task Initialize`.

### DataRepository
Emociones, diario, minijuegos, perfil, rachas, monedas, ítems desbloqueados.
- `GetTodayEmotion()` — registro más reciente del día de hoy
- `GetLastEmotion()` — filtra por `Source == RecordSource.User` primero; fallback al primer registro disponible
- `GetUserEmotionsForPeriod(from, to)` — solo `Source == User`; **la que deben usar estadísticas e informes** (`GetEmotionsForPeriod` incluye placeholders)
- `GetSessionsForPeriod(from, to)` — partidas de todos los minijuegos del rango en una consulta
- `GetDiaryEntriesForPeriod`, `GetLastNotificationOfType`, `GetNotificationsOfTypeForPeriod`, `GetScaleResponsesForPeriod` — consultas por rango para `ReportDataLoader`
- `GetLatestMood(before)` — ánimo más reciente (check-in del usuario o `MoodAfter` de una partida) y su momento; lo usa `MinigameLoader` para `MoodBefore`
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
- `_notificationsButton` → `AppState.Notifications`; `_notificationsBadge` ("!") según `NotificationCenter.NeedsAttention()` y `OnNotificationsChanged`. En `OnScreenFocused` llama también a `CheckWeeklySummary()` y `Who5Scheduler.CheckAvailability()`

### ChartsScreen
Delega en `ChartsController.OpenCharts()`.

---

## UI/Components

| Componente | Descripción |
|---|---|
| `BottomNavBar` | 5 tabs; botón central especial (abre panel Día/Momento en MainMenu, vuelve a MainMenu desde otros estados) |
| `SafeAreaHandler` | Adapta el `RectTransform` asignado al safe area del dispositivo (al cambiar dimensiones). En `Main.unity` está en el padre común de todas las pantallas: las pantallas nuevas no lo llevan |
| `ToastNotification` | Singleton; `ShowError(string)`, `ShowSuccess(string)` |
| `UILineChart` | `MaskableGraphic` que dibuja su malla: serie principal (puntos con color propio, línea cortada en huecos > `maxGap`), serie secundaria y líneas guía. `SetData(points, range, colors, secondary, grid, maxGap)` |
| `StatsBarRow` | Fila de barra horizontal (etiqueta, `Image` Filled, valor); `Setup(label, fraction, value, color)` |
| `FileSharer` | `Share(paths, subject, text)`: menú nativo con NativeShare (`NATIVE_SHARE`); en el editor abre la carpeta |
| `SupportDialog` | Singleton (GameObject siempre activo, hijo `_panel`); `ShowAlert()` (protocolo de apoyo) y `ShowResources()` (Ajustes, notificación de apoyo); botones 024 / 112 con `tel:` |
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
