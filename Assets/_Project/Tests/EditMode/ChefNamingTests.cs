using System.Linq;
using Game.Simulation.Campaign;
using Game.Simulation.Characters;
using Game.Simulation.Days;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Naming yourself as you join, and when you may join a kitchen (2026-11-13).</summary>
    public class ChefNamingTests
    {
        private static readonly string[] Pool = { "Mike", "Jessica", "Tyler", "Ashley", "Brandon", "Megan" };

        [Test]
        public void LastTimesNameIsOnePressAway()
        {
            var naming = new ChefNaming("Rosa", Pool, null, 1);
            Assert.That(naming.Picker.Selected.Kind, Is.EqualTo(NameKeyKind.Keep));
            Assert.That(naming.Press(), Is.True);
            Assert.That(naming.Chosen, Is.EqualTo("Rosa"));
            Assert.That(naming.Press(), Is.False, "nothing more once named");
        }

        [Test]
        public void TwoSuggestionsNeverAnotherPlayersOrYourOwn()
        {
            var naming = new ChefNaming("Mike", Pool, new[] { "Tyler" }, 3);
            var suggested = naming.Picker.Rows[1].Select(k => k.Text).ToList();
            Assert.That(suggested, Has.Count.EqualTo(2));
            Assert.That(suggested, Has.None.EqualTo("Mike").And.None.EqualTo("Tyler"));
            naming.Roll();
            Assert.That(naming.Picker.Rows[1], Has.Count.EqualTo(2));
        }

        [Test]
        public void ANewPlayerTypesTheirOwn()
        {
            var naming = new ChefNaming("", Pool, null, 5);
            Assert.That(naming.Picker.Rows[0], Is.Empty, "no name to keep");
            // Down to the letters, then A types the first letter, "A".
            while (naming.Picker.Selected.Kind != NameKeyKind.Letter)
                naming.Move(0, 1);
            var version = naming.Version;
            Assert.That(naming.Press(), Is.True);
            Assert.That(naming.Picker.Text, Is.Not.Empty);
            Assert.That(naming.Version, Is.GreaterThan(version), "the card can pop");
            Assert.That(naming.Delete(), Is.True);
            Assert.That(naming.Picker.Text, Is.Empty);
        }

        [Test]
        public void NamesFitTheCard()
        {
            var naming = new ChefNaming("", Pool, null, 5);
            for (var i = 0; i < 20; i++)
                naming.Picker.Type("a");
            Assert.That(naming.Picker.Text.Length, Is.EqualTo(ChefNaming.MaxLength));
        }

        [Test]
        public void TheProfileKeepsEachSlotsName()
        {
            var profile = new ChefProfile();
            Assert.That(profile.Name(2), Is.Empty);
            profile.SetName(2, "  Big   Al ");
            Assert.That(profile.Name(2), Is.EqualTo("Big Al"));
            var old = new ChefProfile { Names = null };
            Assert.That(old.Upgrade(), Is.True);
            Assert.That(old.Name(0), Is.Empty, "a file from before names reads fine");
        }

        [TestCase(DayPhase.Plan, true)]
        [TestCase(DayPhase.Prep, true)]
        [TestCase(DayPhase.Service, false)]
        [TestCase(DayPhase.Review, false)]
        public void KitchensTakeNewPlayersBeforeService(DayPhase phase, bool allowed)
        {
            Assert.That(JoinRules.Allows(phase), Is.EqualTo(allowed));
            if (!allowed)
                Assert.That(JoinRules.WaitMessage(phase), Is.Not.Empty);
        }
    }
}
