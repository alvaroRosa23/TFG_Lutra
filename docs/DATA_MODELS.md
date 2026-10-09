# Modelos de datos

## Tablas SQLite

### EmotionRecord (tabla `EmotionRecords`)
```csharp
int          Id                   // PK autoincrement
DateTime     Timestamp            // hora local del dispositivo
EmotionType  EmotionType
int          IntensityLevel       // 1-5; espeja MoodLevel (se asigna IntensityLevel = MoodLevel en el controller)
bool         IsMorningCheck       // true = check de día, false = momento
string       Notes                // texto libre del usuario (nullable)
int          MoodLevel            // 1-5 (caritas del check-in)
string       SelectedEmotionTags  // JSON array de strings
string       SelectedMotiveTags   // JSON array de strings
string       PhotoPath            // ruta local a foto adjunta (nullable)
string       SongTitle            // reservado para uso futuro
string       SongArtist           // reservado para uso futuro
RecordSource Source               // User=0 (defecto), RestoredFirestore=1, RestoredFirestoreHistory=2
                                  // columna añadida via migración automática; defecto 0 correcto para registros previos
string       RemoteId             // id en Firestore emotions/{RemoteId} (GUID; lo asigna DataRepository).
                                  // PhotoPath no se sube (es un archivo local); los placeholders no se suben
```

### DiaryEntry (tabla `DiaryEntries`)
```csharp
int      Id          // PK autoincrement
DateTime Date        // fecha y hora de creación (puede haber varias entradas el mismo día)
string   Title
string   Content
string   Mood        // nombre del enum EmotionType como string
string   AudioPath   // ruta local (nullable; NO se sincroniza a Firestore)
string   ImagePath   // ruta local (nullable; NO se sincroniza a Firestore)
string   RemoteId    // id en Firestore diary/{RemoteId}: GUID, o "yyyy-MM-dd" en entradas antiguas
```

### UserProfile (tabla `UserProfiles`)
```csharp
int        Id
string     Name, Surname
string     Avatar
DateTime   CreationDate, DateOfBirth
int        Coins                   // solo cambia con AddCoins/SpendCoins/SetCoins; SaveUserProfile la conserva
string     FirebaseUserId          // UID de Firebase Auth
string     Email
CultureType Culture
string     HobbiesJson             // JSON de List<HobbyType>
string     PreferencesJson         // JSON de Dictionary<string,string>
// Propiedades de conveniencia [Ignore]:
List<HobbyType>              Hobbies        // serializa/deserializa HobbiesJson
Dictionary<string,string>    Preferences    // serializa/deserializa PreferencesJson
List<string>                 UnlockedItems  // se carga con DataRepository.GetUnlockedItemIds(profile.Id)
```

Claves usadas en `Preferences`: `"featherColorIndex"` (int), `"mascotAccessoryId"` (string).

### InventoryItem (tabla `InventoryItems`)
```csharp
int      Id              // PK autoincrement
int      UserId          // FK UserProfile.Id
string   ItemId          // coincide con SafeZoneItem.itemId / RewardDefinition.itemId
DateTime UnlockedAt
bool     IsPlaced        // true = colocado en la habitación
int      PlacementIndex  // índice en PlacementPoint[] de SafeZoneView; -1 = no colocado
```
Reemplaza al antiguo `UnlockedItem` (tabla `UnlockedItems`). El archivo `UnlockedItem.cs` aún existe pero no se usa; eliminar antes del build final.

---

### StarCollectionEntry (tabla `StarCollection`)
```csharp
int      Id              // PK autoincrement
string   StarId          // único; StarDefinition.starId
int      TimesCaught
DateTime FirstCaughtAt
DateTime LastCaughtAt    // la estrella de racha se da una vez al día: se mira esta fecha
```
Estrellas descubiertas en StarFisher (siempre se liberan; solo queda el registro). Se sincroniza
con Firestore en `users/{uid}/stars/{StarId}`. Acceso: `DataRepository.GetStarCollection`,
`RegisterStarCatch`, `MergeStarCollectionEntry` (a través de `StarCollectionStore`).

---

### MinigameSession (tabla `MinigameSessions`)
```csharp
int          Id                  // PK autoincrement
DateTime     StartTime
float        DurationSeconds
MinigameType MinigameId
EmotionType  EmotionBefore
EmotionType  EmotionAfter
float        RelaxationScore     // 0.0 - 1.0
string       MetricsJson         // métricas propias de cada minijuego
string       RemoteId            // id en Firestore minigameSessions/{RemoteId} (GUID); los récords salen de aquí
int?         MoodBefore          // 1-5; ánimo más reciente al empezar (DataRepository.GetLatestMood), no se pregunta
DateTime?    MoodBeforeRecordedAt// cuándo se registró MoodBefore (para la ventana de 3 h, docs/METRICS.md §4.6)
int?         MoodAfter           // 1-5; caritas de PostMinigameScreen; null = no respondió
```
`EmotionAfter` vale `EmotionBefore` por defecto si el usuario no elige: no usarlo para medir efecto.

`MinigameSessionExtensions` (mismo archivo): `IsValidForMoodEffect()` (hay `MoodAfter` y el ánimo previo es de ≤ 3 h, `MaxMoodBeforeAgeHours`) y `MoodDelta()`. **Toda métrica de efecto de minijuegos debe usar esta regla.**

### AppNotification (tabla `Notifications`)
Centro de notificaciones. Campos, enums `NotificationType` / `RewardSource` (guardados como int: no reordenar) y DTO en memoria `RewardGrant` → `docs/NOTIFICATION_CENTER.md` §2–3.

### ScaleResponse (tabla `ScaleResponses`)
Envío completo de una escala validada (WHO-5). Enum `ScaleType { Who5 = 0 }`. Campos → `docs/PROFESSIONAL_REPORT.md` §3.6.

## Clases auxiliares de modelos (`Core/Data/Models/`)

- **`EmotionCircumplex`**: `ValenceOf()` (`Valence`: Unpleasant / Mixed / Pleasant), `ArousalOf()` (`Arousal`: Low / High), `QuadrantOf()` (`AffectQuadrant`: Tension, LowMood, Calm, Enthusiasm, Mixed). Derivación teórica (`docs/METRICS.md` §3.1).
- **`MotiveTags`**: `Fixed` (motivos fijos del check-in), `Parse(json)`, `GroupKey(tag)` (hobby o fijo → él mismo; texto libre → "Otros"), `DisplayName(key)`.
- **`UserProfile.Preferences`** — claves en uso: `consentVersion`, `consentDate`, `diaryLanguageAnalysis` (`ConsentGate`), `lastDiaryRewardDate` (`DiaryController`), `featherColorIndex`, `mascotAccessoryId` (`MascotCustomizer`), `appSettings` (JSON de `AppSettings`: todos los ajustes del usuario, `SettingsManager`).

---

## Clases en memoria (no SQLite)

### MinigameResult
```csharp
MinigameType                Type
int                         DurationSeconds
float                       RelaxationScore     // 0.0 - 1.0
bool                        CompletedNaturally
EmotionType                 EmotionBefore
EmotionType                 EmotionAfter
Dictionary<string, float>   Metrics
DateTime                    StartTime
int?                        CoinReward          // monedas calculadas por el juego; null = fórmula por defecto de MinigameLoader
```

### AppSettings / UserAccount
Sin tabla propia en SQLite. `AppSettings` (todos los ajustes de la app) se guarda en `PlayerPrefs` (copia del dispositivo) y como JSON en `UserProfile.Preferences["appSettings"]` (`ToJson` / `FromJson`; los campos que falten toman su valor por defecto), que se sincroniza con Firestore. `UserAccount` solo existe en memoria.

---

## Enumeraciones

### RecordSource
```
User                     = 0  // registro introducido por el usuario (defecto)
RestoredFirestore        = 1  // placeholder del check-in de hoy restaurado desde Firestore
RestoredFirestoreHistory = 2  // placeholder de un día anterior restaurado desde Firestore
```
Usado en `EmotionRecord.Source`. `DataRepository.GetLastEmotion()` filtra `Source == User` primero para no devolver placeholders como emoción activa.

### EmotionType (ordenadas por valencia)
| Enum | Valor | Emoción |
|---|---|---|
| `Anxiety` | 0 | Ansiedad |
| `Overwhelm` | 1 | Agobio |
| `Frustration` | 2 | Frustración |
| `Sadness` | 3 | Tristeza |
| `Nostalgia` | 4 | Nostalgia |
| `Calm` | 5 | Calma |
| `Energy` | 6 | Energía |
| `Joy` | 7 | Alegría |

Nombres en español: usar siempre las extensiones `ToDisplayName()` de `EmotionType`, `MinigameType` y `HobbyType` (en el mismo archivo que cada enum, como `StarRarity`). No reimplementar `switch` de nombres.

### MinigameType
`Unpacking=0, FruitNinja=1, Beatmaker=2, SandCastle=3, FluidSim=4, BreathJump=5, Puzzle=6, StarFisher=7` (se guarda como int en `MinigameSession.MinigameId`: no reordenar). Implementados: Beatmaker, BreathJump, FruitNinja y StarFisher (`docs/MINIGAMES.md`).

### MinigameTag (12 valores)
```
Descarga, Energia, Respiracion, Calma, Ritmo, Foco, Orden, Tierra, Flujo, Relax, Logica, Contemplacion
```

### HobbyType (25 valores)
```
Football, Basketball, Tennis, Swimming, Cycling, Running, Yoga, Dancing, Cooking, Reading,
Gaming, Music, Drawing, Photography, Traveling, Hiking, Meditation, Writing, Cinema, Theater,
Crafts, Gardening, Volunteering, Fitness, Surfing
```

### CultureType
```
African, Indian, EastAsian, Western, LatinAmerican
```

### ColorblindMode
```
None, Deuteranopia, Protanopia, Tritanopia
```

### ItemCategory
```
Furniture, Plant, Decoration, WallItem, MascotAccessory
```
`MascotAccessory` se excluye de la barra de inventario y de los PlacementPoints de habitación.

### PlacementType
```
None, Furniture, Plant, WallArt, Decoration
```
Cada `SafeZoneItem` declara su `placementType`; cada `PlacementPoint` declara su `acceptedTypes[]` (vacío = acepta todos).

### MoodLevel
`VeryBad=1 … VeryGood=5` (5 caritas del check-in).

### EmotionCheckMode
`Day` / `Moment`.

### ChartPeriod
`Week`, `Month`, `AllTime` — definido en `ChartsController.cs`.

---

## ScriptableObjects de datos

### EmotionTheme (`Core/Data/ScriptableObjects/`)
Un asset por emoción.
```csharp
EmotionType emotionType
string      displayName              // nombre localizado
Color       primaryColor             // color de _backgroundImage durante la transición
Color       backgroundColor          // color de Camera.backgroundColor
Sprite      mascotExpression         // sprite facial del búho
AnimatorOverrideController mascotAnimatorOverride
AudioClip   ambientLoop              // música en loop
float       ambientVolume            // [0, 1]
GameObject  ambientParticlesPrefab   // sistema de partículas; se instancia como hijo de ThemeManager
float       transitionDuration       // duración del crossfade (defecto 0.8s)
```

### CultureColorOverride (`Core/Data/ScriptableObjects/`)
`[CreateAssetMenu("Lutra/Culture Color Override")]`; campos: `cultureType`, `EmotionColorEntry[] emotionOverrides`; método `TryGetColors(EmotionType, out EmotionColorEntry)` → devuelve true si hay override para esa emoción.
- Crear un asset por cultura y asignarlos al array `_cultureOverrides` del ThemeManager en el Inspector
- ⚠ Los colores deben tener alpha = 1 (el Inspector los inicia a 0 al añadir entradas nuevas)

### EmotionColorEntry
**Clase** (no struct) serializable con `emotionType`, `primaryColor` (default `Color.white`), `backgroundColor` (default `Color.black`); usada por `CultureColorOverride`.

### MinigameDefinition (`Core/Data/ScriptableObjects/`)
Un asset por minijuego.
```csharp
MinigameType  minigameType
string        displayName
string        description
Sprite        logo
Sprite        previewImage
MinigameTag[] tags
int           estimatedTimeSeconds
bool          isAvailable           // false → muestra "Próximamente" en la UI
```

### EmotionMinigameMap (`Core/Data/ScriptableObjects/`)
Mapea cada `EmotionType` a un array de `MinigameTag[]` recomendados.
Usado por `MinigamesController._getRecommended(EmotionType)` para filtrar los minijuegos sugeridos.
Asignado al campo `_emotionMap` del `MinigamesController` en el Inspector.

### SafeZoneItem (`Features/SafeZone/`)
`[CreateAssetMenu("Lutra/SafeZone Item")]`; un asset por ítem decorativo.
```csharp
string        itemId               // identificador único; coincide con InventoryItem.ItemId
string        displayName
Sprite        previewSprite        // icono en barra de inventario y diálogo de compra
GameObject    prefab               // instanciado en PlacementPoint.ItemAnchor al colocar
int           coinCost             // 0 = gratis; se vende al 50% (coinCost / 2)
int           requiredStreakDays   // 0 = compra con monedas; >0 = desbloqueo por racha
ItemCategory  category             // Furniture, Plant, Decoration, WallItem, MascotAccessory
PlacementType placementType        // debe coincidir con acceptedTypes del PlacementPoint destino
bool          isUnlockedByDefault  // true = se desbloquea automáticamente al abrir SafeZone
bool          isRewardOnly         // true = solo como recompensa (telescopio): no sale en la tienda ni se vende
SafeZoneItemInteraction interaction // None | StarSky: botón "Usar" en el menú del ítem colocado
string        interactionLabel     // texto de ese botón ("Mirar las estrellas")
```
- Asignar todos los assets al array `_allItems` del `SafeZoneController` en el Inspector
- `MascotAccessory` no aparece en la barra de inventario ni se coloca en la habitación; se gestiona desde `MascotCustomizer`

### StarDefinition / StarCatalog (`Core/Data/ScriptableObjects/`)
`StarDefinition` (menú *Lutra/Star Definition*): `starId`, `displayName`, `rarity` (`StarRarity`),
`isStreakSpecial`, `sprite`, `tint`, `weight`, `age`, `description`, `phrase`.
`StarCatalog` (menú *Lutra/Star Catalog*): `stars[]`, `rarityColors[5]`, `telescopeItemId`.
Se generan con **Lutra > StarFisher > Crear catálogo de ejemplo** (ver `docs/STARFISHER.md`).

`StarRarity` (enum, `Core/Data/Models/`): `Common, Uncommon, Rare, Epic, Legendary` (+ `ToDisplayName()`).

### RewardDefinition
ScriptableObject evaluado por `RewardSystem`.
