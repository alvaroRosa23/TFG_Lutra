using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lutra.Core.Data.Models;

namespace Lutra.Features.Charts
{
    /// <summary>Datos de cabecera y opciones del informe PDF.</summary>
    public class ReportPdfOptions
    {
        public string    PatientName;
        public DateTime? DateOfBirth;
        public DateTime? ProfileCreatedAt;
        public bool      IncludeNotes;
        public DateTime  GeneratedAt = DateTime.Now;
        public string    AppVersion  = "";
    }

    /// <summary>
    /// Maqueta el informe profesional en PDF (estructura de docs/PROFESSIONAL_REPORT.md §4.3) a partir
    /// de ReportData: las mismas cifras que la pantalla de Estadísticas. Cada cifra lleva su n y cada
    /// sección su nivel de evidencia (A validado · B autoinforme · C exploratorio).
    /// Clase pura (sin Unity), con tests.
    /// </summary>
    public class ReportPdfBuilder
    {
        // ── Página ─────────────────────────────────────────────────────

        private const float Left   = 50f;
        private const float Right  = PdfDocumentWriter.PageWidth - 50f;
        private const float Top    = 60f;
        private const float Bottom = PdfDocumentWriter.PageHeight - 60f;
        private const float Width  = Right - Left;

        private const float BodySize  = 9.5f;
        private const float SmallSize = 8f;

        private static readonly CultureInfo Es = new CultureInfo("es-ES");

        private readonly PdfDocumentWriter _pdf = new PdfDocumentWriter();
        private readonly ReportInput       _input;
        private readonly ReportData        _data;
        private readonly ReportPdfOptions  _options;
        private float _y;

        private ReportPdfBuilder(ReportInput input, ReportData data, ReportPdfOptions options)
        {
            _input   = input;
            _data    = data;
            _options = options ?? new ReportPdfOptions();
        }

        public static byte[] Build(ReportInput input, ReportData data, ReportPdfOptions options)
        {
            var builder = new ReportPdfBuilder(input, data, options);
            builder._build();
            return builder._pdf.ToBytes("Informe de seguimiento emocional");
        }

        // ══════════════════════════════════════════════════════════════

        private void _build()
        {
            _newPage();
            _cover();
            _summary();
            _moodEvolution();
            _dynamics();
            _emotionProfile();
            _motives();
            _patterns();
            _who5();
            _minigames();
            _diary();
            _adherence();
            if (_options.IncludeNotes) _notes();
            _methodology();
            _glossary();
            _footers();
        }

        // ── 0. Portada ─────────────────────────────────────────────────

        private void _cover()
        {
            _pdf.FillRect(Left, _y, Width, 4f, PdfColor.Accent);
            _y += 30f;
            _text("Informe de seguimiento emocional", 20f, true);
            _y += 8f;
            _text("Lutra · bienestar emocional", 11f, false, PdfColor.Gray);
            _y += 14f;

            var rows = new List<(string, string)>
            {
                ("Paciente", string.IsNullOrWhiteSpace(_options.PatientName) ? "—" : _options.PatientName),
                ("Edad", _age()),
                ("Periodo analizado", $"{_date(_data.From)} – {_date(_data.To)} ({_data.DaysInRange} días)"),
                ("Usa Lutra desde", _options.ProfileCreatedAt.HasValue && _options.ProfileCreatedAt.Value.Year >= 2000
                                    ? _date(_options.ProfileCreatedAt.Value) : "—"),
                ("Generado", _options.GeneratedAt.ToString("dd/MM/yyyy HH:mm", Es))
            };
            _keyValues(rows);

            _y += 6f;
            _box("Datos autoinformados por el usuario en la app. Lutra no es una herramienta diagnóstica ni un " +
                 "servicio de emergencias. Los indicadores marcados como exploratorios (C) sirven para comparar al " +
                 "usuario consigo mismo, no con normas poblacionales.");
            _note("Niveles de evidencia: A = instrumento validado · B = autoinforme ecológico (EMA) · C = indicador exploratorio.");
        }

        // ── 1. Resumen ─────────────────────────────────────────────────

        private void _summary()
        {
            _heading("1. Resumen");
            var m = _data.Mood;
            var lastWho5 = _data.Who5.LastOrDefault();
            var best = _data.Minigames.ByGame.Where(g => g.MeanDelta.HasValue).OrderByDescending(g => g.MeanDelta.Value).FirstOrDefault();

            _keyValues(new List<(string, string)>
            {
                ("Constancia", _data.Adherence.Pct.HasValue
                    ? $"{_data.Adherence.DaysWithCheckIn} de {_data.Adherence.DaysInRange} días con check-in ({_n0(_data.Adherence.Pct.Value)} %)" : "—"),
                ("Ánimo medio (1-5)", m.Mean.HasValue
                    ? $"{_n1(m.Mean.Value)} (n = {m.N} días){(m.ChangeVsPrevious.HasValue ? $" · cambio vs periodo anterior: {_signed(m.ChangeVsPrevious.Value)}" : "")}" : $"Datos insuficientes (n = {m.N})"),
                ("Días buenos / malos", m.PctGoodDays.HasValue ? $"{_n0(m.PctGoodDays.Value)} % (≥ 4) · {_n0(m.PctBadDays.Value)} % (≤ 2)" : "—"),
                ("Emoción predominante", _data.Emotions.MostFrequent.HasValue
                    ? $"{_data.Emotions.MostFrequent.Value.ToDisplayName()} ({_data.Emotions.Counts[_data.Emotions.MostFrequent.Value]} de {_data.Emotions.Total} registros)" : "—"),
                ("Último WHO-5", lastWho5 != null
                    ? $"{lastWho5.Score} / 100 ({_date(lastWho5.CompletedAt)}){(lastWho5.ChangeVsPrevious.HasValue ? $" · cambio {_signedInt(lastWho5.ChangeVsPrevious.Value)}" : "")}" : "Sin aplicaciones en el periodo"),
                ("Minijuego con mejor efecto", best != null
                    ? $"{best.Type.ToDisplayName()}: {_signed(best.MeanDelta.Value)} puntos de ánimo (n = {best.ValidSessions} partidas válidas)" : "Datos insuficientes")
            });

            _subheading("Indicadores de atención");
            var flags = _attentionFlags();
            if (flags.Count == 0) _paragraph("Ningún indicador de atención en el periodo.");
            else foreach (string flag in flags) _bullet(flag, PdfColor.Warning);
        }

        private List<string> _attentionFlags()
        {
            var flags = new List<string>();
            foreach (var run in _lowMoodRuns())
                flags.Add($"{run.days} días seguidos con ánimo ≤ 2 desde el {_date(run.start)}.");
            foreach (var p in _data.Who5)
            {
                if (p.ProbableDepression) flags.Add($"WHO-5 = {p.Score} el {_date(p.CompletedAt)} (≤ 28: se recomienda valorar depresión).");
                else if (p.LowWellBeing)  flags.Add($"WHO-5 = {p.Score} el {_date(p.CompletedAt)} (≤ 50: bienestar bajo).");
                if (p.RelevantChange)     flags.Add($"Cambio relevante del WHO-5 ({_signedInt(p.ChangeVsPrevious.Value)} puntos) el {_date(p.CompletedAt)}.");
            }
            foreach (var a in _data.SupportActivations)
                flags.Add($"Protocolo de apoyo activado el {_date(a.At)} ({_triggerText(a.Trigger)}).");
            return flags;
        }

        private List<(DateTime start, int days)> _lowMoodRuns()
        {
            var runs = new List<(DateTime, int)>();
            DateTime? start = null; DateTime last = default; int length = 0;
            foreach (var d in _data.Mood.Daily)
            {
                bool low = d.Mood <= 2;
                bool consecutive = start.HasValue && (d.Date - last).Days == 1;
                if (low && consecutive) length++;
                else
                {
                    if (start.HasValue && length >= 3) runs.Add((start.Value, length));
                    start = low ? d.Date : (DateTime?)null;
                    length = low ? 1 : 0;
                }
                last = d.Date;
            }
            if (start.HasValue && length >= 3) runs.Add((start.Value, length));
            return runs;
        }

        // ── 2. Evolución del ánimo ─────────────────────────────────────

        private void _moodEvolution()
        {
            _heading("2. Evolución del ánimo", "B");
            var m = _data.Mood;
            if (m.N == 0) { _paragraph("Sin check-ins del día en el periodo."); return; }

            DateTime first = _data.From;
            var points  = m.Daily.Select(d => ((float)(d.Date - first).TotalDays, (float)d.Mood)).ToList();
            var average = m.Daily.Select(d => ((float)(d.Date - first).TotalDays, d.MovingAverage7)).ToList();
            _chart(points, average, Math.Max(1f, (float)(_data.To - first).TotalDays), 1f, 5f,
                   new[] { 1f, 2f, 3f, 4f, 5f }, _date(_data.From), _date(_data.To),
                   "Ánimo diario (puntos) y media de 7 días (línea clara). Escala 1-5.");

            _keyValues(new List<(string, string)>
            {
                ("Desviación típica", m.StdDev.HasValue ? _n2(m.StdDev.Value) : "— (mín. 7 días)"),
                ("Tendencia", m.SlopePerWeek.HasValue ? $"{_signed(m.SlopePerWeek.Value)} puntos por semana" : "— (mín. 7 días)"),
                ("Ánimo medio del periodo anterior", m.PreviousMean.HasValue ? _n2(m.PreviousMean.Value) : "—"),
                ("Variación intradía (Momento − Día)", m.IntradayDelta.HasValue ? $"{_signed(m.IntradayDelta.Value)} (n = {m.IntradayPairs})" : $"— (n = {m.IntradayPairs}, mín. 5)")
            });

            var weeks = m.Daily.GroupBy(d => ReportCalculator.WeekStart(d.Date)).OrderBy(g => g.Key)
                .Select(g =>
                {
                    var values = g.Select(d => (double)d.Mood).ToList();
                    double mean = values.Average();
                    string sd = values.Count > 1 ? _n2((float)Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1))) : "—";
                    return new[] { $"Semana del {_date(g.Key)}", _n2((float)mean), sd, values.Count.ToString() };
                }).ToList();
            _table(new[] { "Semana", "Media", "DE", "n" }, new[] { 0.55f, 0.15f, 0.15f, 0.15f }, weeks);
        }

        // ── 3. Dinámica ────────────────────────────────────────────────

        private void _dynamics()
        {
            _heading("3. Dinámica emocional", "B");
            var d = _data.Dynamics;
            _table(new[] { "Indicador", "Periodo", "Periodo anterior" }, new[] { 0.5f, 0.25f, 0.25f }, new List<string[]>
            {
                new[] { "Inestabilidad (MSSD)", _opt2(d.Mssd), _opt2(d.PreviousMssd) },
                new[] { "Inercia (autocorrelación lag-1)", _opt2(d.Inertia), _opt2(d.PreviousInertia) },
                new[] { "Pares de días consecutivos", d.ConsecutivePairs.ToString(), d.PreviousConsecutivePairs.ToString() }
            });
            _note("Calculados solo con pares de días consecutivos (mínimo 14 pares). Mayor inestabilidad e inercia del afecto " +
                  "se asocian a peor bienestar (Houben et al., 2015; Kuppens et al., 2010; Jahng et al., 2008).");
        }

        // ── 4. Perfil emocional ────────────────────────────────────────

        private void _emotionProfile()
        {
            _heading("4. Perfil emocional", "B");
            var e = _data.Emotions;
            if (e.Total == 0) { _paragraph("Sin registros emocionales en el periodo."); return; }

            var rows = e.Counts.OrderByDescending(kv => kv.Value)
                .Select(kv => new[] { kv.Key.ToDisplayName(), kv.Value.ToString(), _n0(100f * kv.Value / e.Total) + " %",
                                      kv.Key.QuadrantOf().ToDisplayName() }).ToList();
            _table(new[] { "Emoción", "n", "%", "Cuadrante (derivado)" }, new[] { 0.35f, 0.15f, 0.15f, 0.35f }, rows);

            _keyValues(new List<(string, string)>
            {
                ("Valencia", e.PctPleasant.HasValue
                    ? $"agradable {_n0(e.PctPleasant.Value)} % · desagradable {_n0(e.PctUnpleasant.Value)} % · mixta {_n0(e.PctMixed.Value)} %" : "— (mín. 5 registros)"),
                ("Emodiversidad (0-1)", e.Emodiversity.HasValue ? _n2(e.Emodiversity.Value) : "— (mín. 10 registros)"),
                ("Emociones distintas", $"{e.DistinctEmotions} de 8")
            });
            _note("Valencia y activación derivadas de la emoción (modelo circumplejo, Russell, 1980): aproximación teórica, no medida. " +
                  "Emodiversidad: entropía de Shannon normalizada (Quoidbach et al., 2014).");
        }

        // ── 5. Motivos ─────────────────────────────────────────────────

        private void _motives()
        {
            _heading("5. Contexto y motivos", "B");
            if (_data.Motives.Count == 0) { _paragraph("Ningún motivo con al menos 3 apariciones en el periodo."); return; }

            var rows = _data.Motives.Select(m => new[]
            {
                m.DisplayName, m.Count.ToString(), _n0(m.Pct) + " %", _n2(m.MeanMoodWith),
                _opt2(m.MeanMoodWithout), m.Difference.HasValue ? _signed(m.Difference.Value) : "—",
                m.MostFrequentEmotion.ToDisplayName()
            }).ToList();
            _table(new[] { "Motivo", "n", "%", "Ánimo con", "Ánimo sin", "Dif.", "Emoción frecuente" },
                   new[] { 0.2f, 0.08f, 0.1f, 0.13f, 0.13f, 0.1f, 0.26f }, rows);
            _note("Asociación, no causalidad. Los motivos escritos a mano se agrupan como \"Otros\".");
        }

        // ── 6. Patrones temporales ─────────────────────────────────────

        private void _patterns()
        {
            _heading("6. Patrones temporales", "B");
            string[] days = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
            var p = _data.Patterns;
            var rows = Enumerable.Range(0, 7).Select(i => new[]
            {
                days[i], p.MeanMoodByWeekday[i].HasValue ? _n2(p.MeanMoodByWeekday[i].Value) : "—", p.DayCheckInsByWeekday[i].ToString()
            }).ToList();
            _table(new[] { "Día", "Ánimo medio", "n" }, new[] { 0.5f, 0.25f, 0.25f }, rows);

            int morning = Enumerable.Range(6, 6).Sum(h => p.MomentRecordsByHour[h]);
            int afternoon = Enumerable.Range(12, 8).Sum(h => p.MomentRecordsByHour[h]);
            int night = p.MomentRecordsByHour.Sum() - morning - afternoon;
            _paragraph($"Registros de Momento: mañana (6-12 h) {morning} · tarde (12-20 h) {afternoon} · noche (20-6 h) {night}.");
        }

        // ── 7. WHO-5 ───────────────────────────────────────────────────

        private void _who5()
        {
            _heading("7. Bienestar — WHO-5", "A");
            if (_data.Who5.Count == 0) { _paragraph("Sin aplicaciones del WHO-5 en el periodo."); return; }

            if (_data.Who5.Count >= 2)
            {
                DateTime first = _data.Who5[0].CompletedAt.Date;
                var points = _data.Who5.Select(w => ((float)(w.CompletedAt.Date - first).TotalDays, (float)w.Score)).ToList();
                _chart(points, null, Math.Max(1f, points.Last().Item1), 0f, 100f, new[] { 0f, 28f, 50f, 100f },
                       _date(first), _date(_data.Who5.Last().CompletedAt), "Índice WHO-5 (0-100). Líneas guía en 28 y 50.",
                       maxGap: float.MaxValue);
            }

            var rows = _data.Who5.Select(w => new[]
            {
                _date(w.CompletedAt), w.Score.ToString(), w.ChangeVsPrevious.HasValue ? _signedInt(w.ChangeVsPrevious.Value) : "—",
                string.Join(" ", w.Answers), _n0(w.DurationSeconds) + " s", w.DelayDays + " d",
                string.Join(", ", new[] { w.ProbableDepression ? "≤ 28" : w.LowWellBeing ? "≤ 50" : null,
                                          w.RelevantChange ? "cambio ≥ 10" : null, w.PossiblyInattentive ? "< 10 s" : null }.Where(s => s != null))
            }).ToList();
            _table(new[] { "Fecha", "Índice", "Cambio", "Ítems 1-5", "Tiempo", "Retraso", "Avisos" },
                   new[] { 0.15f, 0.1f, 0.1f, 0.17f, 0.11f, 0.11f, 0.26f }, rows);
            _note("Topp et al. (2015): ≤ 50 bienestar bajo (cribado de depresión recomendado); ≤ 28 probable depresión; " +
                  "cambio ≥ 10 puntos relevante. Retraso: días entre la disponibilidad y la respuesta.");
        }

        // ── 8. Minijuegos ──────────────────────────────────────────────

        private void _minigames()
        {
            _heading("8. Regulación emocional — minijuegos", "B/C");
            var mg = _data.Minigames;
            if (mg.Sessions == 0) { _paragraph("Sin partidas en el periodo."); return; }

            _paragraph($"{mg.Sessions} partidas terminadas, {_n0(mg.TotalMinutes)} minutos en total; {mg.ValidSessions} válidas para medir el efecto en el ánimo.");

            var rows = mg.ByGame.Select(g => new[]
            {
                g.Type.ToDisplayName(), g.Sessions.ToString(), _n0(g.Minutes), g.ValidSessions.ToString(),
                g.MeanDelta.HasValue ? _signed(g.MeanDelta.Value) : "—",
                g.PctImproved.HasValue ? $"{_n0(g.PctImproved.Value)}/{_n0(g.PctSame.Value)}/{_n0(g.PctWorse.Value)}" : "—"
            }).ToList();
            _table(new[] { "Minijuego", "Partidas", "Min.", "Válidas", "Cambio ánimo", "% mejora/igual/peor" },
                   new[] { 0.26f, 0.12f, 0.1f, 0.12f, 0.14f, 0.26f }, rows);

            if (mg.ByEmotionBefore.Count > 0)
            {
                _subheading("Efecto según la emoción de partida");
                _table(new[] { "Emoción antes", "Partidas válidas", "Cambio de ánimo medio" }, new[] { 0.4f, 0.3f, 0.3f },
                       mg.ByEmotionBefore.Select(e => new[] { e.EmotionBefore.ToDisplayName(), e.ValidSessions.ToString(),
                                                              e.MeanDelta.HasValue ? _signed(e.MeanDelta.Value) : "—" }).ToList());
            }

            if (mg.Breathing.Count > 0)
            {
                _subheading("Respiración en Breath Jump (C)");
                _table(new[] { "Fecha", "Resp./min", "Exh./inh.", "Variabilidad (CV)", "% perfectas" },
                       new[] { 0.24f, 0.18f, 0.18f, 0.2f, 0.2f },
                       mg.Breathing.Select(b => new[] { _date(b.Date), _n1(b.BreathsPerMinute), _n2(b.ExhaleInhaleRatio),
                                                        _n2(b.BreathCv), _n0(b.PctPerfect) + " %" }).ToList());
                _note("Objetivo de relajación ≈ 6 respiraciones/min con espiración más larga que la inspiración " +
                      "(Lehrer y Gevirtz, 2014; Zaccaro et al., 2018; Van Diest et al., 2014).");
            }
            _note("Efecto = ánimo después − ánimo antes (1-5), solo en partidas con valoración final y ánimo previo de ≤ 3 h. " +
                  "Sin grupo control: puede reflejar el paso del tiempo o la regresión a la media.");
        }

        // ── 9. Diario ──────────────────────────────────────────────────

        private void _diary()
        {
            _heading("9. Diario", "C");
            var d = _data.Diary;
            if (d.Entries == 0) { _paragraph("Sin entradas de diario en el periodo."); return; }

            _paragraph($"{d.Entries} entradas · {d.TotalWords} palabras · {_n1(d.WordsPerEntry ?? 0f)} palabras por entrada · " +
                       $"{_n1(d.EntriesPerWeek)} entradas por semana.");

            if (!d.LanguageAnalysisEnabled)
            {
                _note("El usuario ha desactivado el análisis de escritura: no se incluyen indicadores de lenguaje.");
                return;
            }

            _table(new[] { "Semana", "Entradas", "Palabras", "% 1.ª persona", "% emoción neg.", "% emoción pos." },
                   new[] { 0.24f, 0.12f, 0.12f, 0.17f, 0.17f, 0.18f },
                   d.Weeks.Select(w => new[] { _date(w.WeekStart), w.Entries.ToString(), w.Words.ToString(),
                                               _optPct(w.PctFirstPerson), _optPct(w.PctNegative), _optPct(w.PctPositive) }).ToList());
            _note("Calculado en el dispositivo con un diccionario propio (solo cifras; el texto no se comparte salvo autorización). " +
                  "Mínimo 50 palabras por semana. No detecta negaciones y el español omite el sujeto, por lo que la primera " +
                  "persona queda infraestimada (Rude et al., 2004; Ramírez-Esparza et al., 2007).");
        }

        // ── 10. Adherencia ─────────────────────────────────────────────

        private void _adherence()
        {
            _heading("10. Constancia y hábitos");
            var a = _data.Adherence;
            _keyValues(new List<(string, string)>
            {
                ("Días con check-in", a.Pct.HasValue ? $"{a.DaysWithCheckIn} de {a.DaysInRange} ({_n0(a.Pct.Value)} %)" : "—"),
                ("Racha actual / máxima", $"{a.CurrentStreak?.ToString() ?? "—"} / {a.LongestStreak?.ToString() ?? "—"} días"),
                ("Huecos de 2 días o más", a.Gaps.Count == 0 ? "Ninguno" : string.Join("; ", a.Gaps.Select(g => $"{_date(g.Start)} ({g.Days} d)"))),
                ("Ánimo el día previo a un hueco", a.MeanMoodBeforeGaps.HasValue
                    ? $"{_n2(a.MeanMoodBeforeGaps.Value)} (media del periodo: {_opt2(_data.Mood.Mean)})" : "—"),
                ("WHO-5 completados", a.Who5MeanDelayDays.HasValue ? $"{a.Who5Completed} (retraso medio {_n1(a.Who5MeanDelayDays.Value)} días)" : a.Who5Completed.ToString())
            });
            _note("Los días sin registro no son aleatorios: pueden coincidir con peores momentos. Interpretar las medias junto a la constancia.");
        }

        // ── 11. Notas del usuario ──────────────────────────────────────

        private void _notes()
        {
            _heading("11. Notas del usuario");
            _note("Incluidas por decisión del usuario al exportar.");

            var notes = (_input.Records ?? new List<EmotionRecord>())
                .Where(r => r.Source == RecordSource.User && !string.IsNullOrWhiteSpace(r.Notes))
                .OrderBy(r => r.Timestamp).ToList();
            _subheading("Notas de los check-ins");
            if (notes.Count == 0) _paragraph("Sin notas.");
            foreach (var r in notes)
                _paragraph($"{r.Timestamp.ToString("dd/MM/yyyy HH:mm", Es)} · {r.EmotionType.ToDisplayName()} · ánimo {r.MoodLevel}: {r.Notes}");

            _subheading("Diario");
            var entries = (_input.DiaryEntries ?? new List<DiaryEntry>()).OrderBy(e => e.Date).ToList();
            if (entries.Count == 0) _paragraph("Sin entradas.");
            foreach (var e in entries)
            {
                _ensure(30f);
                _text($"{e.Date.ToString("dd/MM/yyyy", Es)} — {(string.IsNullOrWhiteSpace(e.Title) ? "Sin título" : e.Title)}", BodySize, true);
                _y += 4f;
                _paragraph(e.Content ?? string.Empty);
            }
        }

        // ── 12-13. Anexos ──────────────────────────────────────────────

        private void _methodology()
        {
            _heading("Anexo A. Metodología");
            foreach (string line in new[]
            {
                $"Exclusiones: {_data.ExcludedPlaceholders} registros de relleno creados al restaurar datos desde la nube (no son del usuario).",
                "Serie diaria: ánimo del check-in del día (uno por día). Los registros de Momento se analizan como variación intradía.",
                "Mínimos: media 3 días; desviación típica y tendencia 7 días; dinámica 14 pares de días consecutivos; " +
                "emodiversidad 10 registros; motivos 3 apariciones; efecto de minijuegos 3 partidas válidas; lenguaje 50 palabras/semana.",
                "Periodo anterior: misma duración, justo antes del inicio del periodo.",
                "Tendencia: pendiente de la recta de mínimos cuadrados del ánimo diario, en puntos por semana.",
                "MSSD: media de los cuadrados de las diferencias entre días consecutivos. Inercia: correlación de Pearson entre el ánimo de un día y el del anterior.",
                "Emodiversidad: entropía de Shannon normalizada, -suma(p·ln p) / ln 8, sobre la frecuencia de las 8 emociones.",
                "Partida válida: con valoración final y ánimo previo (último registro o valoración anterior) de como mucho 3 horas antes.",
                "Bibliografía: Gross (1998); Houben et al. (2015); Jahng et al. (2008); Kuppens et al. (2010); Lehrer y Gevirtz (2014); " +
                "Pennebaker (1997); Quoidbach et al. (2014); Ramírez-Esparza et al. (2007); Rude et al. (2004); Russell (1980); " +
                "Shiffman et al. (2008); Topp et al. (2015); Van Diest et al. (2014); Zaccaro et al. (2018)."
            }) _bullet(line, PdfColor.Black);
        }

        private void _glossary()
        {
            _heading("Anexo B. Glosario");
            foreach (var (term, definition) in new[]
            {
                ("EMA", "Evaluación ecológica momentánea: registrar el estado en el momento y en el entorno natural, reduciendo el sesgo de recuerdo."),
                ("Check-in del día / de momento", "Registro diario principal (uno por día) / registros puntuales adicionales."),
                ("MSSD", "Mean squared successive differences: inestabilidad del ánimo de un día a otro."),
                ("Inercia emocional", "Cuánto se parece el ánimo de un día al del anterior (resistencia al cambio)."),
                ("Emodiversidad", "Variedad y equilibrio de las emociones registradas (0 = siempre la misma, 1 = todas por igual)."),
                ("WHO-5", "Índice de Bienestar de la OMS: 5 ítems sobre las dos últimas semanas, 0-100."),
                ("Partida válida", "Partida con ánimo antes (≤ 3 h) y después, utilizable para estimar el efecto.")
            })
            {
                _ensure(24f);
                _text(term, BodySize, true);
                _y += 3f;
                _paragraph(definition);
            }
        }

        private void _footers()
        {
            int total = _pdf.PageCount;
            for (int i = 0; i < total; i++)
            {
                _pdf.CurrentPage = i;
                float y = PdfDocumentWriter.PageHeight - 30f;
                _pdf.Line(Left, y - 12f, Right, y - 12f, 0.5f, PdfColor.LightGray);
                _pdf.Text(Left, y, $"Generado por Lutra el {_options.GeneratedAt.ToString("dd/MM/yyyy", Es)} · Datos autoinformados · No diagnóstico",
                          SmallSize, false, PdfColor.Gray);
                string page = $"Página {i + 1} de {total}";
                _pdf.Text(Right - PdfDocumentWriter.TextWidth(page, SmallSize), y, page, SmallSize, false, PdfColor.Gray);
            }
            _pdf.CurrentPage = -1;
        }

        // ══════════════════════════════════════════════════════════════
        // Maquetación
        // ══════════════════════════════════════════════════════════════

        private void _newPage()
        {
            _pdf.NewPage();
            _pdf.CurrentPage = -1;
            _y = Top;
        }

        private void _ensure(float height)
        {
            if (_y + height > Bottom) _newPage();
        }

        private void _text(string text, float size, bool bold, PdfColor? color = null)
        {
            _ensure(size + 4f);
            _y += size;
            _pdf.Text(Left, _y, text, size, bold, color);
        }

        private void _heading(string text, string level = null)
        {
            _ensure(120f);   // título + algo de contenido: que no quede solo al final de la página
            _y += 18f;
            _y += 13f;
            _pdf.Text(Left, _y, text, 13f, true, PdfColor.Accent);
            if (level != null)
            {
                string badge = $"Nivel {level}";
                _pdf.Text(Right - PdfDocumentWriter.TextWidth(badge, SmallSize, true), _y, badge, SmallSize, true, PdfColor.Gray);
            }
            _y += 5f;
            _pdf.Line(Left, _y, Right, _y, 0.8f, PdfColor.AccentSoft);
            _y += 8f;
        }

        private void _subheading(string text)
        {
            _ensure(30f);
            _y += 6f;
            _text(text, 10.5f, true);
            _y += 4f;
        }

        private void _paragraph(string text, float size = BodySize, PdfColor? color = null, float indent = 0f)
        {
            foreach (string line in PdfDocumentWriter.Wrap(text, size, Width - indent))
            {
                _ensure(size + 3f);
                _y += size + 2.5f;
                _pdf.Text(Left + indent, _y, line, size, false, color);
            }
            _y += 4f;
        }

        private void _note(string text) => _paragraph(text, SmallSize, PdfColor.Gray);

        private void _bullet(string text, PdfColor color)
        {
            _ensure(BodySize + 4f);
            _pdf.FillRect(Left + 2f, _y + 6.5f, 3f, 3f, color);
            _paragraph(text, BodySize, null, 12f);
            _y -= 2f;
        }

        private void _box(string text)
        {
            var lines = PdfDocumentWriter.Wrap(text, BodySize, Width - 20f);
            float height = lines.Count * (BodySize + 2.5f) + 14f;
            _ensure(height);
            _pdf.FillRect(Left, _y, Width, height, new PdfColor(0.95f, 0.96f, 0.99f));
            _pdf.FillRect(Left, _y, 3f, height, PdfColor.Accent);
            float y = _y + 7f;
            foreach (string line in lines)
            {
                y += BodySize + 2.5f;
                _pdf.Text(Left + 12f, y, line, BodySize);
            }
            _y += height + 8f;
        }

        private void _keyValues(List<(string key, string value)> rows)
        {
            const float keyWidth = 170f;
            foreach (var (key, value) in rows)
            {
                var lines = PdfDocumentWriter.Wrap(value ?? "—", BodySize, Width - keyWidth);
                _ensure(lines.Count * (BodySize + 3f) + 4f);
                _y += BodySize + 3f;
                _pdf.Text(Left, _y, key, BodySize, true, PdfColor.Gray);
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i > 0) _y += BodySize + 3f;
                    _pdf.Text(Left + keyWidth, _y, lines[i], BodySize);
                }
                _y += 3f;
            }
            _y += 4f;
        }

        /// <summary>Tabla con cabecera sombreada; las celdas que no caben se recortan con "…".</summary>
        private void _table(string[] headers, float[] fractions, List<string[]> rows)
        {
            const float rowHeight = 15f;
            float[] widths = fractions.Select(f => f * Width).ToArray();

            void header()
            {
                _pdf.FillRect(Left, _y, Width, rowHeight, PdfColor.LightGray);
                float x = Left;
                for (int c = 0; c < headers.Length; c++)
                {
                    _pdf.Text(x + 4f, _y + 10.5f, _fit(headers[c], SmallSize, widths[c] - 8f, true), SmallSize, true);
                    x += widths[c];
                }
                _y += rowHeight;
            }

            _ensure(rowHeight * 2f);
            header();
            foreach (var row in rows)
            {
                if (_y + rowHeight > Bottom) { _newPage(); header(); }
                float x = Left;
                for (int c = 0; c < headers.Length && c < row.Length; c++)
                {
                    _pdf.Text(x + 4f, _y + 10.5f, _fit(row[c] ?? "", SmallSize, widths[c] - 8f), SmallSize);
                    x += widths[c];
                }
                _y += rowHeight;
                _pdf.Line(Left, _y, Right, _y, 0.4f, PdfColor.LightGray);
            }
            _y += 8f;
        }

        /// <param name="maxGap">Distancia máxima en X para unir dos puntos (en el ánimo diario, los huecos cortan la línea).</param>
        private void _chart(List<(float x, float y)> points, List<(float x, float y)> secondary, float xMax,
                            float yMin, float yMax, float[] grid, string fromLabel, string toLabel, string caption,
                            float maxGap = 1.5f)
        {
            const float height = 150f, labelWidth = 24f;
            _ensure(height + 40f);
            float top = _y + 6f, left = Left + labelWidth, width = Width - labelWidth;

            (float, float) map(float x, float y) =>
                (left + 6f + x / xMax * (width - 12f), top + height - 6f - (y - yMin) / (yMax - yMin) * (height - 12f));

            _pdf.StrokeRect(left, top, width, height, 0.6f, PdfColor.LightGray);
            foreach (float g in grid)
            {
                var (_, gy) = map(0, g);
                _pdf.Line(left, gy, left + width, gy, 0.4f, PdfColor.LightGray);
                string label = g.ToString("0", Es);
                _pdf.Text(left - 4f - PdfDocumentWriter.TextWidth(label, SmallSize), gy + 3f, label, SmallSize, false, PdfColor.Gray);
            }

            // Media móvil cortada en los huecos mayores que su ventana (no unir periodos sin datos)
            if (secondary != null && secondary.Count > 1)
            {
                var soft = new List<(float, float)>();
                for (int i = 0; i < secondary.Count; i++)
                {
                    if (i > 0 && secondary[i].x - secondary[i - 1].x > ReportCalculator.MovingAverageWindowDays)
                    {
                        if (soft.Count > 1) _pdf.Polyline(soft, 2f, PdfColor.AccentSoft);
                        soft.Clear();
                    }
                    soft.Add(map(secondary[i].x, secondary[i].y));
                }
                if (soft.Count > 1) _pdf.Polyline(soft, 2f, PdfColor.AccentSoft);
            }

            // Línea principal cortada en los huecos (más de 1 unidad entre puntos)
            var segment = new List<(float, float)>();
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0 && points[i].x - points[i - 1].x > maxGap)
                {
                    _pdf.Polyline(segment, 1.2f, PdfColor.Accent);
                    segment.Clear();
                }
                segment.Add(map(points[i].x, points[i].y));
            }
            _pdf.Polyline(segment, 1.2f, PdfColor.Accent);
            foreach (var p in points)
            {
                var (px, py) = map(p.x, p.y);
                _pdf.FillRect(px - 2f, py - 2f, 4f, 4f, PdfColor.Accent);
            }

            _y = top + height + 10f;
            _pdf.Text(left, _y, fromLabel, SmallSize, false, PdfColor.Gray);
            _pdf.Text(left + width - PdfDocumentWriter.TextWidth(toLabel, SmallSize), _y, toLabel, SmallSize, false, PdfColor.Gray);
            _y += 4f;
            _note(caption);
        }

        // ── Formato ────────────────────────────────────────────────────

        private static string _fit(string text, float size, float maxWidth, bool bold = false)
        {
            if (PdfDocumentWriter.TextWidth(text, size, bold) <= maxWidth) return text;
            while (text.Length > 1 && PdfDocumentWriter.TextWidth(text + "…", size, bold) > maxWidth)
                text = text.Substring(0, text.Length - 1);
            return text + "…";
        }

        private string _age()
        {
            if (!_options.DateOfBirth.HasValue || _options.DateOfBirth.Value.Year < 1900) return "—";
            DateTime dob = _options.DateOfBirth.Value.Date, today = _options.GeneratedAt.Date;
            int age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            return $"{age} años";
        }

        private static string _triggerText(string trigger)
            => trigger == "who5_bajo" ? "WHO-5 ≤ 28" : trigger == "animo_bajo_3_dias" ? "3 días seguidos con ánimo ≤ 2" : trigger;

        private static string _date(DateTime d) => d.ToString("dd/MM/yyyy", Es);
        private static string _n0(float v) => v.ToString("0", Es);
        private static string _n1(float v) => v.ToString("0.0", Es);
        private static string _n2(float v) => v.ToString("0.00", Es);
        private static string _opt2(float? v) => v.HasValue ? _n2(v.Value) : "—";
        private static string _optPct(float? v) => v.HasValue ? _n1(v.Value) + " %" : "—";
        private static string _signed(float v) => (v > 0 ? "+" : "") + _n2(v);
        private static string _signedInt(int v) => (v > 0 ? "+" : "") + v;
    }
}
