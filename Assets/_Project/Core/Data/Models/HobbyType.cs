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
}
