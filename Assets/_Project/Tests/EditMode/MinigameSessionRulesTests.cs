using System;
using NUnit.Framework;
using Lutra.Core.Data.Models;

namespace Lutra.Tests
{
    public class MinigameSessionRulesTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 6, 18, 0, 0);

        private static MinigameSession _session(int? before, double hoursBefore, int? after) => new MinigameSession
        {
            StartTime            = Start,
            MoodBefore           = before,
            MoodBeforeRecordedAt = before.HasValue ? Start.AddHours(-hoursBefore) : (DateTime?)null,
            MoodAfter            = after
        };

        [Test]
        public void Valida_ConAnimoPrevioReciente()
        {
            var session = _session(2, 1, 4);
            Assert.IsTrue(session.IsValidForMoodEffect());
            Assert.AreEqual(2, session.MoodDelta());
        }

        [Test]
        public void Valida_JustoEnElLimiteDeTresHoras()
        {
            Assert.IsTrue(_session(3, 3, 3).IsValidForMoodEffect());
        }

        [Test]
        public void NoValida_AnimoPrevioDeMasDeTresHoras()
        {
            var session = _session(2, 4, 4);
            Assert.IsFalse(session.IsValidForMoodEffect());
            Assert.IsNull(session.MoodDelta());
        }

        [Test]
        public void NoValida_SinAnimoDespues()
        {
            Assert.IsFalse(_session(2, 1, null).IsValidForMoodEffect());
        }

        [Test]
        public void NoValida_SinAnimoPrevio()
        {
            Assert.IsFalse(_session(null, 0, 4).IsValidForMoodEffect());
        }
    }
}
