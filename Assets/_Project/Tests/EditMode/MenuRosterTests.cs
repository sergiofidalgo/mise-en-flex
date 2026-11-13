using Game.Gameplay.Office;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Joining in the main menu (the user's calls, 2026-11-13).</summary>
    public class MenuRosterTests
    {
        [Test]
        public void TheFirstToPressIsPlayerOneAndDrives()
        {
            var roster = new MenuRoster(4);
            Assert.That(roster.HasDriver, Is.False);
            Assert.That(roster.Press(7, joiningOpen: false), Is.EqualTo(0), "even before joining opens");
            Assert.That(roster.Driver, Is.EqualTo(7));
            Assert.That(roster.Press(7, true), Is.EqualTo(-1), "player 1's own presses are the menu's");
        }

        [Test]
        public void PlayerOneCanBeClaimedByAnyButton()
        {
            var roster = new MenuRoster(4);
            Assert.That(roster.Claim(3), Is.True);
            Assert.That(roster.Claim(5), Is.False);
            Assert.That(roster.Driver, Is.EqualTo(3));
        }

        [Test]
        public void OthersJoinInOrderWithA()
        {
            var roster = new MenuRoster(4);
            roster.Press(1, true);
            Assert.That(roster.Press(9, true), Is.EqualTo(1));
            Assert.That(roster.Press(4, true), Is.EqualTo(2));
            Assert.That(roster.Press(9, true), Is.EqualTo(-1), "a second press does nothing");
            Assert.That(roster.SlotOf(4), Is.EqualTo(2));
            Assert.That(roster.Driver, Is.EqualTo(1), "joining never takes the menu");
        }

        [Test]
        public void NobodyJoinsBeforeTheMenu()
        {
            var roster = new MenuRoster(4);
            roster.Press(1, false);
            Assert.That(roster.Press(2, false), Is.EqualTo(-1));
            Assert.That(roster.Count, Is.EqualTo(1));
        }

        [Test]
        public void NoMoreThanTheMaximum()
        {
            var roster = new MenuRoster(2);
            roster.Press(1, true);
            roster.Press(2, true);
            Assert.That(roster.Press(3, true), Is.EqualTo(-1));
            Assert.That(roster.Count, Is.EqualTo(2));
        }

        [Test]
        public void ALostControllerLeavesItsSlot()
        {
            var roster = new MenuRoster(4);
            roster.Press(1, true);
            roster.Press(2, true);
            roster.Press(3, true);
            Assert.That(roster.Remove(2), Is.True);
            Assert.That(roster.SlotOf(3), Is.EqualTo(1));
        }
    }
}
