using System.Linq;
using Game.Data;
using Game.Simulation.Saving;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Guests' names translate as names, not as text (decision 2026-11-09).</summary>
    public class GuestNameTests
    {
        [TearDown]
        public void BackToEnglish() => GuestNames.Language = "en";

        [Test]
        public void EveryLanguageFillsEverySlot()
        {
            foreach (var table in GuestNames.All)
            {
                Assert.That(table.Value.Length, Is.EqualTo(GuestNames.Slots), $"{table.Key} has a name for every guest");
                Assert.That(table.Value.All(n => !string.IsNullOrWhiteSpace(n)), Is.True, table.Key);
                Assert.That(table.Value.Distinct().Count(), Is.EqualTo(table.Value.Length), $"{table.Key} names are all different");
            }
        }

        [Test]
        public void TheSameGuestIsShownInThePlayersLanguage()
        {
            var review = new ReviewRecord { Guest = "Mike S.", GuestSlot = 0, GuestInitial = "S" };
            Assert.That(GuestNames.Display(review), Is.EqualTo("Mike S."));
            GuestNames.Language = "es";
            Assert.That(GuestNames.Display(review), Is.EqualTo("Miguel S."));
            Assert.That(GuestNames.FirstOf(review), Is.EqualTo("Miguel"));
            GuestNames.Language = "fr";
            Assert.That(GuestNames.Display(review), Is.EqualTo("Mike S."), "a language with no table shows English");
        }

        [Test]
        public void OlderSavesShowTheNameTheyKept()
        {
            var review = new ReviewRecord { Guest = "Inés R." };
            GuestNames.Language = "es";
            Assert.That(GuestNames.Display(review), Is.EqualTo("Inés R."));
        }

        [Test]
        public void AGuestKeepsTheirSlotAndInitial()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<ServiceConfig>();
            var (slot, initial) = config.GuestSlot(4, 2);
            Assert.That(config.GuestSlot(4, 2), Is.EqualTo((slot, initial)), "the same guest, the same name");
            Assert.That(config.GuestName(4, 2), Is.EqualTo($"{GuestNames.First(slot, "en")} {initial}."));
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
