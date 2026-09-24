# Features — Documentación detallada

## Features/Auth

### LoginController + LoginScreen + LoginView
- Tras login correcto con perfil: llama a `ThemeManager.SetActiveCulture(profile.Culture)`, sincroniza diario desde Firestore → comprueba check-in → navega
- `_restoreDiaryFromFirestore`: inserta en SQLite las entradas de Firestore que no existan localmente (nunca sobreescribe)
- Al restaurar check-in de Firestore: aplica `ThemeManager.ApplyTheme(emotion)` y crea placeholder en SQLite

### RegisterController + RegisterScreen + RegisterView
Registro Firebase → navega a OnboardingProfile.

### OnboardingProfileController + Screen + View
5 pasos: nombre/apellido, fecha de nacimiento, cultura, hobbies, resumen; guarda en SQLite y Firestore.
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
- `OnConfirmClicked`: si `IsMorningCheck=true` y existe registro de hoy → `UpdateEmotion`; si no → `RegisterCheckIn`; si `IsMorningCheck=false` → `SaveEmotion` sin tocar racha
- Guarda en Firestore (`SaveLastCheckIn`) y emite `EmitEmotionRegistered` + `EmitCurrentEmotionChanged` (después de transición a MainMenu para que la pantalla ya esté suscrita)

### EmotionCheckView
Tags de emoción como radio buttons (seleccionar uno deselecciona los demás de ambos contenedores).

### EmotionTagButton
Tag seleccionable reutilizable.

### EmotionCheckData / EmotionRecommender
IService, ScriptableObject.

---

## Features/Diary

### DiaryScreen
Delega en `DiaryController.OpenDiary()` al enfocarse.

### DiaryController
- Al guardar: `SaveDiaryEntry` en SQLite, luego `_syncEntryToFirestore` fire-and-forget
- `_syncEntryToFirestore`: comprueba `Auth.IsLoggedIn` antes de llamar a Firestore
- Servicios lazy: `DataRepository`, `AuthManager`, `FirestoreManager`

### DiaryView
`ShowMainView()` / `ShowEditorView(entry)` / `RefreshEntries(list)` / `GetTitle()` / `GetContent()`.
- `_getEmotionColor(mood)` — mapa hardcoded de EmotionType a hex color para el punto de color de las tarjetas

### DiaryEntryCard
`SetupCard(entry, emotionColor)`; muestra título, fecha, preview (80 chars), punto de color; `OnCardClicked` action.

### DiaryEntryPromptData
ScriptableObject con 10 prompts de escritura.

---

## Features/SafeZone

### SafeZoneScreen / Controller / View / Item / PlacementPoint / DraggableItem / ShopItemView / InventoryItemView
Habitación 2D decorable con tienda, inventario y sistema de colocación.

**SafeZoneController** — gestiona inventario (`List<InventoryItem>`), compra con confirmación, colocación tap-to-place y drag-and-drop, y venta al 50%.
- Inspector: `_view` (SafeZoneView), `_allItems` (SafeZoneItem[])
- `OpenSafeZone()` — carga inventario, desbloquea ítems por defecto y por racha, refresca vista
- Flujo compra: `_onBuyRequested` → `ShowConfirmBuyDialog(item, currentCoins, coinsAfter)` → `_onBuyConfirmed` → SpendCoins + UnlockItem + sync Firestore
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

---

## Features/Charts

### ChartsScreen / Controller / View
`AppState.Charts`. `ChartsScreen` delega en `ChartsController.OpenCharts()`.

### ChartsData
DTO con todos los datos calculados para la vista.

### ChartsCalculator
Clase estática pura.
- `Calculate(records, sessions)` → `ChartsData`
- `FormatStreak(int days)` → `"1 día"` / `"X días"`
- `FormatDuration(int seconds)` → `"Xs"` / `"Xm Ys"` / `"Xh Ym"`

### ChartPeriod
Enum `Week`, `Month`, `AllTime`; definido en `ChartsController.cs`.

---

## Features/Settings

`SettingsController`, `SettingsView`, `SettingsScreen` — `AppState.Settings`.

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
