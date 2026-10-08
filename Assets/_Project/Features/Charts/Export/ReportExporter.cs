using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Lutra.Core.Architecture;
using Lutra.Core.Data.Persistence;

namespace Lutra.Features.Charts
{
    /// <summary>Periodos que se pueden exportar.</summary>
    public enum ExportPeriod { Last7Days, Last30Days, Last90Days, AllTime }

    /// <summary>
    /// Genera el informe profesional: PDF (ReportPdfBuilder) y, opcionalmente, un ZIP con los CSV
    /// (ReportCsvBuilder). Los archivos se escriben en temporaryCachePath/LutraExport; los de la
    /// exportación anterior se borran al empezar una nueva (no antes: el menú de compartir del
    /// sistema puede seguir leyéndolos). Ver docs/PROFESSIONAL_REPORT.md §4.
    /// </summary>
    public static class ReportExporter
    {
        private const string FolderName = "LutraExport";

        public static (DateTime from, DateTime to) GetRange(ExportPeriod period, DateTime now)
        {
            switch (period)
            {
                case ExportPeriod.Last7Days:  return (now.Date.AddDays(-6), now);
                case ExportPeriod.Last90Days: return (now.Date.AddDays(-89), now);
                case ExportPeriod.AllTime:    return (DateTime.MinValue, now);
                default:                      return (now.Date.AddDays(-29), now);
            }
        }

        /// <summary>Genera los archivos y devuelve sus rutas (PDF primero).</summary>
        public static async Task<List<string>> ExportAsync(ExportPeriod period, bool includeNotes, bool includeCsv)
        {
            DateTime now = DateTime.Now;
            var (from, to) = GetRange(period, now);

            var input   = await ReportDataLoader.BuildInputAsync(from, to);
            var profile = await ServiceLocator.Get<DataRepository>().GetUserProfile();
            var options = new ReportPdfOptions
            {
                PatientName      = profile != null ? $"{profile.Name} {profile.Surname}".Trim() : null,
                DateOfBirth      = profile?.DateOfBirth,
                ProfileCreatedAt = profile?.CreationDate,
                IncludeNotes     = includeNotes,
                GeneratedAt      = now,
                AppVersion       = Application.version
            };
            string appVersion = Application.version;

            // El cálculo y la maquetación son código puro: fuera del hilo principal para no congelar la UI
            var (pdf, csvFiles) = await Task.Run(() =>
            {
                var data = ReportCalculator.Calculate(input);
                byte[] bytes = ReportPdfBuilder.Build(input, data, options);
                var csv = includeCsv ? ReportCsvBuilder.Build(input, data, includeNotes, appVersion) : null;
                return (bytes, csv);
            });

            string folder = _prepareFolder();
            string stamp  = now.ToString("yyyy-MM-dd");
            var paths = new List<string>();

            string pdfPath = Path.Combine(folder, $"Lutra_Informe_{stamp}.pdf");
            File.WriteAllBytes(pdfPath, pdf);
            paths.Add(pdfPath);

            if (csvFiles != null)
            {
                string zipPath = Path.Combine(folder, $"Lutra_Datos_{stamp}.zip");
                WriteZip(zipPath, csvFiles);
                paths.Add(zipPath);
            }

            Debug.Log($"[ReportExporter] Informe generado: {string.Join(", ", paths)}");
            return paths;
        }

        /// <summary>ZIP con cada archivo en UTF-8 con BOM (Excel reconoce así las tildes).</summary>
        public static void WriteZip(string zipPath, List<(string name, string content)> files)
        {
            var utf8Bom = new UTF8Encoding(true);
            using (var stream = new FileStream(zipPath, FileMode.Create))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var (name, content) in files)
                {
                    var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(entry.Open(), utf8Bom))
                        writer.Write(content);
                }
            }
        }

        private static string _prepareFolder()
        {
            string folder = Path.Combine(Application.temporaryCachePath, FolderName);
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ReportExporter] No se pudo limpiar la exportación anterior: {ex.Message}");
            }
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
