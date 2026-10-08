using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Lutra.Features.Charts
{
    /// <summary>Color RGB 0-1 para el PDF (sin depender de UnityEngine.Color).</summary>
    public struct PdfColor
    {
        public float R, G, B;
        public PdfColor(float r, float g, float b) { R = r; G = g; B = b; }

        public static readonly PdfColor Black     = new PdfColor(0.10f, 0.10f, 0.12f);
        public static readonly PdfColor Gray      = new PdfColor(0.45f, 0.45f, 0.50f);
        public static readonly PdfColor LightGray = new PdfColor(0.88f, 0.88f, 0.90f);
        public static readonly PdfColor Accent    = new PdfColor(0.25f, 0.45f, 0.85f);
        public static readonly PdfColor AccentSoft = new PdfColor(0.62f, 0.74f, 0.95f);
        public static readonly PdfColor Warning   = new PdfColor(0.80f, 0.35f, 0.20f);
        public static readonly PdfColor White     = new PdfColor(1f, 1f, 1f);
    }

    /// <summary>
    /// Generador mínimo de PDF 1.4, sin dependencias (docs/PROFESSIONAL_REPORT.md §4.2).
    /// Dibuja texto, líneas, rectángulos y polilíneas en páginas A4 con las fuentes estándar
    /// Helvetica y Helvetica-Bold (codificación WinAnsi: cubre á é í ó ú ñ ü ¿ ¡; no emojis).
    /// Las fuentes estándar no se incrustan: cualquier lector de PDF las tiene.
    ///
    /// Coordenadas en puntos con origen ARRIBA a la izquierda (se convierten a las del PDF,
    /// que tiene el origen abajo). Clase pura, sin Unity, con tests.
    /// </summary>
    public class PdfDocumentWriter
    {
        public const float PageWidth  = 595.28f;   // A4
        public const float PageHeight = 841.89f;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly List<StringBuilder> _pages = new List<StringBuilder>();

        public int PageCount => _pages.Count;

        /// <summary>Añade una página y la hace actual.</summary>
        public void NewPage() => _pages.Add(new StringBuilder());

        /// <summary>Página en la que se dibuja (0 = primera). Por defecto, la última añadida.</summary>
        public int CurrentPage { get; set; } = -1;

        private StringBuilder _page
        {
            get
            {
                if (_pages.Count == 0) NewPage();
                int index = CurrentPage >= 0 && CurrentPage < _pages.Count ? CurrentPage : _pages.Count - 1;
                return _pages[index];
            }
        }

        // ── Dibujo ─────────────────────────────────────────────────────

        /// <param name="y">Línea base del texto, medida desde arriba.</param>
        public void Text(float x, float y, string text, float size, bool bold = false, PdfColor? color = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var c = color ?? PdfColor.Black;
            _page.Append($"BT /{(bold ? "F2" : "F1")} {_f(size)} Tf {_rgb(c)} rg {_f(x)} {_f(PageHeight - y)} Td ({_escape(text)}) Tj ET\n");
        }

        public void Line(float x1, float y1, float x2, float y2, float width, PdfColor color)
        {
            _page.Append($"{_rgb(color)} RG {_f(width)} w {_f(x1)} {_f(PageHeight - y1)} m {_f(x2)} {_f(PageHeight - y2)} l S\n");
        }

        /// <param name="y">Borde superior del rectángulo, medido desde arriba.</param>
        public void FillRect(float x, float y, float width, float height, PdfColor color)
        {
            _page.Append($"{_rgb(color)} rg {_f(x)} {_f(PageHeight - y - height)} {_f(width)} {_f(height)} re f\n");
        }

        public void StrokeRect(float x, float y, float width, float height, float lineWidth, PdfColor color)
        {
            _page.Append($"{_rgb(color)} RG {_f(lineWidth)} w {_f(x)} {_f(PageHeight - y - height)} {_f(width)} {_f(height)} re S\n");
        }

        public void Polyline(IList<(float x, float y)> points, float width, PdfColor color)
        {
            if (points == null || points.Count < 2) return;
            var sb = _page;
            sb.Append($"{_rgb(color)} RG {_f(width)} w 1 J 1 j {_f(points[0].x)} {_f(PageHeight - points[0].y)} m");
            for (int i = 1; i < points.Count; i++)
                sb.Append($" {_f(points[i].x)} {_f(PageHeight - points[i].y)} l");
            sb.Append(" S\n");
        }

        // ── Medida de texto ────────────────────────────────────────────

        /// <summary>Ancho del texto en puntos (métricas de Helvetica; la negrita se aproxima).</summary>
        public static float TextWidth(string text, float size, bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float units = 0f;
            foreach (char c in _replaceUnsupported(text)) units += _charWidth(c);
            return units / 1000f * size * (bold ? 1.06f : 1f);
        }

        /// <summary>Parte el texto en líneas que caben en <paramref name="maxWidth"/> (respeta los saltos de línea).</summary>
        public static List<string> Wrap(string text, float size, float maxWidth, bool bold = false)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            foreach (string paragraph in text.Replace("\r", "").Split('\n'))
            {
                var current = new StringBuilder();
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = current.Length == 0 ? word : current + " " + word;
                    if (current.Length > 0 && TextWidth(candidate, size, bold) > maxWidth)
                    {
                        lines.Add(current.ToString());
                        current.Clear().Append(word);
                    }
                    else
                    {
                        current.Clear().Append(candidate);
                    }
                }
                lines.Add(current.ToString());
            }
            return lines;
        }

        // ── Salida ─────────────────────────────────────────────────────

        public byte[] ToBytes(string title = null)
        {
            if (_pages.Count == 0) NewPage();

            var objects = new List<string>();
            // 1 catálogo, 2 árbol de páginas, 3-4 fuentes, 5 info; después página + contenido por página
            int pageCount = _pages.Count;
            var kids = new StringBuilder();
            for (int i = 0; i < pageCount; i++) kids.Append($"{6 + i * 2} 0 R ");

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add($"<< /Type /Pages /Kids [{kids.ToString().Trim()}] /Count {pageCount} >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            objects.Add($"<< /Producer (Lutra) /Title ({_escape(title ?? "Informe Lutra")}) >>");

            for (int i = 0; i < pageCount; i++)
            {
                string content = _pages[i].ToString();
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {_f(PageWidth)} {_f(PageHeight)}] " +
                            $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {7 + i * 2} 0 R >>");
                objects.Add($"<< /Length {content.Length} >>\nstream\n{content}endstream");
            }

            using (var ms = new MemoryStream())
            {
                var offsets = new List<long>();
                _write(ms, "%PDF-1.4\n%âãÏÓ\n");
                for (int i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    _write(ms, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
                }

                long xref = ms.Position;
                var sb = new StringBuilder();
                sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
                foreach (long offset in offsets) sb.Append($"{offset:D10} 00000 n \n");
                sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xref}\n%%EOF\n");
                _write(ms, sb.ToString());
                return ms.ToArray();
            }
        }

        // ── Codificación ───────────────────────────────────────────────

        /// <summary>Escribe la cadena como bytes de 1 byte por carácter (los textos ya van en WinAnsi).</summary>
        private static void _write(Stream stream, string s)
        {
            var bytes = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) bytes[i] = (byte)(s[i] & 0xFF);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Símbolos que no existen en WinAnsi (las fuentes estándar no los tienen) → equivalentes legibles.
        /// </summary>
        private static string _replaceUnsupported(string text)
        {
            if (text.IndexOfAny(_unsupported) < 0) return text;
            return text.Replace("\u2264", "<=").Replace("\u2265", ">=").Replace("\u2248", "~")
                       .Replace("\u2212", "-").Replace("\u2192", "->").Replace("\u0394", "D").Replace("\u03A3", "S");
        }

        private static readonly char[] _unsupported = { '\u2264', '\u2265', '\u2248', '\u2212', '\u2192', '\u0394', '\u03A3' };

        /// <summary>Convierte a WinAnsi (1 byte por carácter) y escapa paréntesis y barras.</summary>
        private static string _escape(string text)
        {
            text = _replaceUnsupported(text);
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                char c = _toWinAnsi(ch);
                if (c == '(' || c == ')' || c == '\\') sb.Append('\\');
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static char _toWinAnsi(char c)
        {
            if (c < 0x80) return c >= 0x20 ? c : ' ';
            if (c >= 0xA0 && c <= 0xFF) return c;   // Latin-1 coincide con WinAnsi
            switch (c)
            {
                case '€': return (char)0x80;   // €
                case '…': return (char)0x85;   // …
                case '‘': return (char)0x91;   // ‘
                case '’': return (char)0x92;   // ’
                case '“': return (char)0x93;   // “
                case '”': return (char)0x94;   // ”
                case '•': return (char)0x95;   // •
                case '–': return (char)0x96;   // –
                case '—': return (char)0x97;   // —
                case '≤': return '<';          // ≤ (no existe en WinAnsi)
                case '≥': return '>';          // ≥
                case '→': return '>';          // →
                default:       return '?';
            }
        }

        // ── Métricas de Helvetica (unidades de 1/1000 em, AFM estándar) ──

        private static readonly int[] _asciiWidths =
        {
            // 32-63:  espacio ! " # $ % & ' ( ) * + , - . / 0-9 : ; < = > ?
            278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
            556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
            // 64-95:  @ A-Z [ \ ] ^ _
            1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
            667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
            // 96-126: ` a-z { | } ~
            333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
            556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
        };

        private static float _charWidth(char c)
        {
            if (c >= 32 && c <= 126) return _asciiWidths[c - 32];

            // Letras acentuadas: mismo ancho que la letra base
            string baseLetter = c.ToString().Normalize(NormalizationForm.FormD);
            if (baseLetter.Length > 1 && baseLetter[0] >= 32 && baseLetter[0] <= 126)
                return _asciiWidths[baseLetter[0] - 32];

            switch (c)
            {
                case '¿': return 611;
                case '¡': return 333;
                case '·': return 278;
                case 'º': return 365;
                case 'ª': return 370;
                case '–': return 556;
                case '—': return 1000;
                case '…': return 1000;
                case '•': return 350;
                default:  return 556;
            }
        }

        private static string _f(float v) => v.ToString("0.##", Inv);

        private static string _rgb(PdfColor c) => $"{_f(c.R)} {_f(c.G)} {_f(c.B)}";
    }
}
