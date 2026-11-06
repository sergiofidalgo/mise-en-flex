using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Where a camera at a fixed tilt has to be to keep some points on the floor in view: as close
    /// as it can get, so it zooms out as they spread apart and in as they gather.
    /// </summary>
    public static class CameraFraming
    {
        // Enough halvings to place the camera to a fraction of a millimetre.
        private const int SearchSteps = 40;

        /// <param name="points">What has to stay in view, on the floor.</param>
        /// <param name="tiltDegrees">How far the camera looks down from the horizon.</param>
        /// <param name="fieldOfViewDegrees">The camera's vertical field of view.</param>
        /// <param name="aspect">Screen width over height.</param>
        /// <param name="margin">Metres of floor to keep around the points, and at the sides.</param>
        /// <param name="extraFar">Metres to add to the margin beyond the farthest point; less than zero for less.</param>
        /// <param name="nearMargin">Metres of floor to keep before the nearest point, if not <paramref name="margin"/>.</param>
        /// <param name="screenTop">Share of the screen's height to leave clear at the top, for what stands up from the floor and the prompts above it.</param>
        /// <param name="screenBottom">The same at the bottom.</param>
        /// <returns>The camera's position: X across, Y up, Z along the floor.</returns>
        public static Vector3 Fit(
            IReadOnlyList<Vector2> points, float tiltDegrees, float fieldOfViewDegrees, float aspect, float margin, float minHeight, float maxHeight,
            float extraFar = 0f, float? nearMargin = null, float screenTop = 0f, float screenBottom = 0f)
        {
            var near = nearMargin ?? margin;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X - margin);
                maxX = Math.Max(maxX, point.X + margin);
                minZ = Math.Min(minZ, point.Y - near);
                maxZ = Math.Max(maxZ, point.Y + margin + extraFar);
            }

            // Screen heights run from -1 at the bottom to 1 at the top. A height s is a ray leaving
            // the camera at atan(s × tan(half the view)) from where it looks.
            var tanHalfView = (float)Math.Tan(Radians(fieldOfViewDegrees) / 2f);
            var top = 1f - 2f * screenTop;
            var bottom = -1f + 2f * screenBottom;
            var tilt = Radians(tiltDegrees);
            var topAngle = tilt - (float)Math.Atan(top * tanHalfView);
            var bottomAngle = tilt - (float)Math.Atan(bottom * tanHalfView);

            // At height H, a ray at an angle below the horizon meets the floor H × cot(angle) ahead.
            // So the floor between the two rays grows in proportion to H.
            var heightForDepth = (maxZ - minZ) / (Cot(topAngle) - Cot(bottomAngle));

            // Across: perspective makes the bottom of that band the narrowest, nearest part of the
            // floor. Fit the width there, so it fits everywhere.
            var depthPerHeight = (float)(Math.Cos(bottomAngle - tilt) / Math.Sin(bottomAngle));
            var heightForWidth = (maxX - minX) / 2f / (depthPerHeight * tanHalfView * aspect);

            var height = Math.Min(maxHeight, Math.Max(minHeight, Math.Max(heightForDepth, heightForWidth)));
            var cameraZ = CentredAlongTheFloor(minZ, maxZ, height, tilt, tanHalfView, top, bottom);
            return new Vector3((minX + maxX) / 2f, height, cameraZ);
        }

        // With height to spare, the spare room is shared equally on screen. It can't be shared in
        // metres of floor: near floor fills far more of the screen than far floor, which would leave
        // a wide empty band at the bottom and none at the top. Moving the camera forward slides
        // everything steadily down the screen, so the balance point can be homed in on.
        private static float CentredAlongTheFloor(float minZ, float maxZ, float height, float tilt, float tanHalfView, float top, float bottom)
        {
            var behind = minZ - height * 20f;
            var ahead = minZ;
            for (var i = 0; i < SearchSteps; i++)
            {
                var camera = (behind + ahead) / 2f;
                var roomAtTop = top - ScreenHeightOf(maxZ, camera, height, tilt, tanHalfView);
                var roomAtBottom = ScreenHeightOf(minZ, camera, height, tilt, tanHalfView) - bottom;
                if (roomAtTop < roomAtBottom)
                    behind = camera;
                else
                    ahead = camera;
            }
            return (behind + ahead) / 2f;
        }

        private static float ScreenHeightOf(float z, float cameraZ, float height, float tilt, float tanHalfView)
        {
            var below = Math.Atan2(height, z - cameraZ);
            return (float)Math.Tan(tilt - below) / tanHalfView;
        }

        private static float Cot(float radians) => 1f / (float)Math.Tan(radians);

        private static float Radians(float degrees) => degrees * (float)Math.PI / 180f;
    }
}
