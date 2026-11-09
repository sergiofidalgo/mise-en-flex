using Game.Simulation.Office;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ElevatorDoorsTests
    {
        private static readonly ElevatorSettings Settings = new ElevatorSettings(ringToOpen: 0.3f, opening: 0.5f, holdOpen: 1f, closing: 0.5f);

        private int _rings;
        private ElevatorDoors _doors;

        [SetUp]
        public void Create()
        {
            _rings = 0;
            _doors = new ElevatorDoors(Settings);
            _doors.Rang += () => _rings++;
        }

        private void Run(float seconds, float step = 0.05f)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += step)
                _doors.Tick(step);
        }

        [Test]
        public void StartsClosed()
        {
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Closed));
            Assert.That(_doors.Openness, Is.EqualTo(0f));
        }

        [Test]
        public void AJoinRingsAtOnceThenOpens()
        {
            _doors.Arrive();
            Assert.That(_rings, Is.EqualTo(1));
            Assert.That(_doors.Openness, Is.EqualTo(0f), "the bell leads the doors");

            Run(0.3f + 0.5f + 0.01f);
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Open));
            Assert.That(_doors.Openness, Is.EqualTo(1f));
        }

        [Test]
        public void ClosesAfterTheHoldOnceClear()
        {
            _doors.Arrive();
            Run(0.3f + 0.5f + 1f + 0.5f + 0.1f);
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Closed));
            Assert.That(_doors.Openness, Is.EqualTo(0f));
        }

        [Test]
        public void StaysOpenWhileSomeoneIsInside()
        {
            _doors.Arrive();
            _doors.Occupied = true;
            Run(10f);
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Open));
        }

        [Test]
        public void SomeoneInTheDoorwayReopensClosingDoors()
        {
            _doors.Arrive();
            Run(0.3f + 0.5f + 1f + 0.2f);
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Closing));

            _doors.Occupied = true;
            _doors.Tick(0.05f);
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Opening));
        }

        [Test]
        public void AJoinWhileOpenRingsAgainAndRestartsTheHold()
        {
            _doors.Arrive();
            Run(0.3f + 0.5f + 0.9f); // open, the hold nearly over
            _doors.Arrive();
            Assert.That(_rings, Is.EqualTo(2));

            Run(0.5f);
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Open), "the second chef gets the full hold");
        }

        [Test]
        public void AJoinWhileClosingRingsAndReopens()
        {
            _doors.Arrive();
            Run(0.3f + 0.5f + 1f + 0.2f);
            var closingFrom = _doors.Openness;

            _doors.Arrive();
            Assert.That(_rings, Is.EqualTo(2));
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Opening));
            _doors.Tick(0.05f);
            Assert.That(_doors.Openness, Is.GreaterThan(closingFrom), "reopens from where it was, no snap");
        }

        [Test]
        public void AJoinWhileArrivingRingsButDoesNotDelayTheDoors()
        {
            _doors.Arrive();
            Run(0.2f);
            _doors.Arrive();
            Run(0.15f);
            Assert.That(_rings, Is.EqualTo(2));
            Assert.That(_doors.State, Is.EqualTo(ElevatorDoors.DoorState.Opening));
        }
    }
}
