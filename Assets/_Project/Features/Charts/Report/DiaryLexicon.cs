using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lutra.Features.Charts
{
    /// <summary>
    /// Diccionario de palabras emocionales para el análisis del diario (docs/METRICS.md §4.7).
    /// Diccionario propio (no LIWC, por licencia), en Assets/Resources/DiaryLexicon_es.txt.
    ///
    /// Formato del archivo:
    ///   # comentario
    ///   [negativo]        sección
    ///   triste            palabra exacta
    ///   deprim*           raíz: cualquier palabra que empiece así (deprimido, deprimida, deprimirse…)
    ///   [positivo]
    ///   …
    /// Las comparaciones ignoran mayúsculas y tildes (Normalize), así "preocupacion" cuenta igual
    /// que "preocupación".
    /// </summary>
    public class DiaryLexicon
    {
        private readonly HashSet<string> _negativeWords = new HashSet<string>();
        private readonly HashSet<string> _positiveWords = new HashSet<string>();
        private readonly List<string>    _negativeStems = new List<string>();
        private readonly List<string>    _positiveStems = new List<string>();

        public int EntryCount => _negativeWords.Count + _positiveWords.Count + _negativeStems.Count + _positiveStems.Count;

        public static DiaryLexicon Parse(string text)
        {
            var lexicon = new DiaryLexicon();
            if (string.IsNullOrEmpty(text)) return lexicon;

            bool? negative = null;
            foreach (var rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("["))
                {
                    string section = Normalize(line.Trim('[', ']'));
                    negative = section == "negativo" ? true : section == "positivo" ? false : (bool?)null;
                    continue;
                }
                if (negative == null) continue;

                bool isStem = line.EndsWith("*");
                string entry = Normalize(isStem ? line.TrimEnd('*') : line);
                if (entry.Length == 0) continue;

                if (isStem) (negative.Value ? lexicon._negativeStems : lexicon._positiveStems).Add(entry);
                else        (negative.Value ? lexicon._negativeWords : lexicon._positiveWords).Add(entry);
            }
            return lexicon;
        }

        /// <summary><paramref name="word"/> ya normalizada.</summary>
        public bool IsNegative(string word) => _matches(word, _negativeWords, _negativeStems);

        /// <summary><paramref name="word"/> ya normalizada.</summary>
        public bool IsPositive(string word) => _matches(word, _positiveWords, _positiveStems);

        /// <summary>Minúsculas y sin tildes ni diéresis (la ñ pasa a n, igual en texto y diccionario).</summary>
        public static string Normalize(string word)
        {
            if (string.IsNullOrEmpty(word)) return string.Empty;

            string decomposed = word.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool _matches(string word, HashSet<string> words, List<string> stems)
        {
            if (words.Contains(word)) return true;
            foreach (var stem in stems)
                if (word.StartsWith(stem, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
