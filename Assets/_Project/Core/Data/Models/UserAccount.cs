using System;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Representa la cuenta de usuario autenticada en Firebase.
    /// No es una entidad SQLite; vive solo en memoria mientras dura la sesión.
    /// </summary>
    public class UserAccount
    {
        /// <summary>UID único asignado por Firebase Authentication.</summary>
        public string UserId { get; set; }

        /// <summary>Email con el que se registró el usuario.</summary>
        public string Email { get; set; }

        /// <summary>Nombre visible del usuario (puede ser nulo hasta que complete el perfil).</summary>
        public string DisplayName { get; set; }

        /// <summary>Fecha y hora (UTC) en que se creó la cuenta en Firebase.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Indica si el usuario ha verificado su dirección de email.</summary>
        public bool IsEmailVerified { get; set; }
    }
}
