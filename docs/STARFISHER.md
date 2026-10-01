# StarFisher — Pescador de Estrellas

> Estado: **código completo; pendiente montar la escena** (guía en el apartado 14), el arte y el audio.
> Emociones objetivo: tristeza, soledad. `MinigameType.StarFisher`.
> Sin arte asignado todo funciona con estrellas y brillos generados en tiempo de ejecución.

## 1. Concepto

Un astronauta sentado en una silla plegable sobre la Luna lanza su caña al espacio para pescar
estrellas. Cada estrella tiene su propio arte, historia y una frase con la que el jugador puede
identificarse. Las estrellas **siempre se liberan**: lo que se conserva es el registro en la
colección (el "librito"). Formato vertical; referencia visual: ilustración del astronauta (Tierra,
galaxias, nebulosa y vía láctea al fondo, Luna con cráteres en primer plano).

## 2. Estructura de la partida

- **5 lanzamientos** por partida; al terminar el 5º → `EndGame(completedNaturally: true)` → PostMinigame.
- Cada lanzamiento acaba en: **estrella pescada** o **estrella perdida** (escapó al picar o durante la recogida).
- **Abandono**: la sesión no se guarda y no hay monedas (comportamiento estándar de `MinigameLoader`),
  pero **las estrellas ya pescadas se quedan en la colección**, que se guarda en el momento de pescarlas.

## 3. Ciclo de un lanzamiento

### 3.1 Lanzar (pose 1 del astronauta: lanzando)
- Mantener pulsado → aparece una **barra de fuerza que oscila** (sube y baja).
- Soltar → se lanza con la fuerza del momento. **Dirección siempre fija** (hacia la izquierda y arriba).
- La fuerza decide la **distancia**: cuanto más lejos, mejores opciones de rareza.
- **Lanzamiento clavado** (zona "perfecta" de la barra) → bonificación extra de rareza.

### 3.2 Espera (pose 2: esperando)
- Duración aleatoria de **5 a 10 s**.
- Dentro de una **zona segura** delimitada, el jugador puede mover la caña (y el anzuelo) con el
  dedo, **lentamente** (con suavizado; el anzuelo sigue al dedo con retraso).
- Aparecen **zonas brillantes** en el espacio; mantener el anzuelo dentro de ellas **aumenta la
  probabilidad de rareza**.
- **Picada**: aviso visual + vibración (+ sonido, pendiente). El jugador tiene **2 s** para tocar la pantalla.
  - Si no toca a tiempo: la estrella se escapa con un mensaje suave, sin castigo visual. Ese
    lanzamiento cuenta como perdido (ya no se puede alcanzar el 100 %).

### 3.3 Recogida (pose 3: tirando)
- La **rareza se decide al picar**, pero el jugador no la conoce todavía (la estrella enganchada
  se ve como una estrella blanca genérica hasta pescarla).
- Tocar repetidamente la pantalla para tirar de la estrella. Número de toques según la rareza:

  | Rareza | Toques |
  |---|---|
  | Común | 5 |
  | Poco común | 7 |
  | Rara | 9 |
  | Épica | 12 |
  | Legendaria | 15 |

- **Revelación progresiva**: al empezar la recogida los bordes toman el color de Común. Cada vez que
  se supera el umbral de una rareza inferior (5, 7, 9, 12 toques) sin haberla pescado, se sabe que es
  al menos la siguiente: **golpe de zoom** (se acumula +0,07 por nivel) y **los bordes cambian al
  color de esa rareza** con un destello. Una legendaria pasa por los cinco colores.
- Una barra opcional muestra el tiempo que queda antes de que escape.
- La estrella se acerca visualmente con cada toque; el sedal se tensa.
- **Se puede perder**: si pasan **1,5 s sin tocar**, la estrella escapa.
- Al completarla: **pantalla en blanco 1–2 s** → pantalla de estrella.

### 3.4 Pantalla de estrella
- Muestra: arte, nombre, rareza, peso, edad, descripción/historia y frase.
- Indicador **"¡Nueva!"** si es la primera vez que se pesca (se registra en la colección).
- Botón **Soltar** → se oculta toda la interfaz; solo queda la estrella y el texto
  *"Arrastra hacia arriba para soltarla"*. El jugador la **arrastra hacia arriba** y la estrella
  vuelve al cielo. Después, siguiente lanzamiento.

## 4. Rarezas y probabilidades

| Rareza | Probabilidad base | Color (`StarCatalog.rarityColors`, provisional) |
|---|---|---|
| Común | 60 % | Blanco / gris claro |
| Poco común | 25 % | Verde |
| Rara | 10 % | Azul |
| Épica | 4 % | Morado |
| Legendaria | 1 % | Dorado |

Bonificación del lanzamiento (`StarRoller`):

```
bonificación = fuerza × 0,3 + (clavado ? 0,4 : 0) + min(1, segundos en zonas / 3) × 0,5
peso(rareza r) = pesoBase(r) × (1 + bonificación × r × 0,5)      (r = 0 Común … 4 Legendaria)
```

| Lanzamiento | Común | Poco común | Rara | Épica | Legendaria |
|---|---|---|---|---|---|
| Flojo, sin zonas | 59 % | 25 % | 10 % | 4 % | 1,1 % |
| A tope | 55 % | 26 % | 12 % | 5 % | 1,5 % |
| A tope y clavado | 49 % | 28 % | 14 % | 7 % | 2 % |
| A tope, clavado y 3 s en zonas | 44 % | 29 % | 16 % | 8 % | 2,5 % |

Elegida la rareza, la estrella concreta se sortea dentro de ella con **peso ×2 para las no
descubiertas**. Si una rareza no tiene estrellas en el catálogo se usa la más cercana.
Todos los valores se ajustan en el Inspector (`StarFisherTuning`).

## 5. Estrella especial de racha

- Estrella **nº 31**, **fuera del sorteo normal** (no cuenta en el reparto de las 30).
- Sale **cada vez** que la racha llega a **5 días** (el día en que `GetCurrentStreak() == 5`).
- Aparece en el **primer lanzamiento de la primera partida de ese día**. Se pesca **sí o sí**:
  en ese lanzamiento no puede escaparse (ni al picar ni en la recogida).
- Solo se puede conseguir **ese día**: si el jugador no juega a StarFisher ese día, la pierde
  hasta la próxima vez que alcance 5 días de racha.
- Para no darla dos veces el mismo día se mira `LastCaughtAt` de su fila en la colección (que ya
  se sincroniza con Firestore): si es de hoy, ya se entregó.
- Condición exacta: hay check-in hoy, `GetCurrentStreak() == streakStarDays` (5) y no se ha pescado hoy.
- En el libro aparece como `???` con la pista *"Se desbloquea en el 5º día de racha"*.

## 6. Contenido: 30 estrellas

Cada estrella es un `StarDefinition` (ScriptableObject):

| Campo | Descripción |
|---|---|
| `starId` | Identificador estable (se guarda en BD y Firestore) |
| `displayName` | Nombre |
| `rarity` | `StarRarity` |
| `sprite` / estilo | Arte propio de cada estrella (+ partículas / estela según rareza) |
| `weight` | Peso (fijo por estrella) |
| `age` | Edad (fija por estrella) |
| `description` | Descripción, vida e historia |
| `phrase` | Frase de identificación / apoyo emocional |
| `isStreakSpecial` | `true` solo para la estrella de racha |

Reparto de las 30: **12 comunes, 8 poco comunes, 5 raras, 3 épicas, 2 legendarias**,
más la especial de racha (nº 31).

## 7. Puntuación y monedas

- **Puntuación**: 20 puntos por estrella pescada → 5/5 = **100 %**. Cada estrella perdida resta
  esa parte (escape al picar o durante la recogida).
- **Monedas**: base **1 por estrella** (máx. 5 con estrellas normales), con bonificaciones por
  rareza hasta un **tope de 10**:

  | Rareza | Monedas |
  |---|---|
  | Común / Poco común | 1 |
  | Rara | 3 (2 raras + 3 comunes = 9; con 3 raras se llega al tope) |
  | Épica / Legendaria | 6 (1 épica + 4 comunes = 10) |

  Se devuelve en `MinigameResult.CoinReward`.

## 8. Colección — el librito

- Accesible desde: **botón en el propio minijuego** (solo entre lanzamientos; pausa la partida) y
  **libro fijo en la habitación de SafeZone** (un botón de la habitación, no un ítem del inventario).
- Lista de las estrellas ordenada por rareza con **filtro** (Todas + una por rareza) y contador
  ("12/30 descubiertas", solo cuentan las 30 normales).
- No descubiertas: `???` con la silueta oscura. La de racha muestra *"Se desbloquea en el 5º día de racha"*.
- Al tocar una estrella descubierta: nombre, peso, edad, rareza, descripción, frase y **veces pescada**.

## 9. Telescopio (SafeZone)

- Se consigue al **completar las 30 estrellas normales** (la de racha no cuenta) y se añade al
  **inventario** (SQLite + Firestore). Si por lo que sea no se entregó al pescar, SafeZone lo
  entrega al abrirse.
- Es un `SafeZoneItem` con `isRewardOnly` (no sale en la tienda ni se puede vender) e
  `interaction = StarSky`.
- Se coloca como cualquier mueble. Al tocarlo colocado, el menú contextual muestra un botón extra
  **"Mirar las estrellas"** (además de Guardar), que abre la **pantalla de cielo**: las estrellas
  descubiertas aparecen y desaparecen cada una a su ritmo y en sitios aleatorios.

## 10. Persistencia (todo en Firestore)

| Dato | SQLite | Firestore | Restauración |
|---|---|---|---|
| Colección | tabla `StarCollection` (`StarCollectionEntry`) | `users/{uid}/stars/{starId}`: `timesCaught`, `firstCaughtAt`, `lastCaughtAt` | `LoginController` → `StarCollectionStore.RestoreFromFirestoreAsync` (fusiona: mayor contador, fechas extremas) |
| Telescopio | `InventoryItems` | `inventoryItems` | ya existente |
| Colocación de muebles (**nuevo**, para todos los ítems) | `InventoryItem.PlacementIndex` | mapa `inventoryPlacements` { itemId: índice } | `LoginController._restorePlacementsFromFirestore` |
| Monedas (**nuevo**: antes solo se subían al guardar el perfil) | `UserProfile.Coins` | `coins` | con el perfil |

- Las capturas se guardan **al momento de pescar** (no al terminar la partida), en segundo plano y
  en orden (`StarCollectionStore._saveChain`).
- `FirestoreManager` escucha `EventBus.OnCoinsChanged` y sube el saldo leído de SQLite, así que
  cualquier cambio de monedas de la app queda sincronizado.

## 11. Arte y audio

- **Capas** (pendiente de confirmar con el equipo de diseño): fondo espacial, Tierra, Luna en primer
  plano, astronauta + silla (`Image` con 3 sprites: lanzando, esperando, tirando), caña (`Image`
  con el pivote en la empuñadura, gira sola) y anzuelo. El sedal lo dibuja el código
  (`StarFisherLine`, curva que se tensa).
- **30 artes de estrella** + la especial (`StarDefinition.sprite`). Sin sprite se usa una estrella
  generada teñida con `tint`.
- Opcionales: sprite de las zonas brillantes y de la viñeta (si no, se generan).
- **Audio**: pendiente.

## 12. Archivos

| Archivo | Rol |
|---|---|
| `Minigames/StarFisher/StarFisherController.cs` | `: MinigameBase`; máquina de fases (`StarFisherPhase`), estrella de racha, catálogo de prueba |
| `Minigames/StarFisher/StarFisherView.cs` | HUD, poses, barra de fuerza, picada, zoom + viñeta de rareza, destello blanco, ficha, liberación, libro y salida |
| `Minigames/StarFisher/StarFisherRod.cs` | Caña (ángulo), vuelo del anzuelo, seguimiento lento del dedo dentro de la zona segura, recogida |
| `Minigames/StarFisher/StarFisherLine.cs` | Sedal (`MaskableGraphic`, curva de Bézier con holgura) |
| `Minigames/StarFisher/StarFisherGlowZones.cs` | Zonas brillantes (pool de `Image`) y detección del anzuelo |
| `Minigames/StarFisher/StarFisherInput.cs` | Zona táctil: pulsar / arrastrar / soltar en coordenadas de la escena |
| `Minigames/StarFisher/StarFisherReleaseDrag.cs` | Arrastrar la estrella hacia arriba para liberarla |
| `Minigames/StarFisher/StarRoller.cs` | Sorteo de rareza y estrella (lógica pura) |
| `Minigames/StarFisher/StarFisherScoring.cs` | Puntuación, monedas y métricas |
| `Minigames/StarFisher/StarFisherTuning.cs` | Todos los parámetros (Inspector) |
| `Minigames/StarFisher/StarFisherEnums.cs` | `StarFisherPhase`, `AstronautPose` |
| `Minigames/StarFisher/Editor/StarFisherCatalogCreator.cs` | Menú *Lutra > StarFisher > Crear catálogo de ejemplo* |
| `Features/StarCollection/StarCollectionStore.cs` | Colección en memoria + guardado (SQLite, Firestore, telescopio) |
| `Features/StarCollection/StarCollectionBookController.cs` / `StarCollectionBookView.cs` / `StarBookEntryView.cs` | Libro de colección |
| `Features/StarCollection/StarSkyView.cs` | Cielo del telescopio |
| `Features/StarCollection/StarSpriteFactory.cs` | Sprites generados (estrella, brillo, viñeta) |
| `Core/Data/Models/StarRarity.cs`, `StarCollectionEntry.cs` | Enum de rareza y tabla SQLite |
| `Core/Data/ScriptableObjects/StarDefinition.cs`, `StarCatalog.cs` | Datos de las estrellas |

Cambios en código existente: `DataRepository` / `DatabaseManager` (tabla y métodos de la colección),
`FirestoreManager` (estrellas, colocaciones, monedas), `LoginController` (restauración),
`SafeZoneItem` (`isRewardOnly`, `interaction`), `SafeZoneView` / `SafeZoneController` (libro,
botón "Usar", telescopio, sincronización de colocaciones).

**Métricas persistidas**: `stars_caught`, `stars_escaped_bite`, `stars_escaped_reeling`, `new_stars`,
`perfect_casts`, `best_rarity` (0-4, −1 si ninguna), `glow_seconds`, `streak_star` (0/1).

## 13. Pendiente

1. **Montar la escena** y los paneles de SafeZone (apartado 14).
2. **Arte** (capas, 3 poses, 30 + 1 estrellas) y **textos** de cada estrella (nombre, peso, edad, historia, frase).
3. **Audio**.
4. Colores definitivos de cada rareza (`StarCatalog.rarityColors`).

## 14. Montaje en Unity (paso a paso)

**0. Datos**: menú **Lutra > StarFisher > Crear catálogo de ejemplo**. Crea en
`Core/Data/ScriptableObjects/StarFisher/` las 31 estrellas y `StarCatalog.asset`, y en
`SafeZoneItems/` el `Telescope.asset`. No sobrescribe nada que ya exista: se puede volver a lanzar.
Después, rellenar en cada estrella nombre, arte, peso, edad, descripción y frase (**no cambiar
`starId`** una vez publicado).

**1. Escena**: File → New Scene → *Empty*. Guardar como `Assets/Scenes/StarFisher.unity` y añadirla
a Build Profiles → Scene List. **No** añadir EventSystem (ya lo tiene `Main`).

**2. Raíz**: GameObject vacío `StarFisher` → Add Component → `StarFisherController`
(asignar `_catalog` = `StarCatalog`).

**3. Canvas** (hijo de la raíz): UI → Canvas, *Screen Space - Overlay*, **Sort Order = 10**, Canvas
Scaler *Scale With Screen Size* 1080×1920, Match 0. Hijos **en este orden** (los de abajo se dibujan
encima):

| # | Objeto | Componentes / ajustes |
|---|---|---|
| 1 | `Scene` | `RectTransform` a pantalla completa (anchors stretch, offsets 0, **pivote 0,5/0,5**): es lo que hace zoom. Todo lo de la escena va dentro |
| 1a | `Scene/Background`, `Earth`, `Moon` | `Image` (Raycast Target off) con las capas de fondo |
| 1b | `Scene/GlowZones` | `RectTransform` a pantalla completa + `StarFisherGlowZones` (`_space` = Scene, `_safeZone` = SafeZone, `_container` = él mismo) |
| 1c | `Scene/SafeZone` | `RectTransform` (sin imagen) que delimita por dónde puede moverse el anzuelo: la zona del cielo a la izquierda del astronauta |
| 1d | `Scene/CastNear`, `Scene/CastFar` | `RectTransform` vacíos dentro de la zona segura: dónde cae el anzuelo con fuerza mínima y máxima (dirección fija) |
| 1e | `Scene/Line` | a pantalla completa + `StarFisherLine` (`_from` = RodTip, `_to` = Hook), color blanco/azulado |
| 1f | `Scene/Hook` | `Image` pequeña del anzuelo; hijos `HookedGlow` (Image ~200×200, Raycast off) y `HookedStar` (Image ~90×90, Raycast off) |
| 1g | `Scene/Astronaut` | `Image` con la pose de lanzar |
| 1h | `Scene/Rod` | `Image` de la caña con el **pivote en la empuñadura** (en las manos del astronauta); hijo `RodTip` (RectTransform vacío en la punta) |
| 2 | `InputArea` | `Image` a pantalla completa, alfa 0, Raycast Target **on** + `StarFisherInput` (`_space` = Scene) |
| 3 | `Vignette` | `Image` a pantalla completa, Raycast Target off (el sprite se genera si se deja vacío) |
| 4 | `HUD` | `CanvasGroup`; dentro: `CastsLabel` (TMP arriba), `HintLabel` (TMP abajo), `MessageLabel` (TMP centrado), `BookButton` y `ExitButton` (esquinas) |
| 5 | `PowerBar` | Panel vertical junto al astronauta; hijos `Fill` (`Image` *Filled*, *Vertical*, origen *Bottom*) y opcional `PerfectZone` (Image fina, anchors x stretch; se coloca sola). Desactivado |
| 6 | `BiteIndicator` | "¡!" sobre el anzuelo o arriba + `TimerFill` (`Image` *Filled* radial). Desactivado |
| 7 | `ReelIdleFill` | Opcional: `Image` *Filled* horizontal (tiempo antes de que escape) |
| 8 | `RevealPanel` | Ficha: `Image` grande, TMP de nombre, rareza, peso, edad, descripción, frase y veces pescada, `NewBadge` ("¡Nueva estrella! Añadida a tu colección") y botón `Release` ("Soltar"). Desactivado |
| 9 | `ReleasePanel` | Transparente a pantalla completa; hijos `ReleaseStar` (`Image` ~350×350 en el centro, Raycast **on** + `StarFisherReleaseDrag`) y `ReleaseHint` (TMP). Desactivado |
| 10 | `WhiteFlash` | `Image` blanca a pantalla completa, Raycast off. Desactivada |
| 11 | `StarBook` | Libro de colección (ver paso 6) |
| 12 | `ExitConfirmPanel` | "¿Salir? Perderás el progreso de la partida (las estrellas pescadas se quedan en tu colección)" con `Yes` / `No`. Desactivado |

**4. View**: `StarFisherView` en el Canvas; asignar `_zoomRoot` = Scene, `_astronaut` + los 3 sprites
de pose, `_hud`, etiquetas, `_powerBar`/`_powerFill`/`_powerPerfectZone`, `_biteIndicator`/`_biteTimerFill`,
`_reelIdleFill`, `_vignette`, `_whiteFlash`, todos los campos de la ficha, `_releasePanel`,
`_releaseStar`, `_releaseDrag`, `_releaseHint`, `_bookButton`, `_exitButton` y el diálogo de salida.

**5. Rod**: `StarFisherRod` en `Scene` (o donde se quiera); asignar `_space` = Scene, `_rod`, `_rodTip`,
`_hook`, `_line`, `_hookedStar`, `_hookedGlow`, `_castNear`, `_castFar`, `_safeZone`. Ajustar
`_restAngle` y `_chargeBackAngle` (signo según hacia dónde mire la caña).

**6. Libro (prefab reutilizable)**: crear un prefab `StarCollectionBook`:
- Raíz (siempre activa, a pantalla completa) con `StarCollectionBookController` (`_catalog`,
  `_view`) y `StarCollectionBookView`.
- Hijo `Panel` (= `_root` de la vista, **desactivado**): fondo, `CloseButton`, `CounterLabel`, fila de
  filtros (`All` + 5 botones de rareza en orden Común → Legendaria), `ScrollView` cuyo `Content`
  (Vertical Layout Group + Content Size Fitter) es `_listContainer`, `EmptyLabel` y `DetailPanel`
  (desactivado: imagen, nombre, rareza, peso, edad, descripción, frase, veces pescada, botón cerrar).
- Prefab de fila: `Image` de fondo + `StarBookEntryView` (`_icon`, `_rarityStripe`, `_nameLabel`,
  `_subtitleLabel`, `_countLabel`), con Layout Element de altura fija. Asignarlo a `_entryPrefab`.
- Colocar una instancia en el Canvas de StarFisher (paso 3, fila 11) y asignarla a `_book` del
  controller; otra en la pantalla de SafeZone (paso 8).

**7. Registrar en Main**: `MinigameLoader._minigameScenes` → `type = StarFisher`, `sceneName = StarFisher`;
`_minigameDefinitions` → `Core/Data/ScriptableObjects/Minigames/StarFisher.asset` (ajustar
`estimatedTimeSeconds` y el nombre visible).

**8. SafeZone (escena Main)**:
- Añadir `Telescope.asset` al array `_allItems` de `SafeZoneController` (y darle `previewSprite` y `prefab`).
- En la habitación, un `Button` con el dibujo del libro → `SafeZoneView._starBookButton`.
- En `Panel_ItemContextMenu`, un botón nuevo "Mirar las estrellas" → `_useItemButton` (+ su TMP en
  `_useItemButtonLabel`). Se muestra solo en ítems con interacción.
- Instancia del prefab `StarCollectionBook` dentro de `SafeZoneScreen` (por encima del resto) →
  `SafeZoneController._starBook`.
- Panel `StarSky` a pantalla completa (fondo oscuro, **desactivado**) con `StarSkyView` (`_root` = él
  mismo, `_skyArea` = un hijo a pantalla completa, `_closeButton`, `_titleLabel`) →
  `SafeZoneController._starSky`. Asignar también `SafeZoneController._starCatalog`.

**9. Probar**: sin catálogo el controller usa 5 estrellas de prueba (no se guardan). Para probar
rápido, bajar en el Inspector `waitSeconds` (p. ej. 1-2) y `casts`; para ver rarezas altas, subir
`rarityWeights` de Épica/Legendaria. Para probar el telescopio, bajar el catálogo a pocas
estrellas en una copia del `StarCatalog`.
