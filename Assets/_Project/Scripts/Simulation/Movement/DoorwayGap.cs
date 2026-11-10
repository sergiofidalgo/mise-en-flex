using System;
using System.Collections.Generic;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// The opening in an outer wall where the street door stands (Phase 5, milestone 3): people walk
    /// out through it to the street's stalls. The door moves along the walls in management mode, so
    /// the wall is split around it again wherever it goes.
    /// </summary>
    public static class DoorwayGap
    {
        /// <summary>
        /// What's left of a wall running from <paramref name="from"/> to <paramref name="to"/> along its
        /// length, with a gap <paramref name="width"/> wide centred at <paramref name="at"/>. Pieces
        /// shorter than <paramref name="shortest"/> are left out, so a door by a corner leaves no sliver.
        /// </summary>
        public static List<(float From, float To)> Split(float from, float to, float at, float width, float shortest = 0.05f)
        {
            var pieces = new List<(float, float)>();
            var low = Math.Min(from, to);
            var high = Math.Max(from, to);
            var gapLow = at - width / 2f;
            var gapHigh = at + width / 2f;
            if (gapHigh <= low || gapLow >= high)
            {
                pieces.Add((low, high));
                return pieces;
            }
            if (gapLow - low >= shortest)
                pieces.Add((low, gapLow));
            if (high - gapHigh >= shortest)
                pieces.Add((gapHigh, high));
            return pieces;
        }
    }
}
