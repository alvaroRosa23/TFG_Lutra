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

// Tema visual
OnThemeColorsChanged(Color, Color)          EmitThemeColorsChanged(p, bg)    // ThemeManager → ThemedGraphic (cada frame de la transición)
```

`ClearAllListeners()` elimina todos los suscriptores. Llamar al reiniciar la app o en tests.

---

## Core/Systems

### AuthManager
Firebase Auth: `RegisterWithEmail`, `LoginWithEmail`, `SendPasswordResetEmail`, `ChangePassword`, `DeleteAccount`, `Logout`, `RefreshCurrentUser`; propiedades `IsLoggedIn`, `CurrentUserId`, `CurrentEmail`.
- Validación de contraseña: mínimo 8 caracteres, una mayúscula, un número
- Mensajes de error traducidos al español en `_getFirebaseErrorMessage`. Con la protección contra enumeración de emails de Firebase, un email o contraseña incorrectos llegan como `AuthError.Failure` con `INVALID_LOGIN_CREDENTIALS` → "Email o contraseña incorrectos" (no se distingue cuál de los dos falla, a propósito)

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
- `SaveNotification` / `GetNotifications` — `notifications/{remoteId}`
- `SaveScaleResponse` / `GetScaleResponses` — `scaleResponses/{remoteId}`
- `DeleteUserData` borra el documento y las subcolecciones `diary`, `emotions`, `minigameSessions`, `stars`, `notifications` y `scaleResponses`

### CloudSync (estático)
Sincronización SQLite ↔ Firestore. **Ninguna feature sube nada a mano**:
- **Subidas automáticas**: cada escritura de `DataRepository` llama a `CloudSync.Push*` (fire-and-forget; sin conexión Firestore las encola). `sync: false` solo para guardar datos que vienen de Firestore.
- **Reconciliación** (`SyncAllAsync(repo)`, idempotente) al iniciar sesión (`LoginController`) y al abrir la app (`GameManager`):
  - Diario, emociones, partidas, estrellas, inventario: unión por id estable (`RemoteId` GUID / `starId` / `itemId`). Lo que falta en local se descarga y lo que falta en remoto se sube.
  - Monedas: manda Firestore (cada variación se sube como `Increment`). La primera vez (`syncVersion` < 2) manda el saldo local.
  - Colocaciones y preferencias: mandan las remotas; las locales que no están en remoto se suben. Los ajustes de la app van en la preferencia `appSettings` (`SettingsManager`).
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

**Reglas de seguridad** (copia en `firestore.rules`): solo el usuario autenticado puede leer/escribir su propio documento y todas sus subcolecciones (`match /users/{userId}/{document=**}`), así que las subcolecciones nuevas no necesitan reglas nuevas.
**SDK instalado**: `FirebaseAuth.unitypackage`, `FirebaseFirestore.unitypackage`. **Sin Analytics**: el paquete se quitó; en Android la librería nativa que arrastran Auth/Firestore está desactivada en `Assets/Plugins/Android/LutraPrivacy.androidlib` (`docs/BUGS.md`). Si se actualiza el SDK de Firebase, no reimportar `FirebaseAnalytics.unitypackage`.

**Logs**: `GameManager._configureLogging` los desactiva en las builds de release (`Debug.isDebugBuild`).

### StreakManager
`GetCurrentStreak`, `GetLongestStreak`, `RegisterCheckIn`, `HasCheckedInToday`.
- `RegisterCheckIn` detecta rotura, emite `OnStreakUpdated` y `OnStreakBroken`, revisa hitos 7/14/30 días → `EmitRewardEarned("streak_reward_{n}d", RewardType.RoomDecoration)`

### RewardSystem
`CheckAndGrantRewards`, evalúa rachas y check-ins contra `RewardDefinition[]`.

### NotificationManager
Recordatorio diario, streak warning, condicionado por `#if` de plataforma.
Además `ScheduleWho5Reminder(fecha)`: aviso de que el WHO-5 vuelve a estar disponible, a la hora del recordatorio diario.
Son notificaciones **push del sistema operativo**. La bandeja dentro de la app es otro sistema, `NotificationCenter` (`docs/NOTIFICATION_CENTER.md`).

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
Preferencias de usuario: notificaciones, apariencia, audio, daltonismo, idioma, exportación y borrado de datos.
- **Ajustes por usuario, sincronizados**: cada cambio se guarda en `PlayerPrefs` (copia del dispositivo) y en `UserProfile.Preferences["appSettings"]` (JSON de `AppSettings`), que viaja a Firestore con el perfil. Al iniciar sesión, `CloudSync` trae las preferencias remotas y `SettingsManager` aplica esos ajustes (filtro de daltonismo, recordatorio diario…) en cualquier dispositivo.
- **Pantallas sin usuario** (Splash, Login, Registro, Onboarding; detectadas con `EventBus.OnScreenChanged`): ajustes por defecto, sin filtro de daltonismo, y se borra la copia del dispositivo (era del usuario que cerró sesión; si no, otra cuenta nueva la heredaría).
- Al pasar a una pantalla con usuario: `_safeLoadUserSettings` carga los del perfil; si el perfil aún no los tiene (usuarios anteriores a esta sincronización), usa los del dispositivo y los sube.
- Limitación: si se cambian en otro dispositivo con la app ya abierta aquí, se aplican en el siguiente inicio de sesión o apertura.
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
`ScriptableRendererFeature` en `Assets/_Project/UI/Theme/ColorblindFeature.cs` (RenderGraph, Unity 6).
- Shader: `Assets/Shaders/Colorblind.shader` (`Lutra/Colorblind`), ya asignado en `Settings/Renderer2D.asset`
- Propiedad estática `CurrentMode` (escribe `SettingsManager` en `Awake` y en `UpdateColorblindMode`); con `None` no se encola el pase
- Usa un RT temporal para no leer y escribir en el mismo render target; `renderPassEvent = AfterRenderingPostProcessing`
- ⚠ **Solo afecta a lo que dibuja la cámara.** Un Canvas en *Screen Space - Overlay* se dibuja después de URP y el filtro no le llega (por eso el modo de daltonismo no se veía). `CanvasCameraBinder` pasa todos los Canvas a *Screen Space - Camera* en ejecución, así que en el editor siguen apareciendo como Overlay.

**Daltonización (corrección), no simulación.** Para cada píxel en RGB lineal (el proyecto usa espacio de color Linear):
1. `sim = Sim · color` — cómo lo percibe la persona (matrices de Machado, Oliveira y Fernandes, 2009, severidad 1.0).
2. `err = color − sim` — la información de color que no percibe.
3. `salida = color + Shift · err` — esa información se lleva a los canales que sí distingue (método de Fidaner, Lin y Ozguven, 2005).

| Modo | Simulación (Machado et al., 2009) | Redistribución del error |
|---|---|---|
| Protanopia | filas (0.152, 1.053, −0.205) · (0.115, 0.786, 0.099) · (−0.004, −0.048, 1.052) | rojo → verde y azul: R (0,0,0) · G (0.7,1,0) · B (0.7,0,1) |
| Deuteranopia | (0.367, 0.861, −0.228) · (0.280, 0.673, 0.047) · (−0.012, 0.043, 0.969) | igual que protanopia |
| Tritanopia | (1.256, −0.077, −0.179) · (−0.078, 0.931, 0.148) · (0.005, 0.691, 0.304) | azul → rojo y verde: R (1,0,0.7) · G (0,1,0.7) · B (0,0,0) |

Antes (hasta 2026-10-09) el shader aplicaba directamente matrices de **simulación** (0.625, 0.375…): mostraban a cualquiera cómo ve una persona daltónica, pero a esa persona le quitaban aún más contraste.

Referencias (verificar antes de la memoria):
- Machado, G. M., Oliveira, M. M., & Fernandes, L. A. F. (2009). A physiologically-based model for simulation of color vision deficiency. *IEEE Transactions on Visualization and Computer Graphics, 15*(6), 1291–1298.
- Fidaner, O., Lin, P., & Ozguven, N. (2005). *Analysis of color blindness*. Stanford University (proyecto de curso; base del algoritmo de daltonización de daltonize.org).

### ThemeManager
`ApplyTheme(emotion, animate=true)`, `GetTheme(EmotionType)`, `GetCurrentTheme()`, `SetActiveCulture(CultureType)`.
- **Colores**: resuelve primario y fondo (tema + paleta cultural) y los interpola durante `transitionDuration` segundos (SmoothStep). En cada frame actualiza `CurrentPrimary` / `CurrentBackground` / `HasAppliedColors`, tiñe el fondo de la cámara y emite `EventBus.OnThemeColorsChanged(primary, background)`. **No tiñe ninguna imagen concreta**: los fondos que cambian de color llevan un `ThemedGraphic` (ver UI/Components)
- Crossfade de audio entre `_audioSourceA` y `_audioSourceB`. Si el segundo falta o es el mismo que el primero, `Awake` crea otro `AudioSource` (antes eran el mismo y el audio se cortaba al terminar cada transición)
- Instancia y destruye `ambientParticlesPrefab` en `transform`
- Cambia sprite de `_mascotImage` al final de la transición
- Eventos: `OnThemeTransition(EmotionTheme, float progress)`, `OnThemeApplied(EmotionTheme)`
- Emite `EventBus.EmitCurrentEmotionChanged` al completar la transición

**Paletas culturales**: Inspector tiene array `_cultureOverrides` (`CultureColorOverride[]`). `SetActiveCulture` selecciona el override activo. `ApplyTheme` llama a `_resolveColors` antes de animar: si hay override con entrada para esa emoción usa sus colores, si no usa los del `EmotionTheme` base. Solo se sobreescriben `primaryColor` y `backgroundColor`; sprite, audio, partículas y duración vienen siempre del `EmotionTheme`. Las 5 paletas (`Core/Data/ScriptableObjects/Culture Overrides/`) tienen los 8 colores. La cultura se elige en el onboarding y no se puede cambiar en Ajustes.

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
| `ToastNotification` | Singleton; `ShowError(string)`, `ShowSuccess(string)`, `ShowInfo(string)` |
| `UILineChart` | `MaskableGraphic` que dibuja su malla: líneas guía, línea principal (cortada en huecos > `maxGap`), serie secundaria encima (cortada en huecos > `secondaryMaxGap`) y por último los puntos (con color propio). `SetData(points, range, colors, secondary, grid, maxGap, secondaryMaxGap)`. Margen interior `_padding` (el eje Y de caritas de `MoodChart` usa el mismo valor) |
| `CanvasCameraBinder` | Estático. `BindScene(scene, mainCamera)` (Main.unity) y `BindSceneAbove(scene, mainCamera, belowScene)` (minijuegos): pasa los Canvas raíz a *Screen Space - Camera* (plano a 1 unidad) **siempre con la cámara de `Main`** y añade su capa a la *Culling Mask* (en Overlay daba igual). Los Canvas de un minijuego suben por encima del Canvas más alto de `Main` conservando su orden relativo. Una cámara propia de minijuego (BreathJump) no se usa para la UI: pinta el mundo en una RenderTexture que muestra una `RawImage` de su Canvas. Escribe un log por Canvas. Lo llaman `GameManager.Start` (en `Awake` la escena aún no cuenta como cargada) y `MinigameLoader.LoadMinigame` |
| `ThemedGraphic` (`UI/Theme/`) | Tiñe su `Graphic` con el color del tema (incluida la paleta cultural) siguiendo la transición. `_slot` (Primary / Background), `_strength` (0 = blanco … 1 = color completo), `_alpha`. Dos usos: **capa teñible** del arte de fondo (diseño entrega esa capa en blanco o tonos claros; strength 1, alpha 1) o **velo** (Image lisa sobre el arte, debajo del contenido, sin Raycast Target; alpha 0,15-0,3). Al activarse aplica el color actual (`ThemeManager.CurrentPrimary`) |
| `HeatmapCell` | Celda del calendario y muestra de leyenda: color, marca X de "sin registro" y texto opcional; `Setup(color, noRecord, label)` |
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
