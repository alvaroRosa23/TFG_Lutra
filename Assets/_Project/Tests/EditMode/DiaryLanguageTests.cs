using System.IO;
using NUnit.Framework;
using Lutra.Features.Charts;

namespace Lutra.Tests
{
    public class DiaryLanguageTests
    {
        private static readonly DiaryLexicon Lexicon = DiaryLexicon.Parse(
            "# prueba\n[negativo]\ntrist*\nmal\npreocup*\n[positivo]\nfeliz\nalegr*\n");

        [Test]
        public void CuentaPalabrasPrimeraPersonaYEmociones()
        {
            var c = DiaryLanguageAnalyzer.Analyze("Hoy me sentí MUY triste, pero mi hermana me alegró.", Lexicon);
            Assert.AreEqual(10, c.Words);
            Assert.AreEqual(3, c.FirstPerson);   // me, mi, me
            Assert.AreEqual(1, c.Negative);      // triste
            Assert.AreEqual(1, c.Positive);      // alegró
        }

        [Test]
        public void IgnoraTildesYMayusculas()
        {
            var c = DiaryLanguageAnalyzer.Analyze("PREOCUPACIÓN preocupacion Mío", Lexicon);
            Assert.AreEqual(2, c.Negative);
            Assert.AreEqual(1, c.FirstPerson);
        }

        [Test]
        public void PalabraExactaNoCapturaOtras()
        {
            var c = DiaryLanguageAnalyzer.Analyze("Hice la maleta", Lexicon);   // "mal" es exacta
            Assert.AreEqual(0, c.Negative);
        }

        [Test]
        public void SinDiccionario_SoloCuentaPalabras()
        {
            var c = DiaryLanguageAnalyzer.Analyze("Estoy triste", null);
            Assert.AreEqual(2, c.Words);
            Assert.AreEqual(0, c.Negative);
        }

        [Test]
        public void Porcentajes()
        {
            var c = new DiaryLanguageCounts { Words = 50, FirstPerson = 5, Negative = 2, Positive = 1 };
            Assert.AreEqual(10f, c.PctFirstPerson, 0.001f);
            Assert.AreEqual(4f,  c.PctNegative,    0.001f);
            Assert.AreEqual(2f,  c.PctPositive,    0.001f);
        }

        [Test]
        public void DiccionarioReal_SeCargaYEvitaFalsosPositivos()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", "DiaryLexicon_es.txt");
            if (!File.Exists(path)) Assert.Ignore("Diccionario no accesible desde este directorio de trabajo.");

            var lexicon = DiaryLexicon.Parse(File.ReadAllText(path));
            Assert.Greater(lexicon.EntryCount, 150);

            var c = DiaryLanguageAnalyzer.Analyze(
                "Solo quiero aterrizar, ver el contenido y los precios. Estoy agobiada y aterrada, pero contenta.", lexicon);
            Assert.AreEqual(2, c.Negative);   // agobiada, aterrada (no "solo", "aterrizar")
            Assert.AreEqual(1, c.Positive);   // contenta (no "contenido", "precios")
        }
    }
}
