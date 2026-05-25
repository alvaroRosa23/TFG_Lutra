# Bugs y calidad

## Pendiente antes del build final

- Eliminar `LutraBootstrapTest.cs` (en `Core/Utils/`)
- Eliminar `UnlockedItem.cs` (en `Core/Data/Models/`) — reemplazado por `InventoryItem`; el archivo antiguo no se usa pero compila sin errores
- **Investigar bug tab Tienda en SafeZone**: al pulsar el tab de Tienda oculta todo excepto BottomNavBar. Causa probable: `_roomPanel` y `_shopPanel` no son hermanos en la jerarquía (ver sección de bugs SafeZone al final de este archivo)

## Sistema SafeZone implementado

**Nuevos archivos:**
- `InventoryItem.cs` — reemplaza `UnlockedItem`; añade `IsPlaced` y `PlacementIndex` para posición en la habitación
- `PlacementType.cs` — enum: None, Furniture, Plant, WallArt, Decoration; asignable en cada `SafeZoneItem` SO
- `PlacementPoint.cs` — MonoBehaviour de escena (IDropHandler + IPointerClickHandler); gestiona el prefab colocado y resaltado en modo colocación
- `DraggableItem.cs` — drag-and-drop desde el inventario a PlacementPoints vía IBeginDragHandler/IDragHandler/IEndDragHandler
- `ShopItemView.cs` — tarjeta de tienda con preview, precio y botón de compra
- `InventoryItemView.cs` — tarjeta de inventario con botones "Colocar" y "Vender"

**Archivos modificados:**
- `SafeZoneItem.cs` — añadido campo `placementType`
- `InventoryItem` sustituye a `UnlockedItem` en `DatabaseManager`, `DataRepository` (nuevos métodos: `GetInventoryItems`, `SetItemPlacement`, `RemoveItem`)
- `SafeZoneController` — reescrito: compra con confirmación, colocación tap-to-place, venta al 50%, desbloqueo por racha automático, sincronización a Firestore
- `SafeZoneView` — reescrita: tabs Habitación/Tienda, barra de inventario, diálogo de compra, diálogo quitar/vender, PlacementPoints con modo resaltado
- `FirestoreManager` — añadidos `AddInventoryItem`, `RemoveInventoryItem`, `GetInventoryItemIds` (campo `inventoryItems` en documento de usuario)
- `StreakManager.RegisterCheckIn` — añade +5 monedas por check-in diario; +20/+50/+100 en hitos de 7/14/30 días
- `DiaryController.SaveEntry` — añade +5 monedas la primera vez que se escribe en el diario cada día (guarda en `Preferences["lastDiaryRewardDate"]`)
- `MinigameLoader` — añadido `_minigameDefinitions[]` (Inspector) y recompensa `Mathf.Max(3, estimatedTimeSeconds/30)` monedas al completar un minijuego
- `LoginController._navigateAfterAuth` — llama a `_restoreInventoryFromFirestore` tras restaurar el diario

## Bugs resueltos

- Token corrupto de Firebase al crear cuenta sin terminar onboarding
- Solapamiento de secciones en EmotionCheckScreen
- MoodButtons sin respuesta visual por Alpha 0
- Tags de emociones y motivos no visibles por prefab mal configurado
- BottomNavBar perdía suscripción al ocultarse con `SetActive(false)`
- Captura de variable en lambda en BottomNavBar (`AppState target`)
- `DateTime.UtcNow` en comparaciones de fecha causaba fallos de racha en zonas UTC+ (reemplazado por `DateTime.Today` / `DateTime.Now` en todo el proyecto; excepción pendiente en `ChartsController._getDateRange`)
- `GetCurrentStreak()` podía no reconocer check-in del día actual en dispositivos con offset positivo
- `EmotionCheckController` creaba un segundo registro al re-abrir el check-in de día; ahora actualiza el existente con `UpdateEmotion`
- Tags de emoción permitían selección múltiple; ahora son radio buttons (seleccionar uno deselecciona los demás)
- Entradas del diario se perdían al cerrar sesión o cambiar de dispositivo; ahora se sincronizan a Firestore en `SaveDiaryEntry` y se restauran en `LoginController._restoreDiaryFromFirestore`
- SQLite no inicializaba en Unity Editor; resuelto configurando `sqlite3.dll` con CPU: x86_64, Editor + Standalone en el Inspector
- Paleta cultural ignorada en arranque en frío: `GameManager.StartApp` no llamaba a `SetActiveCulture`; solo lo hacía `LoginController`, por lo que sesiones restauradas automáticamente por Firebase usaban siempre colores por defecto
- Paleta cultural ignorada en primera sesión tras onboarding: `OnboardingProfileController._finishOnboarding` no llamaba a `SetActiveCulture` antes de navegar a `EmotionCheck`
- `EmotionColorEntry` era struct: `Color` se inicializa a `(0,0,0,0)` en structs, causando alpha 0 en los colores del override y haciendo transparente el `_backgroundImage`; resuelto cambiando a clase con `primaryColor = Color.white` y `backgroundColor = Color.black`
- `MinigamesView` — cards antiguas dejaban hueco al buscar y se solapaban al limpiar el buscador: `Destroy` diferido hacía que el LayoutGroup siguiera contando objetos "muertos"; resuelto con `SetActive(false)` antes de `Destroy` + `LayoutRebuilder.ForceRebuildLayoutImmediate` tras repoblar el contenedor
- `MinigamesView` — altura del dropdown de filtros fija (280px) no se adaptaba a su contenido real; reemplazado por corrutina que espera un frame y lee `_filterDropdownRect.rect.height` para ajustar el ScrollView dinámicamente
- `MinigamesView` — `Instantiate(prefab, container)` sin `worldPositionStays = false` causaba posicionamiento en coordenadas mundiales en lugar de locales; corregido en todos los `Instantiate` de cards y tags
- `DateTime.UtcNow` en constructores de modelos (`MinigameSession`, `UnlockedItem`, `UserProfile`, `EmotionCheckData`, `MinigameResult`) podía causar timestamps incorrectos en zonas UTC+; reemplazado por `DateTime.Now` en todos
- `DateTime.UtcNow` en `MinigameBase.StartGame()` y `CalculateDuration()` causaba duraciones de sesión incorrectas en zonas UTC+; corregido a `DateTime.Now`
- `DateTime.UtcNow` en `ChartsController._getDateRange()` excluía registros del día actual en zonas UTC+; corregido a `DateTime.Now`
- `Instantiate` sin `worldPositionStays = false` en `SafeZoneView`, `MascotCustomizer`, `DiaryView`, `ChartsView`, `EmotionCheckView`, `MinigameCard`, `MainMenuScreen` y `ThemeManager` causaba posicionamiento incorrecto de objetos instanciados; corregido en los 10 casos restantes del proyecto
- Limpieza de contenedores UI sin `SetActive(false)` antes de `Destroy` en `SafeZoneView` (room y shop), `DiaryView`, `ChartsView._clearContainer` y `MinigameCard._populateTags`; corregido con el patrón estándar del proyecto
- `ForceRebuildLayoutImmediate` faltaba tras repoblar en `DiaryView`, `SafeZoneView` (shop) y `MainMenuScreen` (barra de racha); añadido en los tres
- `MinigamesView` — tags del dropdown de filtros no respondían al pulsar: `ShowFilterDropdown` buscaba `GetComponent<Button>()` y si el prefab no lo tenía simplemente ignoraba el click; corregido con `GetComponent<Button>() ?? AddComponent<Button>()` para garantizar interactividad sin modificar el prefab
- `MinigamesController` — al aplicar filtro o búsqueda solo se actualizaba la sección "Todos", no "Recomendados"; corregido en `_onFilterChanged`, `_onSearchChanged` y `_onCurrentEmotionChanged` para que ambas secciones pasen por `_applyFilters`

## Bugs resueltos — SafeZone (sesiones de implementación)

- **DraggableItem crash "Setup() no fue llamado"**: el prefab ya tenía `DraggableItem` en el editor; `AddComponent` añadía un segundo sin configurar y los eventos se disparaban en ambos. Resuelto con `GetComponent<DraggableItem>()` (sin `??`) y null-guards en los tres handlers de drag.
- **Unity fake-null con `??` operator**: `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()` siempre ejecuta `AddComponent` porque el fake-null de Unity no es C# null. Corregido en `DraggableItem.Setup()`, `SafeZoneView._setInventoryContainerRaycasts()` y cualquier otro uso con el patrón `var x = Get...; if (x == null) x = Add...`.
- **MissingComponentException en `_setInventoryContainerRaycasts`**: mismo problema de fake-null. Corregido con null check explícito.
- **Pantalla azul (alpha=0) al volver al MainMenu**: `PlayExitAnimation()` dejaba el `CanvasGroup.alpha` en 0; `Show()` no lo reseteaba. Corregido en `UIScreen.Show()` añadiendo `if (_canvasGroup != null) _canvasGroup.alpha = 1f`.
- **PlacementPoint highlight siempre desactivado**: `_highlightImage.enabled = false` se llamaba en `SetOccupied` y al salir de modo colocación. Corregido: `enabled` es siempre `true`, solo cambia el color.
- **Popup de inventario aparecía lejos del ítem**: el cálculo de posición mezclaba espacios de coordenadas distintos. Eliminado el posicionamiento dinámico; el popup aparece en la posición fija asignada en el editor de Unity.
- **InventoryItemView sin botón serializado**: el campo `_selectButton` fue eliminado del prefab. Corregido implementando `IPointerClickHandler` directamente en el componente.
- **ExitPlacementMode en tab Tienda causaba pantalla negra**: la llamada añadía un `CanvasGroup` con alpha=0 por defecto. Eliminada la llamada a `ExitPlacementMode` del handler del tab de tienda.
- **HideAllDialogs en tab Tienda ocultaba el panel de tienda**: si algún campo de diálogo apuntaba al panel de tienda, `HideAllDialogs` lo ocultaba justo después de mostrarlo. Reemplazado por `HideInventoryItemPopup` únicamente en `_onShopTabClicked`.
- **Filtro de tienda ocultaba todos los ítems en primer uso**: `_shopShowingAccessories = false` (defecto) mostraba solo Decoración; si todos los ítems del usuario eran accesorios, la tienda aparecía vacía. Corregido con `bool? _activeShopFilter = null` (null = mostrar todos).
- **`ShowConfirmBuyDialog` sin información de monedas**: el diálogo de compra no mostraba el saldo actual ni el saldo resultante. Añadidos `_confirmBuyCurrentCoinsLabel` y `_confirmBuyAfterCoinsLabel`; el controller pasa `_cachedProfile.Coins` y `_cachedProfile.Coins - item.coinCost`.
- **Botón "Mover" eliminado**: el flujo de mover un ítem ya no existe. Para mover: guardar en inventario y volver a colocar. Eliminados `_moveButton`, `OnMoveConfirmed` y `_onMoveConfirmed` de View y Controller.

## Bug pendiente — Tab Tienda oculta todo (jerarquía Inspector)

**Síntoma**: al pulsar el tab de Tienda se oculta todo excepto la BottomNavBar. Al volver desde otra sección con la tienda ya abierta, sí se ve.

**Causa probable**: `_shopPanel` es hijo de `_roomPanel` en la jerarquía de la escena. `_roomPanel.SetActive(false)` desactiva toda la jerarquía (incluyendo `_shopPanel`); el posterior `_shopPanel.SetActive(true)` pone el flag interno a true pero el padre sigue inactivo, así que no se renderiza nada. Al volver desde otra sección, `_shopPanel.activeSelf` sigue siendo true y como el padre se reactiva junto con la pantalla, se ve correctamente.

**Solución**: en Unity Editor, asegurarse de que `Panel_Room` y `Panel_Shop` son **hermanos** (hijos del mismo padre), no padre/hijo:
```
SafeZoneScreen
├── Panel_Room    ← _roomPanel
└── Panel_Shop    ← _shopPanel
```

## Bugs resueltos — auditoría técnica (sesión 2)

- **Magic strings en `Notes`**: `"restored_from_firestore"` y `"restored_from_firestore_history"` reemplazados por `RecordSource` enum (`User`, `RestoredFirestore`, `RestoredFirestoreHistory`). El campo `Source` en `EmotionRecord` se migra automáticamente en bases de datos existentes (valor por defecto 0 = `User` correcto para todos los registros previos).
- **Comentarios `(UTC)` incorrectos**: `EmotionRecord.Timestamp`, `DataRepository.GetEmotionsForPeriod`, `GetTodayEmotion` y `UserProfile.CreationDate` decían "(UTC)" siendo todo hora local. Corregidos.
- **`SaveEmotion` con `Debug.Log` + StackTrace en producción**: eliminado el log de diagnóstico que imprimía el stack trace completo en cada guardado emocional.
- **`GetCurrentStreak` / `GetLongestStreak` / `GetAllEmotionDates` cargaban toda la tabla**: refactorizados para usar un helper `_getDistinctDates()` compartido. La optimización vía `SELECT DISTINCT date(Timestamp)` se descartó porque `sqlite-net-pcl` almacena `DateTime` como ticks enteros, incompatibles con la función `date()` de SQLite (devuelve NULL → crash en `ParseExact`). Se mantiene la carga en memoria con LINQ.
- **`AddCoins` no era atómica**: reemplazado el ciclo read-modify-write por `UPDATE UserProfiles SET Coins = Coins + ? WHERE Id = ...` atómico en SQL.
- **`SpendCoins` podía decrementar con saldo insuficiente en concurrencia**: el UPDATE ahora incluye `AND Coins >= ?` como condición, haciendo la operación más robusta.
- **`SafeAreaHandler.Update()` ejecutándose cada frame**: reemplazado por `OnRectTransformDimensionsChange()` que solo se llama al cambiar dimensiones de pantalla/orientación.
- **`EventBus` cancelaba suscriptores restantes si uno lanzaba excepción**: los métodos `Emit*` ahora iteran `GetInvocationList()` con try/catch individual por suscriptor.

## Auditoría de calidad completada — código limpio

- 0 instancias de `FindObjectOfType` / `Find`
- 0 `async void` peligrosos (todos convertidos a fire-and-forget wrappers)
- 0 listeners de botones sin limpiar en `OnDestroy`
- 0 `GetComponent` en bucles repetitivos (todos cacheados en `Awake`)
- 0 suscripciones `EventBus` sin desuscribir
- 0 `Instantiate` sin `worldPositionStays = false`
- 0 `DateTime.UtcNow` en todo el proyecto
- 0 limpieza de contenedores UI sin `SetActive(false)` + `ForceRebuildLayoutImmediate`
- 0 magic strings como metadatos técnicos (sustituidos por `RecordSource` enum)
- `Update()` solo donde es estrictamente necesario (`AppStateMachine`); `SafeAreaHandler` usa `OnRectTransformDimensionsChange`
- `FormatStreak` centralizado en `ChartsCalculator`
- Operaciones de monedas atómicas o condicionalmente atómicas en SQL
