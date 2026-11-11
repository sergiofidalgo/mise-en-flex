using Game.Data;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The kitchen's day card text (decision 2026-11-10).</summary>
    public class DayCardTextTests
    {
        [TearDown]
        public void BackToEnglish()
        {
            Locale.Language = "en";
            Translations.Clear("es");
        }

        [Test]
        public void TheDayIsAlreadyCountedFromOne()
        {
            Assert.That(DayCardText.Title(1), Is.EqualTo("Day 1"));
            Assert.That(DayCardText.Title(12), Is.EqualTo("Day 12"));
        }

        [Test]
        public void TheClockStartsAtOpeningAndRunsAMinuteASecond()
        {
            Assert.That(DayCardText.Clock(DayCardText.Minutes(18f, 0f)), Is.EqualTo("18:00"));
            Assert.That(DayCardText.Clock(DayCardText.Minutes(18f, 42f * 60f)), Is.EqualTo("18:42"));
            Assert.That(DayCardText.Clock(DayCardText.Minutes(18.5f, 5f * 60f)), Is.EqualTo("18:35"));
            Assert.That(DayCardText.Clock(DayCardText.Minutes(23f, 75f * 60f)), Is.EqualTo("00:15"), "wraps past midnight");
            Assert.That(DayCardText.Clock(DayCardText.Minutes(18f, 59.9f * 60f)), Is.EqualTo("18:59"), "never rounds up early");
        }

        [Test]
        public void BeforeServiceItSaysWhenItOpens()
        {
            Assert.That(DayCardText.Line(DayPhase.Plan, ServicePhase.Open, 18f, 0f), Is.EqualTo(("Plan · opens 18:00", false)));
            Assert.That(DayCardText.Line(DayPhase.Prep, ServicePhase.Open, 18f, 999f), Is.EqualTo(("Prep · opens 18:00", false)));
        }

        [Test]
        public void DuringServiceItShowsTheClockAndTheEnding()
        {
            Assert.That(DayCardText.Line(DayPhase.Service, ServicePhase.Open, 18f, 42f * 60f), Is.EqualTo(("Service · 18:42", false)));
            Assert.That(DayCardText.Line(DayPhase.Service, ServicePhase.LastOrders, 18f, 170f * 60f), Is.EqualTo(("Last orders · 20:50", true)));
            Assert.That(DayCardText.Line(DayPhase.Service, ServicePhase.Closed, 18f, 180f * 60f), Is.EqualTo(("Closed · 21:00", true)));
        }

        [Test]
        public void ItSpeaksSpanish()
        {
            Translations.LoadCsv("es", System.IO.File.ReadAllText("Assets/_Project/Data/Localization/es.csv"));
            Locale.Language = "es";
            Assert.That(DayCardText.Title(3), Is.EqualTo("Día 3"));
            Assert.That(DayCardText.Line(DayPhase.Service, ServicePhase.Open, 18f, 42f * 60f).Text, Is.EqualTo("Servicio · 18:42"));
            Assert.That(DayCardText.Line(DayPhase.Prep, ServicePhase.Open, 18f, 0f).Text, Is.EqualTo("Preparación · abre a las 18:00"));
            Assert.That(DayCardText.Line(DayPhase.Service, ServicePhase.LastOrders, 18f, 0f).Text, Is.EqualTo("Últimos pedidos · 18:00"));
        }

        [Test]
        public void PrepSaysHowManyAndInWhatGroups()
        {
            Assert.That(DayCardText.Guests(5), Is.EqualTo("About 5 guests"));
            Assert.That(DayCardText.Guests(1), Is.EqualTo("About 1 guest"));
            Assert.That(DayCardText.Opens(18f), Is.EqualTo("Opens 18:00"));
            Assert.That(DayCardText.Groups(1f), Is.EqualTo("Groups of one"));
            Assert.That(DayCardText.Groups(1.5f), Is.EqualTo("Groups of one or two"), "halfway names both");
            Assert.That(DayCardText.Groups(2f), Is.EqualTo("Groups of two"));
            Assert.That(DayCardText.Groups(2.5f), Is.EqualTo("Groups of two or three"));
            Assert.That(DayCardText.Groups(3f), Is.EqualTo("Groups of three"));
            Assert.That(DayCardText.Groups(4.2f), Is.EqualTo("Groups of about 4"));
        }

        [Test]
        public void PrepSpeaksSpanishToo()
        {
            Translations.LoadCsv("es", System.IO.File.ReadAllText("Assets/_Project/Data/Localization/es.csv"));
            Locale.Language = "es";
            Assert.That(DayCardText.Guests(5), Is.EqualTo("Unos 5 clientes"));
            Assert.That(DayCardText.Opens(18f), Is.EqualTo("Abre a las 18:00"));
            Assert.That(DayCardText.Groups(2f), Is.EqualTo("Grupos de dos"));
        }
    }
}
