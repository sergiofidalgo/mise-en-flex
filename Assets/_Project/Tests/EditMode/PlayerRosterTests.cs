using Game.Gameplay;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode
{
    public class PlayerRosterTests
    {
        private Gamepad _first;
        private Gamepad _second;

        [SetUp]
        public void AddPads()
        {
            _first = InputSystem.AddDevice<Gamepad>();
            _second = InputSystem.AddDevice<Gamepad>();
            PlayerRoster.Take(); // start empty
        }

        [TearDown]
        public void RemovePads()
        {
            if (_first.added)
                InputSystem.RemoveDevice(_first);
            if (_second.added)
                InputSystem.RemoveDevice(_second);
            PlayerRoster.Take();
        }

        [Test]
        public void JoinsAgainInTheOldOrder()
        {
            PlayerRoster.Carry(new[]
            {
                new PlayerRoster.Seat(1, "Gamepad", new InputDevice[] { _second }),
                new PlayerRoster.Seat(0, "Gamepad", new InputDevice[] { _first }),
            });
            var seats = PlayerRoster.Take();
            Assert.That(seats.Count, Is.EqualTo(2));
            Assert.That(seats[0].Devices[0], Is.SameAs(_first));
            Assert.That(seats[1].Devices[0], Is.SameAs(_second));
        }

        [Test]
        public void IsTakenOnce()
        {
            PlayerRoster.Carry(new[] { new PlayerRoster.Seat(0, "Gamepad", new InputDevice[] { _first }) });
            PlayerRoster.Take();
            Assert.That(PlayerRoster.Take(), Is.Empty, "a later scene doesn't join them twice");
        }

        [Test]
        public void LeavesOutAControllerThatWentAway()
        {
            PlayerRoster.Carry(new[]
            {
                new PlayerRoster.Seat(0, "Gamepad", new InputDevice[] { _first }),
                new PlayerRoster.Seat(1, "Gamepad", new InputDevice[] { _second }),
            });
            InputSystem.RemoveDevice(_second);
            var seats = PlayerRoster.Take();
            Assert.That(seats.Count, Is.EqualTo(1));
            Assert.That(seats[0].Devices[0], Is.SameAs(_first));
        }
    }
}
