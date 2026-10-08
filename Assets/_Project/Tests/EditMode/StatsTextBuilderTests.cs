using System;
using System.Collections.Generic;
using NUnit.Framework;
using Lutra.Core.Data.Models;
using Lutra.Features.Charts;

namespace Lutra.Tests
{
    public class StatsTextBuilderTests
    {
        private static ReportData _withMood(float? mean, int n, float? change = null)
        {
            var data = new ReportData();
            data.Mood.Mean = mean;
            data.Mood.N = n;
            data.Mood.ChangeVsPrevious = change;
            return data;
        }

        [Test]
        public void Rangos_SemanaMesYTodo()
        {
            var now = new DateTime(2026, 10, 6, 18, 0, 0);
            Assert.AreEqual(new DateTime(2026, 9, 30), ChartPeriod.Week.GetRange(now).from);
            Assert.AreEqual(new DateTime(2026, 9, 7),  ChartPeriod.Month.GetRange(now).from);
            Assert.AreEqual(DateTime.MinValue,         ChartPeriod.AllTime.GetRange(now).from);
        }

        [Test]
        public void Resumen_SinDatosSuficientes()
        {
            Assert.AreEqual("Te faltan 2 check-ins del día para ver tu resumen.",
                            StatsTextBuilder.Summary(_withMood(null, 1), ChartPeriod.Week));
            Assert.AreEqual("Te falta 1 check-in del día para ver tu resumen.",
                            StatsTextBuilder.Summary(_withMood(null, 2), ChartPeriod.Week));
        }

        [Test]
        public void Resumen_ConTendencia()
        {
            Assert.AreEqual("Tu ánimo medio esta semana ha sido 3,8 de 5, mejor que la semana anterior.",
                            StatsTextBuilder.Summary(_withMood(3.8f, 5, 0.5f), ChartPeriod.Week));
            Assert.AreEqual("Tu ánimo medio estos 30 días ha sido 3,0 de 5, parecido a los 30 días anteriores.",
                            StatsTextBuilder.Summary(_withMood(3f, 20, 0.1f), ChartPeriod.Month));
            Assert.AreEqual("Tu ánimo medio desde que empezaste ha sido 3,0 de 5.",
                            StatsTextBuilder.Summary(_withMood(3f, 20, -1f), ChartPeriod.AllTime));
        }

        [Test]
        public void CaritaDelResumen()
        {
            Assert.AreEqual(4, StatsTextBuilder.SummaryMoodLevel(_withMood(3.6f, 5)));
            Assert.IsNull(StatsTextBuilder.SummaryMoodLevel(_withMood(null, 1)));
        }

        [Test]
        public void Estabilidad_OcultaSinDatosYComparadaConElAnterior()
        {
            var data = new ReportData();
            Assert.IsNull(StatsTextBuilder.Stability(data, ChartPeriod.Week));

            data.Dynamics.Mssd = 0.5f;
            data.Dynamics.PreviousMssd = 1f;
            Assert.AreEqual("Tu ánimo ha estado más estable que los 30 días anteriores.",
                            StatsTextBuilder.Stability(data, ChartPeriod.Month));

            data.Dynamics.PreviousMssd = null;
            Assert.AreEqual("De un día a otro, tu ánimo cambia de media unos 0,7 puntos.",
                            StatsTextBuilder.Stability(data, ChartPeriod.Month));
        }

        [Test]
        public void Motivos_SoloDiferenciasClarasOrdenadas()
        {
            var data = new ReportData();
            data.Motives = new List<MotiveStat>
            {
                new MotiveStat { DisplayName = "Trabajo", MeanMoodWith = 2f, MeanMoodWithout = 4f, Difference = -2f },
                new MotiveStat { DisplayName = "Ocio",    MeanMoodWith = 4f, MeanMoodWithout = 3.5f, Difference = 0.5f },
                new MotiveStat { DisplayName = "Salud",   MeanMoodWith = 3f, MeanMoodWithout = 3.1f, Difference = -0.1f }
            };

            var lines = StatsTextBuilder.MotiveLines(data);
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("Trabajo: cuando aparece, tu ánimo suele ser más bajo (2,0 frente a 4,0).", lines[0]);
            StringAssert.StartsWith("Ocio: cuando aparece, tu ánimo suele ser más alto", lines[1]);
        }

        [Test]
        public void QueTeAyuda_YRespiracion()
        {
            var data = new ReportData();
            Assert.AreEqual("Juega a los minijuegos para descubrir cuáles te ayudan más.", StatsTextBuilder.HelpEmpty(data));

            data.Minigames.Sessions = 4;
            data.Minigames.ByGame.Add(new MinigameStat { Type = MinigameType.BreathJump, MeanDelta = 1.2f, ValidSessions = 4 });
            data.Minigames.ByGame.Add(new MinigameStat { Type = MinigameType.FruitNinja, MeanDelta = -0.5f, ValidSessions = 3 });
            var lines = StatsTextBuilder.HelpLines(data);
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual("Breath Jump: tu ánimo mejora 1,2 puntos de media (4 partidas).", lines[0]);

            Assert.IsNull(StatsTextBuilder.Breathing(data));
            data.Minigames.Breathing.Add(new BreathPoint { BreathsPerMinute = 8.5f });
            data.Minigames.Breathing.Add(new BreathPoint { BreathsPerMinute = 6.4f });
            StringAssert.Contains("pasó de 8,5 a 6,4", StatsTextBuilder.Breathing(data));
        }

        [Test]
        public void Who5_HabitosYDiario()
        {
            var data = new ReportData();
            data.Who5.Add(new Who5Point { Score = 64, ChangeVsPrevious = 12 });
            Assert.AreEqual("Tu último índice de bienestar: 64 / 100 (+12 respecto al anterior).", StatsTextBuilder.Who5(data));

            data.Adherence.DaysInRange = 7; data.Adherence.DaysWithCheckIn = 5; data.Adherence.Pct = 71.4f;
            Assert.AreEqual("Has hecho tu check-in 5 de 7 días (71 %).", StatsTextBuilder.Adherence(data));

            data.Diary.Entries = 3; data.Diary.TotalWords = 1250;
            Assert.AreEqual("3 entradas en tu diario · 1.250 palabras.", StatsTextBuilder.Diary(data));
        }
    }
}
