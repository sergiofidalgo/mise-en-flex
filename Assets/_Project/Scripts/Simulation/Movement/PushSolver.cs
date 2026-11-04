using System;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// How two people share a collision (decision 2026-11-04). They can't overlap, so they get moved
    /// apart, and who gives way depends on who's pushing: someone walking into a person standing
    /// still moves them along, slowed by that person's weight, while two pushing against each other
    /// with equal force stay put, because each is pushed back as far as they just walked.
    /// </summary>
    public static class PushSolver
    {
        /// <summary>How far to move each of two people so they no longer overlap.</summary>
        /// <param name="minDistance">How close their centres can be.</param>
        /// <param name="moveA">How far A walked or wants to walk this frame; the same for B.</param>
        /// <param name="resistance">
        /// How much of a push an idle body soaks up: 0 moves them at the pusher's full pace, 0.8 at a
        /// fifth of it, because the pusher gives back that share of each step, and 1 not at all.
        /// </param>
        public static (Vector2 ShiftA, Vector2 ShiftB) Separate(Vector2 a, Vector2 moveA, Vector2 b, Vector2 moveB, float minDistance, float resistance = 0f)
        {
            var between = b - a;
            var distance = between.Length();
            if (distance >= minDistance)
                return (Vector2.Zero, Vector2.Zero);

            // Exactly on top of each other: any direction will do.
            var axis = distance > 1e-5f ? between / distance : Vector2.UnitX;
            var overlap = minDistance - distance;

            var pushA = Math.Max(0f, Vector2.Dot(moveA, axis));
            var pushB = Math.Max(0f, Vector2.Dot(moveB, -axis));
            var total = pushA + pushB;

            // Nobody pushing, or evenly matched: half each. The more one-sided the push, the more the
            // weaker one gives way, down to the resistance they put up when not pushing at all.
            var aGives = 0.5f;
            if (total > 1e-6f)
            {
                var oneSided = (pushA - pushB) / total; // 1 = only A pushes, -1 = only B
                aGives = 0.5f - (0.5f - resistance) * oneSided;
            }
            return (-axis * overlap * aGives, axis * overlap * (1f - aGives));
        }
    }
}
