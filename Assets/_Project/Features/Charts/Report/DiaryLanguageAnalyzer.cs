using System.Collections.Generic;
using System.Text;

namespace Lutra.Features.Charts
{
    /// <summary>Recuento de palabras de un texto o de un conjunto de textos.</summary>
    public struct DiaryLanguageCounts
    {
        public int Words;
        public int FirstPerson;
        public int Negative;
        public int Positive;

        public void Add(DiaryLanguageCounts other)
        {
            Words       += other.Words;
            FirstPerson += other.FirstPerson;
            Negative    += other.Negative;
            Positive    += other.Positive;
        }

        public float PctFirstPerson => Words > 0 ? 100f * FirstPerson / Words : 0f;
        public float PctNegative    => Words > 0 ? 100f * Negative    / Words : 0f;
        public float PctPositive    => Words > 0 ? 100f * Positive    / Words : 0f;
    }

    /// <summary>
    /// Análisis del lenguaje del diario en el propio dispositivo (docs/METRICS.md §4.7). Solo
    /// produce cifras: el texto nunca sale de aquí. Clase pura, con tests.
    ///
    /// Limitaciones (para la memoria): no detecta negaciones ("no estoy triste" cuenta como
    /// negativa) y el español omite el sujeto, así que la primera persona queda infraestimada.
    /// Sirve para comparar al usuario consigo mismo, no con normas.
    /// </summary>
    public static class DiaryLanguageAnalyzer
    {
        /// <summary>Primera persona del singular (normalizada: sin tildes).</summary>
        private static readonly HashSet<string> _firstPerson = new HashSet<string>
        {
            "yo", "me", "mi", "mis", "mio", "mia", "mios", "mias", "conmigo"
        };

        public static DiaryLanguageCounts Analyze(string text, DiaryLexicon lexicon)
        {
            var counts = new DiaryLanguageCounts();
            if (string.IsNullOrEmpty(text)) return counts;

            foreach (string word in Tokenize(text))
            {
                counts.Words++;
                if (_firstPerson.Contains(word)) counts.FirstPerson++;
                if (lexicon == null) continue;
                if (lexicon.IsNegative(word))      counts.Negative++;
                else if (lexicon.IsPositive(word)) counts.Positive++;
            }
            return counts;
        }

        /// <summary>Palabras normalizadas: secuencias de letras (los números y signos separan).</summary>
        public static IEnumerable<string> Tokenize(string text)
        {
            var current = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    current.Append(c);
                }
                else if (current.Length > 0)
                {
                    yield return DiaryLexicon.Normalize(current.ToString());
                    current.Clear();
                }
            }
            if (current.Length > 0)
                yield return DiaryLexicon.Normalize(current.ToString());
        }
    }
}
