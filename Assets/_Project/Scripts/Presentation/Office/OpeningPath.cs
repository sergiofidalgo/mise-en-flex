using System;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>One stop on the opening's camera path: where the camera is and what it looks at.</summary>
    [Serializable]
    public struct CameraPose
    {
        public Vector3 Position;
        public Vector3 Target;

        public CameraPose(Vector3 position, Vector3 target)
        {
            Position = position;
            Target = target;
        }

        public Quaternion Rotation => Quaternion.LookRotation((Target - Position).sqrMagnitude > 1e-6f ? Target - Position : Vector3.forward);
    }

    /// <summary>
    /// The maths of the opening's camera moves (docs/opening-flow.md): a smooth curve through the
    /// poses (centripetal Catmull-Rom, so it passes through each one), eased to start and stop gently. Pure
    /// functions, tested in EditMode.
    /// </summary>
    public static class OpeningPath
    {
        /// <summary>Slow to start, quick in the middle, slow to stop.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The pose at <paramref name="t"/> (0–1) along the curve through <paramref name="poses"/>.</summary>
        public static CameraPose Sample(CameraPose[] poses, float t)
        {
            if (poses == null || poses.Length == 0)
                return default;
            if (poses.Length == 1)
                return poses[0];
            t = Mathf.Clamp01(t);
            var segments = poses.Length - 1;
            var scaled = t * segments;
            var i = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);
            var u = scaled - i;
            Vector3 Position(int k) => Point(poses, k, pose => pose.Position);
            Vector3 Target(int k) => Point(poses, k, pose => pose.Target);
            return new CameraPose(
                CatmullRom(Position(i - 1), Position(i), Position(i + 1), Position(i + 2), u),
                CatmullRom(Target(i - 1), Target(i), Target(i + 1), Target(i + 2), u));
        }

        // A pose's point, with a phantom one beyond each end (continuing the first and last
        // stretches), so the curve leaves the first pose and reaches the last one heading straight.
        private static Vector3 Point(CameraPose[] poses, int k, System.Func<CameraPose, Vector3> of)
        {
            var last = poses.Length - 1;
            if (k < 0)
                return 2f * of(poses[0]) - of(poses[1]);
            if (k > last)
                return 2f * of(poses[last]) - of(poses[last - 1]);
            return of(poses[k]);
        }

        /// <summary>The length of the walk through <paramref name="points"/>, in straight lines.</summary>
        public static float Length(Vector3[] points)
        {
            var length = 0f;
            for (var i = 1; points != null && i < points.Length; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        /// <summary>
        /// Where someone walking <paramref name="distance"/> metres along <paramref name="points"/>
        /// is, and which way they're heading (the opening's chef on his way to the lift). Clamped to
        /// the ends.
        /// </summary>
        public static Vector3 PointAlong(Vector3[] points, float distance, out Vector3 heading)
        {
            heading = Vector3.forward;
            if (points == null || points.Length == 0)
                return Vector3.zero;
            if (points.Length == 1)
                return points[0];
            distance = Mathf.Max(0f, distance);
            for (var i = 1; i < points.Length; i++)
            {
                var leg = points[i] - points[i - 1];
                var length = leg.magnitude;
                if (length > 1e-5f)
                    heading = leg / length;
                if (distance <= length || i == points.Length - 1)
                    return points[i - 1] + heading * Mathf.Min(distance, length);
                distance -= length;
            }
            return points[points.Length - 1];
        }

        /// <summary>
        /// As <see cref="Sample"/>, but <paramref name="t"/> is the share of the distance travelled
        /// rather than of the poses passed, so the camera keeps one pace through long and short
        /// stretches alike (a stretch's length is taken as the straight line between its poses).
        /// </summary>
        public static CameraPose SampleEven(CameraPose[] poses, float t)
        {
            if (poses == null || poses.Length < 2)
                return Sample(poses, t);
            var lengths = new float[poses.Length - 1];
            var total = 0f;
            for (var i = 0; i < lengths.Length; i++)
            {
                lengths[i] = Mathf.Max(1e-4f, Vector3.Distance(poses[i].Position, poses[i + 1].Position));
                total += lengths[i];
            }
            var travelled = Mathf.Clamp01(t) * total;
            var segment = 0;
            while (segment < lengths.Length - 1 && travelled > lengths[segment])
            {
                travelled -= lengths[segment];
                segment++;
            }
            var u = Mathf.Clamp01(travelled / lengths[segment]);
            return Sample(poses, (segment + u) / lengths.Length);
        }

        // Through p1 at u = 0 and p2 at u = 1. Centripetal (knots spaced by the square root of each
        // stretch's length, Barry and Goldman's form): unlike the uniform curve it never overshoots
        // or loops where a short stretch follows a long one, so the camera never swings back.
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float Knot(Vector3 a, Vector3 b) => Mathf.Max(1e-4f, Mathf.Sqrt(Vector3.Distance(a, b)));
            var t0 = 0f;
            var t1 = t0 + Knot(p0, p1);
            var t2 = t1 + Knot(p1, p2);
            var t3 = t2 + Knot(p2, p3);
            var t = Mathf.Lerp(t1, t2, u);
            var a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            var a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            var a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            var b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            var b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }
    }
}
