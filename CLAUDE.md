# CLAUDE.md

Guía para Claude Code en este repositorio.

> Documentación detallada en `docs/`:
> - `docs/SYSTEMS.md` — sistemas, EventBus, Firebase, UI components, prefabs
> - `docs/FEATURES.md` — features Auth, EmotionCheck, Diary, Minigames, Charts, Settings
> - `docs/DATA_MODELS.md` — modelos SQLite, enumeraciones, ScriptableObjects
> - `docs/MINIGAMES.md` — infraestructura base y minijuegos pendientes
> - `docs/BUGS.md` — bugs resueltos, pendientes de build y auditoría de calidad

---

## Proyecto

**Lutra** — aplicación móvil de bienestar emocional. Unity 6000.4.1f1, URP 2D, targets Android e iOS.

## Entorno de desarrollo

- **Unity**: 6000.4.1f1 (gestionar con Unity Hub)
- **IDE**: Visual Studio 2022 Community o JetBrains Rider
- **Abrir proyecto**: Unity Hub → Add → seleccionar esta carpeta
- **Build**: File > Build Settings → Android o iOS → Build (perfiles en `Assets/Settings/Build Profiles/`)
- **Tests**: Window > General > Test Runner

## Arquitectura central

### Patrones
- **MVC** por feature: cada carpeta en `Features/` tiene su Model, View y Controller
- **Service Locator**: punto de acceso global a servicios (no Singleton directo)
- **BaseService**: clase abstracta base de todos los servicios MonoBehaviour; se auto-registra y desregistra en `ServiceLocator`
- **ScriptableObjects** como datos de configuración y como canales de eventos
- **UIScreen**: clase base abstracta para todas las pantallas; `OnScreenFocused` / `OnScreenUnfocused` son los puntos de entrada de cada pantalla

### Sistemas clave

| Sistema | Responsabilidad |
|---|---|
| `AppStateMachine` | Estados globales; `TransitionTo(AppState)` es el único punto de navegación |
| `EventBus` | Comunicación desacoplada; suscribir en `OnEnable`, desuscribir en `OnDisable` |
| `DataRepository` | Capa de persistencia sobre SQLite; todas las operaciones son `async/await` |
| `AuthManager` | Firebase Authentication: registro, login, logout, recuperación |
| `FirestoreManager` | Persistencia remota; SQLite es la fuente local |
| `ThemeManager` | `ApplyTheme(emotion, animate=true)`; `SetActiveCulture(CultureType)` |
| `MascotController` | Búho animado con `Animator`; hashes pre-cacheados en `Awake()` |
| `MinigameLoader` | Carga y descarga minijuegos con Additive Scene Loading |
| `StreakManager` | `RegisterCheckIn`, `GetCurrentStreak`, `HasCheckedInToday`; hitos 7/14/30 días |
| `RewardSystem` | Evalúa y otorga recompensas tras cada check-in emocional |
| `SettingsManager` | Preferencias de usuario; escribe `ColorblindFeature.CurrentMode` en Awake y al cambiar ajustes |
| `ColorblindFeature` | URP ScriptableRendererFeature; propiedad estática `CurrentMode`; shader `Assets/Shaders/Colorblind.shader` |
| `NotificationManager` | Recordatorio diario y aviso de racha en peligro |
| `StreakWarningChecker` | Programa aviso si racha ≥ 3 días sin check-in |
| `ScreenManager` | `NavigateTo` / `NavigateBack`, historial de pantallas |

### Estados de la aplicación (AppState)

```
Splash, Login, Register, OnboardingProfile, EmotionCheck,
MainMenu, Diary, SafeZone, Minigames, MinigameActive, Charts, Settings
```

### Flujo de navegación principal

```
Login correcto
  ├── Sin perfil local → Firestore → sin perfil → OnboardingProfile
  ├── Sin perfil local → Firestore → perfil restaurado → sincronizar diario → ver check-in
  └── Con perfil → sincronizar diario desde Firestore
        ├── Check-in hecho hoy → MainMenu
        └── Sin check-in hoy → EmotionCheck

Registro correcto → OnboardingProfile → EmotionCheck → MainMenu
```

### Emociones (8, ordenadas por valencia)

`Anxiety=0, Overwhelm=1, Frustration=2, Sadness=3, Nostalgia=4, Calm=5, Energy=6, Joy=7`

Cada emoción tiene un `EmotionTheme` ScriptableObject en `Core/Data/ScriptableObjects/`.

## Estructura de carpetas

```
Assets/
├── _Project/
│   ├── Core/
│   │   ├── Architecture/  (GameManager, AppStateMachine, ServiceLocator, BaseService, IService, IAppState)
│   │   ├── Data/
│   │   │   ├── Models/    (EmotionRecord, DiaryEntry, MinigameSession, UserProfile, enums...)
│   │   │   ├── Persistence/ (DatabaseManager, DataRepository)
│   │   │   └── ScriptableObjects/ (EmotionTheme, CultureColorOverride, MinigameDefinition, EmotionMinigameMap)
│   │   ├── Events/        (EventBus)
│   │   ├── Systems/       (AuthManager, FirestoreManager, RewardSystem, StreakManager,
│   │   │                   NotificationManager, SettingsManager, StreakWarningChecker)
│   │   └── Utils/         (LutraBootstrapTest — eliminar antes del build)
│   ├── Features/
│   │   ├── Auth/          (Login, Register, OnboardingProfile)
│   │   ├── EmotionCheck/
│   │   ├── Diary/
│   │   ├── SafeZone/
│   │   ├── Minigames/
│   │   ├── Charts/
│   │   ├── Settings/
│   │   ├── MainMenu/      (MascotController)
│   │   └── Onboarding/    (legacy, mantener por compatibilidad)
│   ├── Minigames/         (IMinigame, MinigameBase, MinigameLoader, MinigameResult)
│   └── UI/
│       ├── Screens/       (UIScreen, ScreenManager, MainMenuScreen, ChartsScreen)
│       ├── Components/    (BottomNavBar, SafeAreaHandler, ToastNotification, StreakIconController...)
│       └── Theme/         (ThemeManager)
├── Plugins/SQLite/
└── Scenes/Main.unity      (única escena; minijuegos como escenas aditivas)
```

## Dependencias

| Paquete | Uso |
|---|---|
| `sqlite-net-pcl` (Plugins/SQLite/) | ORM SQLite local |
| `com.unity.nuget.newtonsoft-json` | JSON en UserProfile y EmotionRecord |
| `TextMeshPro` | Todos los textos de la UI |
| Firebase Auth + Firestore + Analytics | SDK instalado via `.unitypackage` |
| URP 2D | Pipeline de renderizado |

- `LutraCore.asmdef` referencias: `SQLiteNet`, `Unity.TextMeshPro`
- `SQLite.asmdef` con `allowUnsafeCode: true`
- `sqlite3.dll` nativo en `Assets/Plugins/SQLite/Native/Windows/x86_64/`

## Convenciones de código

- **Idioma**: código en inglés, comentarios y logs en español
- **Clases**: `PascalCase`; métodos privados: prefijo `_` — `_miMetodo()`
- **Namespaces**: `Lutra.Core.*`, `Lutra.Features.*`, `Lutra.UI.*`, `Lutra.Minigames`
- **Servicios con patrón lazy**:
  ```csharp
  private DataRepository _repo;
  private DataRepository Repo => _repo ??= ServiceLocator.Get<DataRepository>();
  ```
- **Referencias**: cachear en `Awake()`, nunca `Find()` en `Update()`
- **BD**: todas las operaciones de `DataRepository` son `async/await`
- **Eventos**: suscribir en `OnEnable`, desuscribir en `OnDisable`
- **`async void` PROHIBIDO** salvo lifecycle Unity. Patrón obligatorio:
  ```csharp
  private void _onAlgo() => _ = _safeAlgo();
  private async Task _safeAlgo()
  {
      try { await HacerAlgo(); }
      catch (Exception ex) { Debug.LogError($"[Clase] {ex.Message}"); }
  }
  ```
- **DateTime**: `DateTime.Now` para timestamps, `DateTime.Today` para comparar fechas. **Prohibido `DateTime.UtcNow`**
- **`FormatStreak`**: usar siempre `ChartsCalculator.FormatStreak(int)`, nunca reimplementar
- **`FormatDuration`**: usar `ChartsCalculator.FormatDuration(int seconds)`
- **`RecordSource`**: usar el enum `RecordSource` para distinguir registros de usuario de placeholders Firestore. Nunca usar strings literales en el campo `Notes` como metadatos técnicos.
- **`AddCoins`**: UPDATE atómico en SQL; nunca hacer ciclo read-modify-write para sumar monedas.
- **`EventBus.Emit*`**: cada método itera `GetInvocationList()` con try/catch individual; una excepción en un suscriptor no cancela los demás.

### Patrón obligatorio para limpiar e instanciar en contenedores UI

```csharp
private void _clearContainer(Transform container)
{
    if (container == null) return;
    for (int i = container.childCount - 1; i >= 0; i--)
    {
        var child = container.GetChild(i);
        child.gameObject.SetActive(false);  // síncrono; LayoutGroup ignora inactivos inmediatamente
        Destroy(child.gameObject);
    }
}

private void _populateContainer(Transform container, List<T> items)
{
    _clearContainer(container);
    // ... Instantiate(prefab, container, false) por cada item ...
    if (container is RectTransform rt)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        if (rt.parent is RectTransform parentRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
    }
}
```

### Patrón para Instantiate en UI

```csharp
Instantiate(prefab, container, false);  // worldPositionStays = false — SIEMPRE
```

## Estado actual del proyecto

Todos los sistemas core y features están completados y sin errores de compilación.
Ver `docs/SYSTEMS.md` y `docs/FEATURES.md` para detalles de cada archivo.

**Pendiente antes del build final** (ver `docs/BUGS.md`):
- Eliminar `LutraBootstrapTest.cs`
- Eliminar `UnlockedItem.cs` (reemplazado por `InventoryItem`; existe pero no se usa)
- Investigar bug de tab Tienda en SafeZone (ver sección SafeZone más abajo)
- **[EDITOR]** Añadir `ColorblindFeature` al `Renderer2D.asset`: Project → Settings/Renderer2D → Add Renderer Feature → Colorblind Feature → asignar shader `Lutra/Colorblind`

**Minijuegos pendientes**: 8 por implementar (ver `docs/MINIGAMES.md`).

---

## SafeZone — detalles de implementación

### Archivos principales

| Archivo | Rol |
|---|---|
| `SafeZoneView.cs` | Vista MVC; todos los campos UI son `[SerializeField]`; no contiene lógica de negocio |
| `SafeZoneController.cs` | Controlador; se suscribe a eventos de la vista en `OnEnable`/`OnDisable` |
| `PlacementPoint.cs` | Punto de colocación en la habitación; `IDropHandler` + `IPointerClickHandler` |
| `DraggableItem.cs` | Drag & drop de ítems del inventario; `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler` |
| `InventoryItemView.cs` | Icono en la barra de inventario; implementa `IPointerClickHandler` directamente (sin Button serializado) |
| `ShopItemView.cs` | Ítem en la tienda; contiene el botón de compra |
| `SafeZoneItem.cs` | ScriptableObject con: `itemId`, `displayName`, `previewSprite`, `prefab`, `coinCost`, `requiredStreakDays`, `category` (ItemCategory), `placementType`, `isUnlockedByDefault` |

### Enumeraciones relevantes

```csharp
enum ItemCategory    { Furniture, Plant, Decoration, WallItem, MascotAccessory }
enum PlacementType   { None, ... }
```

### Flujo de compra

1. Usuario pulsa ítem en tienda → `OnBuyRequested(item)` → controller llama `ShowConfirmBuyDialog(item, currentCoins, coinsAfter)`
2. `ShowConfirmBuyDialog` rellena: nombre, precio, monedas actuales (`_confirmBuyCurrentCoinsLabel`), monedas restantes (`_confirmBuyAfterCoinsLabel`)
3. Confirmar → `_safeBuyConfirmed()` → `Repo.SpendCoins` → `RefreshView`

### Flujo de colocación (inventario → habitación)

1. Tap en ítem del inventario → popup flotante (Colocar / Vender) en posición fija del editor
2. "Colocar" → `OnPlaceRequested` → `EnterPlacementMode(placementType)`
3. Los `PlacementPoint` se colorean: verde = válido, rojo = inválido
4. Tap en punto válido → `OnPlacementConfirmed(index)` → `Repo.SetItemPlacement`
5. Drag & drop alternativo: `DraggableItem` suelta sobre `PlacementPoint` → `OnDropReceived`

### Flujo contextMenu de ítem colocado (habitación)

- Tap en `PlacementPoint` ocupado → `OnRoomItemTapped(index)` → `ShowRoomItemOptions(item)`
- Panel_ItemContextMenu tiene: **Guardar** (`_removeButton`) y **Vender** (`_sellButton`)
- No existe botón "Mover" — para mover: guardar en inventario y volver a colocar
- "Vender" → `OnSellPreviewRequested` → `ShowSellConfirmDialog(item, coinCost/2)`

### Colores de PlacementPoint

| Estado | Color |
|---|---|
| Vacío | Blanco |
| Ocupado | Azul (0.3, 0.6, 1.0, 0.55) |
| Válido durante drag | Verde (0.0, 0.8, 0.0, 0.55) |
| Inválido durante drag | Rojo (1.0, 0.0, 0.0, 0.45) |

`_highlightImage` **nunca** se desactiva (`enabled` siempre `true`); solo cambia su color.

### Filtro de tienda

`_activeShopFilter: bool?` — `null` = mostrar todos (defecto al abrir), `false` = solo Decoración, `true` = solo Accesorios. Solo se filtra al pulsar explícitamente un tab de categoría.

### Pitfalls importantes (bugs ya resueltos)

**Unity fake-null con `??`**
`GetComponent<T>() ?? AddComponent<T>()` falla porque el fake-null de Unity no es C# null. Siempre usar:
```csharp
var cg = GetComponent<CanvasGroup>();
if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
```

**UIScreen.Show() debe resetear alpha**
`PlayExitAnimation()` deja el `CanvasGroup.alpha` en 0. `Show()` debe forzar `alpha = 1f` antes de marcar `IsVisible = true`.

**DraggableItem: null-guard obligatorio en todos los handlers**
Si `Setup()` no se llamó (prefab con componente duplicado), los handlers de drag deben hacer `return` en lugar de lanzar excepción:
```csharp
if (_rectTransform == null || _canvasGroup == null || _rootCanvas == null) return;
```

**Bug pendiente — Tab Tienda oculta todo**
Al pulsar el tab de Tienda, `_roomPanel.SetActive(false)` puede apagar también `_shopPanel` si este es hijo de `_roomPanel` en la jerarquía. La jerarquía correcta es que sean **hermanos** (hijos del mismo padre), no padre/hijo:
```
SafeZoneScreen
├── Panel_Room    ← _roomPanel
└── Panel_Shop    ← _shopPanel
```
Si están anidados incorrectamente, `SetActive(false)` en el padre desactiva la jerarquía completa y el `SetActive(true)` posterior en el hijo no tiene efecto.
