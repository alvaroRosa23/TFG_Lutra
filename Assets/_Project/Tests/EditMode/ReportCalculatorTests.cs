using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lutra.Core.Data.Models;
using Lutra.Features.Charts;

namespace Lutra.Tests
{
    public class ReportCalculatorTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 1);   // martes
        private static readonly DateTime Today = new DateTime(2026, 10, 6);

        // ── Helpers ────────────────────────────────────────────────────

        private static EmotionRecord _day(int dayOffset, int mood, EmotionType emotion = EmotionType.Calm,
                                          string motivesJson = null, RecordSource source = RecordSource.User) => new EmotionRecord
        {
            Timestamp = Start.AddDays(dayOffset).AddHours(9), IsMorningCheck = true, MoodLevel = mood,
            EmotionType = emotion, SelectedMotiveTags = motivesJson, Source = source
        };

        private static EmotionRecord _moment(int dayOffset, int hour, int mood, EmotionType emotion = EmotionType.Calm) => new EmotionRecord
        {
            Timestamp = Start.AddDays(dayOffset).AddHours(hour), IsMorningCheck = false, MoodLevel = mood, EmotionType = emotion
        };

        private static ReportInput _input(List<EmotionRecord> records, int days = 30) => new ReportInput
        {
            From = Start, To = Start.AddDays(days - 1).AddHours(23), Today = Today, Records = records
        };

        private static List<EmotionRecord> _series(params int[] moods)
            => moods.Select((m, i) => _day(i, m)).ToList();

        // ── Exclusiones y periodo ──────────────────────────────────────

        [Test]
        public void ExcluyePlaceholdersYLosCuenta()
        {
            var records = _series(4, 4, 4);
            records.Add(_day(3, 3, source: RecordSource.RestoredFirestore));
            records.Add(_day(4, 3, source: RecordSource.RestoredFirestoreHistory));

            var data = ReportCalculator.Calculate(_input(records));

            Assert.AreEqual(2, data.ExcludedPlaceholders);
            Assert.AreEqual(3, data.TotalUserRecords);
            Assert.AreEqual(4f, data.Mood.Mean);
        }

        [Test]
        public void ElPeriodoNoEmpiezaAntesDelAlta()
        {
            var input = _input(_series(3, 3, 3));
            input.ProfileCreatedAt = Start.AddDays(10);
            var data = ReportCalculator.Calculate(input);
            Assert.AreEqual(Start.AddDays(10), data.From);
            Assert.AreEqual(20, data.DaysInRange);
        }

        [Test]
        public void HoySinCheckInNoCuentaComoHueco()
        {
            var input = new ReportInput { From = Today.AddDays(-2), To = Today.AddHours(20), Today = Today,
                                          Records = new List<EmotionRecord>() };
            var data = ReportCalculator.Calculate(input);
            Assert.AreEqual(Today.AddDays(-1), data.To);
            Assert.AreEqual(2, data.DaysInRange);
        }

        // ── §4.1 Ánimo ─────────────────────────────────────────────────

        [Test]
        public void Animo_SinMediaConMenosDeTresDias()
        {
            var data = ReportCalculator.Calculate(_input(_series(4, 5)));
            Assert.IsNull(data.Mood.Mean);
            Assert.AreEqual(2, data.Mood.N);
        }

        [Test]
        public void Animo_MediaDiasBuenosYMalos()
        {
            var data = ReportCalculator.Calculate(_input(_series(5, 4, 3, 2)));
            Assert.AreEqual(3.5f, data.Mood.Mean);
            Assert.AreEqual(50f, data.Mood.PctGoodDays);
            Assert.AreEqual(25f, data.Mood.PctBadDays);
            Assert.IsNull(data.Mood.StdDev);       // < 7 días
        }

        [Test]
        public void Animo_TendenciaPositivaEnPuntosPorSemana()
        {
            // Sube 1 punto cada 7 días: pendiente = 1/semana
            var records = new List<EmotionRecord> { _day(0, 1), _day(7, 2), _day(14, 3), _day(21, 4),
                                                    _day(1, 1), _day(8, 2), _day(15, 3) };
            var data = ReportCalculator.Calculate(_input(records));
            Assert.IsNotNull(data.Mood.SlopePerWeek);
            Assert.Greater(data.Mood.SlopePerWeek.Value, 0.8f);
        }

        [Test]
        public void Animo_VariacionIntradia()
        {
            var records = _series(3, 3, 3, 3, 3);
            for (int i = 0; i < 5; i++) records.Add(_moment(i, 18, 4));
            records.Add(_moment(10, 18, 1));   // sin check-in de Día ese día: no forma par

            var data = ReportCalculator.Calculate(_input(records));
            Assert.AreEqual(5, data.Mood.IntradayPairs);
            Assert.AreEqual(1f, data.Mood.IntradayDelta);
        }

        [Test]
        public void Animo_ComparacionConPeriodoAnterior()
        {
            var input = _input(_series(4, 4, 4));
            input.PreviousRecords = new List<EmotionRecord> { _day(-3, 2), _day(-2, 2), _day(-1, 3) };
            var data = ReportCalculator.Calculate(input);
            Assert.AreEqual(4f - 7f / 3f, data.Mood.ChangeVsPrevious.Value, 0.001f);
        }

        // ── §4.2 Dinámica ──────────────────────────────────────────────

        [Test]
        public void Dinamica_SoloParesDeDiasConsecutivos()
        {
            // 15 días seguidos alternando 2 y 4 → 14 pares, cada diferencia al cuadrado = 4
            var records = Enumerable.Range(0, 15).Select(i => _day(i, i % 2 == 0 ? 2 : 4)).ToList();
            // Un día suelto tras un hueco no añade par
            records.Add(_day(20, 5));

            var data = ReportCalculator.Calculate(_input(records));
            Assert.AreEqual(14, data.Dynamics.ConsecutivePairs);
            Assert.AreEqual(4f, data.Dynamics.Mssd);
            Assert.AreEqual(-1f, data.Dynamics.Inertia.Value, 0.001f);   // alterna: correlación perfecta negativa
        }

        [Test]
        public void Dinamica_NullConMenosDeCatorcePares()
        {
            var data = ReportCalculator.Calculate(_input(_series(3, 4, 3, 4, 3)));
            Assert.IsNull(data.Dynamics.Mssd);
            Assert.IsNull(data.Dynamics.Inertia);
        }

        [Test]
        public void Dinamica_InerciaPositivaSiElAnimoPersiste()
        {
            var moods = new[] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 4, 4, 3, 3, 2 };
            var data  = ReportCalculator.Calculate(_input(_series(moods)));
            Assert.Greater(data.Dynamics.Inertia.Value, 0.5f);
        }

        // ── §4.3 Perfil emocional ──────────────────────────────────────

        [Test]
        public void Emodiversidad_ExtremosCeroYUno()
        {
            Assert.AreEqual(0f, ReportCalculator.Emodiversity(new[] { 10 }), 0.0001f);
            Assert.AreEqual(1f, ReportCalculator.Emodiversity(Enumerable.Repeat(3, 8)), 0.0001f);
        }

        [Test]
        public void PerfilEmocional_ValenciaYCuadrantes()
        {
            var emotions = new[] { EmotionType.Anxiety, EmotionType.Anxiety, EmotionType.Sadness, EmotionType.Calm,
                                   EmotionType.Joy, EmotionType.Nostalgia, EmotionType.Joy, EmotionType.Energy,
                                   EmotionType.Calm, EmotionType.Calm };
            var records = emotions.Select((e, i) => _day(i, 3, e)).ToList();

            var data = ReportCalculator.Calculate(_input(records));
            Assert.AreEqual(30f, data.Emotions.PctUnpleasant);
            Assert.AreEqual(60f, data.Emotions.PctPleasant);
            Assert.AreEqual(10f, data.Emotions.PctMixed);
            Assert.AreEqual(2, data.Emotions.QuadrantCounts[AffectQuadrant.Tension]);
            Assert.AreEqual(EmotionType.Calm, data.Emotions.MostFrequent);
            Assert.AreEqual(6, data.Emotions.DistinctEmotions);
            Assert.IsNotNull(data.Emotions.Emodiversity);
        }

        // ── §4.4 Motivos ───────────────────────────────────────────────

        [Test]
        public void Motivos_AnimoConYSinYAgrupacionDeOtros()
        {
            var records = new List<EmotionRecord>
            {
                _day(0, 2, EmotionType.Anxiety, "[\"Trabajo\"]"),
                _day(1, 2, EmotionType.Anxiety, "[\"Trabajo\",\"mi jefe\"]"),
                _day(2, 2, EmotionType.Frustration, "[\"Trabajo\",\"Football\"]"),
                _day(3, 4, EmotionType.Joy, "[\"Football\",\"el perro\"]"),
                _day(4, 5, EmotionType.Joy, "[\"Football\",\"la playa\"]"),
                _day(5, 4, EmotionType.Calm, "[]")
            };
            var data = ReportCalculator.Calculate(_input(records));

            var trabajo = data.Motives.Single(m => m.Key == "Trabajo");
            Assert.AreEqual(3, trabajo.Count);
            Assert.AreEqual(2f, trabajo.MeanMoodWith);
            Assert.AreEqual(13f / 3f, trabajo.MeanMoodWithout.Value, 0.001f);
            Assert.AreEqual(EmotionType.Anxiety, trabajo.MostFrequentEmotion);

            var futbol = data.Motives.Single(m => m.Key == "Football");
            Assert.AreEqual("Fútbol", futbol.DisplayName);

            Assert.AreEqual(3, data.Motives.Single(m => m.Key == MotiveTags.OtherKey).Count);
        }

        [Test]
        public void Motivos_SeOmitenConMenosDeTresApariciones()
        {
            var records = new List<EmotionRecord> { _day(0, 3, motivesJson: "[\"Salud\"]"), _day(1, 3, motivesJson: "[\"Salud\"]") };
            Assert.IsEmpty(ReportCalculator.Calculate(_input(records)).Motives);
        }

        // ── §4.5 Patrones ──────────────────────────────────────────────

        [Test]
        public void Patrones_PorDiaDeLaSemana()
        {
            // Start es martes (índice 1). Dos martes y un miércoles.
            var records = new List<EmotionRecord> { _day(0, 2), _day(7, 4), _day(1, 5) };
            var data = ReportCalculator.Calculate(_input(records));
            Assert.AreEqual(3f, data.Patterns.MeanMoodByWeekday[1]);
            Assert.IsNull(data.Patterns.MeanMoodByWeekday[2]);   // un solo miércoles
            Assert.AreEqual(1, data.Patterns.DayCheckInsByWeekday[2]);
        }

        // ── §4.6 Minijuegos ────────────────────────────────────────────

        private static MinigameSession _session(MinigameType type, int? before, double hoursBefore, int? after,
                                                EmotionType emotion = EmotionType.Anxiety)
        {
            DateTime start = Start.AddDays(2).AddHours(18);
            return new MinigameSession
            {
                MinigameId = type, StartTime = start, DurationSeconds = 120, EmotionBefore = emotion,
                MoodBefore = before, MoodBeforeRecordedAt = before.HasValue ? start.AddHours(-hoursBefore) : (DateTime?)null,
                MoodAfter = after
            };
        }

        [Test]
        public void Minijuegos_EfectoSoloConPartidasValidas()
        {
            var input = _input(_series(3, 3, 3));
            input.Sessions = new List<MinigameSession>
            {
                _session(MinigameType.BreathJump, 2, 1, 4),
                _session(MinigameType.BreathJump, 2, 1, 3),
                _session(MinigameType.BreathJump, 3, 1, 3),
                _session(MinigameType.BreathJump, 1, 5, 5),     // ánimo previo de hace 5 h: no válida
                _session(MinigameType.FruitNinja, 2, 1, null)   // sin ánimo después: no válida
            };

            var data = ReportCalculator.Calculate(input);
            var breath = data.Minigames.ByGame.Single(g => g.Type == MinigameType.BreathJump);

            Assert.AreEqual(5, data.Minigames.Sessions);
            Assert.AreEqual(3, data.Minigames.ValidSessions);
            Assert.AreEqual(10f, data.Minigames.TotalMinutes, 0.001f);
            Assert.AreEqual(4, breath.Sessions);
            Assert.AreEqual(3, breath.ValidSessions);
            Assert.AreEqual(1f, breath.MeanDelta);
            Assert.AreEqual(200f / 3f, breath.PctImproved.Value, 0.01f);
            Assert.IsNull(data.Minigames.ByGame.Single(g => g.Type == MinigameType.FruitNinja).MeanDelta);
        }

        [Test]
        public void Minijuegos_EvolucionDeLaRespiracion()
        {
            var input = _input(_series(3, 3, 3));
            var session = _session(MinigameType.BreathJump, null, 0, null);
            session.Metrics = new Dictionary<string, float>
            {
                ["perfect_breaths"] = 15, ["good_breaths"] = 4, ["off_rhythm_breaths"] = 1,
                ["breaths_per_minute"] = 6.2f, ["exhale_inhale_ratio"] = 1.4f, ["breath_cv"] = 0.08f
            };
            var old = _session(MinigameType.BreathJump, null, 0, null);   // partida antigua sin métricas nuevas
            input.Sessions = new List<MinigameSession> { session, old };

            var data = ReportCalculator.Calculate(input);
            Assert.AreEqual(1, data.Minigames.Breathing.Count);
            Assert.AreEqual(6.2f, data.Minigames.Breathing[0].BreathsPerMinute);
            Assert.AreEqual(75f, data.Minigames.Breathing[0].PctPerfect);
        }

        // ── §4.7 Diario ────────────────────────────────────────────────

        [Test]
        public void Diario_PorcentajesSoloConCincuentaPalabrasYAnalisisActivado()
        {
            string longText = string.Join(" ", Enumerable.Repeat("hoy me siento triste", 15));   // 60 palabras
            var input = _input(_series(3, 3, 3));
            input.Lexicon = DiaryLexicon.Parse("[negativo]\ntrist*\n");
            input.DiaryEntries = new List<DiaryEntry>
            {
                new DiaryEntry { Date = Start.AddHours(20), Title = "", Content = longText },        // semana 1
                new DiaryEntry { Date = Start.AddDays(7).AddHours(20), Title = "", Content = "breve" } // semana 2
            };

            var data = ReportCalculator.Calculate(input);
            Assert.AreEqual(2, data.Diary.Entries);
            Assert.AreEqual(61, data.Diary.TotalWords);
            Assert.AreEqual(25f, data.Diary.Weeks[0].PctNegative.Value, 0.001f);
            Assert.AreEqual(25f, data.Diary.Weeks[0].PctFirstPerson.Value, 0.001f);
            Assert.IsNull(data.Diary.Weeks[1].PctNegative);

            input.DiaryAnalysisEnabled = false;
            data = ReportCalculator.Calculate(input);
            Assert.IsFalse(data.Diary.LanguageAnalysisEnabled);
            Assert.AreEqual(61, data.Diary.TotalWords);          // las palabras se cuentan igualmente
            Assert.IsNull(data.Diary.Weeks[0].PctNegative);
        }

        // ── §4.8 WHO-5 ─────────────────────────────────────────────────

        [Test]
        public void Who5_CambioRespectoAlAnteriorYAvisos()
        {
            var input = _input(_series(3, 3, 3));
            input.Who5Responses = new List<ScaleResponse>
            {
                new ScaleResponse { Scale = ScaleType.Who5, CompletedAt = Start.AddDays(-10), AvailableSince = Start.AddDays(-12), Score = 60, RawScore = 15, DurationSeconds = 40 },
                new ScaleResponse { Scale = ScaleType.Who5, CompletedAt = Start.AddDays(5),   AvailableSince = Start.AddDays(4),   Score = 24, RawScore = 6,  DurationSeconds = 6 }
            };

            var data = ReportCalculator.Calculate(input);
            Assert.AreEqual(1, data.Who5.Count);   // la anterior solo sirve para el cambio
            var p = data.Who5[0];
            Assert.AreEqual(-36, p.ChangeVsPrevious);
            Assert.IsTrue(p.RelevantChange);
            Assert.IsTrue(p.LowWellBeing);
            Assert.IsTrue(p.ProbableDepression);
            Assert.IsTrue(p.PossiblyInattentive);
            Assert.AreEqual(1, p.DelayDays);
        }

        // ── §4.9 Adherencia ────────────────────────────────────────────

        [Test]
        public void Adherencia_HuecosDeDosDiasOMas()
        {
            // 10 días: registros en 0,1,2 · hueco 3-4 · 5 · hueco de 1 día (6) · 7,8,9
            var records = new[] { 0, 1, 2, 5, 7, 8, 9 }.Select(d => _day(d, d == 2 ? 1 : 3)).ToList();
            var data = ReportCalculator.Calculate(_input(records, days: 10));

            Assert.AreEqual(10, data.Adherence.DaysInRange);
            Assert.AreEqual(7, data.Adherence.DaysWithCheckIn);
            Assert.AreEqual(70f, data.Adherence.Pct);
            Assert.AreEqual(1, data.Adherence.Gaps.Count);
            Assert.AreEqual(Start.AddDays(3), data.Adherence.Gaps[0].Start);
            Assert.AreEqual(2, data.Adherence.Gaps[0].Days);
            Assert.AreEqual(1f, data.Adherence.MeanMoodBeforeGaps);   // el día 2 tuvo ánimo 1
        }

        // ── Apoyo ──────────────────────────────────────────────────────

        [Test]
        public void ActivacionesDelProtocoloDeApoyo()
        {
            var input = _input(_series(3, 3, 3));
            input.SupportNotifications = new List<AppNotification>
            {
                new AppNotification { Type = NotificationType.Support, CreatedAt = Start.AddDays(4), SourceRef = "who5_bajo" }
            };
            var data = ReportCalculator.Calculate(input);
            Assert.AreEqual("who5_bajo", data.SupportActivations.Single().Trigger);
        }
    }
}
