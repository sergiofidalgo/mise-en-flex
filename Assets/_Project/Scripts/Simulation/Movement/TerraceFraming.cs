using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// The office's terrace shot (user's request, 2026-11-10): while anyone's out on the terrace the
    /// camera pulls back and holds a still view of the whole terrace and the rooftops past its rail,
    /// so the city can be enjoyed, and closes in again when they're all back inside.
    /// </summary>
    public static class TerraceFraming
    {
        /// <summary>
        /// Whether a point counts as on the terrace. A doorway band of <paramref name="slack"/> metres
        /// either side of its edge keeps the answer it had, so someone standing in the door doesn't
        /// flip the camera back and forth: stepping out takes going that far past the edge, and
        /// coming back in takes going that far inside.
        /// </summary>
        public static bool OnTerrace(Vector2 point, Vector2 terraceMin, Vector2 terraceMax, bool wasOn, float slack) =>
            CameraFraming.Inside(point, terraceMin, terraceMax, wasOn ? slack : -slack);

        /// <summary>Whether anyone counts as on the terrace, with the same doorway band for the group.</summary>
        public static bool AnyoneOnTerrace(IReadOnlyList<Vector2> points, Vector2 terraceMin, Vector2 terraceMax, bool wasOn, float slack)
        {
            foreach (var point in points)
                if (OnTerrace(point, terraceMin, terraceMax, wasOn, slack))
                    return true;
            return false;
        }

        /// <summary>
        /// The floor the terrace shot shows: the terrace, and <paramref name="beyondRail"/> metres
        /// past its far rail, over the roofs. The far rail is the high end of the second coordinate.
        /// </summary>
        public static (Vector2 Min, Vector2 Max) Vista(Vector2 terraceMin, Vector2 terraceMax, float beyondRail) =>
            (terraceMin, new Vector2(terraceMax.X, terraceMax.Y + beyondRail));

        /// <summary>
        /// Where the camera holds the terrace shot. Those on the terrace are in it already, so they
        /// don't count, and the shot stays still while they walk around; anyone still inside counts,
        /// so the shot grows to take them in, up to <paramref name="maxHeight"/>. It isn't kept on the
        /// building: the point is to look past it.
        /// </summary>
        public static Vector3 Fit(
            IReadOnlyList<Vector2> points, Vector2 terraceMin, Vector2 terraceMax, float beyondRail, float slack,
            float tiltDegrees, float fieldOfViewDegrees, float aspect, float margin, float minHeight, float maxHeight, float screenTop = 0f)
        {
            var inside = new List<Vector2>();
            foreach (var point in points)
                if (!CameraFraming.Inside(point, terraceMin, terraceMax, slack))
                    inside.Add(point);
            var (min, max) = Vista(terraceMin, terraceMax, beyondRail);
            return CameraFraming.Fit(inside, tiltDegrees, fieldOfViewDegrees, aspect, margin, minHeight, maxHeight,
                screenTop: screenTop, areaMin: min, areaMax: max);
        }
    }
}
