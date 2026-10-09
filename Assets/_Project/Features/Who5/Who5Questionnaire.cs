using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lutra.Features.Who5
{
    /// <summary>
    /// Contenido, puntuación y calendario del WHO-5 (Índice de bienestar de la OMS).
    /// Clase estática pura (sin Unity) para poder probarla. Ver docs/PROFESSIONAL_REPORT.md §3
    /// y docs/METRICS.md §4.8.
    ///
    /// Antes de publicar la app: verificar el texto de los ítems con la versión oficial en español
    /// (Psychiatric Research Unit, Mental Health Centre North Zealand). Las formas "/a" son una
    /// adaptación de lenguaje inclusivo.
    /// </summary>
    public static class Who5Questionnaire
    {
        public const int ItemCount    = 5;
        public const int MaxItemValue = 5;

        /// <summary>Días entre aplicaciones: coincide con la ventana de la escala ("últimas dos semanas").</summary>
        public const int IntervalDays = 14;

        /// <summary>Monedas por enviar el cuestionario completo (independientes de las respuestas).</summary>
        public const int CoinReward = 20;

        // Puntos de corte (Topp et al., 2015). Solo se interpretan en el informe profesional.
        public const int LowWellBeingCutoff       = 50;
        public const int ProbableDepressionCutoff = 28;
        public const int RelevantChange           = 10;

        public const string NotificationTitle = "Cuestionario de bienestar";
        public const string NotificationBody  = "Ya puedes responder tu cuestionario de bienestar: 5 preguntas, alrededor de 1 minuto.";

        public const string Instructions =
            "Piensa en cómo te has sentido durante las últimas dos semanas. " +
            "Para cada frase, elige la opción que mejor lo describa.";

        public const string ItemPrefix = "Durante las últimas dos semanas...";

        public static readonly string[] Items =
        {
            "Me he sentido alegre y de buen humor.",
            "Me he sentido tranquilo/a y relajado/a.",
            "Me he sentido activo/a y enérgico/a.",
            "Me he despertado fresco/a y descansado/a.",
            "Mi vida diaria ha estado llena de cosas que me interesan."
        };

        /// <summary>Opciones en el orden en que se muestran, con su puntuación.</summary>
        public static readonly (string label, int value)[] Options =
        {
            ("Todo el tiempo",                 5),
            ("La mayor parte del tiempo",      4),
            ("Más de la mitad del tiempo",     3),
            ("Menos de la mitad del tiempo",   2),
            ("De vez en cuando",               1),
            ("Nunca",                          0)
        };

        // ── Puntuación ─────────────────────────────────────────────────

        /// <summary>Suma de los 5 ítems (0-25). Lanza si faltan respuestas o hay valores fuera de rango.</summary>
        public static int RawScore(IReadOnlyList<int> answers)
        {
            if (answers == null || answers.Count != ItemCount)
                throw new ArgumentException($"El WHO-5 necesita {ItemCount} respuestas.");

            int sum = 0;
            foreach (int value in answers)
            {
                if (value < 0 || value > MaxItemValue)
                    throw new ArgumentOutOfRangeException(nameof(answers), $"Respuesta fuera de rango: {value}");
                sum += value;
            }
            return sum;
        }

        /// <summary>Índice 0-100 (bruta × 4).</summary>
        public static int ToIndex(int rawScore) => rawScore * 4;

        // ── Calendario ─────────────────────────────────────────────────

        /// <summary>
        /// Día desde el que el cuestionario está disponible, o null si aún no toca.
        /// Sin respuestas previas: desde el alta del perfil (línea base). Con respuestas: 14 días
        /// después del último envío.
        /// </summary>
        public static DateTime? GetAvailableSince(DateTime profileCreated, DateTime? lastCompletedAt, DateTime today)
        {
            if (lastCompletedAt == null)
            {
                // Perfiles sin fecha de alta válida: disponible desde hoy
                DateTime baseline = profileCreated.Year < 2000 ? today.Date : profileCreated.Date;
                return baseline <= today.Date ? baseline : today.Date;
            }

            DateTime next = NextAvailableDate(lastCompletedAt.Value);
            return next <= today.Date ? next : (DateTime?)null;
        }

        /// <summary>Día en que vuelve a estar disponible tras un envío.</summary>
        public static DateTime NextAvailableDate(DateTime completedAt) => completedAt.Date.AddDays(IntervalDays);

        /// <summary>Id determinista de la notificación anclada: no se duplica entre dispositivos.</summary>
        public static string NotificationRemoteId(DateTime availableSince) => $"who5-{availableSince:yyyy-MM-dd}";

        /// <summary>Fecha de disponibilidad a partir del id de la notificación ("who5-yyyy-MM-dd").</summary>
        public static bool TryParseAvailableSince(string remoteId, out DateTime availableSince)
        {
            availableSince = default;
            if (string.IsNullOrEmpty(remoteId) || !remoteId.StartsWith("who5-")) return false;
            return DateTime.TryParseExact(remoteId.Substring(5), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                          DateTimeStyles.None, out availableSince);
        }
    }
}
