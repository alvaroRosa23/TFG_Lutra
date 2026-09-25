# Minijuegos

## Estado actual

La infraestructura base está lista (`IMinigame`, `MinigameBase`, `MinigameLoader`, `MinigameResult`).

**Beatmaker**: implementado y montado (escena `Assets/Scenes/Beatmaker.unity`).

**BreathJump**: código completo; **pendiente montar la escena en el editor** (guía paso a paso en su sección).

El resto de carpetas de minijuegos están pendientes de implementar.

## Pendientes de implementar (en orden)

| Orden | Carpeta | Mecánica | Emociones objetivo |
|---|---|---|---|
| 1 | `Unpacking/` | Ordenar objetos | tristeza, agotamiento |
| 2 | `FruitNinja/` | Cortar objetos | frustración, ira |
| 3 | `Beatmaker/` | Secuenciador 8 pistas × 8 beats — **implementado** | tristeza, apatía |
| 4 | `SandCastle/` | Arena + acelerómetro | estrés |
| 5 | `FluidSim/` | Fluido interactivo | ansiedad |
| 6 | `BreathJump/` | Plataformas con respiración como control — **código listo, falta escena** | ansiedad, agobio |
| 7 | `Puzzle/` | Por definir | ansiedad, rumiación |
| 8 | `StarFisher/` | Astronauta pesca estrellas con frases | tristeza, soledad |

Crear las carpetas en `Assets/_Project/Minigames/`.

## Infraestructura base

### IMinigame
```csharp
MinigameType Type { get; }
bool IsPlaying { get; }
void Initialize(EmotionType emotionBefore);
void StartGame();
void PauseGame();
void ResumeGame();
void EndGame(bool completedNaturally);
event Action<MinigameResult> OnGameCompleted;
```

### MinigameBase
Clase base abstracta; implementa `IMinigame`.
- Campos protegidos: `_emotionBefore`, `_startTime`, `_isPlaying`, `_result`
- Método protegido: `BuildResult(float relaxationScore, bool completed, Dictionary<string,float> metrics = null)`

### MinigameLoader
Carga escena aditiva → busca `IMinigame` → `Initialize` → `StartGame` → `OnGameCompleted` → consulta récord previo → guarda `MinigameSession` en BD → `EmitMinigameCompleted` → monedas → `LastOutcome` → descarga escena → `TransitionTo(MinigameActive)` (PostMinigameScreen).

**Abandono**: si el resultado llega con `CompletedNaturally = false` (el jugador sale antes
de terminar un minijuego con final), no se guarda la sesión ni se dan monedas: se descarga
la escena y se vuelve directamente a `AppState.Minigames`. Los minijuegos sin final (Beatmaker)
terminan siempre con `completedNaturally: true`.

**Pausa automática**: `MinigameBase.OnApplicationPause` pausa la partida al pasar la app a
segundo plano y la reanuda al volver (solo si la pausa la provocó la app).

### MinigameResult (en memoria, no SQLite)
```csharp
MinigameType                Type
int                         DurationSeconds
float                       RelaxationScore     // 0.0 - 1.0
bool                        CompletedNaturally
EmotionType                 EmotionBefore
EmotionType                 EmotionAfter
Dictionary<string, float>   Metrics
DateTime                    StartTime
int?                        CoinReward          // null = recompensa genérica del loader
```

### Puntuación y monedas (común a todos los minijuegos)

- Cada minijuego calcula su puntuación **0–100** y la guarda como `RelaxationScore` (0,0–1,0).
  En pantalla se muestra con `MinigameOutcome.ToDisplayScore` (redondeo a entero).
- El **récord** de cada minijuego es la mejor puntuación histórica (`GetBestRelaxationScore`);
  PostMinigameScreen marca "nuevo récord" si la supera.
- **Monedas**: 1 moneda por cada 10 puntos mostrados (100 puntos = 10 monedas, máximo por partida).
  El minijuego las devuelve en `MinigameResult.CoinReward`; si lo deja a `null`, `MinigameLoader`
  usa la recompensa genérica antigua `Mathf.Max(3, estimatedTimeSeconds / 30)`.
- **Abandono** (`CompletedNaturally = false`): 0 puntos, 0 monedas, no se guarda la sesión y se
  vuelve a la lista de minijuegos (ver MinigameLoader).

## Input

Acciones en `Assets/InputSystem_Actions.inputactions`, action map `Player`:
`Move`, `Look`, `Attack`, `Interact` (hold), `Crouch`, `Jump`, `Sprint`, `Previous`, `Next`.

Estas acciones se usan en los minijuegos; la navegación de la app usa UI/botones.

## MinigameDefinition SO

Un asset por minijuego en `Core/Data/ScriptableObjects/`. Ver `docs/DATA_MODELS.md` para la estructura completa.

## EmotionMinigameMap SO

Mapea `EmotionType` → `MinigameTag[]` para filtrar recomendaciones. Ver `docs/DATA_MODELS.md`.

## Beatmaker (implementado)

Secuenciador estilo FL Studio: 8 pistas (Kick 1, Kick 2, Hat 1, Hat 2, Clap 1, Clap 2,
Snare 1, Snare 2 — 2 variaciones por estilo) × 8 steps. Cada step se puede activar por
separado en cada pista. Además hay loops instrumentales exclusivos (activar uno detiene el
anterior), un metrónomo que se activa/desactiva con un botón y un desplegable para cambiar
de `BeatmakerSoundPack` en caliente. Sin condición de fin: el usuario sale cuando quiere y
eso cuenta como partida completada con normalidad.

**Transporte**: solo corre mientras haya al menos un step activo. Al activar el primero
arranca desde el step 1; al desactivar el último se para (se ocultan las líneas y se corta el loop).

**Tempo (igual que el Channel Rack de FL Studio)**: cada cuadrado es un **step**
(semicorchea) y **4 steps = 1 beat** (`BeatmakerSoundPack.StepsPerBeat`). Un step dura
`60 / (bpm * 4)` s (`SecondsPerStep`), medido con `AudioSettings.dspTime`. Las 8 columnas
son 2 beats: a 80 BPM el patrón completo dura 1,5 s. El metrónomo suena en cada beat
(steps 1 y 5) con acento en el step 1; si no se asigna clip, se genera un "tic" sintético.
**Loops largos**: un loop ocupa N vueltas del patrón, con N = `round(duración del clip /
duración de la vuelta)` (mínimo 1). A 80 BPM (vuelta de 1,5 s), un loop de 1 compás (3 s)
ocupa 2 vueltas y uno de 2 compases (6 s), 4. Conviene que los clips duren un múltiplo
exacto de 2 beats para que no se note el corte al reiniciar.

**Líneas de reproducción**: `_playhead` recorre la rejilla (cruza el borde izquierdo de cada
step justo cuando suena, `CycleProgress`) y la línea del loop activo recorre su botón en las
N vueltas que dura el loop (`LoopProgress`). El loop se reinicia en el step 1 de la primera
vuelta de cada bloque de N (contando desde que arrancó el transporte); si se activa a mitad,
entra en la posición que le toca para seguir sincronizado.

**Cambio de pack**: actualiza el texto de BPM y los nombres de loop; si está sonando,
reinicia el ciclo desde el step 1 con el nuevo tempo, manteniendo el patrón.

### Archivos (`Assets/_Project/Minigames/Beatmaker/`)

| Archivo | Rol |
|---|---|
| `BeatmakerPatternState.cs` | Estado puro: rejilla `bool[8,8]` (pista × step) + loop activo; `InstrumentOf(track)` / `VariationOf(track)` |
| `BeatmakerMetricsTracker.cs` | Acumula la puntuación (0-100), las monedas y los récords mientras dura la partida |
| `BeatmakerAudioEngine.cs` | Transporte `dspTime`; `AudioSource` de beats, loop y metrónomo (se crean si faltan); `CycleProgress` |
| `BeatmakerTrackRow.cs` | Prefab de fila: etiqueta + 8 `Button` de step |
| `BeatmakerView.cs` | Vista pura; genera las 8 filas, loops, metrónomo, dropdown de pack, BPM, puntuación y líneas |
| `BeatmakerController.cs` | `: MinigameBase`; conecta patrón + audio + vista + métricas |

También: `BeatmakerInstrument` (enum `Kick, Hat, Clap, Snare`, `Core/Data/Models/`) y
`BeatmakerSoundPack` (ScriptableObject, `Core/Data/ScriptableObjects/`, con `bpm`,
2 clips por instrumento y `loopClips`/`loopNames`). Al iniciar se elige un
pack al azar de `_soundPacks`; el usuario puede cambiarlo con el desplegable.

### Puntuación (0–100) y monedas

La calcula `BeatmakerMetricsTracker` cada frame; `RelaxationScore` = puntos / 100.
Idea: cuanto más tiempo creando música, más puntos; hacer patrones ricos (más de 5 notas)
acelera; no tener nada puesto resta; y dejar el móvil sonando sin tocarlo deja de sumar.

| Regla | Efecto |
|---|---|
| Tiempo con ≥1 nota puesta | +2,5 puntos cada 30 s, de forma continua |
| Bonus: >5 notas puestas en la rejilla | +2,5 puntos por cada 30 s acumulados |
| Inactividad: ninguna nota puesta | −2 puntos por cada 30 s acumulados (no suma tiempo) |
| Límite suave: 2 min sin tocar ningún control | ni el tiempo ni el bonus suman hasta la siguiente interacción |

La puntuación se limita a [0, 100] en cada paso. Con bonus todo el rato el máximo se alcanza
en 10 min; solo con tiempo, en 20 min. Constantes ajustables en `BeatmakerMetricsTracker`
(`TimePoints`, `BonusPoints`, `BonusNoteThreshold`, `InactivityPenalty`, `IdleLimitSeconds`).

Detalles de funcionamiento:
- **"Notas"** = cuadrados activos en la rejilla 8×8 (`BeatmakerPatternState.ActiveStepCount`).
  Los loops, el metrónomo y el pack no cuentan como notas.
- **Los intervalos de 30 s son acumulados, no un reloj fijo**: el bonus suma el tiempo total
  con más de 5 notas y la penalización el tiempo total sin notas. Quitar una nota justo antes de
  los 30 s no hace perder el progreso del bonus… salvo que se quiten *todas* (entonces el
  contador de bonus se reinicia).
- **Sin deuda**: la penalización nunca baja de 0, así que empezar sin tocar nada no "hipoteca"
  los puntos que se ganen después.
- **Límite suave**: cualquier interacción (step, loop, pack, metrónomo) reinicia el contador
  de 2 min (`RegisterInteraction`). Pasado el límite la música sigue sonando, pero no suma.
- **Pausa** (app en segundo plano): no corre ningún contador.

Ejemplos: 10 min con más de 5 notas y tocando algo cada poco ⇒ 100 (10 monedas).
10 min con 3 notas (tocando algo cada poco) ⇒ 50 (5 monedas). 5 min con 6 notas y luego
10 min sin tocar nada ⇒ 70 (los 2 primeros minutos sin tocar aún suman; después, no).

**Monedas**: 1 por cada 10 puntos (100 ⇒ 10 monedas). El minijuego las devuelve en
`MinigameResult.CoinReward`; si un minijuego lo deja a `null`, `MinigameLoader` usa la
recompensa genérica `Mathf.Max(3, estimatedTimeSeconds / 30)`.

### Métricas persistidas (`MinigameSession.Metrics` → `MetricsJson`)

`max_layers`, `longest_groove` (segundos), `session_richness` (% tiempo con ≥2 sonidos),
`max_notes` (máximo de notas puestas a la vez).
Consultar el mejor valor histórico con `DataRepository.GetBestMetric(MinigameType, string)`.
El récord mostrado en el panel de detalle es la mejor puntuación (`RelaxationScore` × 100),
igual para todos los minijuegos (`MinigamesController._getRecordText`).

### Montaje en Unity (hecho)

1. Crear el asset `BeatmakerSoundPack` (o varios) con BPM y clips de audio asignados.
2. Crear el asset `MinigameDefinition` para `MinigameType.Beatmaker` y añadirlo al array
   `_allMinigames` de `MinigamesController`.
3. Montar la escena aditiva: prefab raíz con `BeatmakerController` + `BeatmakerView` +
   `BeatmakerAudioEngine`; prefab `BeatmakerTrackRow` (etiqueta + 8 botones) asignado a
   `_trackRowPrefab`, `_tracksContainer` con `VerticalLayoutGroup`, `_playhead` fuera del
   contenedor, botones de loop con su línea hija, botón de metrónomo, `TMP_Dropdown`, texto
   de BPM, texto de puntuación y botón de salida.
4. Registrar la escena en `MinigameLoader._minigameScenes` (`MinigameSceneData { type = Beatmaker, sceneName = "..." }`).

## BreathJump (código completo, pendiente escena)

La nutria avanza hacia la derecha, subiendo suavemente, por plataformas generadas de forma
procedural. Cada salto es **una respiración** con ritmo fijo **4-6** (inspirar 4 s, espirar 6 s).
No es un juego de habilidad: la dificultad es mínima y el objetivo es que seguir el ritmo sea
agradable. No hay game over.

### Mecánicas

**Ciclo de una respiración**

1. **Inspirar = mantener pulsado** (en cualquier punto de la pantalla, estando en una plataforma).
   La carga del salto sube de 0 a 1 en 4 s (`carga = tiempo pulsado / 4`, máx. 1). Mientras,
   la nutria se agacha (squash), los bordes de la pantalla se oscurecen (viñeta), la cámara hace
   un zoom leve hacia la nutria y el círculo guía se expande y pasa de **blanco a rojo**.
   A los 4 s el círculo está en **rojo máximo** (= hay que soltar) y el texto cambia a
   "Suelta y espira". Mantener más de 4 s no da más distancia (pero la respiración ya no es perfecta).
2. **Espirar = soltar**. La nutria salta y planea. La viñeta y el zoom se deshacen y el círculo
   se contrae y vuelve de rojo a **blanco** poco a poco durante los 6 s de espiración.
   Círculo blanco y pequeño = listo para inspirar otra vez.
3. El vuelo con carga completa dura **4,5 s** y aterriza en el **centro** de la siguiente
   plataforma. Los **1,5 s** restantes de espiración se pasan en la plataforma (texto "Espira...").
   Cuando el círculo termina de contraerse aparece "Mantén pulsado e inspira" y empieza la
   siguiente respiración.

Toques de menos de 1 s (`minInhaleToJump`) se ignoran: no hay salto (evita saltos accidentales).

**El salto** (cinemático, sin Rigidbody, para que sea exacto y predecible)

- Se calcula para caer justo en el centro de la plataforma objetivo en 4,5 s. La velocidad
  vertical siempre es la del salto ideal; la horizontal se multiplica por la carga.
- Una inspiración corta ⇒ salto proporcionalmente más corto. Con las medidas por defecto basta
  una carga de ~0,65 (≈ 2,6 s) para llegar al borde de la plataforma, así que casi siempre se llega.
- Si el salto es tan corto que la nutria vuelve a caer sobre su propia plataforma, cuenta como
  fallo (sin reaparición: ya está en una plataforma).

**Corrección en el aire (mantener pulsado mientras vuela)**

- Empuja suavemente la nutria hacia el centro de la plataforma objetivo (`steerAcceleration`)
  y, al bajar, reduce la gravedad a la mitad (planeo, `glideGravityMultiplier`).
- Sirve para rescatar un salto corto, pero **esa respiración ya no puede ser perfecta**
  (se está "inspirando" durante la espiración).
- Solo funciona antes de pasar por debajo de la superficie de la plataforma.

**Caídas**: si la nutria pasa por debajo de la plataforma objetivo sin tocarla, cae más rápido
y, a 4 unidades por debajo, reaparece en la plataforma desde la que saltó
(`respawnPlatformsBack = 0`; con 1 retrocede una plataforma más).

**Recorrido procedural** (`BreathJumpLevelGenerator`, semilla aleatoria por partida)

| Parámetro | Valor por defecto |
|---|---|
| Distancia entre centros | 3,6 – 4,6 (se amplía si hiciera falta para dejar ≥ 0,8 de hueco entre bordes) |
| Subida por plataforma | −0,3 – +1,0 (tendencia ascendente) |
| Ancho | 2,2 – 2,8 (primera: 3; meta: 5) |

Solo hay instanciadas unas 6 plataformas a la vez (3 por delante, 2 por detrás; el resto se
reciclan en un pool).

**Meta y final**: la plataforma número `breathsToComplete` (20) es la meta (más ancha y con
bandera). Al aterrizar en ella el texto pasa a "¡Has llegado!", se termina la espiración, se
valora la última respiración y, 1,5 s después, la partida acaba ⇒ PostMinigameScreen.
Duración aproximada: 20 × 10 s ≈ 3,5 min.

**Salir**: el botón de salida pausa y abre un diálogo de confirmación. "Sí" = abandono:
no se guarda nada, 0 monedas y se vuelve directamente a la lista de minijuegos. "No" = reanuda.
Al pasar la app a segundo plano la partida se pausa sola y se cancela la inspiración en curso.

### Puntuación (0–100) y monedas

Cada respiración se valora **al empezar la siguiente inspiración** (hasta entonces no se sabe
cuánto duró la espiración = tiempo entre soltar y volver a pulsar). La última respiración se
valora al terminar la espiración en la meta. El texto de valoración aparece 1,6 s y se desvanece.

| Valoración | Condición | Puntos | Texto |
|---|---|---|---|
| Perfecta | inspiración 4 ± 0,6 s, espiración 6 ± 0,8 s **y** sin corregir en el aire | 1 | "¡Respiración perfecta!" |
| Buena | inspiración 4 ± 1,5 s y espiración 6 ± 2 s (puede haber corregido) | 0,6 | "Muy bien" |
| Fuera de ritmo | llegó a la plataforma, pero fuera de esos márgenes | 0,25 | "Sigue el ritmo del círculo" |
| Fallo | no llegó a la plataforma | 0 | "Sin prisa, otra vez" |

- **Puntuación = suma de puntos / max(intentos, 20) × 100**. Durante la partida se muestra
  arriba ("Puntuación: X") y **sube desde 0** con cada respiración (es lo que llevas sobre el
  total). Al llegar a la meta los intentos ya son ≥ 20, así que equivale a la media por intento.
  Los fallos cuentan como intento, así que bajan la nota, pero no hay que repetir nada más que
  ese salto.
- **Perfección sin medias tintas**: solo se obtiene 100 si **todas** las respiraciones son
  perfectas y no hay ningún fallo. Cualquier otra partida se limita a 99 (aunque el redondeo
  diera 100).
- **Monedas** = puntos / 10 (100 ⇒ 10 monedas; 99 ⇒ 9).

Ejemplos (20 respiraciones): 20 perfectas ⇒ 100. 19 perfectas + 1 buena ⇒ 98.
10 perfectas + 10 buenas ⇒ 80. 20 perfectas + 1 fallo (21 intentos) ⇒ 95.

Consejo para el jugador: pulsar cuando aparece "Mantén pulsado e inspira", soltar cuando
aparece "Suelta y espira" y no tocar nada hasta que vuelva a aparecer el primer texto.

### Métricas persistidas

`perfect_breaths`, `good_breaths`, `off_rhythm_breaths`, `missed_jumps`, `air_corrections`,
`avg_inhale`, `avg_exhale` (segundos, media de respiraciones aterrizadas), `best_perfect_streak`.

### Parámetros ajustables

Todo está en `BreathJumpTuning` (campo `Tuning` del `BreathJumpController` en el Inspector):
ritmo y márgenes, `breathsToComplete`, `respawnPlatformsBack`, tiempo de vuelo y altura del
arco (la gravedad se deriva de ambos), corrección en el aire y generación de plataformas.
Si se cambia `inhaleSeconds`/`exhaleSeconds`, mantener `airTimeSeconds` algo menor que la espiración.
El resto de ajustes visuales están en cada componente (zoom en `BreathJumpCameraRig`, viñeta y
círculo en `BreathJumpView`, squash e inclinación en `BreathJumpPlayer`).

### Archivos (`Assets/_Project/Minigames/BreathJump/`)

| Archivo | Rol |
|---|---|
| `BreathJumpTuning.cs` | Parámetros serializables: ritmo, márgenes, salto, generación de plataformas |
| `BreathJumpEnums.cs` | `BreathQuality`, `BreathGuidePhase` |
| `BreathJumpMetricsTracker.cs` | Valora respiraciones; puntuación, monedas y métricas |
| `BreathJumpLevelGenerator.cs` | Recorrido procedural determinista (`PlatformLayout`); índice `breathsToComplete` = meta |
| `BreathJumpLevel.cs` | Instancia las plataformas cercanas y recicla el resto (pool) |
| `BreathJumpPlatform.cs` | Prefab de plataforma; pivote = centro de la superficie superior |
| `BreathJumpPlayer.cs` | Nutria: salto cinemático, corrección en el aire, squash e inclinación |
| `BreathJumpCameraRig.cs` | Seguimiento suave + zoom al inspirar; renderiza a RenderTexture |
| `BreathJumpInputArea.cs` | Zona táctil a pantalla completa (mantener / soltar) |
| `BreathJumpView.cs` | HUD: círculo guía (tamaño y color blanco→rojo), viñeta, progreso, puntuación, valoración, diálogo de salida |
| `BreathJumpController.cs` | `: MinigameBase`; ciclo de respiración y flujo de la partida |

### Montaje en Unity (paso a paso)

**Por qué hay RenderTexture**: el Canvas de `Main` es *Screen Space - Overlay* y se dibuja
encima de cualquier cámara. La cámara de BreathJump renderiza a una RenderTexture que se
muestra en un `RawImage` dentro del Canvas del minijuego (con Sort Order mayor que Main).
La textura la crea `BreathJumpCameraRig` en `Awake`; no hay que crear ningún asset.

**0. Layer (recomendado)**: Project Settings → Tags and Layers → añadir la layer `BreathJump`.
En `Main`, quitarla del Culling Mask de la Main Camera (para que no dibuje el mundo del minijuego).

**1. Escena**: File → New Scene → *Empty*. Guardar como `Assets/Scenes/BreathJump.unity`.
File → Build Profiles → Scene List → *Add Open Scenes* (debe estar en la lista para poder cargarse).
**No** añadir EventSystem (ya lo tiene `Main`).

**2. Raíz**: GameObject vacío `BreathJump` en (0,0,0). Add Component → `BreathJumpController`
y `BreathJumpLevel`.

**3. Cámara**: hijo `BreathJumpCamera` → Add Component → `Camera`:
Projection *Orthographic*, Position (0, 0, −10), Background *Solid Color* (color de cielo),
Culling Mask = solo `BreathJump`. **Eliminar su `AudioListener`** (ya hay uno en Main).
Add Component → `BreathJumpCameraRig`; asignar `_camera` = esta cámara (`_output` en el paso 7).

**4. Nutria**: hijo `Otter` en (0,0,0) → Add Component → `BreathJumpPlayer`.
Hijo de `Otter`: `Visual` con los sprites (`Sprites/Character/upper_body_otter` y
`lower_body_otter` como hijos con `SpriteRenderer`), colocados de forma que **los pies queden en
el origen de `Otter`** (el pivote son los pies). Los PNG son lienzos 1080×1920 (100 PPU)
recortados en el Sprite Editor (pivote Center de cada recorte). **Ojo: los nombres de los
recortes están cruzados**: `upper_body_otter_0` (en `lower_body_otter.png`) es la barriga con
los pies y `lower_body_otter_0` (en `upper_body_otter.png`) es la cabeza con el pecho.
Valores calculados a partir de los rects de recorte:
`Visual` Position (0,0,0) Scale (0.15, 0.15, 1) → ~1,5 unidades de alto;
`upper_body_otter_0` (barriga) Position (0, 3.03, 0), Order in Layer 0;
`lower_body_otter_0` (cabeza) Position (0.03, 7.79, 0), Order in Layer 1.
Los sprites se desplazan dentro de `Visual` (y no `Visual` entero) para que el squash al
inspirar se haga desde los pies.
Asignar `_visual` = `Visual`. `_animator` es opcional (parámetros float `Charge` y bool `Airborne`).

**5. Prefab de plataforma**: GameObject `Platform` (fuera de la raíz) → Add Component →
`BreathJumpPlatform`. Hijos:
- `Body`: `SpriteRenderer` con sprite *Square* (Create → 2D → Sprites → Square) o el sprite de
  plataforma, **Draw Mode = Sliced**, Size (3, 0.5), Position (0, −0.25, 0) para que su borde
  superior quede en el origen. (Con Draw Mode *Simple*, usar un sprite de 1 unidad de ancho:
  se escala en X.)
- `GoalFlag`: bandera / decoración de meta (p. ej. otro sprite) encima de la superficie; desactivado.

Asignar `_renderer` = Body y `_goalMarker` = GoalFlag. Poner todo en la layer `BreathJump`.
Arrastrar a `Assets/_Project/Minigames/BreathJump/Prefabs/Platform.prefab` y borrarlo de la escena.

**6. Level**: en `BreathJumpLevel` asignar `_platformPrefab` = Platform.prefab.
Opcional: hijo vacío `Platforms` como `_platformsRoot`.

**7. Canvas del HUD**: hijo de la raíz → UI → Canvas: *Screen Space - Overlay*,
**Sort Order = 10** (mayor que Main), Canvas Scaler *Scale With Screen Size* 1080×1920,
Match 0 (igual que Beatmaker). Hijos **en este orden** (de arriba abajo en la jerarquía = de
atrás hacia delante):

| # | Objeto | Componentes / ajustes |
|---|---|---|
| 1 | `World` | `RawImage` a pantalla completa (anchors stretch, offsets 0), Raycast Target **off**. → `_output` del CameraRig |
| 2 | `InputArea` | `Image` a pantalla completa, color alfa 0, Raycast Target **on** + `BreathJumpInputArea` |
| 3 | `Vignette` | `Image` a pantalla completa **sin sprite** (se genera sola), Raycast Target off |
| 4 | `GuideRing` | `Image` circular (sprite *Knob* u otro círculo blanco), ~300×300, abajo centrado. El color lo pone el código (`_guideEmptyColor` blanco → `_guideFullColor` rojo) |
| 5 | `GuideLabel` | TMP bajo el círculo |
| 6 | `ProgressLabel` | TMP arriba ("0 / 20") |
| 6b | `ScoreLabel` | TMP arriba ("Puntuación: 0") → `_scoreLabel` |
| 7 | `FeedbackLabel` | TMP centrado en la parte superior (el `CanvasGroup` se añade solo si falta) |
| 8 | `ExitButton` | `Button` arriba a la esquina |
| 9 | `ExitConfirmPanel` | Panel a pantalla completa (bloquea toques) con texto "¿Salir? Perderás el progreso" y botones `Yes` / `No`. Desactivado |

Añadir `BreathJumpView` al Canvas y asignar: `_inputArea`, `_vignette`, `_guideRing`,
`_guideLabel`, `_progressLabel`, `_scoreLabel`, `_feedbackLabel`, `_exitButton`, `_exitConfirmPanel`,
`_exitConfirmButton` (Yes), `_exitCancelButton` (No).

**8. Controller**: en `BreathJumpController` asignar `_view`, `_player`, `_level`, `_cameraRig`
(si se dejan vacíos los busca en hijos). Revisar `Tuning` (valores por defecto ya ajustados al 4-6).

**9. Registrar en Main**: abrir `Main.unity` → objeto con `MinigameLoader`:
- `_minigameScenes` → añadir elemento: `type = BreathJump`, `sceneName = BreathJump`.
- `_minigameDefinitions` → añadir `Core/Data/ScriptableObjects/Minigames/BreathJump.asset`
  (ya existe y ya está en `_allMinigames` de `MinigamesController`).

**Problemas típicos**
- *Se ve la interfaz de Main a través del juego*: fondo de la cámara con alfa 0 (valor por
  defecto de Unity). `BreathJumpCameraRig` ya fuerza alfa 1 al usar RenderTexture.
- *Se ve el cielo pero ni nutria ni plataformas*: los objetos del mundo no están en la layer que
  ve la cámara (Culling Mask). Poner `Otter` (con hijos) y el prefab `Platform` en `BreathJump`.
- *Nutria enorme*: sprites de 1080×1920 a 100 PPU = 19 unidades; escalar `Visual` (ver paso 4).
- *Plataformas gigantes*: el `Body` del prefab debe tener Scale (1,1,1) en modo Sliced; el ancho
  lo pone el código en `Size.x`.
- *"No hay escena configurada para: BreathJump"*: falta el paso 9, o se hizo en Play Mode / sin guardar Main.

**10. Probar**: Play en `Main` → Minijuegos → Respira y vuela. Comprobar: se ve el mundo,
mantener pulsado oscurece bordes y hace zoom, soltar salta y aterriza en el centro con 4 s,
toques cortos no saltan, caer reaparece, el botón de salida vuelve a la lista y la meta abre
PostMinigameScreen. Para probar rápido el final, bajar `breathsToComplete` a 3 en el Inspector.
