# Estadísticas, WHO-5 e informe profesional

Especificación funcional de la parte de seguimiento de Lutra: lo que ve el usuario, el cuestionario WHO-5, el informe que el usuario exporta para su profesional y los requisitos de seguridad.
Las definiciones y fórmulas de cada métrica están en `docs/METRICS.md`; el plan de trabajo en `docs/ROADMAP.md`.

> Estado: **todo este documento es [PLAN]** salvo donde se indique [ACTUAL].

---

## 1. Objetivo y contexto de uso

- **Usuarios:** personas **mayores de 18 años**.
- **Uso profesional:** un psicólogo o profesional de salud mental usa Lutra con sus pacientes. El paciente registra en el día a día y **exporta él mismo** el informe para compartirlo con su profesional. No hay panel de profesional ni vinculación de cuentas (trabajo futuro).
- **Finalidad:** ver evolución, hábitos, contextos que influyen y qué estrategias de regulación ayudan. **No es una herramienta diagnóstica** ni un servicio de emergencias. Se evita cualquier finalidad diagnóstica para no entrar en el ámbito del Reglamento (UE) 2017/745 de productos sanitarios.

---

## 2. Pantalla de Estadísticas (usuario)

`AppState.Charts` — se rehace `ChartsView`; `ChartsController` pasa a usar `ReportCalculator`.

### 2.1 Problemas actuales que se corrigen [ACTUAL]

| Problema | Corrección |
|---|---|
| Los placeholders restaurados (`RecordSource.Restored*`) cuentan en las estadísticas | **Hecho:** `GetUserEmotionsForPeriod` |
| Efecto de minijuegos = resta de índices de `EmotionType`; si el usuario no elige, `EmotionAfter = EmotionBefore` → efecto ≈ 0 | Efecto con `MoodBefore`/`MoodAfter` y solo sesiones válidas (`METRICS.md` §4.6) |
| "Línea" de emociones con el índice del enum como eje Y | Gráfico de ánimo 1–5 real |
| Mapa de calor siempre del mes actual | Sigue el periodo elegido |
| Emoción y minijuego mostrados con `.ToString()` (en inglés) | **Hecho:** `ToDisplayName()` |
| `GenerateWeeklyReport()` no se llama desde ningún sitio | Se sustituye por el resumen semanal en el centro de notificaciones (`NOTIFICATION_CENTER.md` §9) |
| 8 consultas de sesiones (una por tipo) filtradas en memoria | **Hecho:** `DataRepository.GetSessionsForPeriod(from, to)` |

### 2.2 Estructura de la pantalla

De arriba abajo (scroll vertical):

1. **Selector de periodo:** Semana · Mes · Todo.
2. **Resumen:** ánimo medio (carita) + flecha de tendencia + frase ("Esta semana te has sentido mejor que la anterior").
3. **Tu ánimo:** gráfico de línea 1–5 de la serie diaria, puntos coloreados por emoción (`EmotionTheme`) y media de 7 días.
4. **Calendario:** mapa de calor del periodo — Semana: 7 celdas · Mes: calendario del mes · Todo: últimas 12 semanas. Celda gris = sin registro.
5. **Tus emociones:** barras de distribución + "Has sentido X emociones distintas".
6. **Estabilidad** (solo con ≥ 14 pares de días consecutivos): frase positiva ("Tu ánimo ha estado más estable que el mes pasado").
7. **Qué influye en ti:** hasta 3 motivos con mayor diferencia de ánimo (↑ / ↓), mínimo 3 apariciones.
8. **Qué te ayuda:** minijuegos con mejor ΔÁnimo (≥ 3 sesiones válidas) + progreso de respiración en BreathJump ("Ritmo medio: 8 → 6,5 respiraciones/min").
9. **Bienestar (WHO-5):** índice 0–100 en línea a lo largo del tiempo. **Sin etiquetas clínicas** para el usuario.
10. **Hábitos:** racha actual y máxima, % de días registrados, entradas de diario y palabras escritas.
11. Botón **"Exportar informe para mi profesional"** → §4.

**Estados vacíos:** cada bloque sin datos suficientes muestra "Registra X días más para ver esto" en vez de cifras poco fiables.

**Lenguaje:** sin términos técnicos (MSSD, inercia, entropía) ni etiquetas clínicas. Las cifras técnicas solo están en el informe.

---

## 3. Cuestionario WHO-5

### 3.1 Por qué WHO-5

Índice de bienestar de la OMS: 5 ítems, ~1 minuto, uso libre sin licencia, validado en español y con puntos de corte publicados (Topp et al., 2015). Aporta al informe una medida estandarizada (nivel A) junto a los datos diarios.

> **Estado: código implementado (Fase 3).** Falta montar la pantalla en Unity (`ROADMAP.md` → "Pasos en Unity"). El protocolo de apoyo tras un índice ≤ 28 llega en la Fase 4.

Código: `Features/Who5/` (`Who5Questionnaire` con ítems, puntuación y calendario; `Who5Screen`, `Who5Controller`, `Who5View`) y `Core/Systems/Who5Scheduler`.

### 3.2 Disponibilidad (cada 14 días)

- **Primera aplicación (línea base):** disponible desde el alta del perfil (`UserProfile.CreationDate`; si no es válida, desde hoy). Los usuarios que ya existían lo tienen disponible en cuanto actualizan la app.
- **Siguientes:** disponible cuando `hoy ≥ día del último envío + 14`. El intervalo coincide con la ventana de la escala ("las últimas dos semanas").
- **No** se muestra tras el check-in. Cuando está disponible:
  1. `Who5Scheduler.CheckAvailability()` crea una **notificación anclada** con id `who5-{yyyy-MM-dd}` (fecha de disponibilidad; no se duplica entre dispositivos). Se comprueba en `MainMenuScreen.OnScreenFocused`.
  2. El botón de notificaciones del menú principal muestra **"!"**.
  3. **Push del sistema:** al enviar un cuestionario se programa con `NotificationManager.ScheduleWho5Reminder` el aviso del siguiente, el día en que vuelva a estar disponible, a la hora del recordatorio diario (o la hora por defecto). No hay push para la línea base: el usuario ya está en la app.
- Si el usuario tarda, la notificación sigue anclada con su fecha original (el informe mide el retraso con `AvailableSince`); el siguiente ciclo cuenta 14 días **desde el envío**.

### 3.3 Flujo

```
Centro de notificaciones → notificación anclada "Cuestionario de bienestar" → [Hacer ahora]
  → Who5Screen (AppState.Who5, sin barra inferior)
      Introducción: "Piensa en cómo te has sentido durante las últimas dos semanas…" → [Empezar]
      5 ítems, uno por pantalla: progreso "Pregunta N de 5", 6 opciones, [Anterior] / [Siguiente]
      [Siguiente] solo activo con una opción elegida; en el 5.º pasa a [Enviar]
  → Envío (Who5Controller._safeSubmit):
      1. Guardar ScaleResponse (sync automático a Firestore)
      2. ResolvePinned("who5-{fecha}") → desaparece del centro y se quita la "!"
      3. +20 monedas → EmitCoinsChanged + notificación de recompensa
      4. Programar el push del siguiente cuestionario
      5. (Fase 4) Si índice ≤ 28 → protocolo de apoyo (§6.3)
      6. Agradecimiento: "Tu índice de bienestar: N / 100", "+20 monedas",
         "Volverá a estar disponible el 20 de octubre" → [Listo] → centro de notificaciones
```

- **Salir sin terminar** (botón cerrar, cambiar de pantalla o cerrar la app): las respuestas se descartan (`Who5Screen.OnScreenUnfocused` → `DiscardAnswers`) y la notificación anclada y la "!" **siguen hasta el envío**. Al volver empieza de cero: la escala se responde de una vez (~1 min) y así cada aplicación es comparable.
- Si el guardado falla, se avisa con un toast y se puede reintentar. Si el guardado sale bien pero falla un paso posterior, se registra el error y se muestra igualmente el agradecimiento.
- La recompensa es **independiente de las respuestas** para no influir en ellas.
- Al usuario se le muestra solo su índice 0–100 y la evolución en Estadísticas, **sin interpretación clínica**.

### 3.4 Ítems (versión española)

Enunciado: *"Durante las últimas dos semanas…"*

1. Me he sentido alegre y de buen humor.
2. Me he sentido tranquilo/a y relajado/a.
3. Me he sentido activo/a y enérgico/a.
4. Me he despertado fresco/a y descansado/a.
5. Mi vida diaria ha estado llena de cosas que me interesan.

Opciones (puntuación): Todo el tiempo (5) · La mayor parte del tiempo (4) · Más de la mitad del tiempo (3) · Menos de la mitad del tiempo (2) · De vez en cuando (1) · Nunca (0).

> Textos en `Who5Questionnaire.Items` / `Options`. **Verificar el texto exacto con la versión oficial en español** publicada por la Psychiatric Research Unit (Mental Health Centre North Zealand) antes de publicar la app. Las formas "/a" son una adaptación de lenguaje inclusivo que hay que mencionar en la memoria.

### 3.5 Puntuación

Bruta 0–25 → índice ×4 = 0–100. Interpretación **solo en el informe**: ≤ 50 bienestar bajo, ≤ 28 probable depresión, cambio ≥ 10 puntos relevante (`METRICS.md` §4.8).

### 3.6 Modelo `ScaleResponse` (SQLite `ScaleResponses`)

```csharp
int       Id               // PK autoincrement
string    RemoteId         // GUID; Firestore users/{uid}/scaleResponses/{RemoteId}
ScaleType Scale            // enum: Who5 = 0 (preparado para otras escalas)
DateTime  AvailableSince   // día en que pasó a estar disponible (del id de la notificación)
DateTime  CompletedAt      // DateTime.Now al enviar  [Indexed]
string    AnswersJson      // JSON int[5], cada uno 0-5 (propiedad [Ignore] Answers)
int       RawScore         // 0-25
int       Score            // 0-100
float     DurationSeconds  // desde el primer ítem hasta Enviar
```

- `DataRepository`: `SaveScaleResponse`, `GetLastScaleResponse(scale)`, `GetScaleResponsesForPeriod(scale, from, to)`, `GetAllScaleResponses`. `DeleteAllData` vacía la tabla.
- Firestore `users/{uid}/scaleResponses/{RemoteId}`: `scale`, `availableSince` (yyyy-MM-dd), `completedAt` (ISO 8601), `answersJson`, `rawScore`, `score`, `durationSeconds`. `DeleteUserData` la borra.
- Sincronización: `CloudSync.PushScaleResponse` y paso `_syncScaleResponses` (unión por `RemoteId`; los envíos no se editan). **Requiere permiso en las reglas de Firestore.**

---

## 4. Exportación del informe

### 4.1 Flujo

Estadísticas → **Exportar informe para mi profesional** → panel de exportación:

| Opción | Defecto |
|---|---|
| Periodo | Últimos 30 días (otras: 7 días, 3 meses, todo, desde–hasta) |
| Incluir notas y texto del diario | **Desmarcado** |
| Incluir datos en bruto (CSV) | Marcado |

→ **Generar** → se crean los archivos en `Application.temporaryCachePath` → menú nativo de compartir (plugin **NativeShare**, licencia MIT) → el usuario elige destino (correo, WhatsApp, Drive…).

- Nombres: `Lutra_Informe_yyyy-MM-dd.pdf` y `Lutra_Datos_yyyy-MM-dd.zip`.
- Los archivos temporales se borran al volver a la app.
- La exportación JSON actual de Ajustes (`SettingsManager.ExportUserData`) [ACTUAL] guarda en `persistentDataPath`, inaccesible para el usuario en móvil. Se mantiene como "Descargar todos mis datos" (derecho de portabilidad, RGPD art. 20), pero se comparte con NativeShare.

### 4.2 Generación del PDF

- Generador propio `PdfDocumentWriter` (PDF 1.4), sin dependencias: texto, líneas, rectángulos y polilíneas para los gráficos (vectoriales, nítidos al imprimir).
- Fuentes estándar Helvetica / Helvetica-Bold con codificación WinAnsi: no hay que incrustarlas y cubren los caracteres del español (á, é, ñ, ü, ¿, ¡). Sin emojis.
- A4 vertical; pie con "Página N de M", fecha de generación y "Generado por Lutra".
- Los contenidos se calculan en `ReportCalculator` (los mismos números que la pantalla de Estadísticas) y se maquetan en `ReportPdfBuilder`.

### 4.3 Estructura del PDF

Cada cifra lleva su *n*; cada sección indica su nivel de evidencia (A / B / C).

| # | Sección | Contenido |
|---|---|---|
| 0 | **Portada** | Nombre, edad, periodo, fecha de generación, días en la app. Aviso: "Datos autoinformados por el usuario. No es una herramienta diagnóstica." |
| 1 | **Resumen** (1 página) | Adherencia · ánimo medio y tendencia vs periodo anterior · % días buenos/malos · emoción predominante · último WHO-5 y su cambio · minijuego con mejor efecto · **indicadores de atención**: rachas de ≥ 3 días con ánimo ≤ 2, WHO-5 ≤ 50 / ≤ 28, cambio ≥ 10 puntos, activaciones del protocolo de apoyo |
| 2 | **Evolución del ánimo** (B) | Gráfico de la serie diaria + media de 7 días · tabla semanal (media, DE, n) · pendiente · variación intradía |
| 3 | **Dinámica emocional** (B) | Inestabilidad (MSSD), inercia (autocorrelación), variabilidad (DE), comparadas con el periodo anterior. Nota metodológica breve con referencias |
| 4 | **Perfil emocional** (B) | Distribución de las 8 emociones · balance de valencia · cuadrantes de activación derivados (indicado como aproximación teórica) · emodiversidad |
| 5 | **Contexto y motivos** (B) | Tabla por motivo: frecuencia, ánimo con/sin, diferencia, emoción más frecuente. Aviso: asociación, no causalidad |
| 6 | **Patrones temporales** (B) | Ánimo por día de la semana · horas de los registros de Momento |
| 7 | **Bienestar WHO-5** (A) | Tabla de aplicaciones (fecha, índice, cambio, tiempo de respuesta) · gráfico · líneas de corte 50 y 28 · respuestas por ítem |
| 8 | **Regulación emocional: minijuegos** (B/C) | Uso (partidas, minutos, horario, emoción de partida) · efecto en el ánimo por juego y por emoción de partida (solo sesiones válidas, con n) · evolución de la respiración en BreathJump (rpm, exhalación/inhalación, regularidad) |
| 9 | **Diario** (C) | Entradas/semana, palabras/entrada · tendencia semanal de % primera persona, % emoción negativa, % positiva (si está activado). Limitaciones del análisis |
| 10 | **Adherencia y hábitos** | % días registrados, rachas, huecos y su duración, ánimo previo a los huecos, WHO-5 completados / disponibles |
| 11 | **Notas del usuario** | Solo si se marcó "Incluir notas y diario": notas de los check-ins y entradas del diario en orden cronológico |
| 12 | **Anexo: metodología** | Definición y fórmula de cada métrica, mínimos de datos, criterios de exclusión (placeholders, sesiones no válidas), niveles de evidencia, bibliografía |
| 13 | **Anexo: glosario** | Términos para profesionales no familiarizados con EMA |

### 4.4 Datos en bruto (`Lutra_Datos_yyyy-MM-dd.zip`)

Formato pensado para Excel, SPSS, R o Python: UTF-8 con BOM, separador `,`, decimales con `.`, fechas ISO 8601, una fila por observación. Los identificadores son **números secuenciales anónimos**, no los `RemoteId`.

| Archivo | Una fila por… | Columnas |
|---|---|---|
| `registros.csv` | registro emocional | `id, fecha, hora, tipo (dia/momento), animo, emocion, valencia_derivada, activacion_derivada, motivos (separados por \|), nota*` |
| `partidas.csv` | partida | `id, inicio, minijuego, duracion_s, emocion_antes, emocion_despues, animo_antes, minutos_desde_registro_antes, animo_despues, sesion_valida, puntuacion` |
| `partidas_metricas.csv` | métrica de partida (formato largo) | `id_partida, minijuego, clave, valor` |
| `who5.csv` | aplicación | `id, disponible_desde, completado, item1…item5, bruta, indice, duracion_s` |
| `diario_semanal.csv` | semana | `semana_inicio, entradas, palabras, pct_primera_persona, pct_emocion_negativa, pct_emocion_positiva` (vacías si el análisis está desactivado) |
| `diario.csv`* | entrada | `id, fecha, titulo, contenido, emocion` |
| `avisos_apoyo.csv` | activación del protocolo | `fecha, motivo (animo_bajo_3_dias / who5_bajo)` |
| `LEEME.txt` | — | Diccionario de variables, códigos de cada valor, criterios de exclusión y versión de la app |

\* Solo si se marcó "Incluir notas y diario"; si no, la columna `nota` va vacía y `diario.csv` no se genera.

---

## 5. Análisis del diario en la app

> **Código implementado (Fase 5).**

- `DiaryLexicon` + `DiaryLanguageAnalyzer` (clases puras, con tests) + diccionario `Assets/Resources/DiaryLexicon_es.txt` (cargado por `ReportDataLoader.Lexicon`).
- Se calcula al abrir Estadísticas o generar el informe, a partir del título y el contenido locales. No añade campos a BD.
- Interruptor en Ajustes: **"Análisis de escritura para el informe"** (activado por defecto; se elige también en el consentimiento). Desactivado, se siguen contando las palabras pero no los porcentajes de lenguaje.
- El usuario ve solo "X entradas · Y palabras"; las métricas de lenguaje solo aparecen en el informe.
- Detalle y limitaciones: `METRICS.md` §4.7.

---

## 6. Seguridad y requisitos legales

> **Código implementado (Fase 4), salvo §6.1 (edad), que queda pendiente en `ROADMAP.md`.**

### 6.1 Edad (≥ 18) — pendiente

- `OnboardingProfile`, paso de fecha de nacimiento: si la edad calculada con `DateTime.Today` es menor de 18, se muestra "Lutra está pensada para personas mayores de 18 años" y no se puede continuar.
- Perfiles restaurados al iniciar sesión: misma comprobación antes de navegar.

### 6.2 Consentimiento (RGPD art. 9, datos de salud)

Pantalla `ConsentScreen` (`AppState.Consent`). Se llega con `ConsentGate.ContinueTo(destino)`, que sustituye a la navegación a MainMenu / EmotionCheck en tres puntos:
- **Usuarios nuevos:** al terminar el onboarding de perfil y **antes del primer check-in**. Nombre y fecha de nacimiento no son datos de salud; los registros emocionales sí, y empiezan en el check-in. Así el consentimiento llega antes de tratar datos de salud y se puede guardar en el perfil.
- **Usuarios existentes sin consentimiento** (o con una versión anterior): al abrir la app (`GameManager`) y al iniciar sesión (`LoginController`).

Textos en `ConsentTexts` (resumen):

- Qué datos se registran y para qué (bienestar personal y seguimiento con su profesional).
- Dónde se guardan: en el dispositivo y en una copia en la nube (Firebase) asociada a su cuenta.
- Que **nadie** recibe sus datos salvo que él exporte y comparta el informe.
- Que no es una herramienta diagnóstica ni de emergencias (con el 024 y el 112 visibles).
- Derechos: acceso y portabilidad (exportar), supresión (Ajustes → eliminar datos), retirada del consentimiento.

Casillas:
1. **Obligatoria:** "Acepto el tratamiento de mis datos de bienestar emocional para el funcionamiento de Lutra".
2. **Opcional (marcada por defecto, editable):** "Permitir el análisis de mi escritura del diario para mis informes".

"Aceptar y continuar" solo se activa con la casilla obligatoria marcada. **"No acepto"** cierra la sesión y vuelve al login con un aviso: sin consentimiento no se pueden registrar datos de bienestar.

Se guarda en `UserProfile.Preferences` (se sincroniza con Firestore con el resto de preferencias): `consentVersion` (`ConsentGate.CurrentVersion` = "1.0"), `consentDate` (ISO 8601), `diaryLanguageAnalysis` ("true"/"false"). Si cambia el texto del consentimiento, se sube `CurrentVersion` y se vuelve a pedir.

### 6.3 Protocolo de apoyo (crisis)

Código: `SupportRules` (reglas puras, con tests), `SupportProtocol` (comprobación y activación) y `SupportDialog` (componente global, como `ToastNotification`).

**Disparadores:**
1. Tres días naturales **consecutivos** (hoy y los dos anteriores) con check-in de Día y `MoodLevel ≤ 2` — `EmotionCheckController` lo comprueba tras guardar un check-in de Día (nuevo o editado).
2. WHO-5 con índice **≤ 28** — `Who5Controller` lo comprueba al enviar.

**Acción:**
- Diálogo `SupportDialog`, que se puede cerrar, en tono cálido y no diagnóstico: "Parece que estos días están siendo difíciles. No tienes que pasarlo solo/a."
  - **Llamar al 024** (Línea de atención a la conducta suicida, gratuita, 24 h) → `Application.OpenURL("tel:024")`
  - **Emergencias 112**
  - Recordatorio: "Puedes compartir cómo te sientes con tu profesional"
  - **Ahora no**
- Se guarda una notificación de tipo `Support` (no anclada; `SourceRef` = `animo_bajo_3_dias` o `who5_bajo`) con botón **"Ver recursos"**, para encontrarlos después.
- Como máximo **una activación cada 7 días** (evitar fatiga de avisos): se mira la última notificación `Support`.
- Esas notificaciones son el registro de activaciones: `ReportCalculator` las lista en `SupportActivations` para el informe (§4.3, sección 1) y `avisos_apoyo.csv`.
- Acceso permanente: **Ajustes → Recursos de ayuda** (`SupportDialog.ShowResources`).

> El 024 es un servicio de España. Si la app se distribuye en otros países, los recursos deben depender de la región.

---

## 7. Limitaciones metodológicas (para la memoria)

- Todo el seguimiento diario es **autoinformado**: deseabilidad social y adherencia variable.
- La **activación** no se mide; se deriva de la emoción (aproximación teórica).
- **Una emoción por registro**: no se puede medir la granularidad emocional; se usa la emodiversidad.
- El **efecto de los minijuegos** compara antes/después sin grupo control: puede reflejar el simple paso del tiempo o la regresión a la media.
- `MoodBefore` se toma del último registro, no se pregunta al empezar; se limita a registros de ≤ 3 h para mantener la validez.
- Las métricas de minijuegos y de lenguaje son **exploratorias** (nivel C), útiles para comparar al usuario consigo mismo, no con normas poblacionales.
- El análisis de lenguaje infraestima la primera persona en español (sujeto omitido).
- Los datos que faltan no faltan al azar.
