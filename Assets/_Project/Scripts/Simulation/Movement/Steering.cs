using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Keeps staff from walking into other people (decision 2026-11-04). Each step heads for the
    /// next point on the route, pushed away from anyone close and to the right of anyone ahead, the
    /// way people pass in a corridor: both keeping right means they get past each other instead
    /// of stopping nose to nose.
    /// </summary>
    public static class Steering
    {
        /// <summary>How close two people's centres can be: two walkers' widths.</summary>
        public const float BodyDistance = 0.6f;

        // People start giving way from a little further out than that.
        private const float PersonalSpace = 1.0f;
        private const float PushStrength = 2f;
        private const float PassStrength = 1.5f;

        /// <summary>Where someone at <paramref name="position"/> gets to in one step.</summary>
        /// <param name="others">Everyone else nearby.</param>
        /// <param name="isFree">Whether a point is clear of furniture.</param>
        public static Vector2 Step(Vector2 position, Vector2 waypoint, float step, IReadOnlyList<Vector2> others, Func<Vector2, bool> isFree)
        {
            var toWaypoint = waypoint - position;
            var distance = toWaypoint.Length();
            if (distance < 1e-5f)
                return position;

            var heading = toWaypoint / distance;
            var right = new Vector2(heading.Y, -heading.X);
            var desired = heading;
            foreach (var other in others)
            {
                var away = position - other;
                var gap = away.Length();
                if (gap >= PersonalSpace || gap < 1e-5f)
                    continue;

                away /= gap;
                var closeness = (PersonalSpace - gap) / PersonalSpace;
                desired += away * closeness * PushStrength;
                if (Vector2.Dot(heading, -away) > 0f)
                    desired += right * closeness * PassStrength;
            }

            var length = desired.Length();
            desired = length > 1e-5f ? desired / length : right;

            var move = Math.Min(step, distance);
            foreach (var direction in new[] { desired, right, -right })
            {
                var candidate = position + direction * move;
                if (isFree(candidate) && !Bumps(position, candidate, others))
                    return candidate;
            }
            return position;
        }

        // Moving into someone's body isn't allowed; moving out of it always is.
        private static bool Bumps(Vector2 from, Vector2 to, IReadOnlyList<Vector2> others)
        {
            foreach (var other in others)
            {
                var gap = (to - other).Length();
                if (gap < BodyDistance && gap < (from - other).Length())
                    return true;
            }
            return false;
        }
    }
}
