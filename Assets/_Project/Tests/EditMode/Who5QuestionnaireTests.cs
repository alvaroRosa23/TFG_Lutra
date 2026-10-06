using System;
using NUnit.Framework;
using Lutra.Features.Who5;

namespace Lutra.Tests
{
    public class Who5QuestionnaireTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 6);

        [Test]
        public void Contenido_CincoItemsYSeisOpcionesDeCincoACero()
        {
            Assert.AreEqual(Who5Questionnaire.ItemCount, Who5Questionnaire.Items.Length);
            Assert.AreEqual(6, Who5Questionnaire.Options.Length);
            Assert.AreEqual(5, Who5Questionnaire.Options[0].value);
            Assert.AreEqual(0, Who5Questionnaire.Options[5].value);
        }

        [Test]
        public void Puntuacion_BrutaEIndice()
        {
            int raw = Who5Questionnaire.RawScore(new[] { 5, 4, 3, 2, 1 });
            Assert.AreEqual(15, raw);
            Assert.AreEqual(60, Who5Questionnaire.ToIndex(raw));
            Assert.AreEqual(100, Who5Questionnaire.ToIndex(Who5Questionnaire.RawScore(new[] { 5, 5, 5, 5, 5 })));
            Assert.AreEqual(0, Who5Questionnaire.ToIndex(Who5Questionnaire.RawScore(new[] { 0, 0, 0, 0, 0 })));
        }

        [Test]
        public void Puntuacion_RechazaRespuestasIncompletasOFueraDeRango()
        {
            Assert.Throws<ArgumentException>(() => Who5Questionnaire.RawScore(new[] { 5, 4, 3, 2 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Who5Questionnaire.RawScore(new[] { 5, 4, 3, 2, 6 }));
        }

        [Test]
        public void Disponible_LineaBaseDesdeElAlta()
        {
            var created = new DateTime(2026, 9, 1, 12, 30, 0);
            Assert.AreEqual(created.Date, Who5Questionnaire.GetAvailableSince(created, null, Today));
        }

        [Test]
        public void Disponible_LineaBaseHoySiElAltaNoEsValida()
        {
            Assert.AreEqual(Today, Who5Questionnaire.GetAvailableSince(default, null, Today));
        }

        [Test]
        public void NoDisponible_AntesDeCatorceDias()
        {
            var completed = Today.AddDays(-13).AddHours(20);
            Assert.IsNull(Who5Questionnaire.GetAvailableSince(new DateTime(2026, 1, 1), completed, Today));
        }

        [Test]
        public void Disponible_AlCumplirseCatorceDiasYSeMantieneSiSeRetrasa()
        {
            var completed = Today.AddDays(-14).AddHours(23);
            Assert.AreEqual(Today, Who5Questionnaire.GetAvailableSince(new DateTime(2026, 1, 1), completed, Today));
            Assert.AreEqual(Today, Who5Questionnaire.GetAvailableSince(new DateTime(2026, 1, 1), completed, Today.AddDays(5)));
        }

        [Test]
        public void IdDeNotificacion_IdaYVuelta()
        {
            string id = Who5Questionnaire.NotificationRemoteId(new DateTime(2026, 10, 20));
            Assert.AreEqual("who5-2026-10-20", id);
            Assert.IsTrue(Who5Questionnaire.TryParseAvailableSince(id, out var date));
            Assert.AreEqual(new DateTime(2026, 10, 20), date);
            Assert.IsFalse(Who5Questionnaire.TryParseAvailableSince("weekly-2026-10-20", out _));
        }
    }
}
