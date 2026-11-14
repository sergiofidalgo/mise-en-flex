using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// The office camera leans towards a side window (user's request, 2026-11-14): with a player
    /// next to the west or east glass, or the terrace's side rail, the shot slides a few metres that
    /// way, past the building's wall, so more of the city shows on that side. The lean is all or
    /// nothing, so it doesn't creep as they edge closer to the glass. Players at opposite windows
    /// cancel out, and a lean never pushes anyone out of view.
    /// </summary>
    public static class WindowLean
    {
        /// <summary>
        /// Which way to lean: −1 west (towards low X), 1 east, 0 not at all. A player counts as by a
        /// window within <paramref name="zone"/> metres of it; a band of <paramref name="slack"/>
        /// metres keeps the answer it had, so standing on the line doesn't flicker it. Players by
        /// windows on both sides cancel out.
        /// </summary>
        public static int Side(IReadOnlyList<Vector2> points, float westWall, float eastWall, float zone, float slack, int previous)
        {
            var west = false;
            var east = false;
            foreach (var point in points)
            {
                west |= point.X <= westWall + zone + (previous < 0 ? slack : -slack);
                east |= point.X >= eastWall - zone - (previous > 0 ? slack : -slack);
            }
            if (west == east)
                return 0;
            return west ? -1 : 1;
        }

        /// <summary>
        /// The camera leaned <paramref name="distance"/> metres to one side, without changing its
        /// height, and then brought back only as far as keeps every player in view with
        /// <paramref name="keepMargin"/> metres of floor round them.
        /// </summary>
        public static Vector3 Apply(
            Vector3 camera, int side, float distance, IReadOnlyList<Vector2> points, float tiltDegrees, float fieldOfViewDegrees, float aspect,
            float keepMargin, float screenTop = 0f)
        {
            if (side == 0 || distance <= 0f)
                return camera;
            var leaned = new Vector3(camera.X + side * distance, camera.Y, camera.Z);
            var kept = CameraFraming.KeepInView(points, leaned, tiltDegrees, fieldOfViewDegrees, aspect, keepMargin, camera.Y, screenTop);
            // Only across: the shot keeps its height and its place along the floor.
            return new Vector3(kept.X, camera.Y, camera.Z);
        }
    }
}
