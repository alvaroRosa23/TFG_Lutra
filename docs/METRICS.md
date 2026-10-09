# Datos y métricas — catálogo completo

Qué registra Lutra, dónde se guarda, cómo se calcula cada métrica y en qué evidencia científica se apoya.
Es la referencia para la pantalla de Estadísticas, el informe profesional (`docs/PROFESSIONAL_REPORT.md`) y la memoria del TFG.

> Estado: **[ACTUAL]** = ya existe en el código · **[PLAN]** = acordado, pendiente de implementar (ver `docs/ROADMAP.md`).

---

## 1. Principios metodológicos

1. **Tres niveles de evidencia.** Todo dato del informe se etiqueta con su nivel para que el profesional sepa qué peso darle:

   | Nivel | Qué es | Ejemplos en Lutra |
   |---|---|---|
   | **A — Instrumento validado** | Escala con propiedades psicométricas y puntos de corte publicados | WHO-5 |
   | **B — Autoinforme ecológico (EMA)** | Registro en el momento, en el entorno natural del usuario | Ánimo 1–5, emoción, motivos, ánimo antes/después de minijuego |
   | **C — Indicador exploratorio** | Comportamiento dentro de la app, sin validación clínica | Métricas de minijuegos, lenguaje del diario, uso de la app |

   Las tareas cognitivas convertidas en juego pierden fiabilidad frente a sus versiones de laboratorio (Lumsden et al., 2016), por eso los minijuegos nunca pasan del nivel C.

2. **Evaluación ecológica momentánea (EMA).** Registrar el estado en el momento reduce el sesgo de recuerdo de los cuestionarios retrospectivos (Shiffman, Stone y Hufford, 2008). El check-in diario y el registro de momento son EMA.

3. **Nombrar la emoción ya es intervención.** Poner nombre a lo que se siente reduce la reactividad emocional (Lieberman et al., 2007). Justifica el check-in como parte terapéutica, no solo como medida.

4. **Mínimos de datos.** Ninguna métrica se muestra si no hay datos suficientes (umbral indicado en cada una). El informe siempre muestra la *n* en la que se basa cada cifra.

5. **No se puede suponer que los datos faltan al azar.** En los registros diarios, los días sin registro pueden coincidir con peores momentos (justo cuando cuesta más registrar), y entonces la media de los días registrados sobreestima el ánimo. Por eso el informe muestra la adherencia y los huecos junto a las medias, nunca medias aisladas, e incluye el ánimo del día previo a cada hueco (§4.9) como indicador exploratorio.

6. **Usuario vs profesional.** El usuario ve progreso en lenguaje sencillo y en positivo; el profesional ve los valores técnicos y la metodología.

7. **Registros excluidos.** Los `EmotionRecord` con `Source != RecordSource.User` (placeholders restaurados desde Firestore, ánimo 3 inventado) se excluyen de **todas** las métricas. El informe indica cuántos se excluyeron.

---

## 2. Inventario de datos registrados

### 2.1 Check-in emocional — `EmotionRecord` (SQLite `EmotionRecords`, Firestore `users/{uid}/emotions/{RemoteId}`)

| Campo | Qué es | Origen | Uso analítico | Nivel |
|---|---|---|---|---|
| `Timestamp` | Fecha y hora local | Automático | Series temporales, día de la semana, hora | — |
| `IsMorningCheck` | `true` = check-in de Día (1 por día, se sobrescribe), `false` = registro de Momento | Modo elegido en MainMenu | Serie diaria (Día) vs variación intradía (Momento) | — |
| `MoodLevel` | Ánimo 1–5 (5 caritas, `MoodLevel` enum) | Usuario | **Variable principal** de todas las métricas de ánimo | B |
| `EmotionType` | Una de las 8 emociones (selección única) | Usuario | Distribución, emodiversidad, valencia/activación derivadas | B |
| `SelectedEmotionTags` | JSON con la emoción elegida (selección única: repite `EmotionType`) | Usuario | Ninguno adicional | — |
| `SelectedMotiveTags` | JSON con los motivos: hobbies del perfil + fijos (*Trabajo, Familia, Salud, Ocio, Relaciones, Estudio, Deporte*) + texto libre "Otro" | Usuario | Análisis de contexto | B |
| `Notes` | Nota libre | Usuario | Solo en el informe si el usuario lo autoriza | — |
| `IntensityLevel` | **Copia de `MoodLevel`** (`EmotionCheckController`) | — | **No se usa en métricas.** Se mantiene la columna por compatibilidad | — |
| `PhotoPath`, `SongTitle`, `SongArtist` | Foto local / reservado | Usuario | No se analizan | — |
| `Source` | `User` / `RestoredFirestore` / `RestoredFirestoreHistory` | Sistema | Filtro de exclusión | — |

### 2.2 Partida de minijuego — `MinigameSession` (SQLite `MinigameSessions`, Firestore `users/{uid}/minigameSessions/{RemoteId}`)

| Campo | Qué es | Estado | Nivel |
|---|---|---|---|
| `StartTime`, `DurationSeconds` | Inicio y duración | [ACTUAL] | — |
| `MinigameId` | `MinigameType` | [ACTUAL] | — |
| `EmotionBefore` | Emoción del último registro del usuario (`GetLastEmotion`) | [ACTUAL] | B |
| `EmotionAfter` | Emoción elegida en `PostMinigameScreen`. **Por defecto = `EmotionBefore`** si el usuario no elige | [ACTUAL] (se mantiene; no se usa para medir efecto) | B |
| `RelaxationScore` | Puntuación del juego 0–1 | [ACTUAL] | C |
| `MetricsJson` | Métricas específicas de cada juego (§2.3) | [ACTUAL] | C |
| `MoodBefore` (int?) | Ánimo 1–5 más reciente antes de empezar: el último check-in del usuario (Día o Momento) **o** la valoración `MoodAfter` de una partida anterior, el que sea más reciente (`DataRepository.GetLatestMood`). **No se pregunta** | [ACTUAL] | B |
| `MoodBeforeRecordedAt` (DateTime?) | Momento de ese dato (para una partida: inicio + duración) | [ACTUAL] | — |
| `MoodAfter` (int?) | Caritas 1–5 en `PostMinigameScreen`. `null` si el usuario no responde | [ACTUAL] | B |

Solo se guardan las partidas **terminadas de forma natural** (`CompletedNaturally`); las abandonadas no se registran.

### 2.3 Métricas por minijuego (`MetricsJson`)

| Minijuego | Claves | Interpretación | Nivel |
|---|---|---|---|
| **BreathJump** | `perfect_breaths`, `good_breaths`, `off_rhythm_breaths`, `missed_jumps`, `air_corrections`, `avg_inhale`, `avg_exhale`, `best_perfect_streak` | Calidad y ritmo de la respiración guiada | C (el que más base tiene, §4.6) |
| BreathJump | `breaths_per_minute`, `exhale_inhale_ratio`, `breath_cv` | Frecuencia respiratoria, relación exhalación/inhalación, regularidad | C |
| **Beatmaker** | `max_layers`, `longest_groove`, `session_richness`, `max_notes` | Implicación y complejidad creativa | C (solo uso) |
| **FruitNinja** | `objects_spawned`, `objects_cut`, `objects_missed`, `specials_cut`, `accuracy`, `best_streak`, `max_combo`, `big_combos` | Precisión y ritmo de descarga | C (solo uso) |
| **StarFisher** | `stars_caught`, `stars_escaped_bite`, `stars_escaped_reeling`, `new_stars`, `perfect_casts`, `best_rarity`, `glow_seconds`, `streak_star` | Paciencia / contemplación | C (solo uso) |

Detalle de cómo se calcula cada puntuación en `docs/MINIGAMES.md` y `docs/STARFISHER.md`.

### 2.4 Diario — `DiaryEntry` (SQLite `DiaryEntries`, Firestore `users/{uid}/diary/{RemoteId}`)

| Campo | Uso analítico | Nivel |
|---|---|---|
| `Date` | Frecuencia de escritura | — |
| `Content`, `Title` | **Análisis de lenguaje en el dispositivo** [ACTUAL] (§4.7). El texto solo sale del dispositivo hacia el informe si el usuario lo autoriza al exportar | C |
| `Mood` | Emoción asociada (nombre de `EmotionType`) | B |
| `AudioPath`, `ImagePath` | No se analizan ni se sincronizan | — |

### 2.5 Escalas — `ScaleResponse` [ACTUAL] (SQLite `ScaleResponses`, Firestore `users/{uid}/scaleResponses/{RemoteId}`)

[ACTUAL] WHO-5 cada 14 días. Esquema completo y flujo en `docs/PROFESSIONAL_REPORT.md` §3.

### 2.6 Uso de la app y hábitos

| Dato | Origen | Nivel |
|---|---|---|
| Días con check-in, rachas actual y máxima | `StreakManager`, `EmotionRecords` | B |
| Horas de los registros y partidas | `Timestamp`, `StartTime` | C |
| Entradas de diario por semana | `DiaryEntries` | C |
| Retraso en completar el WHO-5 respecto a su disponibilidad | `ScaleResponse.AvailableSince` vs `CompletedAt` | C |
| Activaciones del protocolo de apoyo | `AppNotification` tipo `Support` (`SourceRef` = disparador) | — |

### 2.7 Perfil — `UserProfile`

`DateOfBirth` (edad en el informe, comprobación ≥ 18 [PLAN]), `CreationDate` (antigüedad en la app), `Hobbies` (alimentan los motivos), `Culture` (solo visual).
`Preferences` guarda `consentVersion`, `consentDate` y `diaryLanguageAnalysis` (on/off) — `ConsentGate`.

---

## 3. Derivaciones teóricas

### 3.1 Valencia y activación derivadas de la emoción

Lutra no mide la activación (arousal) directamente (la Affect Grid se descartó para mantener el diseño de caritas). Se **deriva** de la emoción elegida según el modelo circumplejo del afecto (Russell, 1980). Es una aproximación teórica, no una medida, y el informe lo indica.

| Emoción | Valencia | Activación | Cuadrante |
|---|---|---|---|
| Anxiety (Ansiedad) | Desagradable | Alta | Tensión |
| Overwhelm (Agobio) | Desagradable | Alta | Tensión |
| Frustration (Frustración) | Desagradable | Alta | Tensión |
| Sadness (Tristeza) | Desagradable | Baja | Decaimiento |
| Nostalgia | Mixta | Baja | Decaimiento / calma (se cuenta aparte como "mixta") |
| Calm (Calma) | Agradable | Baja | Calma |
| Energy (Energía) | Agradable | Alta | Entusiasmo |
| Joy (Alegría) | Agradable | Alta | Entusiasmo |

Implementado en `EmotionCircumplex` (`ValenceOf`, `ArousalOf`, `QuadrantOf`; enums `Valence`, `Arousal`, `AffectQuadrant`), no se guarda en BD.

### 3.2 Serie diaria del ánimo

- **Ánimo del día** = `MoodLevel` del check-in de Día (`IsMorningCheck = true`). Hay como máximo uno por día.
- Los registros de **Momento** no entran en la serie diaria; se analizan como variación intradía (Momento − Día del mismo día).
- Así la serie diaria mide siempre lo mismo y las métricas de dinámica son comparables.

---

## 4. Métricas calculadas

**[ACTUAL]** Todas se implementan en `ReportCalculator` (`Features/Charts/Report/`, clase estática pura, sin Unity, con tests en `ReportCalculatorTests`). `ReportDataLoader.LoadAsync(from, to)` reúne los datos de `DataRepository` y devuelve un `ReportData`. `ChartsCalculator` mantiene `FormatStreak` y `FormatDuration`.

Reglas comunes de la implementación:
- **Periodo efectivo:** no empieza antes del alta del perfil; "Todo" empieza en el alta. Si el periodo acaba hoy y aún no hay check-in de Día, hoy no cuenta (no es un día "sin registro").
- **Periodo anterior** (para comparar): misma duración, justo antes del inicio.
- Los mínimos son constantes públicas de `ReportCalculator` (`MinDaysForMean`, `MinPairsForDynamics`…); por debajo, la métrica vale `null`.

### 4.1 Estado de ánimo (nivel B)

| Métrica | Fórmula | Mínimo | Usuario | Profesional |
|---|---|---|---|---|
| Ánimo medio | media de la serie diaria | 3 días | Sí | Sí |
| Tendencia | media móvil de 7 días (media de los días con registro dentro de los 7 últimos) + pendiente de regresión lineal (puntos/semana) | 7 días | Flecha ↑ → ↓ | Pendiente y *n* |
| Cambio vs periodo anterior | media(periodo) − media(periodo anterior de igual duración) | 3 días en cada uno | "Mejor que el mes pasado" | Diferencia |
| Días buenos / malos | % días con ánimo ≥ 4 / ≤ 2 | 3 días | Sí | Sí |
| Variabilidad | desviación típica de la serie diaria | 7 días | No | Sí |
| Variación intradía | media de (Momento − Día del mismo día) | 5 pares | No | Sí |

### 4.2 Dinámica emocional (nivel B)

La forma en que el ánimo cambia predice el bienestar tanto como su media. Un metaanálisis de 79 estudios asocia peor bienestar con mayor inestabilidad, variabilidad e inercia del afecto (Houben, Van Den Noortgate y Kuppens, 2015).

| Métrica | Fórmula | Mínimo | Base |
|---|---|---|---|
| **Inestabilidad (MSSD)** | media de (xₜ − xₜ₋₁)² usando **solo pares de días consecutivos** | 14 pares | Jahng, Wood y Trull (2008) |
| **Inercia** | correlación de Pearson entre xₜ y xₜ₋₁ (pares de días consecutivos) | 14 pares | Kuppens, Allen y Sheeber (2010) |

Se usan solo pares consecutivos porque comparar días separados por huecos inflaría la inestabilidad (Jahng et al., 2008).
Al usuario se le muestra solo la estabilidad en lenguaje positivo ("Tu ánimo ha estado más estable que el mes pasado"); la inercia no se muestra al usuario.

### 4.3 Perfil emocional (nivel B)

| Métrica | Fórmula | Mínimo | Base |
|---|---|---|---|
| Distribución de emociones | % de cada `EmotionType` (todos los registros) | 1 | — |
| Balance de valencia | % agradables / desagradables / mixta (§3.1) | 5 | Russell (1980) |
| Cuadrantes de activación | % por cuadrante (§3.1) | 5 | Russell (1980) |
| **Emodiversidad** | entropía de Shannon normalizada: −Σ pᵢ·ln pᵢ / ln 8 (0 = siempre la misma emoción, 1 = las 8 por igual) | 10 registros | Quoidbach et al. (2014) |
| Emociones distintas usadas | nº de `EmotionType` distintos | 1 | — |

La **emodiversidad** sustituye a la granularidad emocional: la granularidad (Kashdan, Barrett y McKnight, 2015) necesita valorar varias emociones a la vez en cada registro, y el check-in de Lutra es de selección única. Quoidbach et al. (2014) relacionan una mayor emodiversidad con mejor salud mental.

### 4.4 Contexto y motivos (nivel B)

Para cada motivo con ≥ 3 apariciones en el periodo (registros de Día y de Momento con ánimo; agrupación con `MotiveTags.GroupKey`):
- frecuencia (nº y % de registros);
- ánimo medio de los registros **con** ese motivo vs **sin** él, y la diferencia;
- emoción más frecuente con ese motivo.

Los motivos escritos en "Otro" se agrupan como *Otros* en las tablas; su texto solo aparece si el usuario autoriza incluir notas.
Es asociación, no causalidad: el informe lo indica.

### 4.5 Patrones temporales (nivel B)

- Ánimo medio por día de la semana (mínimo 2 registros por día).
- Distribución horaria de los registros de Momento.
- Comparación Día vs Momento (§4.1).

### 4.6 Regulación emocional: minijuegos

**Uso (nivel C):** partidas y minutos por juego, horario, emoción de partida (`EmotionBefore`) por juego, y si el usuario elige juegos acordes a su emoción (modelo de proceso de la regulación emocional; Gross, 1998).

**Efecto en el ánimo (nivel B):**
- **ΔÁnimo** = `MoodAfter` − `MoodBefore`.
- **Sesión válida** para medir efecto: `MoodAfter` no nulo **y** `MoodBefore` registrado como mucho **3 horas** antes del inicio (`StartTime − MoodBeforeRecordedAt ≤ 3 h`). El ánimo cambia a lo largo del día; un check-in de la mañana no representa el estado antes de una partida por la tarde. Las sesiones no válidas cuentan para el uso pero no para el efecto.
- Al encadenar partidas ("Jugar otra vez"), el `MoodBefore` de la segunda es el `MoodAfter` de la primera, porque es el dato más reciente.
- Por juego y por emoción de partida: ΔÁnimo medio, % de sesiones que mejoran / igual / empeoran, *n* válidas. Mínimo 3 sesiones válidas.
- `EmotionAfter` se exporta en el CSV pero **no** se usa para medir efecto (valor por defecto sesgado).

**Respiración — BreathJump (nivel C, con base fisiológica):** la respiración lenta, en torno a 6 respiraciones por minuto, aumenta la variabilidad de la frecuencia cardiaca y la actividad parasimpática (Lehrer y Gevirtz, 2014; Zaccaro et al., 2018). Una exhalación más larga que la inhalación se asocia a mayor sensación de relajación (Van Diest et al., 2014).

| Métrica [ACTUAL] | Fórmula | Referencia |
|---|---|---|
| `breaths_per_minute` | 60 / ciclo medio (inspiración + espiración de cada respiración aterrizada) | objetivo ≈ 6 |
| `exhale_inhale_ratio` | Σ espiraciones / Σ inspiraciones | > 1 deseable |
| `breath_cv` | desviación típica muestral / media del ciclo (0 si hay < 2 respiraciones) | menor = más regular |
| % respiraciones perfectas | `perfect_breaths` / (`perfect` + `good` + `off_rhythm`) | — |

**Diseño del juego:** el ritmo guía por defecto (`BreathJumpTuning`: 4 s de inspiración + 6 s de espiración) da ciclos de 10 s, es decir **6 respiraciones/min**, con una relación espiración/inspiración de **1,5**. Coincide con la respiración lenta que respalda la literatura citada. La espiración se mide desde que se suelta hasta la siguiente pulsación, así que el ciclo incluye el tiempo de salto.

En el informe: evolución sesión a sesión (¿aprende a respirar más despacio y regular?).

### 4.7 Diario: escritura y lenguaje (nivel C)

- La **escritura expresiva** tiene efectos positivos pequeños pero consistentes (Pennebaker, 1997). Métricas: entradas por semana, palabras por entrada.
- **Análisis del lenguaje** (`DiaryLanguageAnalyzer`, clase pura): el uso de primera persona singular y de palabras emocionales negativas es mayor en personas con depresión (Rude, Gortner y Pennebaker, 2004).

| Métrica | Fórmula |
|---|---|
| `WordCount` | nº de palabras |
| % primera persona | palabras de {yo, me, mi, mis, mío, mía, míos, mías, conmigo} / total |
| % emoción negativa | palabras del diccionario negativo / total |
| % emoción positiva | palabras del diccionario positivo / total |

- **Se calcula al generar estadísticas o informe**, a partir del `Content` local. No se guarda en BD; así un cambio en el diccionario se aplica a todo el historial y no hace falta sincronizar nada nuevo.
- Agregación **semanal**; solo se calcula con ≥ 50 palabras en la semana (los porcentajes de textos muy cortos no son fiables).
- Diccionario propio en español (`Assets/Resources/DiaryLexicon_es.txt`, ~200 entradas): palabras exactas y raíces (`trist*`). No se usa LIWC por su licencia. Se ignoran mayúsculas y tildes. Se excluyen palabras ambiguas ("solo", "quiero", "nostalgia", "ánimo") y se evitan raíces que capturen palabras sin relación ("contenido", "precios", "aterrizar"); los criterios están en la cabecera del archivo.
- **No detecta negaciones** ("no estoy triste" cuenta como negativa): limitación para la memoria.
- **Limitación (para la memoria):** el español omite el sujeto ("estoy cansado"), así que la primera persona queda infraestimada; la adaptación de LIWC al español afronta el mismo problema (Ramírez-Esparza et al., 2007). Sirve para comparar al usuario consigo mismo, no con normas.
- Los porcentajes solo se calculan si el usuario lo tiene activado (`Preferences["diaryLanguageAnalysis"]`, activado por defecto y elegido en el consentimiento). El número de palabras se cuenta siempre.
- El usuario **no** ve estas cifras (evitar autoobservación ansiosa); solo "X entradas · Y palabras".

### 4.8 Bienestar — WHO-5 (nivel A)

| Métrica | Fórmula / criterio | Base |
|---|---|---|
| Puntuación bruta | suma de 5 ítems (0–5) → 0–25 | Topp et al. (2015) |
| Índice | bruta × 4 → 0–100 | Topp et al. (2015) |
| Bienestar bajo | índice ≤ 50 → se recomienda valorar depresión | Topp et al. (2015) |
| Probable depresión | índice ≤ 28 | Topp et al. (2015) |
| Cambio relevante | diferencia ≥ 10 puntos entre aplicaciones | Topp et al. (2015) |
| Tiempo de respuesta | `DurationSeconds`; < 10 s se marca como posible respuesta poco atenta | Indicador de calidad (C) |

Si el profesional aporta la fiabilidad y la desviación típica normativa, el informe puede añadir el Índice de Cambio Fiable (Jacobson y Truax, 1991). Por defecto se usa el criterio de 10 puntos.

### 4.9 Adherencia y hábitos

| Métrica | Fórmula |
|---|---|
| Adherencia | días con check-in de Día / días del periodo |
| Rachas | actual y máxima (`StreakManager`) |
| Huecos | nº y duración de periodos ≥ 2 días sin check-in |
| Ánimo antes de un hueco | ánimo medio del día previo a cada hueco vs media general (exploratorio) |
| WHO-5 | aplicaciones completadas / disponibles; días de retraso medio |

---

## 5. Economía de monedas y recompensas

Cada fila genera una notificación en el centro de notificaciones (`docs/NOTIFICATION_CENTER.md` §3) mediante `EventBus.EmitRewardGranted`.

| Fuente | Recompensa | Condición | Código |
|---|---|---|---|
| Check-in de Día (nuevo del día) | 5 monedas; 20 / 50 / 100 en los días 7 / 14 / 30 de racha | Solo el primer check-in de Día de cada día | `StreakManager.RegisterCheckIn` |
| Hito de racha 7 / 14 / 30 | Decoración `streak_reward_{n}d` — **no se entrega** (bug, ver `BUGS.md`) | Racha exacta | `StreakManager._checkMilestoneRewards` |
| `RewardDefinition` (SO) | `coinValue` y/o `itemId` | `requiredStreakDays` o `requiredCheckIns` exactos | `RewardSystem._grantReward` |
| Diario | 5 monedas | Primera entrada de cada día | `DiaryController._grantDiaryReward` |
| Minijuego terminado | `CoinReward` del juego (puntuación/10; StarFisher: `min(coins, maxCoins)`); si no lo calcula: `max(3, estimatedTime/30)` | Solo partidas terminadas | `MinigameLoader` |
| Colección de estrellas completa | Decoración | Completar el catálogo | `StarCollectionStore.GrantRewardIfCompleteAsync` |
| WHO-5 completado | 20 monedas | Cada envío completo. **Independiente de las respuestas**, para no sesgarlas | `Who5Controller` |
| Venta en SafeZone | `coinCost / 2` | — | No es recompensa: **sin notificación** |

---

## 6. Privacidad de los datos

| Dato | Sale del dispositivo hacia… | Condición |
|---|---|---|
| Registros, partidas, escalas, perfil | Firestore (copia de seguridad del propio usuario) | Siempre (sincronización) |
| Texto del diario y notas | Firestore (sync) / informe | Informe: solo si el usuario marca "Incluir notas y diario" |
| Métricas de lenguaje del diario | Informe | Solo si `diaryLanguageAnalysis` está activado |
| Fotos, audios | Nunca | Rutas locales |
| Informe PDF / CSV | Quien elija el usuario (menú compartir) | Siempre iniciado por el usuario |

Base legal: datos de salud = categoría especial (RGPD art. 9). Requiere consentimiento explícito (ver `docs/PROFESSIONAL_REPORT.md` §6).

---

## 7. Bibliografía

- Gross, J. J. (1998). The emerging field of emotion regulation: An integrative review. *Review of General Psychology, 2*(3), 271–299.
- Houben, M., Van Den Noortgate, W., & Kuppens, P. (2015). The relation between short-term emotion dynamics and psychological well-being: A meta-analysis. *Psychological Bulletin, 141*(4), 901–930.
- Jacobson, N. S., & Truax, P. (1991). Clinical significance: A statistical approach to defining meaningful change in psychotherapy research. *Journal of Consulting and Clinical Psychology, 59*(1), 12–19.
- Jahng, S., Wood, P. K., & Trull, T. J. (2008). Analysis of affective instability in ecological momentary assessment: Indices using successive difference and group comparison via multilevel modeling. *Psychological Methods, 13*(4), 354–375.
- Kashdan, T. B., Barrett, L. F., & McKnight, P. E. (2015). Unpacking emotion differentiation: Transforming unpleasant experience by perceiving distinctions in negativity. *Current Directions in Psychological Science, 24*(1), 10–16.
- Kuppens, P., Allen, N. B., & Sheeber, L. B. (2010). Emotional inertia and psychological maladjustment. *Psychological Science, 21*(7), 984–991.
- Lehrer, P. M., & Gevirtz, R. (2014). Heart rate variability biofeedback: How and why does it work? *Frontiers in Psychology, 5*, 756.
- Lieberman, M. D., Eisenberger, N. I., Crockett, M. J., Tom, S. M., Pfeifer, J. H., & Way, B. M. (2007). Putting feelings into words: Affect labeling disrupts amygdala activity in response to affective stimuli. *Psychological Science, 18*(5), 421–428.
- Lumsden, J., Edwards, E. A., Lawrence, N. S., Coyle, D., & Munafò, M. R. (2016). Gamification of cognitive assessment and cognitive training: A systematic review of applications and efficacy. *JMIR Serious Games, 4*(2), e11.
- Pennebaker, J. W. (1997). Writing about emotional experiences as a therapeutic process. *Psychological Science, 8*(3), 162–166.
- Quoidbach, J., Gruber, J., Mikolajczak, M., Kogan, A., Kotsou, I., & Norton, M. I. (2014). Emodiversity and the emotional ecosystem. *Journal of Experimental Psychology: General, 143*(6), 2057–2066.
- Ramírez-Esparza, N., Pennebaker, J. W., García, F. A., & Suriá, R. (2007). La psicología del uso de las palabras: Un programa de computadora que analiza textos en español. *Revista Mexicana de Psicología, 24*(1), 85–99.
- Rude, S., Gortner, E.-M., & Pennebaker, J. (2004). Language use of depressed and depression-vulnerable college students. *Cognition and Emotion, 18*(8), 1121–1133.
- Russell, J. A. (1980). A circumplex model of affect. *Journal of Personality and Social Psychology, 39*(6), 1161–1178.
- Shiffman, S., Stone, A. A., & Hufford, M. R. (2008). Ecological momentary assessment. *Annual Review of Clinical Psychology, 4*, 1–32.
- Topp, C. W., Østergaard, S. D., Søndergaard, S., & Bech, P. (2015). The WHO-5 Well-Being Index: A systematic review of the literature. *Psychotherapy and Psychosomatics, 84*(3), 167–176.
- Van Diest, I., Verstappen, K., Aubert, A. E., Widjaja, D., Vansteenwegen, D., & Vlemincx, E. (2014). Inhalation/exhalation ratio modulates the effect of slow breathing on heart rate variability and relaxation. *Applied Psychophysiology and Biofeedback, 39*(3–4), 171–180.
- Zaccaro, A., Piarulli, A., Laurino, M., Garbella, E., Menicucci, D., Neri, B., & Gemignani, A. (2018). How breath-control can change your life: A systematic review on psycho-physiological correlates of slow breathing. *Frontiers in Human Neuroscience, 12*, 353.

> Antes de entregar la memoria, comprobar cada referencia (DOI, páginas) contra la fuente original.
