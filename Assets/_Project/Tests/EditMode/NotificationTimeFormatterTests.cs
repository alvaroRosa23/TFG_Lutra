using System;
using NUnit.Framework;
using Lutra.Features.Notifications;

namespace Lutra.Tests
{
    public class NotificationTimeFormatterTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 6);

        [Test]
        public void Grupos()
        {
            Assert.AreEqual("Hoy",         NotificationTimeFormatter.GroupLabel(Today.AddHours(10), Today));
            Assert.AreEqual("Ayer",        NotificationTimeFormatter.GroupLabel(Today.AddDays(-1), Today));
            Assert.AreEqual("Esta semana", NotificationTimeFormatter.GroupLabel(Today.AddDays(-6), Today));
            Assert.AreEqual("29 de septiembre", NotificationTimeFormatter.GroupLabel(Today.AddDays(-7), Today));
        }

        [Test]
        public void Grupo_ConAnioSiEsDeOtroAnio()
        {
            Assert.AreEqual("20 de diciembre de 2025",
                NotificationTimeFormatter.GroupLabel(new DateTime(2025, 12, 20), Today));
        }

        [Test]
        public void Hora_ConDiaSoloEnEstaSemana()
        {
            Assert.AreEqual("18:42", NotificationTimeFormatter.TimeText(Today.AddHours(18).AddMinutes(42), Today));
            Assert.AreEqual("viernes, 09:05",
                NotificationTimeFormatter.TimeText(new DateTime(2026, 10, 2, 9, 5, 0), Today));
        }
    }
}
