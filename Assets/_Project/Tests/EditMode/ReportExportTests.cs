using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Lutra.Core.Data.Models;
using Lutra.Features.Charts;

namespace Lutra.Tests
{
    public class ReportExportTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 7);

        /// <summary>Datos de ejemplo de 30 días (también los usa el PDF de muestra).</summary>
        public static ReportInput SampleInput(bool withNotes = true)
        {
            var rnd = new Random(7);
            var records = new List<EmotionRecord>();
            var emotions = (EmotionType[])Enum.GetValues(typeof(EmotionType));
            for (int d = 0; d < 30; d++)
            {
                if (d == 12 || d == 13) continue;   // hueco
                int mood = Math.Max(1, Math.Min(5, 2 + d / 10 + rnd.Next(-1, 2)));
                records.Add(new EmotionRecord
                {
                    Timestamp = Start.AddDays(d).AddHours(9), IsMorningCheck = true, MoodLevel = mood,
                    EmotionType = emotions[rnd.Next(emotions.Length)],
                    SelectedMotiveTags = d % 3 == 0 ? "[\"Trabajo\"]" : d % 3 == 1 ? "[\"Football\",\"mi perro\"]" : "[]",
                    Notes = withNotes && d % 7 == 0 ? "Día con reunión, \"complicado\", pero bien." : null
                });
                if (d % 4 == 0)
                    records.Add(new EmotionRecord { Timestamp = Start.AddDays(d).AddHours(19), MoodLevel = Math.Min(5, mood + 1),
                                                    EmotionType = EmotionType.Calm });
            }
            records.Add(new EmotionRecord { Timestamp = Start.AddDays(12).AddHours(9), IsMorningCheck = true, MoodLevel = 3,
                                            Source = RecordSource.RestoredFirestore });

            var sessions = Enumerable.Range(0, 6).Select(i =>
            {
                DateTime t = Start.AddDays(i * 4).AddHours(18);
                var s = new MinigameSession { MinigameId = i % 2 == 0 ? MinigameType.BreathJump : MinigameType.FruitNinja,
                    StartTime = t, DurationSeconds = 180, EmotionBefore = EmotionType.Anxiety, EmotionAfter = EmotionType.Calm,
                    MoodBefore = 2, MoodBeforeRecordedAt = t.AddHours(-1), MoodAfter = 3 + i % 2, RelaxationScore = 0.8f };
                if (s.MinigameId == MinigameType.BreathJump)
                    s.Metrics = new Dictionary<string, float> { ["breaths_per_minute"] = 8f - i * 0.4f, ["exhale_inhale_ratio"] = 1.3f,
                        ["breath_cv"] = 0.1f, ["perfect_breaths"] = 14, ["good_breaths"] = 5, ["off_rhythm_breaths"] = 1 };
                return s;
            }).ToList();

            return new ReportInput
            {
                From = Start, To = Start.AddDays(29).AddHours(23), Today = Start.AddDays(40), ProfileCreatedAt = Start.AddDays(-60),
                Records = records, Sessions = sessions,
                DiaryEntries = new List<DiaryEntry>
                {
                    new DiaryEntry { Date = Start.AddDays(2).AddHours(22), Title = "Semana dura",
                                     Content = string.Join(" ", Enumerable.Repeat("Hoy me he sentido triste y agobiada, pero mi amiga me ayudó mucho.", 6)) },
                    new DiaryEntry { Date = Start.AddDays(20).AddHours(22), Title = "Mejor", Content = "Estoy más tranquila y contenta." }
                },
                Who5Responses = new List<ScaleResponse>
                {
                    new ScaleResponse { Scale = ScaleType.Who5, CompletedAt = Start.AddDays(1), AvailableSince = Start, RawScore = 6, Score = 24,
                                        DurationSeconds = 45, AnswersJson = "[1,1,2,1,1]" },
                    new ScaleResponse { Scale = ScaleType.Who5, CompletedAt = Start.AddDays(16), AvailableSince = Start.AddDays(15), RawScore = 13, Score = 52,
                                        DurationSeconds = 38, AnswersJson = "[3,2,3,2,3]" }
                },
                SupportNotifications = new List<AppNotification>
                {
                    new AppNotification { Type = NotificationType.Support, CreatedAt = Start.AddDays(1).AddHours(10), SourceRef = "who5_bajo" }
                },
                Lexicon = DiaryLexicon.Parse("[negativo]\ntrist*\nagobi*\n[positivo]\ncontent*\ntranquil*\n"),
                CurrentStreak = 5, LongestStreak = 12
            };
        }

        public static byte[] SamplePdf(bool includeNotes = true)
        {
            var input = SampleInput();
            return ReportPdfBuilder.Build(input, ReportCalculator.Calculate(input), new ReportPdfOptions
            {
                PatientName = "Ana López", DateOfBirth = new DateTime(1995, 3, 14), ProfileCreatedAt = input.ProfileCreatedAt,
                IncludeNotes = includeNotes, GeneratedAt = Start.AddDays(40).AddHours(12)
            });
        }

        // ── PDF ────────────────────────────────────────────────────────

        [Test]
        public void Pdf_EstructuraValidaYTablaDeReferenciasCorrecta()
        {
            byte[] bytes = SamplePdf();
            string pdf = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);

            StringAssert.StartsWith("%PDF-1.4", pdf);
            StringAssert.EndsWith("%%EOF\n", pdf);

            int xref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)").Groups[1].Value);
            StringAssert.StartsWith("xref", pdf.Substring(xref));

            // Cada entrada de la tabla apunta exactamente al inicio de su objeto
            var offsets = Regex.Matches(pdf.Substring(xref), @"(\d{10}) 00000 n").Cast<Match>()
                               .Select(m => int.Parse(m.Groups[1].Value)).ToList();
            for (int i = 0; i < offsets.Count; i++)
                StringAssert.StartsWith($"{i + 1} 0 obj", pdf.Substring(offsets[i]));

            int pages = int.Parse(Regex.Match(pdf, @"/Count (\d+)").Groups[1].Value);
            Assert.GreaterOrEqual(pages, 3);
        }

        [Test]
        public void Pdf_LongitudDeCadaStreamCorrecta()
        {
            string pdf = Encoding.GetEncoding("ISO-8859-1").GetString(SamplePdf());
            foreach (Match m in Regex.Matches(pdf, @"<< /Length (\d+) >>\nstream\n"))
            {
                int length = int.Parse(m.Groups[1].Value);
                int start  = m.Index + m.Length;
                StringAssert.StartsWith("endstream", pdf.Substring(start + length));
            }
        }

        [Test]
        public void Pdf_TildesEnWinAnsiYParentesisEscapados()
        {
            var writer = new PdfDocumentWriter();
            writer.Text(50, 50, "Ánimo (bajo) ñ ≤ 2", 10);
            string pdf = Encoding.GetEncoding("ISO-8859-1").GetString(writer.ToBytes());
            StringAssert.Contains("(\u00C1nimo \\(bajo\\) \u00F1 <= 2) Tj", pdf);
        }

        [Test]
        public void Pdf_SinNotasNoIncluyeElTextoDelUsuario()
        {
            string with    = Encoding.GetEncoding("ISO-8859-1").GetString(SamplePdf(includeNotes: true));
            string without = Encoding.GetEncoding("ISO-8859-1").GetString(SamplePdf(includeNotes: false));
            StringAssert.Contains("Semana dura", with);
            StringAssert.DoesNotContain("Semana dura", without);
            StringAssert.DoesNotContain("reuni", without);
        }

        [Test]
        public void Pdf_AnchoDeTextoYCorteDeLineas()
        {
            Assert.AreEqual(5.56f, PdfDocumentWriter.TextWidth("a", 10f), 0.001f);
            Assert.AreEqual(PdfDocumentWriter.TextWidth("a", 10f), PdfDocumentWriter.TextWidth("á", 10f), 0.001f);

            var lines = PdfDocumentWriter.Wrap("uno dos tres cuatro cinco seis siete ocho", 10f, 60f);
            Assert.Greater(lines.Count, 1);
            Assert.IsTrue(lines.All(l => PdfDocumentWriter.TextWidth(l, 10f) <= 60f || !l.Contains(' ')));
        }

        // ── CSV ────────────────────────────────────────────────────────

        [Test]
        public void Csv_ArchivosYPrivacidad()
        {
            var input = SampleInput();
            var data  = ReportCalculator.Calculate(input);

            var without = ReportCsvBuilder.Build(input, data, includeNotes: false, appVersion: "1.0").ToDictionary(f => f.name, f => f.content);
            CollectionAssert.IsSupersetOf(without.Keys, new[] { "registros.csv", "partidas.csv", "partidas_metricas.csv",
                                                               "who5.csv", "diario_semanal.csv", "avisos_apoyo.csv", "LEEME.txt" });
            Assert.IsFalse(without.ContainsKey("diario.csv"));
            StringAssert.DoesNotContain("mi perro", without["registros.csv"]);
            StringAssert.Contains("Football|Otros", without["registros.csv"]);
            StringAssert.DoesNotContain("reunión", without["registros.csv"]);

            var with = ReportCsvBuilder.Build(input, data, includeNotes: true, appVersion: "1.0").ToDictionary(f => f.name, f => f.content);
            Assert.IsTrue(with.ContainsKey("diario.csv"));
            StringAssert.Contains("mi perro", with["registros.csv"]);
            StringAssert.Contains("\"Día con reunión, \"\"complicado\"\", pero bien.\"", with["registros.csv"]);
        }

        [Test]
        public void Csv_ExcluyePlaceholdersYMarcaPartidasValidas()
        {
            var input = SampleInput();
            var data  = ReportCalculator.Calculate(input);
            var files = ReportCsvBuilder.Build(input, data, false, "1.0").ToDictionary(f => f.name, f => f.content);

            int userRecords = input.Records.Count(r => r.Source == RecordSource.User);
            Assert.AreEqual(userRecords + 1, files["registros.csv"].Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Length);

            var sessionLines = files["partidas.csv"].Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(7, sessionLines.Length);
            StringAssert.EndsWith(",1,80", sessionLines[1]);   // válida, puntuación 80
            StringAssert.Contains("who5_bajo", files["avisos_apoyo.csv"]);
            StringAssert.Contains(",1,1,2,1,1,6,24,45", files["who5.csv"]);
        }

        [Test]
        public void Csv_Escape()
        {
            Assert.AreEqual("simple", ReportCsvBuilder.Escape("simple"));
            Assert.AreEqual("\"a,b\"", ReportCsvBuilder.Escape("a,b"));
            Assert.AreEqual("\"di \"\"hola\"\"\"", ReportCsvBuilder.Escape("di \"hola\""));
            Assert.AreEqual("\"dos\nlíneas\"", ReportCsvBuilder.Escape("dos\nlíneas"));
        }
    }
}
