using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Timing for small presentation motions that follow an on/off state, like a fridge door or a
    /// chef bending: an amount that moves between 0 and 1 at its own speed each way, and easings
    /// that turn it into motion that feels physical rather than mechanical.
    /// </summary>
    public static class Swing
    {
        /// <summary>Moves <paramref name="amount"/> towards 1 when on, or 0 when off, over the given seconds.</summary>
        public static float Step(float amount, bool on, float onSeconds, float offSeconds, float deltaTime)
        {
            var seconds = Mathf.Max(on ? onSeconds : offSeconds, 0.0001f);
            return Mathf.MoveTowards(amount, on ? 1f : 0f, deltaTime / seconds);
        }

        /// <summary>
        /// How far something can turn, up to <paramref name="max"/> degrees, before it's
        /// <paramref name="blocked"/>, checked every <paramref name="step"/> degrees on the way, since
        /// it sweeps through every angle short of where it stops. 0 if the first step is blocked.
        /// </summary>
        public static float FreeAngle(float max, float step, System.Func<float, bool> blocked)
        {
            step = Mathf.Max(step, 0.1f);
            var free = 0f;
            while (free < max)
            {
                var next = Mathf.Min(free + step, max);
                if (blocked(next))
                    return free;
                free = next;
            }
            return max;
        }

        /// <summary>
        /// Which way a door's free edge points, on the floor of the thing it's in (x right, y
        /// forward), turned <paramref name="angle"/> degrees from shut. Shut, it runs across the
        /// front; opening, its free edge swings out to the front (−y).
        /// </summary>
        public static Vector2 DoorDirection(float angle, bool hingedRight)
        {
            var radians = angle * Mathf.Deg2Rad;
            return new Vector2((hingedRight ? -1f : 1f) * Mathf.Cos(radians), -Mathf.Sin(radians));
        }

        /// <summary>
        /// True if a door of <paramref name="length"/> swinging open to <paramref name="maxAngle"/>
        /// passes within <paramref name="radius"/> of <paramref name="centre"/> (someone standing there).
        /// </summary>
        public static bool SweepTouches(Vector2 hinge, float length, bool hingedRight, float maxAngle, float step, Vector2 centre, float radius)
        {
            step = Mathf.Max(step, 0.1f);
            for (var angle = 0f; ; angle = Mathf.Min(angle + step, maxAngle))
            {
                var end = hinge + DoorDirection(angle, hingedRight) * length;
                if (DistanceToSegment(centre, hinge, end) < radius)
                    return true;
                if (angle >= maxAngle)
                    return false;
            }
        }

        /// <summary>
        /// Where someone at <paramref name="from"/> steps to, clear of a door's swing: just past the
        /// edge away from the hinge, as far forward as they were (and at least clear of the front).
        /// </summary>
        public static Vector2 StepAsideSpot(Vector2 hinge, bool hingedRight, Vector2 from, float radius) =>
            new Vector2((hingedRight ? -1f : 1f) * (Mathf.Abs(hinge.x) + radius), Mathf.Min(from.y, hinge.y - radius));

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }

        /// <summary>
        /// Ease-out with a small overshoot: fast at first, swinging slightly past 1 before settling,
        /// like a door thrown open. 0 and 1 map exactly to themselves.
        /// </summary>
        public static float EaseOutBack(float t, float overshoot = 1.2f)
        {
            t = Mathf.Clamp01(t);
            var c3 = overshoot + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }
    }
}
