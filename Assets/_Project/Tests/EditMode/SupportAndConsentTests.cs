using System;
using System.Collections.Generic;
using NUnit.Framework;
using Lutra.Core.Data.Models;
using Lutra.Core.Systems;

namespace Lutra.Tests
{
    public class SupportAndConsentTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 6);

        private static EmotionRecord _day(int daysAgo, int mood, RecordSource source = RecordSource.User) => new EmotionRecord
        {
            Timestamp = Today.AddDays(-daysAgo).AddHours(9), IsMorningCheck = true, MoodLevel = mood, Source = source
        };

        [Test]
        public void AnimoBajo_TresDiasSeguidos()
        {
            var records = new List<EmotionRecord> { _day(0, 2), _day(1, 1), _day(2, 2) };
            Assert.IsTrue(SupportRules.HasLowMoodStreak(records, Today));
        }

        [Test]
        public void AnimoBajo_NoSiUnDiaEsMejor()
        {
            var records = new List<EmotionRecord> { _day(0, 2), _day(1, 3), _day(2, 1) };
            Assert.IsFalse(SupportRules.HasLowMoodStreak(records, Today));
        }

        [Test]
        public void AnimoBajo_NoSiFaltaUnDia()
        {
            var records = new List<EmotionRecord> { _day(0, 1), _day(2, 1), _day(3, 1) };
            Assert.IsFalse(SupportRules.HasLowMoodStreak(records, Today));
        }

        [Test]
        public void AnimoBajo_IgnoraMomentosYPlaceholders()
        {
            var moment = _day(1, 1); moment.IsMorningCheck = false;
            var records = new List<EmotionRecord> { _day(0, 1), moment, _day(1, 1, RecordSource.RestoredFirestore), _day(2, 1) };
            Assert.IsFalse(SupportRules.HasLowMoodStreak(records, Today));
        }

        [Test]
        public void Who5_UmbralVeintiocho()
        {
            Assert.IsTrue(SupportRules.IsWho5Low(28));
            Assert.IsFalse(SupportRules.IsWho5Low(32));
        }

        [Test]
        public void PeriodoDeEspera_SieteDias()
        {
            DateTime now = Today.AddHours(12);
            Assert.IsFalse(SupportRules.IsInCooldown(null, now));
            Assert.IsTrue(SupportRules.IsInCooldown(now.AddDays(-6.9), now));
            Assert.IsFalse(SupportRules.IsInCooldown(now.AddDays(-7), now));
        }

        [Test]
        public void Consentimiento_SegunVersion()
        {
            var profile = new UserProfile();
            Assert.IsFalse(ConsentGate.HasConsent(profile));

            profile.Preferences = new Dictionary<string, string> { [ConsentGate.VersionKey] = "0.9" };
            Assert.IsFalse(ConsentGate.HasConsent(profile));

            profile.Preferences = new Dictionary<string, string> { [ConsentGate.VersionKey] = ConsentGate.CurrentVersion };
            Assert.IsTrue(ConsentGate.HasConsent(profile));
        }

        [Test]
        public void AnalisisDelDiario_ActivadoSalvoQueSeDesactive()
        {
            var profile = new UserProfile();
            Assert.IsTrue(ConsentGate.IsDiaryAnalysisEnabled(profile));
            profile.Preferences = new Dictionary<string, string> { [ConsentGate.DiaryAnalysisKey] = "false" };
            Assert.IsFalse(ConsentGate.IsDiaryAnalysisEnabled(profile));
        }
    }
}
