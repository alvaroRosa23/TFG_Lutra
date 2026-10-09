# Centro de notificaciones

Bandeja dentro de la app, accesible desde el menú principal, que guarda las recompensas obtenidas, el resumen semanal y los avisos importantes (cuestionario WHO-5, recursos de apoyo).

> Estado: **implementado y montado en Unity** (fases 2, 3 y 4).

**No confundir** con `NotificationManager`, que programa **notificaciones push del sistema operativo** (recordatorio diario, aviso de racha). Los dos conviven:

| | `NotificationCenter` | `NotificationManager` |
|---|---|---|
| Dónde se ve | Dentro de Lutra (botón en el menú principal) | Bandeja del sistema del móvil |
| Persistencia | SQLite + Firestore, para siempre | La gestiona el sistema operativo |
| Contenido | Recompensas, resumen semanal, WHO-5 disponible, recursos de apoyo | Recordatorios para abrir la app |

---

## 1. Requisitos

1. Botón normal en el **menú principal**; cuando hay algo nuevo muestra una **"!"**.
2. **Cada recompensa** (monedas o ítems) obtenida en cualquier minijuego o acción genera una notificación.
3. Cuando el **WHO-5** está disponible aparece como notificación **importante, anclada arriba**, con la opción de hacerlo.
4. La notificación del WHO-5 **solo desaparece cuando se envía el cuestionario completo**. Si se cierra la app a medias, la notificación y la "!" persisten.
5. Completar el WHO-5 da una recompensa de 20 monedas (que también se notifica).
6. Cada lunes, un **resumen de la semana anterior**.
7. Las demás notificaciones **se guardan para siempre**.

---

## 2. Datos

### `AppNotification` (SQLite `Notifications`, Firestore `users/{uid}/notifications/{RemoteId}`)

```csharp
int              Id           // PK autoincrement
string           RemoteId     // GUID; determinista en las que no deben duplicarse entre dispositivos:
                              //   "who5-yyyy-MM-dd" (fecha de disponibilidad), "weekly-yyyy-MM-dd" (lunes)
DateTime         CreatedAt    // DateTime.Now  [Indexed]
NotificationType Type
string           Title
string           Body
int              Coins        // 0 si no aplica
string           ItemId       // nullable
RewardSource     Source       // None si no es una recompensa
string           SourceRef    // nullable: MinigameType, rewardId, días de racha…
bool             IsRead
bool             IsPinned     // true = sección "Importante"
DateTime?        ResolvedAt   // anclada completada → deja de mostrarse (se conserva en BD)
```

```csharp
enum NotificationType { Reward = 0, Who5Available = 1, Support = 2, WeeklySummary = 3 }
enum RewardSource     { None = 0, CheckIn = 1, StreakMilestone = 2, RewardDefinition = 3, Diary = 4,
                        Minigame = 5, StarCollection = 6, Who5 = 7 }
```

Se guardan como int: **no reordenar** los enums.

Campos en Firestore: `createdAt` (ISO 8601), `type`, `title`, `body`, `coins`, `itemId` (o null), `source`, `sourceRef` (o null), `isRead`, `isPinned`, `resolvedAt` (ISO 8601 o null).

### `DataRepository` (sección NOTIFICACIONES)

| Método | Uso |
|---|---|
| `SaveNotification(n, sync)` / `UpdateNotification(n, sync)` | Escritura + `CloudSync.PushNotification` |
| `GetNotificationByRemoteId(id)` | Evitar duplicados de las deterministas |
| `GetPinnedNotifications()` | Ancladas sin resolver |
| `GetNotificationsPage(offset, count)` | No ancladas y no resueltas, más recientes primero |
| `HasNotificationsNeedingAttention()` | Regla de la "!" (§4) |
| `MarkAllNotificationsRead()` | Marca leídas las no ancladas y las sube |
| `GetAllNotifications()` | Para la sincronización |

`DeleteAllData` vacía la tabla y `FirestoreManager.DeleteUserData` borra la subcolección.

### Sincronización (`CloudSync._syncNotifications`)

- Unión por `RemoteId`: lo que falta en local se descarga (`sync: false`) y lo que falta en remoto se sube.
- Si existe en los dos lados, **leída y resuelta ganan**: `IsRead = local || remoto`; si uno tiene `ResolvedAt`, el otro lo adopta (y deja de estar anclada). Si el remoto va por detrás, se vuelve a subir.
- Al terminar llama a `NotificationCenter.RefreshAttention()` para actualizar la "!".
- Reglas de Firestore: cubiertas por `match /users/{userId}/{document=**}` (`firestore.rules`); comprobado que se sincronizan.

---

## 3. Recompensas → notificaciones

```csharp
// Core/Data/Models/RewardGrant.cs — DTO en memoria
public class RewardGrant { RewardSource Source; int Coins; string ItemId; string Title; string Body; string SourceRef; }

// EventBus
public static event Action<RewardGrant> OnRewardGranted;
public static void EmitRewardGranted(RewardGrant grant);
```

Cada punto que entrega una recompensa emite el evento **justo después** de guardarla; `NotificationCenter` lo convierte en notificación. La reconciliación de monedas de `CloudSync` **no** emite nada (no es una recompensa nueva). **Toda recompensa nueva que se añada a la app debe emitir `EmitRewardGranted`.**

| Origen | Dónde se emite | Notificación |
|---|---|---|
| Check-in de Día | `StreakManager.RegisterCheckIn` | **Check-in del día** · +5 monedas |
| Hito de racha 7 / 14 / 30 | `StreakManager.RegisterCheckIn` (sustituye a la de check-in ese día) | **¡Racha de 7 días!** · +20 monedas |
| `RewardDefinition` | `RewardSystem._grantReward` | **{displayName}** · +N monedas y/o "un objeto nuevo para tu Zona Segura" |
| Diario | `DiaryController._grantDiaryReward` | **Has escrito en tu diario** · +5 monedas |
| Minijuego (solo si da monedas) | `MinigameLoader._onMinigameFinished` | **{nombre} completado** · +8 monedas (· ¡Nuevo récord!) |
| Colección de estrellas | `StarCollectionStore.GrantRewardIfCompleteAsync` | **¡Colección de estrellas completa!** · Has desbloqueado el telescopio para tu Zona Segura |
| WHO-5 | `Who5Controller` al enviar | **Cuestionario de bienestar completado** · +20 monedas |

Las ventas en la Zona Segura **no** generan notificación (no son recompensas).

> **Bug existente:** en los hitos de racha `StreakManager` emite `EmitRewardEarned("streak_reward_{n}d")`, pero ningún sistema escucha ese evento ni existen esos ítems, así que **la decoración nunca se entrega**. La notificación del hito solo menciona las monedas. Ver `docs/BUGS.md`.

---

## 4. Regla de la "!"

```
MostrarExclamación = existe anclada sin resolver  ||  existe no anclada sin leer
```

- Al abrir el centro, todas las **no ancladas** pasan a leídas (los puntos de "no leída" se ven durante esa visita).
- Las **ancladas** no se marcan como resueltas al verlas: la "!" sigue mientras haya una pendiente.
- `NotificationCenter` emite `EventBus.OnNotificationsChanged(bool needsAttention)` al crear, leer o resolver. `MainMenuScreen` se suscribe y además lo consulta en `OnScreenFocused`.

---

## 5. Notificación anclada del WHO-5

| Momento | Qué pasa |
|---|---|
| El WHO-5 pasa a estar disponible | `Who5Scheduler.CheckAvailability()` → `NotificationCenter.CreatePinned("who5-{fecha}", Who5Available, …)`; si ese id ya existe, no se duplica. El push del sistema se programó al enviar el anterior |
| El usuario abre el centro | Aparece en **Importante**, arriba del todo, con el botón **Hacer ahora** |
| Empieza y sale sin enviar / cierra la app | Nada cambia: sigue anclada y la "!" sigue visible |
| Envía el cuestionario completo | `NotificationCenter.ResolvePinned("who5-{fecha}")` → `ResolvedAt`, deja de estar anclada y desaparece; recompensa → notificación nueva |
| Lo completa en otro dispositivo | Al sincronizar llega `ResolvedAt` y desaparece también aquí |

---

## 6. Interfaz

### 6.1 Botón en el menú principal (`MainMenuScreen`)

- `[SerializeField] Button _notificationsButton` → `TransitionTo(AppState.Notifications)`.
- `[SerializeField] GameObject _notificationsBadge` → círculo con "!"; se activa según §4.
- En `OnScreenFocused`: `CheckWeeklySummary()` y luego `NeedsAttention()`.

### 6.2 Pantalla (`Features/Notifications/`, `AppState.Notifications`)

| Clase | Rol |
|---|---|
| `NotificationsScreen` | `UIScreen`; en `OnScreenFocused` llama a `NotificationsController.OpenNotifications()` |
| `NotificationsController` | Carga ancladas + primera página, marca leídas, pagina; "atrás" → MainMenu; acciones: "Hacer ahora" (`Who5Available` → `AppState.Who5`) y "Ver recursos" (`Support` → `SupportDialog.ShowResources`) |
| `NotificationsView` | Pinta la sección "Importante", la lista agrupada y el estado vacío; eventos `OnBackRequested`, `OnLoadMoreRequested`, `OnActionRequested(AppNotification)` |
| `NotificationCard` | Tarjeta: icono, título, cuerpo, hora, punto de no leída, botón de acción opcional |
| `NotificationTimeFormatter` | Clase pura: cabeceras ("Hoy", "Ayer", "Esta semana", "3 de octubre") y hora ("18:42", "lunes, 18:42") |

```
┌──────────────────────────────┐
│ ←  Notificaciones            │
├──────────────────────────────┤
│ IMPORTANTE                   │  ← _pinnedSection (oculta si no hay ancladas)
│ ┌──────────────────────────┐ │
│ │ Cuestionario de bienestar│ │  ← _pinnedCardPrefab
│ │ ~1 minuto  [Hacer ahora] │ │
│ └──────────────────────────┘ │
│ Hoy                          │  ← _groupHeaderPrefab
│ ● Breath Jump completado     │  ← _cardPrefab (● = _unreadDot)
│   +8 monedas · 18:42         │
│ Ayer                         │
│   Check-in del día  …        │
│        [ Cargar más ]        │  ← _loadMoreButton
└──────────────────────────────┘
```

- Páginas de 30 (`NotificationCenter.PageSize`); se pide una más para saber si hay otra página.
- Iconos opcionales por tipo/origen en `NotificationsView` (si se dejan vacíos se usa el del prefab).
- La barra inferior se oculta en esta pantalla (`BottomNavBar`), como en Ajustes.
- Contenedores con el patrón obligatorio `_clearContainer` / rebuild de CLAUDE.md.

---

## 7. Servicio `NotificationCenter` (`Core/Systems/`, `BaseService`)

`GameManager._registerServices` lo crea con `AddComponent` en su propio GameObject si no está en la escena y lo registra: **no hace falta añadirlo a mano**.

```csharp
const int PageSize = 30;
Task<bool>                  NeedsAttention();
Task<List<AppNotification>> GetPinned();
Task<List<AppNotification>> GetPage(int offset, int count = PageSize);
Task                        MarkAllRead();
Task<AppNotification>       CreatePinned(string remoteId, NotificationType type, string title, string body);
Task                        ResolvePinned(string remoteId);
Task                        AddSupport(string trigger);    // protocolo de apoyo; trigger → SourceRef
Task                        CheckWeeklySummary();   // idempotente; reutiliza la comprobación en curso
void                        RefreshAttention();     // recalcula y emite la "!"
```

- Se suscribe a `OnRewardGranted` en `OnEnable` y se da de baja en `OnDisable`.
- `ServiceLocator.TryGet<T>(out T)` permite usarlo como servicio opcional (lo usa `CloudSync`).

---

## 8. Casos límite

| Caso | Comportamiento |
|---|---|
| Sin conexión | Todo es local; `CloudSync` encola las subidas |
| Dos dispositivos crean el WHO-5 o el resumen a la vez | Mismo `RemoteId` determinista → una sola notificación |
| Reinstalación / nuevo dispositivo | Se restauran desde Firestore al iniciar sesión |
| Recompensa restaurada al sincronizar | No se crea notificación (solo se notifica la entrega en el momento) |
| Cerrar sesión / borrar datos | `DeleteAllData` vacía la tabla |
| Muchas notificaciones en Firestore | `SyncAllAsync` las descarga todas; si crece demasiado, descargar solo las posteriores a la última sincronización (optimización futura) |

---

## 9. Resumen semanal

Sustituye a `ChartsController.GenerateWeeklyReport()`, que no se usaba. Lo construye `WeeklySummaryBuilder` (`Features/Charts/`, clase pura con tests).

- **Cuándo:** `NotificationCenter.CheckWeeklySummary()` al enfocar el menú principal. Resume la semana anterior (lunes–domingo) si aún no existe su notificación (`RemoteId = "weekly-{lunes}"`).
- **Requisito:** al menos **3 check-ins de Día** del usuario en esa semana (sin placeholders). Si no, no se publica.
- **No anclada**; cuenta como no leída (enciende la "!").
- **Contenido** (ejemplo):

  > **Tu resumen semanal**
  > Semana del 28 sep al 4 oct: tu ánimo medio fue 3,8 de 5, mejor que la semana anterior. La emoción que más registraste fue calma. Lo que más te ayudó: Breath Jump.

  | Parte | Regla |
  |---|---|
  | Ánimo medio | Media de los check-ins de Día |
  | Tendencia | Solo si la semana anterior también tiene ≥ 3; diferencia ≥ +0,3 "mejor", ≤ −0,3 "algo más bajo", si no "parecido" |
  | Emoción | La más frecuente de todos los registros del usuario de la semana |
  | Minijuego | El de mayor mejora media de ánimo **entre partidas válidas** (`IsValidForMoodEffect`, ánimo previo ≤ 3 h) si es > 0; si no, "Tu minijuego más jugado" |
