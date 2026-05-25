using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SQLite;
// UnlockedItems ahora se persisten en la tabla UnlockedItem (relación 1-N).
// La propiedad [Ignore] se carga por separado con DataRepository.GetUnlockedItemIds().

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Perfil del usuario con su progreso, monedas y preferencias.
    /// Los campos de colección se serializan como JSON para SQLite.
    /// </summary>
    [Table("UserProfiles")]
    public class UserProfile
    {
        /// <summary>Clave primaria autoincremental.</summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Nombre de usuario elegido en el onboarding.</summary>
        public string Name { get; set; }

        /// <summary>Apellido del usuario.</summary>
        public string Surname { get; set; }

        /// <summary>Identificador del avatar seleccionado.</summary>
        public string Avatar { get; set; }

        /// <summary>Fecha de creación del perfil (hora local del dispositivo).</summary>
        public DateTime CreationDate { get; set; }

        /// <summary>Fecha de nacimiento del usuario.</summary>
        public DateTime DateOfBirth { get; set; }

        /// <summary>Monedas disponibles para desbloquear decoraciones.</summary>
        public int Coins { get; set; }

        // ── Campos de cuenta Firebase ──────────────────────────────────

        /// <summary>UID de Firebase Auth vinculado a este perfil.</summary>
        public string FirebaseUserId { get; set; }

        /// <summary>Email con el que el usuario se autenticó.</summary>
        public string Email { get; set; }

        // ── Campos culturales y de aficiones ───────────────────────────

        /// <summary>Región cultural con la que el usuario se identifica.</summary>
        public CultureType Culture { get; set; }

        /// <summary>Lista de aficiones serializada como JSON para SQLite.</summary>
        [Column("HobbiesJson")]
        public string HobbiesJson { get; set; }

        // ── Propiedad de conveniencia (ignorada por SQLite) ────────────

        /// <summary>
        /// Lista de aficiones del usuario. Se serializa/deserializa automáticamente
        /// desde/hacia <see cref="HobbiesJson"/>.
        /// </summary>
        [Ignore]
        public List<HobbyType> Hobbies
        {
            get => string.IsNullOrEmpty(HobbiesJson)
                ? new List<HobbyType>()
                : JsonConvert.DeserializeObject<List<HobbyType>>(HobbiesJson);
            set => HobbiesJson = JsonConvert.SerializeObject(value);
        }

        // ── Columna JSON (preferencias) ────────────────────────────────

        /// <summary>JSON serializado del diccionario de preferencias clave-valor.</summary>
        [Column("PreferencesJson")]
        public string PreferencesJson { get; set; }

        // ── Propiedades ignoradas por SQLite ───────────────────────────

        /// <summary>
        /// IDs de ítems desbloqueados. No persiste en esta tabla; se carga con
        /// <c>DataRepository.GetUnlockedItemIds(profile.Id)</c> y se asigna en memoria.
        /// </summary>
        [Ignore]
        public List<string> UnlockedItems { get; set; } = new List<string>();

        [Ignore]
        public Dictionary<string, string> Preferences
        {
            get => string.IsNullOrEmpty(PreferencesJson)
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(PreferencesJson);
            set => PreferencesJson = JsonConvert.SerializeObject(value);
        }

        // ── Constructor sin parámetros requerido por sqlite-net-pcl ──
        public UserProfile() { }

        public UserProfile(string name, string avatar)
        {
            Name         = name;
            Avatar       = avatar;
            CreationDate = DateTime.Now;
            Coins        = 0;
            Preferences  = new Dictionary<string, string>();
        }
    }
}
