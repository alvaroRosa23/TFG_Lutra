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
```

### DiaryEntry (tabla `DiaryEntries`)
```csharp
int      Id          // PK autoincrement
DateTime Date        // fecha normalizada a medianoche (sin hora)
string   Title
string   Content
string   Mood        // nombre del enum EmotionType como string
string   AudioPath   // ruta local (nullable; NO se sincroniza a Firestore)
string   ImagePath   // ruta local (nullable; NO se sincroniza a Firestore)
```

### UserProfile (tabla `UserProfiles`)
```csharp
int        Id
string     Name, Surname
string     Avatar
DateTime   CreationDate, DateOfBirth
int        Coins
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

### MinigameSession (tabla `MinigameSessions`)
```csharp
int          Id                  // PK autoincrement
DateTime     StartTime
float        DurationSeconds
MinigameType MinigameId
EmotionType  EmotionBefore
EmotionType  EmotionAfter
float        RelaxationScore     // 0.0 - 1.0
```

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
```

### AppSettings / UserAccount
Solo en memoria, no persisten en SQLite.

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
```
- Asignar todos los assets al array `_allItems` del `SafeZoneController` en el Inspector
- `MascotAccessory` no aparece en la barra de inventario ni se coloca en la habitación; se gestiona desde `MascotCustomizer`

### RewardDefinition
ScriptableObject evaluado por `RewardSystem`.
