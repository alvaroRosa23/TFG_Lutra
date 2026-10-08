using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Lutra.Core.Data.Models;
using Lutra.Minigames;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Datos en bruto del informe en CSV para Excel, SPSS, R o Python (docs/PROFESSIONAL_REPORT.md §4.4).
    /// Formato: separador coma, decimales con punto, fechas ISO 8601, una fila por observación,
    /// identificadores secuenciales anónimos (nunca los RemoteId). El texto libre del usuario
    /// (notas, motivos escritos, diario) solo se incluye si <c>includeNotes</c> es true.
    /// La codificación (UTF-8 con BOM) la pone quien escribe los archivos. Clase pura, con tests.
    /// </summary>
    public static class ReportCsvBuilder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Nombre de archivo → contenido.</summary>
        public static List<(string name, string content)> Build(ReportInput input, ReportData data,
                                                                 bool includeNotes, string appVersion)
        {
            var files = new List<(string, string)>
            {
                ("registros.csv",          _records(input, includeNotes)),
                ("partidas.csv",           _sessions(input)),
                ("partidas_metricas.csv",  _sessionMetrics(input)),
                ("who5.csv",               _who5(data)),
                ("diario_semanal.csv",     _diaryWeeks(data)),
                ("avisos_apoyo.csv",       _support(data))
            };
            if (includeNotes) files.Add(("diario.csv", _diary(input)));
            files.Add(("LEEME.txt", _readme(data, includeNotes, appVersion)));
            return files;
        }

        // ── Archivos ───────────────────────────────────────────────────

        private static string _records(ReportInput input, bool includeNotes)
        {
            var sb = new StringBuilder();
            _row(sb, "id", "fecha", "hora", "tipo", "animo", "emocion", "valencia_derivada", "activacion_derivada", "motivos", "nota");

            int id = 1;
            foreach (var r in (input.Records ?? new List<EmotionRecord>())
                              .Where(r => r.Source == RecordSource.User).OrderBy(r => r.Timestamp))
            {
                var tags = MotiveTags.Parse(r.SelectedMotiveTags)
                    .Select(t => includeNotes ? t : MotiveTags.GroupKey(t)).Distinct();
                _row(sb, (id++).ToString(Inv), r.Timestamp.ToString("yyyy-MM-dd", Inv), r.Timestamp.ToString("HH:mm", Inv),
                     r.IsMorningCheck ? "dia" : "momento", r.MoodLevel.ToString(Inv), r.EmotionType.ToString(),
                     _valence(r.EmotionType.ValenceOf()), r.EmotionType.ArousalOf() == Arousal.High ? "alta" : "baja",
                     string.Join("|", tags), includeNotes ? r.Notes : string.Empty);
            }
            return sb.ToString();
        }

        private static string _sessions(ReportInput input)
        {
            var sb = new StringBuilder();
            _row(sb, "id", "inicio", "minijuego", "duracion_s", "emocion_antes", "emocion_despues", "animo_antes",
                 "minutos_desde_registro_antes", "animo_despues", "sesion_valida", "puntuacion");

            int id = 1;
            foreach (var s in _orderedSessions(input))
            {
                string minutesSince = s.MoodBeforeRecordedAt.HasValue
                    ? ((s.StartTime - s.MoodBeforeRecordedAt.Value).TotalMinutes).ToString("0", Inv)
                    : string.Empty;
                _row(sb, (id++).ToString(Inv), s.StartTime.ToString("yyyy-MM-ddTHH:mm:ss", Inv), s.MinigameId.ToString(),
                     s.DurationSeconds.ToString("0", Inv), s.EmotionBefore.ToString(), s.EmotionAfter.ToString(),
                     _opt(s.MoodBefore), minutesSince, _opt(s.MoodAfter),
                     s.IsValidForMoodEffect() ? "1" : "0", MinigameOutcome.ToDisplayScore(s.RelaxationScore).ToString(Inv));
            }
            return sb.ToString();
        }

        private static string _sessionMetrics(ReportInput input)
        {
            var sb = new StringBuilder();
            _row(sb, "id_partida", "minijuego", "clave", "valor");

            int id = 1;
            foreach (var s in _orderedSessions(input))
            {
                foreach (var kv in s.Metrics.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    _row(sb, id.ToString(Inv), s.MinigameId.ToString(), kv.Key, kv.Value.ToString("0.####", Inv));
                id++;
            }
            return sb.ToString();
        }

        private static string _who5(ReportData data)
        {
            var sb = new StringBuilder();
            _row(sb, "id", "disponible_desde", "completado", "item1", "item2", "item3", "item4", "item5",
                 "bruta", "indice", "duracion_s");

            int id = 1;
            foreach (var p in data.Who5)
            {
                var items = Enumerable.Range(0, 5).Select(i => i < p.Answers.Count ? p.Answers[i].ToString(Inv) : string.Empty);
                var fields = new List<string> { (id++).ToString(Inv), p.AvailableSince.ToString("yyyy-MM-dd", Inv),
                                                p.CompletedAt.ToString("yyyy-MM-ddTHH:mm:ss", Inv) };
                fields.AddRange(items);
                fields.Add(p.Raw.ToString(Inv));
                fields.Add(p.Score.ToString(Inv));
                fields.Add(p.DurationSeconds.ToString("0", Inv));
                _row(sb, fields.ToArray());
            }
            return sb.ToString();
        }

        private static string _diaryWeeks(ReportData data)
        {
            var sb = new StringBuilder();
            _row(sb, "semana_inicio", "entradas", "palabras", "pct_primera_persona", "pct_emocion_negativa", "pct_emocion_positiva");
            foreach (var w in data.Diary.Weeks)
                _row(sb, w.WeekStart.ToString("yyyy-MM-dd", Inv), w.Entries.ToString(Inv), w.Words.ToString(Inv),
                     _opt(w.PctFirstPerson), _opt(w.PctNegative), _opt(w.PctPositive));
            return sb.ToString();
        }

        private static string _diary(ReportInput input)
        {
            var sb = new StringBuilder();
            _row(sb, "id", "fecha", "titulo", "contenido", "emocion");
            int id = 1;
            foreach (var e in (input.DiaryEntries ?? new List<DiaryEntry>()).OrderBy(e => e.Date))
                _row(sb, (id++).ToString(Inv), e.Date.ToString("yyyy-MM-ddTHH:mm", Inv), e.Title, e.Content, e.Mood);
            return sb.ToString();
        }

        private static string _support(ReportData data)
        {
            var sb = new StringBuilder();
            _row(sb, "fecha", "motivo");
            foreach (var a in data.SupportActivations)
                _row(sb, a.At.ToString("yyyy-MM-ddTHH:mm", Inv), a.Trigger);
            return sb.ToString();
        }

        private static string _readme(ReportData data, bool includeNotes, string appVersion)
        {
            string emotions = string.Join(", ", Enum.GetValues(typeof(EmotionType)).Cast<EmotionType>()
                .Select(e => $"{e} = {e.ToDisplayName()}"));
            string games = string.Join(", ", Enum.GetValues(typeof(MinigameType)).Cast<MinigameType>()
                .Select(g => $"{g} = {g.ToDisplayName()}"));

            return
$@"DATOS EN BRUTO DEL INFORME DE LUTRA
Periodo: {data.From:yyyy-MM-dd} a {data.To:yyyy-MM-dd} · Generado: {DateTime.Now:yyyy-MM-dd HH:mm} · Versión de la app: {appVersion}

Formato: CSV, UTF-8, separador coma, decimales con punto, fechas ISO 8601. Los identificadores son
números secuenciales anónimos. Datos autoinformados por el usuario; Lutra no es una herramienta diagnóstica.

registros.csv — un registro emocional por fila
  tipo: dia = check-in del día (como máximo uno por día) · momento = registro puntual
  animo: 1 (muy mal) a 5 (muy bien)
  emocion: {emotions}
  valencia_derivada / activacion_derivada: derivadas de la emoción según el modelo circumplejo
    (Russell, 1980); aproximación teórica, no medida.
  motivos: separados por |. {(includeNotes ? "Incluye el texto libre escrito por el usuario." : "Los escritos a mano por el usuario aparecen como \"Otros\".")}
  nota: {(includeNotes ? "texto libre del usuario." : "vacía (el usuario no autorizó incluir sus notas).")}

partidas.csv — una partida de minijuego terminada por fila
  minijuego: {games}
  animo_antes: ánimo (1-5) más reciente antes de empezar (no se pregunta: último check-in o valoración de la partida anterior)
  minutos_desde_registro_antes: antigüedad de ese ánimo
  animo_despues: valoración 1-5 al terminar (vacío si no respondió)
  sesion_valida: 1 si sirve para medir el efecto (hay animo_despues y animo_antes de como mucho 3 h)
  emocion_despues: si el usuario no la elige vale lo mismo que emocion_antes; no usar para medir efecto
  puntuacion: 0-100 propia de cada juego (indicador exploratorio)

partidas_metricas.csv — métricas específicas de cada juego, formato largo (id_partida, clave, valor)
  BreathJump: breaths_per_minute (respiraciones/min; objetivo de relajación ~6), exhale_inhale_ratio,
  breath_cv (variabilidad del ciclo; menor = más regular), perfect_breaths, good_breaths, off_rhythm_breaths…

who5.csv — Índice de Bienestar OMS-5 (WHO-5), cada 14 días
  item1-item5: 0 (nunca) a 5 (todo el tiempo) · bruta: 0-25 · indice: bruta x 4 (0-100)
  Interpretación orientativa (Topp et al., 2015): indice <= 50 bienestar bajo; <= 28 probable depresión;
  cambio >= 10 puntos relevante. duracion_s < 10 puede indicar respuesta poco atenta.

diario_semanal.csv — indicadores de escritura por semana (lunes)
  Porcentajes calculados en el dispositivo con un diccionario propio; vacíos con menos de 50 palabras
  en la semana o si el usuario desactivó el análisis. No detecta negaciones. Exploratorio.
{(includeNotes ? "\ndiario.csv — entradas del diario con su texto (incluido por decisión del usuario)\n" : "")}
avisos_apoyo.csv — activaciones del protocolo de apoyo (024 / 112)
  animo_bajo_3_dias: tres check-ins del día seguidos con ánimo <= 2 · who5_bajo: WHO-5 <= 28

Excluidos: {data.ExcludedPlaceholders} registros de relleno creados al restaurar datos (no son del usuario).
";
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static IEnumerable<MinigameSession> _orderedSessions(ReportInput input)
            => (input.Sessions ?? new List<MinigameSession>()).OrderBy(s => s.StartTime);

        private static string _valence(Valence v)
            => v == Valence.Pleasant ? "agradable" : v == Valence.Unpleasant ? "desagradable" : "mixta";

        private static string _opt(int? v) => v.HasValue ? v.Value.ToString(Inv) : string.Empty;

        private static string _opt(float? v) => v.HasValue ? v.Value.ToString("0.##", Inv) : string.Empty;

        private static void _row(StringBuilder sb, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(fields[i]));
            }
            sb.Append("\r\n");
        }

        /// <summary>Entre comillas si contiene coma, comillas o saltos de línea (RFC 4180).</summary>
        public static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            bool quote = field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            string escaped = field.Replace("\"", "\"\"");
            return quote ? $"\"{escaped}\"" : escaped;
        }
    }
}
