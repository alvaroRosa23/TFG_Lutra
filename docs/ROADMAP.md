# Hoja de ruta — Estadísticas, informe profesional y centro de notificaciones

Plan de implementación por fases y registro de decisiones.
Especificaciones: `docs/METRICS.md` (datos y métricas), `docs/PROFESSIONAL_REPORT.md` (Estadísticas, WHO-5, informe, seguridad), `docs/NOTIFICATION_CENTER.md`.

Marca cada tarea al terminarla y actualiza el doc de referencia correspondiente (`SYSTEMS.md`, `FEATURES.md`, `DATA_MODELS.md`) para que describa el código real.

---

## Mejoras del gráfico "Tu ánimo" (acordado 2026-10-08, hecho 2026-10-09)

Contexto: sin ejes no se entendía qué es arriba/abajo ni qué día es cada punto, había colores de emoción casi iguales y la tendencia era lo que menos se veía.

- [x] **Eje Y con caritas** (Unity): `MoodChartRow` (`HorizontalLayoutGroup`) con `MoodAxisY` (`VerticalLayoutGroup`, padding arriba/abajo = `_padding` del `UILineChart`, 14) y `MoodChart`. El rango Y es 0,5–5,5, así que cada carita queda centrada en su línea guía.
- [x] **Eje X con fechas**: `StatsTextBuilder.AxisDate` ("9 sep", meses propios) y "Hoy" en `_moodChartStartLabel` / `_moodChartEndLabel` (ocultos sin datos). Fila `MoodAxisX` con padding izquierdo = columna Y + spacing + `_padding`.
- [x] **Colores de las líneas**: diaria `#BDBDBD` grosor 2; media de 7 días `#555555` grosor 4, dibujada **encima** de la diaria (`UILineChart`).
- [ ] **Paleta de emociones**: Agobio ≈ Energía (rosa/salmón) y Ansiedad ≈ Nostalgia (morados) no se distinguen. Propuesta: Ansiedad `#6A5ACD`, Agobio `#9E3B6E`, Nostalgia `#B08A5B`, Energía `#E040A0` (resto igual). **Decisión del autor**: `EmotionTheme.primaryColor` también lo usa `ThemeManager` para teñir la app, y `DiaryView._getEmotionColor` tiene los mismos colores copiados (habría que cambiarlos también o leerlos de `EmotionTheme`).
- [x] **Sin X en el gráfico**: leyenda del gráfico en `_moodChartLegendContainer` (8 emociones, sin "Sin registro"; `_legendContainers` queda para el calendario) y texto explicativo `_moodChartCaptionLabel` (`StatsTextBuilder.MoodChartCaption`).

## Fase 1 · Datos y captura

- [x] `DataRepository.GetUserEmotionsForPeriod(from, to)`: solo `Source == RecordSource.User` (para estadísticas e informe)
- [x] `DataRepository.GetSessionsForPeriod(from, to)`: una consulta en vez de 8
- [x] `MinigameSession`: `MoodBefore` (int?), `MoodBeforeRecordedAt` (DateTime?), `MoodAfter` (int?) + `FirestoreManager` Save/Get
- [x] `MinigameLoader`: rellenar `MoodBefore` y `MoodBeforeRecordedAt` con `DataRepository.GetLatestMood` (último check-in o `MoodAfter` de una partida anterior; no se pregunta al usuario)
- [x] `PostMinigameScreen`: fila de 5 caritas → guarda `MoodAfter` con `UpdateMinigameSession`. **Los botones de emoción se mantienen**
- [x] Nombres en español: extensiones `ToDisplayName()` en `EmotionType`, `MinigameType` y `HobbyType`; eliminados los `switch` de `ChartsController` y `EmotionCheckView`; `ChartsView` y `MinigameLoader` ya las usan
- [x] `ChartsController` usa `GetUserEmotionsForPeriod` y `GetSessionsForPeriod`
- [x] `BreathJumpMetricsTracker`: `breaths_per_minute`, `exhale_inhale_ratio`, `breath_cv` (acumular suma de cuadrados de la duración de cada ciclo)
- [x] `IntensityLevel`: documentado como copia de `MoodLevel`; excluido de métricas (sin cambios en BD)

## Fase 2 · Centro de notificaciones (código hecho)

- [x] Modelos: `AppNotification`, `NotificationType`, `RewardSource`, `RewardGrant`; tabla `Notifications` en `DatabaseManager`
- [x] `DataRepository` (sección NOTIFICACIONES) + `FirestoreManager` Save/Get + `CloudSync.PushNotification` + `_syncNotifications` (leída y resuelta ganan)
- [x] `EventBus`: `OnRewardGranted`, `OnNotificationsChanged`
- [x] `ServiceLocator.TryGet<T>`
- [x] Servicio `NotificationCenter` (lo crea `GameManager` por código)
- [x] Resumen semanal de los lunes: `WeeklySummaryBuilder`
- [x] Emitir `RewardGrant` en: `StreakManager`, `RewardSystem`, `DiaryController`, `MinigameLoader`, `StarCollectionStore`
- [x] `AppState.Notifications` + `NotificationsScreen` / `Controller` / `View` / `NotificationCard` / `NotificationTimeFormatter`; `BottomNavBar` se oculta en esa pantalla
- [x] `MainMenuScreen`: botón + badge "!"
- [x] `DeleteAllData` y `DeleteUserData` incluyen las notificaciones
- [x] Ensamblado de tests `Tests/EditMode` (primeros tests: regla de partida válida, resumen semanal, fechas del centro)

## Arreglos pendientes

- [ ] **Hitos de racha sin decoración** (`BUGS.md`): crear los `SafeZoneItem` `streak_reward_7d`, `streak_reward_14d` y `streak_reward_30d` y desbloquearlos con `DataRepository.UnlockItem` en `StreakManager` (añadiendo `ItemId` y el texto "y una decoración nueva para tu Zona Segura" a su `RewardGrant`), o quitar la emisión de `EmitRewardEarned`. Requiere decidir las decoraciones (arte) antes de programarlo

## Fase 3 · WHO-5 (código hecho)

- [x] Modelos: `ScaleResponse`, `ScaleType`; tabla `ScaleResponses`
- [x] `DataRepository` + `FirestoreManager` + `CloudSync` (`scaleResponses`); `DeleteAllData` y `DeleteUserData` incluidos
- [x] `Who5Questionnaire` (clase pura): ítems, opciones, puntuación, calendario e id de notificación
- [x] `Who5Scheduler`: disponibilidad (línea base desde el alta, después cada 14 días) → `NotificationCenter.CreatePinned("who5-{fecha}", …)`; lo llama `MainMenuScreen.OnScreenFocused`
- [x] `NotificationManager.ScheduleWho5Reminder`: push del sistema el día en que vuelve a estar disponible
- [x] `AppState.Who5` + `Who5Screen` / `Who5Controller` / `Who5View` (introducción, 5 ítems, progreso, anterior/siguiente, enviar, agradecimiento); `BottomNavBar` se oculta
- [x] `NotificationsController`: "Hacer ahora" en la anclada → `AppState.Who5`
- [x] Al enviar: guardar, `ResolvePinned`, +20 monedas con `RewardGrant`, programar el siguiente aviso
- [x] Tests de `Who5Questionnaire`
- [ ] Verificar el texto oficial en español de los ítems (antes de publicar)
- [x] Protocolo de apoyo si el índice es ≤ 28 (`SupportProtocol.CheckWho5Score` en `Who5Controller._safeSubmit`)

## Fase 4 · Seguridad y legal (código hecho, salvo la edad)

- [ ] **Edad ≥ 18** en el onboarding (paso de fecha de nacimiento) y al restaurar el perfil al iniciar sesión — **pendiente, se hará más adelante**
- [x] Consentimiento: `ConsentGate` + `AppState.Consent` + `ConsentScreen` / `ConsentController` / `ConsentView` / `ConsentTexts`. Se pide tras crear el perfil y antes del primer check-in, y a los usuarios existentes sin consentimiento (arranque y login)
- [x] `Preferences`: `consentVersion`, `consentDate`, `diaryLanguageAnalysis` (se sincronizan con el perfil)
- [x] Protocolo de apoyo: `SupportRules` (puras, con tests) + `SupportProtocol` + `SupportDialog`; disparadores tras el check-in de Día (3 días seguidos con ánimo ≤ 2) y tras el WHO-5 (≤ 28); máximo 1 vez cada 7 días; notificación `Support` con botón "Ver recursos"
- [x] Ajustes: botón "Recursos de ayuda" (024, 112) e interruptor "Análisis de escritura para el informe"
- [x] Tests (`SupportAndConsentTests`)

## Fase 5 · Cálculo (código hecho)

- [x] `EmotionCircumplex` (valencia, activación y cuadrante derivados) y `MotiveTags` (lista de motivos fijos, agrupación de los libres como "Otros"; `EmotionCheckView` ya la usa)
- [x] `DiaryLexicon` + `DiaryLanguageAnalyzer` + `Assets/Resources/DiaryLexicon_es.txt`
- [x] `ReportInput` / `ReportData` / `ReportCalculator`: todas las métricas de `METRICS.md` §4, con mínimos de datos
- [x] `ReportDataLoader`: reúne los datos de `DataRepository` y calcula (lo usarán las fases 6 y 7)
- [x] `DataRepository`: `GetDiaryEntriesForPeriod`, `GetLastNotificationOfType`, `GetNotificationsOfTypeForPeriod`
- [x] Tests: `ReportCalculatorTests`, `DiaryLanguageTests`, `ModelHelpersTests` (72 tests en total, todos pasan)

## Fase 6 · Pantalla de Estadísticas (código hecho)

- [x] `ChartsView` nuevo según `PROFESSIONAL_REPORT.md` §2.2 (resumen, gráfico de ánimo, calendario por periodo, emociones, estabilidad, motivos, minijuegos y respiración, WHO-5, hábitos, botón exportar)
- [x] `ChartsController` con `ReportDataLoader`; periodos `ChartPeriodExtensions` (Mes = últimos 30 días)
- [x] `StatsTextBuilder` (frases y estados vacíos, con tests)
- [x] Componentes `UILineChart` y `StatsBarRow`
- [x] Eliminados `ChartsData`, `ChartsCalculator.Calculate` y `GenerateWeeklyReport` (sustituido por el resumen semanal)

## Fase 7 · Exportación (código hecho)

- [x] NativeShare añadido a `Packages/manifest.json` (git) y a `LutraCore.asmdef` (`NATIVE_SHARE` con *version define*)
- [x] `PdfDocumentWriter` y `ReportPdfBuilder` (estructura §4.3; revisado visualmente con un informe de ejemplo)
- [x] `ReportCsvBuilder` + ZIP + `LEEME.txt` (§4.4)
- [x] `ReportExporter`, `FileSharer`, `ExportPanelView` + `ReportExportController`
- [x] Exportación JSON de Ajustes con todos los datos y compartible
- [x] Tests (`ReportExportTests`, `StatsTextBuilderTests`): 88 tests en total, todos pasan
- [ ] Rango de fechas personalizado en la exportación (fuera por ahora)

## Pasos en Unity y consola (manuales)

Se hacen todos juntos al terminar las fases de código. Al completarlos, probar en Play Mode.

### Fase 1

- [x] **`PostMinigameScreen`**: crear una fila de 5 caritas (muy mal → muy bien) y asignar en el Inspector `_moodButtons` (los 5 `Button`, en ese orden), `_moodButtonImages` (sus `Image`), `_moodSelectedColor` y `_moodNormalColor`. Los botones de emoción se quedan como están.

### Fase 2 — centro de notificaciones

- [x] **Prefab `NotificationCardPrefab`**: raíz con `NotificationCard` (+ `LayoutElement`), hijos `Image` (icono), 3 `TextMeshProUGUI` (título, cuerpo, hora) y un punto de "no leída" (`GameObject`). Asignar `_icon`, `_titleLabel`, `_bodyLabel`, `_timeLabel`, `_unreadDot`. Sin botón de acción.
- [x] **Prefab `PinnedNotificationCardPrefab`**: igual pero destacado (color de acento) y con un `Button` "Hacer ahora" → `_actionButton` y su texto → `_actionLabel`. Puede dejar `_unreadDot` vacío.
- [x] **Prefab `NotificationGroupHeaderPrefab`**: un `TextMeshProUGUI` ("Hoy", "Ayer"…).
- [x] **Pantalla `NotificationsScreen`** en `Main.unity`, hermana de las demás pantallas:
  - Raíz: `CanvasGroup` + `NotificationsScreen` (asignar `_controller`) + `NotificationsController` (asignar `_view`) + `NotificationsView`.
  - Cabecera con título "Notificaciones" y botón atrás → `_backButton`.
  - `ScrollView` vertical cuyo `Content` tenga `VerticalLayoutGroup` + `ContentSizeFitter`. Dentro:
    - Sección "Importante" (título + contenedor con `VerticalLayoutGroup`) → `_pinnedSection` y `_pinnedContainer`.
    - Contenedor de la lista con `VerticalLayoutGroup` → `_listContainer`.
    - Botón "Cargar más" → `_loadMoreButton` (puede ir dentro de `_listContainer` o justo debajo).
  - Estado vacío ("Aquí verás tus recompensas y avisos") → `_emptyState`.
  - Prefabs → `_pinnedCardPrefab`, `_cardPrefab`, `_groupHeaderPrefab`. Iconos opcionales en la sección "Iconos".
  - Sin `SafeAreaHandler`: el área segura la aplica el padre común de todas las pantallas.
- [x] **`ScreenManager`**: añadir `NotificationsScreen` al array `_screens`.
- [x] **`MainMenuScreen`**: botón de notificaciones junto al de Ajustes → `_notificationsButton`; hijo con círculo "!" → `_notificationsBadge` (empieza desactivado).
- [x] **No hace falta** añadir `NotificationCenter` a la escena: lo crea `GameManager`.
- [x] **Firebase (consola)**: reglas de Firestore que permitan al propio usuario leer y escribir `users/{uid}/notifications` (y, para la Fase 3, `users/{uid}/scaleResponses`). Si las reglas ya usan un comodín para las subcolecciones del usuario, no hay que tocar nada.
- [x] **Tests**: Window > General > Test Runner → EditMode → Run All (ensamblado `Lutra.Tests.EditMode`).

### Fase 3 — WHO-5

- [x] **Pantalla `Who5Screen`** en `Main.unity`, hermana de las demás:
  - Raíz: `CanvasGroup` + `Who5Screen` (asignar `_controller`) + `Who5Controller` (asignar `_view`) + `Who5View`.
  - Cabecera con botón cerrar (X) → `_closeButton`.
  - **Panel de introducción** → `_introPanel`: texto → `_introLabel`, botón "Empezar" → `_startButton`.
  - **Panel de pregunta** → `_questionPanel`:
    - Texto "Pregunta N de 5" → `_progressLabel`; barra opcional (`Image` en modo *Filled*) → `_progressFill`.
    - Texto "Durante las últimas dos semanas…" → `_itemPrefixLabel`; texto del ítem → `_itemLabel`.
    - **6 botones** de opción en vertical, en este orden: Todo el tiempo, La mayor parte del tiempo, Más de la mitad del tiempo, Menos de la mitad del tiempo, De vez en cuando, Nunca → `_optionButtons`; sus textos → `_optionLabels` (el código los rellena). Colores `_optionSelectedColor` / `_optionNormalColor`.
    - Botón "Anterior" → `_previousButton`; botón "Siguiente/Enviar" → `_nextButton` y su texto → `_nextButtonLabel`.
  - **Panel de agradecimiento** → `_thanksPanel`: textos → `_scoreLabel`, `_coinsLabel`, `_nextDateLabel`; botón "Listo" → `_doneButton`.
- [x] **`ScreenManager`**: añadir `Who5Screen` al array `_screens`.
- [x] **Firebase (consola)**: permitir `users/{uid}/scaleResponses` (ver el paso de la Fase 2).
- [x] **Probar**: con un usuario nuevo debe aparecer la notificación anclada al entrar al menú principal; responder, comprobar que desaparece, que se suman 20 monedas y que llega la notificación de recompensa.

### Fase 4 — consentimiento y apoyo

- [x] **Pantalla `ConsentScreen`** en `Main.unity`, hermana de las demás:
  - Raíz: `CanvasGroup` + `ConsentScreen` (asignar `_controller`) + `ConsentController` (asignar `_view`) + `ConsentView`.
  - Título → `_titleLabel`; `ScrollView` con el texto largo → `_bodyLabel` (TextMeshPro con *Rich Text* activado: usa `<b>`).
  - Casilla obligatoria (`Toggle` + texto) → `_requiredToggle`, `_requiredLabel`; casilla del diario → `_diaryAnalysisToggle`, `_diaryAnalysisLabel`. Los textos los pone el código.
  - Botones "Aceptar y continuar" → `_continueButton` y "No acepto" → `_declineButton`.
- [x] **`ScreenManager`**: añadir `ConsentScreen` al array `_screens`.
- [x] **`SupportDialog`**: GameObject **siempre activo** en el canvas principal, por encima de las pantallas (como `ToastNotification`), con el componente `SupportDialog`. Hijo `_panel` (empieza desactivado) con fondo que bloquee la pantalla, título → `_titleLabel`, mensaje → `_messageLabel`, botones "Llamar al 024" → `_call024Button`, "Emergencias 112" → `_call112Button` y cerrar → `_closeButton` con su texto → `_closeLabel`.
- [x] **Ajustes (`SettingsView`)**: botón "Recursos de ayuda" → `_helpResourcesButton`; `Toggle` "Análisis de escritura para el informe" → `_diaryAnalysisToggle`.
- [x] **Tarjeta de notificación**: para que las de apoyo muestren "Ver recursos", el prefab normal (`NotificationCardPrefab`) necesita también un botón → `_actionButton` y `_actionLabel` (se oculta solo en las demás).
- [x] **Probar**: usuario existente → al entrar debe salir el consentimiento una vez; "No acepto" → vuelve al login. Tres check-ins de Día seguidos con ánimo 1–2 → aparece el diálogo de apoyo y la notificación.

### Fase 5 — cálculo

- Nada que montar. Comprobar que `Assets/Resources/DiaryLexicon_es.txt` se importa como `TextAsset` y ejecutar los tests (Test Runner → EditMode).

### Fase 6 — pantalla de Estadísticas

`ChartsView` ha cambiado entera: las referencias antiguas del Inspector se pierden y hay que montar la pantalla de nuevo.

- [x] **Prefabs:**
  - `EmotionBarRowPrefab`: `StatsBarRow` con texto de la emoción → `_label`, `Image` de barra (*Image Type* = Filled, *Fill Method* = Horizontal, con un sprite blanco) → `_fill`, texto del número → `_valueLabel`. Con `LayoutElement` (alto fijo).
  - `StatsTextRowPrefab`: un `TextMeshProUGUI` con *word wrapping* y `LayoutElement` (o `ContentSizeFitter` vertical), para motivos y minijuegos.
  - `HeatmapCellPrefab`: `Image` + `HeatmapCell` (`_background` = la propia Image, `_noRecordMark` = hijo con una X, desactivado).
  - `LegendItemPrefab`: `HorizontalLayoutGroup` + `HeatmapCell` (muestra de color con su X como `_background`/`_noRecordMark`, texto → `_label`).
- [x] **`ChartsScreen`** (`ScrollView` vertical con `VerticalLayoutGroup` + `ContentSizeFitter` en `Content`). En `ChartsView` asignar:
  - Periodo: `_weekButton`, `_monthButton`, `_allTimeButton`.
  - Resumen: `_summaryLabel`, `_summaryFace` (`Image`) + `_moodFaceSprites` (las 5 caritas del check-in, de muy mal a muy bien) y, opcionales, `_trendUpIcon`, `_trendFlatIcon`, `_trendDownIcon`.
  - Gráfico: un GameObject de UI con el componente **`UILineChart`** (desactivar *Raycast Target*; alto ~250) → `_moodChart`; texto de vacío → `_moodChartEmptyLabel`.
  - Calendario: contenedor con `GridLayoutGroup` (*Constraint* = Fixed Column Count = 7) → `_heatmapContainer`; `_heatmapCellPrefab`.
  - Emociones: `_emotionsSummaryLabel`, contenedor vertical → `_emotionBarsContainer`, `_emotionBarPrefab`.
  - Estabilidad: raíz del bloque → `_stabilitySection`, texto → `_stabilityLabel`.
  - Qué influye en ti: contenedor vertical → `_motivesContainer`, `_motivesEmptyLabel`.
  - Qué te ayuda: contenedor vertical → `_helpContainer`, `_helpEmptyLabel`, `_breathingLabel`.
  - `_textRowPrefab` = `StatsTextRowPrefab`.
  - Bienestar: otro **`UILineChart`** → `_who5Chart`, `_who5Label`.
  - Hábitos: `_streakCurrentLabel`, `_streakMaxLabel`, `_adherenceLabel`, `_diaryLabel`.
  - Botón "Exportar informe para mi profesional" → `_exportButton`.
  - `_emotionThemes`: los 8 `EmotionTheme`.
- [x] `ChartsScreen` → `_chartsController`; `ChartsController` → `_view`.

### Fase 7 — exportación

- [x] **Package Manager:** al abrir Unity se descargará NativeShare desde GitHub (`com.yasirkula.nativeshare`). **Hace falta Git instalado y en el PATH.** Si falla: *Window → Package Manager → + → Add package from git URL* → `https://github.com/yasirkula/UnityNativeShare.git`.
- [x] **Panel de exportación** dentro de `ChartsScreen` (por encima del resto):
  - GameObject con `ExportPanelView` (siempre activo) y un hijo `_panel` (fondo que bloquee la pantalla, empieza desactivado).
  - 4 botones de periodo → `_last7Button` ("7 días"), `_last30Button` ("30 días"), `_last90Button` ("3 meses"), `_allTimeButton` ("Todo").
  - `Toggle` "Incluir mis notas y mi diario" → `_includeNotesToggle`; `Toggle` "Incluir datos en bruto (CSV)" → `_includeCsvToggle`.
  - Botones "Generar y compartir" → `_generateButton`, "Cancelar" → `_cancelButton`; texto de estado → `_statusLabel`.
- [x] **`ReportExportController`** en `ChartsScreen` (o en el panel): asignar `_charts` (el `ChartsController`) y `_view` (el `ExportPanelView`).
- [x] **Probar en el editor:** Estadísticas → Exportar → Generar: se abre la carpeta con `Lutra_Informe_fecha.pdf` y `Lutra_Datos_fecha.zip`. En el móvil se abre el menú de compartir.

---

## Registro de decisiones

| Fecha | Decisión | Motivo |
|---|---|---|
| 2026-10-06 | Usuarios **≥ 18 años** | Simplifica el consentimiento y permite usar escalas para adultos |
| 2026-10-06 | Un profesional usa Lutra con sus pacientes; **el paciente exporta y comparte** el informe | Privacidad: el paciente controla sus datos. El panel de profesional queda como trabajo futuro |
| 2026-10-06 | **Affect Grid descartada** hasta nuevo aviso | Los diseños existentes se basan en las 5 caritas. La activación se deriva de la emoción (`METRICS.md` §3.1) |
| 2026-10-06 | **Sueño en el check-in: descartado** | Decisión del autor |
| 2026-10-06 | Ánimo antes del minijuego: **se toma del último registro**, no se pregunta | Menos fricción. Para medir efecto solo cuentan registros de ≤ 3 h antes |
| 2026-10-06 | Caritas en `PostMinigameScreen`; **los botones de emoción se mantienen** | Medida comparable 1–5 sin perder la emoción |
| 2026-10-06 | **WHO-5 cada 14 días**, vía centro de notificaciones (anclada), no tras el check-in; 20 monedas | Medida validada (nivel A); recompensa independiente de las respuestas |
| 2026-10-06 | **Centro de notificaciones** con todas las recompensas, guardadas para siempre | Petición del autor |
| 2026-10-06 | Análisis de lenguaje del diario **incluido**, calculado al vuelo (sin campos en BD), con interruptor | Privacidad y diccionario actualizable |
| 2026-10-06 | Granularidad emocional → **emodiversidad** | El check-in es de selección única |
| 2026-10-06 | Fase de seguridad **completa** (edad, consentimiento, protocolo de apoyo) | Uso con pacientes reales y datos de salud (RGPD art. 9) |
| 2026-10-06 | Informe **PDF propio + ZIP de CSV**, compartido con NativeShare | Lectura para el profesional + análisis avanzado |
| 2026-10-06 | **Resumen semanal** como notificación de los lunes (aprobado) y **20 monedas** por el WHO-5 (confirmado) | Decisión del autor |
| 2026-10-06 | `MoodBefore` = dato de ánimo más reciente, incluida la valoración `MoodAfter` de la partida anterior | Al encadenar partidas es el dato más fiel del estado previo |
| 2026-10-06 | `NotificationCenter` lo crea `GameManager` por código | Un paso menos en Unity y no se puede olvidar en la escena |
| 2026-10-06 | La notificación del hito de racha solo menciona las monedas | La decoración del hito nunca se entrega (bug existente, `BUGS.md`) |
| 2026-10-06 | Resumen semanal: mínimo 3 check-ins **de Día** | Coherente con la serie diaria de `METRICS.md` §3.2 |
| 2026-10-06 | WHO-5: línea base disponible desde el alta del perfil; push del sistema solo para los siguientes | Medida inicial lo antes posible; en la línea base el usuario ya está en la app |
| 2026-10-06 | WHO-5: un ítem por pantalla con "Anterior"; las respuestas a medias se descartan al salir | Menos carga visual; cada aplicación se responde de una vez y es comparable |
| 2026-10-06 | Fase 4 hecha junto a la 5, **salvo la comprobación de edad**, que queda pendiente | Decisión del autor |
| 2026-10-06 | El consentimiento se pide **tras crear el perfil y antes del primer check-in** (no antes del onboarding) | Nombre y fecha de nacimiento no son datos de salud; los registros emocionales empiezan en el check-in. Así el consentimiento se guarda en el perfil y se sincroniza |
| 2026-10-06 | "No acepto" cierra la sesión | Sin consentimiento no se pueden tratar datos de bienestar |
| 2026-10-06 | El recuento de palabras del diario se calcula siempre; solo los porcentajes de lenguaje dependen del interruptor | El número de palabras no analiza el contenido; los porcentajes sí |
| 2026-10-06 | Estadísticas: "Mes" = últimos 30 días (ventana móvil) en vez del mes natural | El periodo anterior tiene la misma duración y la comparación es justa; a principios de mes hay datos |
| 2026-10-06 | Los archivos exportados se borran al iniciar la siguiente exportación | El menú de compartir del sistema puede seguir leyéndolos después de volver a la app |
| 2026-10-06 | Sin rango de fechas personalizado en la exportación (7 días, 30 días, 3 meses, todo) | Alcance |
| 2026-10-08 | Estadísticas, calendario: Semana = semana natural en curso (una fila); Todo = 12 semanas fijas con celdas más pequeñas | La ventana móvil de 7 días partía la semana en dos filas; el tope evita que el calendario crezca sin límite |
| 2026-10-08 | La media de 7 días no se une a través de huecos de más de 7 días (pantalla y PDF) | Una línea recta entre periodos sin datos sugería una evolución que no se ha medido |
| 2026-10-08 | Leyenda de colores fija (las 8 emociones, no solo las del periodo) + "Sin registro" marcado con X | Los colores no se entienden sin leyenda; una leyenda fija no cambia de forma entre periodos |
| 2026-10-08 | El gráfico de ánimo **no** marca con X los días sin registro | Un día sin registro no tiene valor en el eje Y; la línea cortada ya lo muestra y el calendario lo detalla |
| 2026-10-09 | Gráfico de ánimo con ejes (caritas en Y, fecha de inicio y "Hoy" en X), media de 7 días oscura y gruesa por encima de la línea diaria gris | Sin ejes el gráfico no se entendía; la tendencia es lo más importante y era lo que menos se veía |
| 2026-10-09 | Los textos de la UI usan "..." en vez de "…" | La fuente `cheeseusauceu` no tiene el carácter U+2026 y TMP lo sustituía por un espacio |
| 2026-10-09 | Login fallido por credenciales: "Email o contraseña incorrectos" | Con la protección contra enumeración de emails, Firebase no distingue email y contraseña (`BUGS.md`) |
| 2026-10-09 | **Sin Firebase Analytics**: paquete quitado y librería nativa de Android desactivada por manifiesto | No se usaba y recogería datos que el consentimiento no menciona (datos de salud, RGPD art. 9) |
| 2026-10-09 | **Sin logs en las builds de release** | Varios logs incluían email, nombre y UID |
| 2026-10-09 | Paneles de la colección de estrellas y del telescopio en SafeZone: se montan con el rediseño de la Zona Segura | Evitar montarlos dos veces |
| 2026-10-06 | Fuera de alcance: PHQ-8, GAD-7, ERQ, registro de actividades, pasos (Health Connect/HealthKit), FruitNinja como indicador de impulsividad | Alcance del TFG |

## Pendiente de confirmar

- Nada por ahora.
