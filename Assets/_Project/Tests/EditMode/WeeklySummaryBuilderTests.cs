using System;
using System.Collections.Generic;
using NUnit.Framework;
using Lutra.Core.Data.Models;
using Lutra.Features.Charts;

namespace Lutra.Tests
{
    public class WeeklySummaryBuilderTests
    {
        private static readonly DateTime Monday = new DateTime(2026, 9, 28);   // lunes

        private static EmotionRecord _day(int dayOffset, int mood, EmotionType emotion = EmotionType.Calm,
                                          RecordSource source = RecordSource.User) => new EmotionRecord
        {
            Timestamp      = Monday.AddDays(dayOffset).AddHours(9),
            IsMorningCheck = true,
            MoodLevel      = mood,
            EmotionType    = emotion,
            Source         = source
        };

        private static List<EmotionRecord> _week(params int[] moods)
        {
            var list = new List<EmotionRecord>();
            for (int i = 0; i < moods.Length; i++) list.Add(_day(i, moods[i]));
            return list;
        }

        [TestCase(2026, 10, 5)]   // lunes
        [TestCase(2026, 10, 7)]   // miércoles
        [TestCase(2026, 10, 11)]  // domingo
        public void LunesDeLaSemanaAnterior(int y, int m, int d)
        {
            Assert.AreEqual(Monday, WeeklySummaryBuilder.GetPreviousWeekMonday(new DateTime(y, m, d)));
        }

        [Test]
        public void SinResumen_ConMenosDeTresCheckInsDeDia()
        {
            Assert.IsNull(WeeklySummaryBuilder.BuildBody(Monday, _week(4, 4), null, null));
        }

        [Test]
        public void SinResumen_SiLosRegistrosSonPlaceholders()
        {
            var week = new List<EmotionRecord>
            {
                _day(0, 3, source: RecordSource.RestoredFirestore),
                _day(1, 3, source: RecordSource.RestoredFirestore),
                _day(2, 3, source: RecordSource.RestoredFirestoreHistory)
            };
            Assert.IsNull(WeeklySummaryBuilder.BuildBody(Monday, week, null, null));
        }

        [Test]
        public void AnimoMedioYTendenciaMejor()
        {
            string body = WeeklySummaryBuilder.BuildBody(Monday, _week(4, 4, 4), _week(2, 3, 2), null);
            StringAssert.Contains("4,0 de 5", body);
            StringAssert.Contains("mejor que la semana anterior", body);
        }

        [Test]
        public void TendenciaParecida()
        {
            string body = WeeklySummaryBuilder.BuildBody(Monday, _week(3, 3, 3), _week(2, 3, 4), null);
            StringAssert.Contains("parecido a la semana anterior", body);
        }

        [Test]
        public void SinTendencia_SiLaSemanaAnteriorNoTieneDatos()
        {
            string body = WeeklySummaryBuilder.BuildBody(Monday, _week(3, 3, 3), _week(5), null);
            StringAssert.DoesNotContain("semana anterior", body);
        }

        [Test]
        public void MinijuegoQueMasAyudo_SoloConPartidasValidas()
        {
            DateTime start = Monday.AddHours(18);
            var sessions = new List<MinigameSession>
            {
                new MinigameSession { MinigameId = MinigameType.BreathJump, StartTime = start,
                                      MoodBefore = 2, MoodBeforeRecordedAt = start.AddHours(-1), MoodAfter = 4 },
                // Más jugado, pero sin ánimo después: no cuenta para "lo que más te ayudó"
                new MinigameSession { MinigameId = MinigameType.FruitNinja, StartTime = start },
                new MinigameSession { MinigameId = MinigameType.FruitNinja, StartTime = start }
            };

            string body = WeeklySummaryBuilder.BuildBody(Monday, _week(3, 3, 3), null, sessions);
            StringAssert.Contains("Lo que más te ayudó: Breath Jump", body);
        }

        [Test]
        public void MinijuegoMasJugado_SiNingunoMejora()
        {
            DateTime start = Monday.AddHours(18);
            var sessions = new List<MinigameSession>
            {
                new MinigameSession { MinigameId = MinigameType.FruitNinja, StartTime = start },
                new MinigameSession { MinigameId = MinigameType.FruitNinja, StartTime = start },
                new MinigameSession { MinigameId = MinigameType.Beatmaker,  StartTime = start }
            };

            string body = WeeklySummaryBuilder.BuildBody(Monday, _week(3, 3, 3), null, sessions);
            StringAssert.Contains("Tu minijuego más jugado: Fruit Ninja", body);
        }
    }
}
