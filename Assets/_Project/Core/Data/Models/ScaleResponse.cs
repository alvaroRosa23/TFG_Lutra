using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SQLite;

namespace Lutra.Core.Data.Models
{
    /// <summary>
    /// Una aplicación completa de una escala validada (WHO-5). Solo se guarda al enviar el
    /// cuestionario entero: las respuestas a medias no se persisten.
    /// Ver docs/PROFESSIONAL_REPORT.md §3.
    /// </summary>
    [Table("ScaleResponses")]
    public class ScaleResponse
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Id en Firestore scaleResponses/{RemoteId} (GUID).</summary>
        [Indexed]
        public string RemoteId { get; set; }

        public ScaleType Scale { get; set; }

        /// <summary>Día en que el cuestionario pasó a estar disponible (para medir el retraso).</summary>
        public DateTime AvailableSince { get; set; }

        /// <summary>Momento del envío (hora local del dispositivo).</summary>
        [Indexed]
        public DateTime CompletedAt { get; set; }

        /// <summary>JSON con la respuesta de cada ítem en orden (WHO-5: 5 valores de 0 a 5).</summary>
        public string AnswersJson { get; set; }

        /// <summary>Suma de las respuestas (WHO-5: 0-25).</summary>
        public int RawScore { get; set; }

        /// <summary>Puntuación final (WHO-5: bruta × 4 → 0-100).</summary>
        public int Score { get; set; }

        /// <summary>Segundos desde el primer ítem hasta Enviar (indicador de calidad de la respuesta).</summary>
        public float DurationSeconds { get; set; }

        [Ignore]
        public List<int> Answers
        {
            get => string.IsNullOrEmpty(AnswersJson)
                ? new List<int>()
                : JsonConvert.DeserializeObject<List<int>>(AnswersJson);
            set => AnswersJson = JsonConvert.SerializeObject(value);
        }

        public ScaleResponse() { }
    }
}
