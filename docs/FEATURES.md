# Features — Documentación detallada

## Features/Auth

### LoginController + LoginScreen + LoginView
- Tras login correcto con perfil: llama a `ThemeManager.SetActiveCulture(profile.Culture)`, `CloudSync.SyncAllAsync` (todo: diario, emociones, partidas, estrellas, inventario, monedas, preferencias) → comprueba check-in → navega
- Perfil local de otra cuenta → `DeleteAllData` antes de restaurar

### RegisterController + RegisterScreen + RegisterView
Registro Firebase → navega a OnboardingProfile.

### OnboardingProfileController + Screen + View
5 pasos: nombre/apellido, fecha de nacimiento, cultura, hobbies, resumen; guarda en SQLite y crea el perfil en Firestore con el saldo inicial (`SaveUserProfile(profile, includeCoins: true)`, sin esperar).
- `_finishOnboarding`: llama a `ThemeManager.SetActiveCulture(profile.Culture)` tras guardar el perfil, antes de navegar a `EmotionCheck`

### OnboardingProfileData
DTO temporal entre pasos del onboarding.

---

## Features/EmotionCheck

### EmotionCheckScreen
- `PendingMode` — campo estático `EmotionCheckMode`; `MainMenuScreen` lo establece antes de navegar
- Guard `_hasInitialized` para evitar reinicializar si `OnScreenFocused` se llama dos veces
- Al desenfocarse (`OnScreenUnfocused`): resetea `_hasInitialized = false`

### EmotionCheckController
- `OpenDayCheck()` / `OpenMomentCheck()` — puntos de entrada diferenciados
- Tras guardar un check-in de Día: `SupportProtocol.CheckLowMoodStreak()` (protocolo de apoyo)
- `OnConfirmClicked`: si `IsMorningCheck=true` y existe registro de hoy → `UpdateEmotion`; si no → `RegisterCheckIn`; si `IsMorningCheck=false` → `SaveEmotion` sin tocar racha
- Marca el último check-in en Firestore (`SaveLastCheckIn`, sin esperar; el registro completo lo sube DataRepository) y emite `EmitEmotionRegistered` + `EmitCurrentEmotionChanged` (después de transición a MainMenu para que la pantalla ya esté suscrita)

### EmotionCheckView
Tags de emoción como radio buttons (seleccionar uno deselecciona los demás de ambos contenedores).
Por eso `SelectedEmotionTags` contiene solo la emoción elegida (repite `EmotionType`). Motivos: hobbies del perfil (nombre del enum) + fijos (`MotiveTags.Fixed`) + "Otro" con texto libre.

### EmotionTagButton
Tag seleccionable reutilizable.

### EmotionCheckData / EmotionRecommender
IService, ScriptableObject.

---

## Features/Diary

### DiaryScreen
Delega en `DiaryController.OpenDiary()` al enfocarse.

### DiaryController
- Muestra las entradas **mes a mes** (de la más reciente a la más antigua dentro del mes). Flechas anterior/siguiente y etiqueta "Abril 2026" entre ellas. El historial va del mes de creación de la cuenta (`UserProfile.CreationDate`, o el de la entrada más antigua si es anterior) al mes actual. Mes vacío: "No hubo entradas este mes". Puede haber varias entradas el mismo día.
- La búsqueda busca en todos los meses (con texto, la etiqueta muestra "Resultados" y las flechas se desactivan).
- Al abrir el diario se muestra el mes actual; tras guardar, el mes de la entrada guardada.
- Editor (nueva entrada o edición): oculta la barra inferior con `EventBus.EmitNavBarVisibilityRequested(false)`; al volver a la lista la muestra.
- Al guardar: `SaveDiaryEntry` en SQLite (DataRepository la sube sola a Firestore con su `RemoteId`). La recompensa diaria solo se da con entradas nuevas, no al editar.
- Borrar: diálogo de confirmación → `Repo.DeleteDiaryEntry` (SQLite + marcador de borrado en Firestore, que borra la entrada en los demás dispositivos al sincronizar).
- Servicio lazy: `DataRepository`

### DiaryView
`ShowMainView()` / `ShowEditorView(entry)` / `RefreshEntries(list, emptyMessage)` / `SetMonthNavigation(label, canPrev, canNext)` / `ShowDeleteConfirm(entry)` / `HideDeleteConfirm()` / `ClearSearch()` / `GetTitle()` / `GetContent()`.
- Campos: `_previousMonthButton`, `_nextMonthButton`, `_monthLabel`, `_deleteConfirmPanel`, `_deleteConfirmLabel`, `_deleteConfirmButton`, `_deleteCancelButton`.
- `_getEmotionColor(mood)` — mapa hardcoded de EmotionType a hex color para el punto de color de las tarjetas

### DiaryEntryCard
`SetupCard(entry, emotionColor)`; muestra título, fecha, preview (80 chars), punto de color. La tarjeta **no** es pulsable: botones `_editButton` / `_deleteButton` a la izquierda del punto de emoción → `OnEditClicked` / `OnDeleteClicked`.

### DiaryEntryPromptData
ScriptableObject con 10 prompts de escritura.

---

## Features/SafeZone

### SafeZoneScreen / Controller / View / Item / PlacementPoint / DraggableItem / ShopItemView / InventoryItemView
Habitación 2D decorable con tienda, inventario y sistema de colocación.

**SafeZoneController** — gestiona inventario (`List<InventoryItem>`), compra con confirmación, colocación tap-to-place y drag-and-drop, y venta al 50%.
- Inspector: `_view` (SafeZoneView), `_allItems` (SafeZoneItem[])
- `OpenSafeZone()` — carga inventario, desbloquea ítems por defecto y por racha, refresca vista
- Flujo compra: `_onBuyRequested` → `ShowConfirmBuyDialog(item, currentCoins, coinsAfter)` → `_onBuyConfirmed` → SpendCoins + UnlockItem (DataRepository sube ambos a Firestore)
- Flujo colocación (popup): tap en ítem de inventario → popup flotante → "Colocar" → `_onPlaceRequested` → `EnterPlacementMode` → tap en PlacementPoint → `_onPlacementConfirmed(index)` → SetItemPlacement
- Flujo colocación (drag): `DraggableItem` sobre `PlacementPoint` → `OnDropReceived(index, itemId)` → SetItemPlacement
- Flujo quitar de habitación: `_onRoomItemTapped(index)` → `ShowRoomItemOptions` → "Guardar" → `_onRemoveConfirmed` → SetItemPlacement(false)
- Flujo venta desde habitación: `ShowRoomItemOptions` → "Vender" → `_onSellPreviewRequested` → `ShowSellConfirmDialog` → `_onSellConfirmed` → RemoveItem + AddCoins(50%)
- Flujo venta desde inventario: popup flotante → "Vender" → `_onSellInventoryRequested` → `ShowSellConfirmDialog` → `_onSellConfirmed` → RemoveItem + AddCoins(50%)

**SafeZoneView** — gestiona tabs Habitación/Tienda, barra de inventario, popups y diálogos. No contiene lógica de negocio.

Campos serializados principales:
- Tabs: `_roomTabButton`, `_shopTabButton`, `_roomPanel`, `_shopPanel`
- Habitación: `_placementPoints` (PlacementPoint[]), `_coinsLabel`
- Inventario: `_rootCanvasOverride` (Canvas), `_inventoryBar`, `_inventoryContainer`, `_inventoryItemPrefab`
- Tienda: `_shopContainer`, `_shopItemPrefab`, `_shopTabDecorationButton`, `_shopTabAccessoryButton`
- Diálogo compra: `_confirmBuyDialog`, `_confirmBuyPreview`, `_confirmBuyNameLabel`, `_confirmBuyPriceLabel`, `_confirmBuyCurrentCoinsLabel`, `_confirmBuyAfterCoinsLabel`, `_confirmBuyButton`, `_cancelBuyButton`
- Diálogo opciones habitación: `_roomItemOptionsDialog`, `_roomItemNameLabel`, `_removeButton` (Guardar), `_sellButton`, `_cancelRoomOptionsButton`
- Diálogo confirmación venta: `_sellConfirmDialog`, `_sellMessageLabel`, `_sellCoinsLabel`, `_confirmSellButton`, `_cancelSellButton`
- Popup inventario: `_inventoryItemPopup`, `_popupNameLabel`, `_popupPlaceButton`, `_popupSellButton`, `_popupBackdrop`
- Feedback: `_notEnoughCoinsPanel`

Métodos públicos clave:
- `ShowConfirmBuyDialog(SafeZoneItem, int currentCoins, int coinsAfter)` — rellena nombre, precio, monedas actuales y monedas restantes
- `RefreshRoom(inventory, allItems)` — limpia PlacementPoints y coloca prefabs en los ocupados
- `RefreshInventoryBar(inventory, allItems)` — muestra ítems no colocados; añade `DraggableItem` en runtime
- `RefreshShop(items, ownedIds, coins)` — instancia ShopItemPrefabs y aplica filtro activo
- `EnterPlacementMode(PlacementType)` — bloquea raycasts del inventario, colorea PlacementPoints
- `ExitPlacementMode()` — restaura raycasts, quita colores de modo colocación

Filtro de tienda: `bool? _activeShopFilter` — `null` = mostrar todos (defecto al abrir), `false` = solo Decoración, `true` = solo Accesorios. Solo se filtra al pulsar un tab de categoría explícitamente.

Eventos: `OnBuyRequested`, `OnBuyConfirmed`, `OnBuyCancelled`, `OnPlaceRequested`, `OnPlacementConfirmed`, `OnPlacementCancelled`, `OnSellInventoryRequested`, `OnRoomItemTapped`, `OnRemoveConfirmed`, `OnSellPreviewRequested`, `OnSellConfirmed`, `OnSellCancelled`, `OnDropReceived`

⚠ **Jerarquía Inspector obligatoria**: `_roomPanel` y `_shopPanel` deben ser **hermanos** (hijos del mismo padre), nunca padre/hijo. Si `_shopPanel` es hijo de `_roomPanel`, al desactivar `_roomPanel` se oculta toda la jerarquía y el tab de tienda no funciona.

**PlacementPoint** — MonoBehaviour de escena; IDropHandler + IPointerClickHandler.
- Inspector: `placementIndex` (int, único por punto), `acceptedTypes` (PlacementType[], vacío = acepta todos), `_itemAnchor` (Transform), `_highlightImage` (Image)
- `SetOccupied(bool, itemId, prefab)` — instancia/destruye el prefab del ítem
- `SetPlacementMode(bool, PlacementType)` — colorea highlight: verde = válido, rojo = inválido
- `_refreshHighlight()` — blanco = vacío, azul = ocupado; `_highlightImage.enabled` **siempre true**, nunca se desactiva
- Eventos: `OnDropReceived(int, string)`, `OnTapped(int)`

**DraggableItem** — IBeginDragHandler/IDragHandler/IEndDragHandler; se añade en runtime a cada InventoryItemView.
- `Setup(itemId, PlacementType, Canvas)` — usar siempre null check explícito para CanvasGroup (ver pitfall Unity fake-null en CLAUDE.md)
- Al drag: sube al canvas raíz, alpha 0.75; al soltar: vuelve al padre original, dispara `OnDragCancelled`
- Todos los handlers tienen null-guard: `if (_rectTransform == null || _canvasGroup == null || _rootCanvas == null) return;`

**InventoryItemView** — implementa `IPointerClickHandler` directamente en el componente (sin `Button` serializado).
- `Setup(SafeZoneItem, Action<SafeZoneItem, RectTransform> onSelected)`
- Al pulsar, llama a `onSelected` → SafeZoneView muestra el popup flotante en posición fija del editor

**Fuentes de monedas:**
- Check-in diario: +5 monedas; hitos 7/14/30 días → +20/+50/+100 (`StreakManager.RegisterCheckIn`)
- Entrada de diario: +5 monedas la primera vez al día (Preferences["lastDiaryRewardDate"]) (`DiaryController._grantDiaryReward`)
- Completar minijuego: `Mathf.Max(3, estimatedTimeSeconds/30)` monedas (`MinigameLoader._calculateMinigameCoinReward`, requiere `_minigameDefinitions[]` en Inspector)

### MascotCustomizer
12 colores de plumas predefinidos, accesorios por itemId; persiste en `UserProfile.Preferences` con claves `"featherColorIndex"` y `"mascotAccessoryId"`; restaura al cargar con `LoadSavedCustomization()`.

---

## Features/MainMenu

### MascotController
Animator, hashes pre-cacheados, idle aleatorio, EventBus.

---

## Features/Minigames

### MinigamesScreen
`OnScreenFocused` delega en `MinigamesController.OpenMinigames()`; `AppState.Minigames`.

### MinigamesController
Lógica de filtrado, búsqueda y recomendaciones.
- Inspector: `_view` (MinigamesView), `_allMinigames` (MinigameDefinition[]), `_emotionMap` (EmotionMinigameMap)
- `OpenMinigames()` — suscribe eventos de la vista, carga definiciones, calcula recomendadas por emoción actual
- `_onSearchChanged(string)` / `_onFilterChanged(MinigameTag?)` — filtran `_allMinigames` y llaman a `_view.UpdateAllSection`
- `_onFilterDropdownRequested()` — obtiene tags únicos con `_getDistinctTags()` y llama a `_view.ShowFilterDropdown`
- `_getRecordText(MinigameType)` — `DataRepository.GetBestRelaxationScore` → "Mejor puntuación: N" (0-100) o "Sin récord"; se consulta cada vez que se abre el panel de detalle

### MinigamesView
UI del selector; campos serializados:
- **Header**: `_searchInput`, `_filterButton`, `_filterDropdown` (GameObject), `_filterTagsContainer`
- **Recomendados**: `_recommendedSection` (GameObject on/off), `_recommendedContainer`
- **Todos**: `_allMinigamesContainer`
- **Panel detalle**: `_detailPanel`, `_detailTitle`, `_detailPreviewImage`, `_detailDescription`, `_detailTime`, `_detailRecord`, `_detailTagsContainer`, `_detailPlayButton`, `_detailCloseButton`
- **Layout**: `_scrollViewRect` (RectTransform del ScrollView), `_filterDropdownRect` (RectTransform del panel dropdown)
- **Prefabs**: `_minigameCardPrefab`, `_tagPrefab`
- Dropdown dinámico: `ShowFilterDropdown` activa el panel y arranca corrutina `_adjustScrollViewAfterDropdown` que espera un frame, lee `_filterDropdownRect.rect.height` y ajusta `_scrollViewRect.offsetMax = (0, -(160 + height))`; `HideFilterDropdown` resetea a `(0, -160)`
- `_clearContainer` y `_populateContainer` siguen el patrón de layout obligatorio (ver CLAUDE.md)

### MinigameCard
Card individual; campos: `_logoImage`, `_nameLabel`, `_timeNumberLabel`, `_timeUnitLabel`, `_tagsContainer`, `_tagPrefab`, `_cardButton`; método `Setup(MinigameDefinition, Action onClicked)`.

### PostMinigameScreen
Pantalla post-minijuego; `AppState.MinigameActive`.
- En `OnScreenFocused` lee `MinigameLoader.LastOutcome` (`MinigameOutcome`: sesión ya guardada, nombre, récord previo, `IsNewRecord`, monedas). No depende de `EventBus.OnMinigameCompleted`, que se emite cuando la pantalla aún está desactivada.
- Campos: `_titleLabel`, `_scoreLabel` ("Puntuación: N", 0-100), `_recordLabel`, `_newRecordBadge` (GameObject), `_durationLabel` (`ChartsCalculator.FormatDuration`), `_coinsLabel`, `_messageLabel` (mensaje según `EmotionBefore`)
- Botones de emoción (`_emotionButtons` + `_emotionTypes`, mismo índice) → guarda `EmotionAfter` con `DataRepository.UpdateMinigameSession` y resalta el seleccionado
- `_playAgainButton` → `TransitionTo(Minigames)` + `MinigameLoader.LoadMinigame` (con la emoción elegida o la previa); `_backButton` → `TransitionTo(Minigames)`
- Caritas (`_moodButtons` + `_moodButtonImages`, índice 0 = muy mal … 4 = muy bien) → guarda `MoodAfter` (1-5) con `UpdateMinigameSession`. Los botones de emoción se mantienen. `MoodBefore` no se pregunta: lo rellena `MinigameLoader` con `GetLatestMood` (`docs/METRICS.md` §2.2 y §4.6)

---

## Features/Charts

Pantalla de Estadísticas (`AppState.Charts`) y exportación del informe profesional. Especificación en `docs/PROFESSIONAL_REPORT.md` §2 y §4; métricas en `docs/METRICS.md`.

### ChartsScreen / ChartsController / ChartsView
- `ChartsScreen.OnScreenFocused` → `ChartsController.OpenCharts()`.
- `ChartsController.LoadDataForPeriod(period)`: rango con `ChartPeriod.GetRange` → `ReportDataLoader.LoadAsync` → `ChartsView.Render(data, period, chartFrom, heatmapFrom, today)`. Descarta resultados de cargas anteriores si se cambia de periodo rápido. En "Semana", el calendario muestra la semana natural en curso (una fila; el lunes siempre cae dentro de los últimos 7 días). En "Todo", el gráfico abarca todo el historial y el calendario las últimas 12 semanas (tope fijo). Expone `CurrentPeriod` y el evento `OnExportRequested`.
- `ChartsView`: bloques periodo · resumen (carita + tendencia) · gráfico de ánimo (`UILineChart`: puntos del color de la emoción, media de 7 días; ambas líneas se cortan en los huecos: la diaria a partir de 2 días, la media a partir de `ReportCalculator.MovingAverageWindowDays`) · calendario (celdas alineadas lunes-domingo, color de la emoción del día, última fila completada con celdas invisibles, celdas reducidas con más de `_heatmapCompactAfterWeeks` filas, también la fila de letras `_heatmapWeekdays`; celdas con `HeatmapCell`: marca X en los días sin registro) · leyenda (`_legendContainers` + `_legendItemPrefab` con `HeatmapCell`: 8 emociones en orden de valencia + "Sin registro", se pinta una vez) · emociones (`StatsBarRow`) · estabilidad (oculta sin 14 pares) · qué influye en ti · qué te ayuda + respiración · WHO-5 (`UILineChart`) · hábitos · botón exportar. Todos los campos son opcionales.

### ChartPeriod / ChartPeriodExtensions
`Week` = últimos 7 días, `Month` = últimos 30 días (ventanas móviles: el periodo anterior tiene la misma duración), `AllTime` = desde el alta. `GetRange(now)`, `CurrentText()`, `PreviousText()`.

### StatsTextBuilder
Clase pura (con tests): frases en lenguaje sencillo y positivo a partir de `ReportData` (resumen, tendencia, estabilidad, motivos, qué te ayuda, respiración, WHO-5, constancia, diario) y los textos de "faltan datos". Sin términos técnicos ni etiquetas clínicas.

### ChartsCalculator
Solo formato compartido: `FormatStreak(int days)` → "1 día" / "X días"; `FormatDuration(int seconds)` → "Xs" / "Xm Ys" / "Xh Ym".

### Report (`Features/Charts/Report/`)
Motor de métricas (`docs/METRICS.md` §4).
- `ReportInput` → `ReportCalculator.Calculate` (clase pura, con tests) → `ReportData`.
- `ReportDataLoader.BuildInputAsync(from, to)` reúne los datos de `DataRepository`, el periodo anterior, las rachas y el diccionario; `LoadAsync` además calcula.
- `DiaryLexicon` + `DiaryLanguageAnalyzer`: análisis del lenguaje del diario con `Assets/Resources/DiaryLexicon_es.txt`.

### Export (`Features/Charts/Export/`)
- `PdfDocumentWriter`: PDF 1.4 propio (Helvetica / Helvetica-Bold WinAnsi, texto, líneas, rectángulos, polilíneas, medida de texto con las métricas de Helvetica). Los símbolos que no existen en WinAnsi se sustituyen (≤ → "<=", ≥ → ">=", ≈ → "~", − → "-").
- `ReportPdfBuilder.Build(input, data, options)`: las secciones del informe (§4.3), con tablas, gráficos, avisos de atención, notas opcionales, anexos y pie "Página N de M".
- `ReportCsvBuilder.Build(input, data, includeNotes, appVersion)`: CSV + `LEEME.txt` (§4.4).
- `ReportExporter.ExportAsync(period, includeNotes, includeCsv)`: genera PDF y ZIP (UTF-8 con BOM) en `temporaryCachePath/LutraExport` (borra la exportación anterior). `ExportPeriod`: 7 días, 30 días, 3 meses, todo.
- `ExportPanelView` + `ReportExportController`: panel con periodo, "Incluir notas y diario" (desmarcado) e "Incluir datos en bruto (CSV)" (marcado); genera y abre el menú de compartir (`FileSharer`). El periodo por defecto sigue al de la pantalla.

### WeeklySummaryBuilder
Clase pura: texto del resumen semanal del centro de notificaciones (`docs/NOTIFICATION_CENTER.md` §9).

---

## Features/Notifications

Centro de notificaciones (`AppState.Notifications`). Especificación completa en `docs/NOTIFICATION_CENTER.md`.

### NotificationsScreen
`UIScreen`; `OnScreenFocused` → `NotificationsController.OpenNotifications()`. La barra inferior se oculta.

### NotificationsController
Carga ancladas y la primera página (30, pide una más para saber si hay otra), marca como leídas las no ancladas al abrir y pagina con "Cargar más". Atrás → MainMenu. Acción de anclada según `NotificationType`: `Who5Available` → `AppState.Who5`.

### NotificationsView
Campos: `_backButton`, `_pinnedSection`, `_pinnedContainer`, `_pinnedCardPrefab`, `_listContainer`, `_cardPrefab`, `_groupHeaderPrefab`, `_loadMoreButton`, `_emptyState` y sprites opcionales por tipo/origen. Agrupa por "Hoy", "Ayer", "Esta semana" y fecha. Eventos: `OnBackRequested`, `OnLoadMoreRequested`, `OnPinnedActionRequested`.

### NotificationCard
Icono, título, cuerpo, hora, `_unreadDot` y `_actionButton`/`_actionLabel` opcionales. `Setup(notification, icon, timeText, actionText)`.

### NotificationTimeFormatter
Clase pura: `GroupLabel(createdAt, today)` y `TimeText(createdAt, today)`.

---

## Features/Who5

Cuestionario de bienestar WHO-5 (`AppState.Who5`). Especificación en `docs/PROFESSIONAL_REPORT.md` §3.

### Who5Questionnaire
Clase pura: `Items`, `Options` (etiqueta + valor 5…0), `RawScore`, `ToIndex`, `GetAvailableSince(profileCreated, lastCompletedAt, today)`, `NextAvailableDate`, `NotificationRemoteId` / `TryParseAvailableSince`, constantes (`IntervalDays` 14, `CoinReward` 20, puntos de corte 50 / 28 / 10).

### Who5Screen
`UIScreen`; `OnScreenFocused` → `Who5Controller.OpenQuestionnaire()`; `OnScreenUnfocused` → `DiscardAnswers()`.

### Who5Controller
Estado de las 5 respuestas (índice de opción), navegación entre ítems y envío: guarda `ScaleResponse`, resuelve la anclada, da 20 monedas (`RewardGrant`), programa el siguiente aviso y muestra el agradecimiento. Cerrar o "Listo" → `AppState.Notifications`.

### Who5View
Paneles `_introPanel`, `_questionPanel`, `_thanksPanel`; 6 `_optionButtons` + `_optionLabels`; `_previousButton`, `_nextButton` ("Siguiente"/"Enviar"), `_closeButton`, `_doneButton`. Eventos `OnStartRequested`, `OnOptionSelected(int)`, `OnPreviousRequested`, `OnNextRequested`, `OnCloseRequested`, `OnDoneRequested`.

---

## Features/Consent

Consentimiento de datos de salud (`AppState.Consent`). Especificación en `docs/PROFESSIONAL_REPORT.md` §6.2; lógica de navegación en `ConsentGate`.

### ConsentScreen / ConsentController / ConsentView / ConsentTexts
- `ConsentScreen.OnScreenFocused` → `ConsentController.OpenConsent()` (rellena textos, casilla obligatoria desmarcada y la del diario marcada).
- Aceptar → `ConsentGate.SaveConsent(diaryAnalysis)` → `TransitionTo(ConsentGate.PendingTarget)`. No acepto → `AuthManager.Logout()` + toast + Login.
- `ConsentView`: `_titleLabel`, `_bodyLabel` (rich text), `_requiredToggle` / `_requiredLabel`, `_diaryAnalysisToggle` / `_diaryAnalysisLabel`, `_continueButton` (solo activo con la obligatoria), `_declineButton`.
- `ConsentTexts`: todos los textos. Si cambian, subir `ConsentGate.CurrentVersion`.

---

## Features/Settings

`SettingsController`, `SettingsView`, `SettingsScreen` — `AppState.Settings`.
- Exportar datos: `SettingsManager.ExportUserData()` genera `lutra_datos.json` con todos los datos (perfil, registros, diario, partidas, cuestionarios, notificaciones) y se comparte con `FileSharer`.
- Ayuda y privacidad: `_helpResourcesButton` → `SupportDialog.ShowResources()`; `_diaryAnalysisToggle` → `ConsentGate.SetDiaryAnalysis` (se carga del perfil en `OpenSettings`).

---

## Features/Onboarding (legacy)

`OnboardingScreen` — flujo legacy sin Firebase, mantener por compatibilidad.

---

## Minigames (base)

### IMinigame
Interfaz: `Type`, `IsPlaying`, `Initialize(EmotionType)`, `StartGame/PauseGame/ResumeGame/EndGame(bool)`, evento `OnGameCompleted: Action<MinigameResult>`.

### MinigameBase
Clase base abstracta; implementa `IMinigame`; campos protegidos `_emotionBefore`, `_startTime`, `_isPlaying`, `_result`; método protegido `BuildResult(float relaxationScore, bool completed, Dictionary<string,float> metrics = null)`.

### MinigameLoader
Carga escena aditiva → busca `IMinigame` → `Initialize` → `StartGame` → `OnGameCompleted` → consulta récord previo → guarda `MinigameSession` en BD → `EmitMinigameCompleted` → monedas → `LastOutcome` → descarga escena → `TransitionTo(MinigameActive)` (PostMinigameScreen).
