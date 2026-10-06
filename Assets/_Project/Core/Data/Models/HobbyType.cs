namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Aficiones disponibles para seleccionar durante el onboarding.
    /// El usuario puede elegir varias; se persisten como JSON en UserProfile.HobbiesJson.
    /// </summary>
    public enum HobbyType
    {
        Football      = 0,
        Basketball    = 1,
        Tennis        = 2,
        Swimming      = 3,
        Cycling       = 4,
        Running       = 5,
        Yoga          = 6,
        Dancing       = 7,
        Cooking       = 8,
        Reading       = 9,
        Gaming        = 10,
        Music         = 11,
        Drawing       = 12,
        Photography   = 13,
        Traveling     = 14,
        Hiking        = 15,
        Meditation    = 16,
        Writing       = 17,
        Cinema        = 18,
        Theater       = 19,
        Crafts        = 20,
        Gardening     = 21,
        Volunteering  = 22,
        Fitness       = 23,
        Surfing       = 24
    }

    public static class HobbyTypeExtensions
    {
        /// <summary>Nombre visible en español.</summary>
        public static string ToDisplayName(this HobbyType hobby)
        {
            switch (hobby)
            {
                case HobbyType.Football:     return "Fútbol";
                case HobbyType.Basketball:   return "Baloncesto";
                case HobbyType.Tennis:       return "Tenis";
                case HobbyType.Swimming:     return "Natación";
                case HobbyType.Cycling:      return "Ciclismo";
                case HobbyType.Running:      return "Running";
                case HobbyType.Yoga:         return "Yoga";
                case HobbyType.Dancing:      return "Baile";
                case HobbyType.Cooking:      return "Cocina";
                case HobbyType.Reading:      return "Lectura";
                case HobbyType.Gaming:       return "Videojuegos";
                case HobbyType.Music:        return "Música";
                case HobbyType.Drawing:      return "Dibujo";
                case HobbyType.Photography:  return "Fotografía";
                case HobbyType.Traveling:    return "Viajes";
                case HobbyType.Hiking:       return "Senderismo";
                case HobbyType.Meditation:   return "Meditación";
                case HobbyType.Writing:      return "Escritura";
                case HobbyType.Cinema:       return "Cine";
                case HobbyType.Theater:      return "Teatro";
                case HobbyType.Crafts:       return "Manualidades";
                case HobbyType.Gardening:    return "Jardinería";
                case HobbyType.Volunteering: return "Voluntariado";
                case HobbyType.Fitness:      return "Fitness";
                case HobbyType.Surfing:      return "Surf";
                default:                     return hobby.ToString();
            }
        }
    }
}
