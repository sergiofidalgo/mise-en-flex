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
    /// poses (Catmull-Rom, so it passes through each one), eased to start and stop gently. Pure
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
            CameraPose At(int k) => poses[Mathf.Clamp(k, 0, poses.Length - 1)];
            return new CameraPose(
                CatmullRom(At(i - 1).Position, At(i).Position, At(i + 1).Position, At(i + 2).Position, u),
                CatmullRom(At(i - 1).Target, At(i).Target, At(i + 1).Target, At(i + 2).Target, u));
        }

        // Through p1 at u = 0 and p2 at u = 1, its tangents set by the neighbours.
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            var u2 = u * u;
            var u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }
    }
}
