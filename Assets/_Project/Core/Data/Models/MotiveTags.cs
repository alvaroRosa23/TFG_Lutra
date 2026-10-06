using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Motivos del check-in (EmotionRecord.SelectedMotiveTags). Hay tres tipos:
    ///   - Hobbies del perfil: se guarda el nombre del enum HobbyType ("Football").
    ///   - Motivos fijos: se guarda el texto ("Trabajo").
    ///   - "Otro": se guarda el texto libre que escribió el usuario.
    /// Para estadísticas, los libres se agrupan como "Otros" (su texto es contenido del usuario).
    /// </summary>
    public static class MotiveTags
    {
        public const string OtherKey = "Otros";

        public static readonly string[] Fixed =
        {
            "Trabajo", "Familia", "Salud", "Ocio", "Relaciones", "Estudio", "Deporte"
        };

        /// <summary>Lee el JSON del registro; lista vacía si no hay motivos o el JSON no es válido.</summary>
        public static List<string> Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<string>();
            try   { return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        /// <summary>Clave para agrupar: el propio motivo si es hobby o fijo; "Otros" si es texto libre.</summary>
        public static string GroupKey(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return OtherKey;
            return Array.IndexOf(Fixed, tag) >= 0 || _tryParseHobby(tag, out _) ? tag : OtherKey;
        }

        /// <summary>Nombre visible de una clave de GroupKey.</summary>
        public static string DisplayName(string key)
            => _tryParseHobby(key, out var hobby) ? hobby.ToDisplayName() : key;

        /// <summary>Nombre exacto de un HobbyType (no acepta números ni mayúsculas distintas).</summary>
        private static bool _tryParseHobby(string tag, out HobbyType hobby)
        {
            hobby = default;
            return !string.IsNullOrEmpty(tag) && !char.IsDigit(tag[0]) && tag.IndexOf(',') < 0
                && Enum.TryParse(tag, out hobby) && Enum.IsDefined(typeof(HobbyType), hobby);
        }
    }
}
