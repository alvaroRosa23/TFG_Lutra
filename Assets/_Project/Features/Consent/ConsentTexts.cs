namespace Lutra.Features.Consent
{
    /// <summary>
    /// Textos del consentimiento de datos de salud (versión ConsentGate.CurrentVersion).
    /// Si se cambian, subir ConsentGate.CurrentVersion para volver a pedirlo.
    /// </summary>
    public static class ConsentTexts
    {
        public const string Title = "Antes de empezar";

        public const string Body =
            "Lutra te ayuda a registrar cómo te sientes y a ver tu evolución. Para eso guarda datos sobre tu " +
            "bienestar emocional, que la ley considera datos de salud. Por eso necesitamos tu permiso.\n\n" +
            "<b>Qué guardamos</b>\n" +
            "· Tus check-ins: ánimo, emoción, motivos y notas.\n" +
            "· Tu diario, tus partidas de minijuegos y tus cuestionarios de bienestar.\n" +
            "· Tu perfil: nombre, fecha de nacimiento y aficiones.\n\n" +
            "<b>Para qué</b>\n" +
            "· Mostrarte tus estadísticas y tu progreso.\n" +
            "· Generar, solo cuando tú lo pidas, un informe para compartir con tu profesional.\n\n" +
            "<b>Dónde</b>\n" +
            "· En tu móvil y en una copia en la nube (Google Firebase) asociada a tu cuenta, para no perderlos si cambias de dispositivo.\n" +
            "· Nadie más recibe tus datos: solo tú decides si exportas y compartes un informe.\n\n" +
            "<b>Tus derechos</b>\n" +
            "· Puedes exportar y borrar tus datos cuando quieras desde Ajustes, y retirar este consentimiento eliminando tu cuenta.\n\n" +
            "<b>Importante</b>\n" +
            "· Lutra no es una herramienta de diagnóstico ni un servicio de emergencias. Si lo necesitas: " +
            "024 (atención a la conducta suicida, 24 horas) o 112 (emergencias).";

        public const string RequiredCheck =
            "Acepto el tratamiento de mis datos de bienestar emocional para el funcionamiento de Lutra.";

        public const string DiaryAnalysisCheck =
            "Permitir el análisis de la escritura de mi diario para mis informes. Solo se calculan cifras " +
            "(número de palabras y tipo de palabras); el texto no se comparte salvo que tú lo incluyas.";

        public const string DeclinedToast = "Necesitamos tu consentimiento para que puedas usar Lutra";
    }
}
