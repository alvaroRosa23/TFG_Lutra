using System;
using Lutra.Core.Data.Models;

namespace Lutra.Features.EmotionCheck
{
    /// <summary>
    /// Datos de trabajo del flujo de check-in emocional.
    /// Se instancia al abrir la pantalla y se destruye al confirmar o cancelar.
    /// No persiste en base de datos directamente; EmotionCheckController
    /// crea el EmotionRecord definitivo al confirmar.
    /// </summary>
    public class EmotionCheckData
    {
        public EmotionType SelectedEmotion { get; set; }

        /// <summary>Intensidad percibida de 1 (muy baja) a 5 (muy alta). Por defecto 3.</summary>
        public int Intensity { get; set; } = 3;

        /// <summary>True si el check-in corresponde al momento matutino del día.</summary>
        public bool IsMorningCheck { get; set; }

        /// <summary>Nota libre opcional del usuario.</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>Momento de apertura del flujo (UTC). Se asigna automáticamente.</summary>
        public DateTime Timestamp { get; }

        public EmotionCheckData(bool isMorningCheck = false)
        {
            IsMorningCheck = isMorningCheck;
            Timestamp      = DateTime.Now;
        }
    }
}
