# Minijuegos

## Estado actual

La infraestructura base está lista (`IMinigame`, `MinigameBase`, `MinigameLoader`, `MinigameResult`). Las carpetas de minijuegos concretos están pendientes de implementar.

## Pendientes de implementar (en orden)

| Orden | Carpeta | Mecánica | Emociones objetivo |
|---|---|---|---|
| 1 | `Unpacking/` | Ordenar objetos | tristeza, agotamiento |
| 2 | `FruitNinja/` | Cortar objetos | frustración, ira |
| 3 | `Beatmaker/` | Secuenciador 4×4 | tristeza, apatía |
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
Carga escena aditiva → busca `IMinigame` → `Initialize` → `StartGame` → `OnGameCompleted` → guarda `MinigameSession` en BD → `EmitMinigameCompleted` → descarga escena → `TransitionTo(MinigameActive)` (PostMinigameScreen).

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
