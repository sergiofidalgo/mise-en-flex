using Game.Simulation.Movement;
using NUnit.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Counters side by side join their tops into one surface (the user's note, 2026-11-08).
    /// </summary>
    public class CounterJoinsTests
    {
        private static int Joined(Footprint counter, params Footprint[] others) => CounterJoins.Joined(counter, 0.5f, others);

        private static Footprint At(float x, float z, float degrees = 0f) => new Footprint(new Vector2(x, z), degrees);

        [Test]
        public void ACounterOnItsOwnJoinsNothing() => Assert.That(Joined(At(0f, 0f)), Is.EqualTo(0));

        [Test]
        public void TheMiddleOfARunJoinsBothSides() =>
            Assert.That(Joined(At(0f, 0f), At(-1f, 0f), At(1f, 0f)), Is.EqualTo(CounterJoins.PlusX | CounterJoins.MinusX));

        [Test]
        public void TheEndOfARunJoinsOneSide() =>
            Assert.That(Joined(At(0f, 0f), At(1f, 0f)), Is.EqualTo(CounterJoins.PlusX));

        [Test]
        public void ACounterBehindJoinsAtTheBack() =>
            Assert.That(Joined(At(0f, 0f), At(0f, 1f)), Is.EqualTo(CounterJoins.PlusZ));

        [Test]
        public void ACounterAGapAwayDoesNotJoin() =>
            Assert.That(Joined(At(0f, 0f), At(1.5f, 0f)), Is.EqualTo(0));

        [Test]
        public void SidesAreTheCountersOwnWhenItIsTurned()
        {
            // Turned a quarter, its right faces the world's -z: a counter there meets its right side.
            Assert.That(Joined(At(0f, 0f, 90f), At(0f, -1f)), Is.EqualTo(CounterJoins.PlusX));
        }

        [Test]
        public void ADiagonalNeighbourDoesNotJoin() =>
            Assert.That(Joined(At(0f, 0f), At(1f, 1f)), Is.EqualTo(0));
    }
}
