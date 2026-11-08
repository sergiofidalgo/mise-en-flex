using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Which sides of a counter meet another counter, so their tops can join into one continuous
    /// surface (the user's note, 2026-11-08). Sides are in the counter's own axes, whichever way
    /// it's turned: <see cref="PlusX"/> is its right, <see cref="PlusZ"/> its forward.
    /// </summary>
    public static class CounterJoins
    {
        public const int PlusX = 1;
        public const int MinusX = 2;
        public const int PlusZ = 4;
        public const int MinusZ = 8;

        // How far past the counter's edge to look for a neighbour: snapped counters touch exactly,
        // and a little slack covers ones placed by hand.
        private const float Probe = 0.06f;

        /// <summary>The sides of <paramref name="counter"/> that another counter is right up against, as bits.</summary>
        public static int Joined(Footprint counter, float halfSize, IEnumerable<Footprint> others)
        {
            var mask = 0;
            var half = counter.HalfSize(halfSize);
            foreach (var other in others)
            {
                mask |= Side(PlusX, counter.Right, half.X);
                mask |= Side(MinusX, -counter.Right, half.X);
                mask |= Side(PlusZ, counter.Forward, half.Y);
                mask |= Side(MinusZ, -counter.Forward, half.Y);

                int Side(int bit, Vector2 direction, float reach) =>
                    other.Contains(counter.Centre + direction * (reach + Probe), halfSize) ? bit : 0;
            }
            return mask;
        }
    }
}
