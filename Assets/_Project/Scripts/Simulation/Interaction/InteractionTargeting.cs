using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Simulation.Movement;

namespace Game.Simulation.Interaction
{
    public readonly struct TargetingSettings
    {
        public TargetingSettings(float range, float halfAngleDegrees, float stickiness)
        {
            Range = range;
            HalfAngleDegrees = halfAngleDegrees;
            Stickiness = stickiness;
        }

        /// <summary>Metres from the player to a station's centre.</summary>
        public float Range { get; }

        /// <summary>How far off the facing direction a station can be and still be targeted.</summary>
        public float HalfAngleDegrees { get; }

        /// <summary>Score bonus for the current target, so the highlight doesn't flicker between near-ties.</summary>
        public float Stickiness { get; }
    }

    /// <summary>
    /// Picks what the player's Interact button acts on. Forgiving on purpose: anything in a
    /// wide cone in front counts, and the best mix of close and centred wins.
    /// </summary>
    public static class InteractionTargeting
    {
        // Within this distance the direction to a candidate is meaningless, so treat it as dead ahead.
        private const float OverlapDistance = 1e-3f;

        /// <param name="origin">Player position on the floor plane (X right, Y forward).</param>
        /// <param name="facingDegrees">Player yaw, same convention as <see cref="PlanarMover"/>.</param>
        /// <param name="candidates">Station positions on the floor plane.</param>
        /// <param name="currentIndex">Index of the current target, or -1.</param>
        /// <returns>Index of the best candidate, or -1 if none is in reach.</returns>
        public static int SelectBest(
            Vector2 origin,
            float facingDegrees,
            IReadOnlyList<Vector2> candidates,
            TargetingSettings settings,
            int currentIndex = -1)
        {
            var bestIndex = -1;
            var bestScore = float.MaxValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var offset = candidates[i] - origin;
                var distance = offset.Length();
                if (distance > settings.Range)
                    continue;

                var angle = 0f;
                if (distance > OverlapDistance)
                {
                    var yaw = (float)(Math.Atan2(offset.X, offset.Y) * 180.0 / Math.PI);
                    angle = Math.Abs(PlanarMover.DeltaDegrees(facingDegrees, yaw));
                }

                if (angle > settings.HalfAngleDegrees)
                    continue;

                // Lower is better. Distance and angle are each normalised to 0..1 by their limits
                // and weighted equally, so a station slightly further away but straight ahead
                // beats one right beside the player's shoulder.
                var score = distance / settings.Range + angle / settings.HalfAngleDegrees;
                if (i == currentIndex)
                    score -= settings.Stickiness;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}
