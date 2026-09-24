# Minijuegos

## Estado actual

La infraestructura base está lista (`IMinigame`, `MinigameBase`, `MinigameLoader`, `MinigameResult`).

**Beatmaker**: código completo (ver sección propia más abajo), **pendiente montar la escena aditiva en el editor** (prefab, AudioSources, wiring de botones, `MinigameSceneData`/`MinigameDefinition` assets). El resto de carpetas de minijuegos están pendientes de implementar.

## Pendientes de implementar (en orden)

| Orden | Carpeta | Mecánica | Emociones objetivo |
|---|---|---|---|
| 1 | `Unpacking/` | Ordenar objetos | tristeza, agotamiento |
| 2 | `FruitNinja/` | Cortar objetos | frustración, ira |
| 3 | `Beatmaker/` | Secuenciador 8 pistas × 8 beats — **código listo, falta escena** | tristeza, apatía |
| 4 | `SandCastle/` | Arena + acelerómetro | estrés |
| 5 | `FluidSim/` | Fluido interactivo | ansiedad |
| 6 | `BreathJump/` | Plataformas con respiración como control | ansiedad, agobio |
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
```

## Input

Acciones en `Assets/InputSystem_Actions.inputactions`, action map `Player`:
`Move`, `Look`, `Attack`, `Interact` (hold), `Crouch`, `Jump`, `Sprint`, `Previous`, `Next`.

Estas acciones se usan en los minijuegos; la navegación de la app usa UI/botones.

## MinigameDefinition SO

Un asset por minijuego en `Core/Data/ScriptableObjects/`. Ver `docs/DATA_MODELS.md` para la estructura completa.

## EmotionMinigameMap SO

Mapea `EmotionType` → `MinigameTag[]` para filtrar recomendaciones. Ver `docs/DATA_MODELS.md`.

## Beatmaker (código completo, pendiente escena)

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
| `BeatmakerMetricsTracker.cs` | Acumula RelaxationScore y récords mientras dura la partida |
| `BeatmakerAudioEngine.cs` | Transporte `dspTime`; `AudioSource` de beats, loop y metrónomo (se crean si faltan); `CycleProgress` |
| `BeatmakerTrackRow.cs` | Prefab de fila: etiqueta + 8 `Button` de step |
| `BeatmakerView.cs` | Vista pura; genera las 8 filas, loops, metrónomo, dropdown de pack, BPM, puntuación y líneas |
| `BeatmakerController.cs` | `: MinigameBase`; conecta patrón + audio + vista + métricas |

También: `BeatmakerInstrument` (enum `Kick, Hat, Clap, Snare`, `Core/Data/Models/`) y
`BeatmakerSoundPack` (ScriptableObject, `Core/Data/ScriptableObjects/`, con `bpm`,
2 clips por instrumento y `loopClips`/`loopNames`). Al iniciar se elige un
pack al azar de `_soundPacks`; el usuario puede cambiarlo con el desplegable.

### RelaxationScore (0.0–1.0)

| Factor | Peso | Cómo se mide |
|---|---|---|
| Diversidad instrumental | 40% | % de tiempo con ≥3 estilos distintos sonando en el step actual (Kick 1 + Kick 2 cuentan como 1) |
| Engagement | 40% | % de tiempo con ≥1 instrumento activo en el step actual |
| Uso de loop | 20% | ¿Activó al menos un loop instrumental? |

### Métricas persistidas (`MinigameSession.Metrics` → `MetricsJson`)

`max_layers`, `longest_groove` (segundos), `session_richness` (% tiempo con ≥2 sonidos).
Consultar el mejor valor histórico con `DataRepository.GetBestMetric(MinigameType, string)`.
El récord mostrado en el panel de detalle es la mejor puntuación (`RelaxationScore` × 100),
igual para todos los minijuegos (`MinigamesController._getRecordText`).

### Pendiente para que sea jugable

1. Crear el asset `BeatmakerSoundPack` (o varios) con BPM y clips de audio asignados.
2. Crear el asset `MinigameDefinition` para `MinigameType.Beatmaker` y añadirlo al array
   `_allMinigames` de `MinigamesController`.
3. Montar la escena aditiva: prefab raíz con `BeatmakerController` + `BeatmakerView` +
   `BeatmakerAudioEngine`; prefab `BeatmakerTrackRow` (etiqueta + 8 botones) asignado a
   `_trackRowPrefab`, `_tracksContainer` con `VerticalLayoutGroup`, `_playhead` fuera del
   contenedor, botones de loop con su línea hija, botón de metrónomo, `TMP_Dropdown`, texto
   de BPM, texto de puntuación y botón de salida.
4. Registrar la escena en `MinigameLoader._minigameScenes` (`MinigameSceneData { type = Beatmaker, sceneName = "..." }`).
