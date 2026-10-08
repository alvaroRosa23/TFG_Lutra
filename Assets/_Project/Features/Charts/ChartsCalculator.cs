namespace Lutra.Features.Charts
{
    /// <summary>
    /// Formato de duraciones y rachas, compartido por varias pantallas.
    /// Las métricas de Estadísticas se calculan en ReportCalculator.
    /// </summary>
    public static class ChartsCalculator
    {
        /// <summary>
        /// Formatea segundos como duración legible.
        /// < 60  → "Xs"   | < 3600 → "Xm Ys"   | ≥ 3600 → "Xh Ym"
        /// </summary>
        public static string FormatDuration(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}s";

            if (seconds < 3600)
            {
                int m = seconds / 60;
                int s = seconds % 60;
                return $"{m}m {s:D2}s";
            }

            int h  = seconds / 3600;
            int rm = (seconds % 3600) / 60;
            return $"{h}h {rm}m";
        }

        /// <summary>Formatea días de racha: "1 día" o "X días".</summary>
        public static string FormatStreak(int days)
        {
            return days == 1 ? "1 día" : $"{days} días";
        }
    }
}
