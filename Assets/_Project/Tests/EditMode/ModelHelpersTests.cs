using NUnit.Framework;
using Lutra.Core.Data.Models;

namespace Lutra.Tests
{
    public class ModelHelpersTests
    {
        [TestCase(EmotionType.Anxiety,     AffectQuadrant.Tension)]
        [TestCase(EmotionType.Overwhelm,   AffectQuadrant.Tension)]
        [TestCase(EmotionType.Frustration, AffectQuadrant.Tension)]
        [TestCase(EmotionType.Sadness,     AffectQuadrant.LowMood)]
        [TestCase(EmotionType.Nostalgia,   AffectQuadrant.Mixed)]
        [TestCase(EmotionType.Calm,        AffectQuadrant.Calm)]
        [TestCase(EmotionType.Energy,      AffectQuadrant.Enthusiasm)]
        [TestCase(EmotionType.Joy,         AffectQuadrant.Enthusiasm)]
        public void Circumplejo(EmotionType emotion, AffectQuadrant expected)
        {
            Assert.AreEqual(expected, emotion.QuadrantOf());
        }

        [Test]
        public void Motivos_Agrupacion()
        {
            Assert.AreEqual("Trabajo",  MotiveTags.GroupKey("Trabajo"));
            Assert.AreEqual("Football", MotiveTags.GroupKey("Football"));
            Assert.AreEqual(MotiveTags.OtherKey, MotiveTags.GroupKey("mi perro"));
            Assert.AreEqual(MotiveTags.OtherKey, MotiveTags.GroupKey("2"));
            Assert.AreEqual(MotiveTags.OtherKey, MotiveTags.GroupKey("Football, Tennis"));
            Assert.AreEqual("Fútbol", MotiveTags.DisplayName("Football"));
            Assert.AreEqual("Trabajo", MotiveTags.DisplayName("Trabajo"));
        }

        [Test]
        public void Motivos_JsonInvalidoDaListaVacia()
        {
            Assert.IsEmpty(MotiveTags.Parse("no es json"));
            Assert.IsEmpty(MotiveTags.Parse(null));
            CollectionAssert.AreEqual(new[] { "Trabajo", "Salud" }, MotiveTags.Parse("[\"Trabajo\",\"Salud\"]"));
        }
    }
}
