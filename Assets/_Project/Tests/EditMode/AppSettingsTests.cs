using NUnit.Framework;
using Lutra.Core.Data.Models;

namespace Lutra.Tests
{
    public class AppSettingsTests
    {
        [Test]
        public void Json_ConservaTodosLosAjustes()
        {
            var original = new AppSettings
            {
                Colorblind           = ColorblindMode.Tritanopia,
                NotificationsEnabled = false,
                ReminderHour         = 9,
                ReminderMinute       = 30,
                FontSize             = 2,
                HighContrast         = true,
                MusicVolume          = 0.25f,
                Language             = "en"
            };

            var copy = AppSettings.FromJson(original.ToJson());

            Assert.IsNotNull(copy);
            Assert.AreEqual(ColorblindMode.Tritanopia, copy.Colorblind);
            Assert.IsFalse(copy.NotificationsEnabled);
            Assert.AreEqual(9, copy.ReminderHour);
            Assert.AreEqual(30, copy.ReminderMinute);
            Assert.AreEqual(2, copy.FontSize);
            Assert.IsTrue(copy.HighContrast);
            Assert.AreEqual(0.25f, copy.MusicVolume);
            Assert.AreEqual("en", copy.Language);
        }

        [Test]
        public void Json_CamposQueFaltanTomanElValorPorDefecto()
        {
            // JSON guardado por una versión anterior, sin los ajustes añadidos después
            var copy = AppSettings.FromJson("{\"Colorblind\":1}");

            Assert.AreEqual(ColorblindMode.Deuteranopia, copy.Colorblind);
            Assert.IsTrue(copy.NotificationsEnabled);
            Assert.AreEqual(20, copy.ReminderHour);
            Assert.AreEqual(1f, copy.SfxVolume);
        }

        [Test]
        public void Json_VacioDevuelveNull()
        {
            Assert.IsNull(AppSettings.FromJson(null));
            Assert.IsNull(AppSettings.FromJson("  "));
        }
    }
}
