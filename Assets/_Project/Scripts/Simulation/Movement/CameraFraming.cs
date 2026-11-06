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
        /// <param name="points">What has to stay in view, on the floor.</param>
        /// <param name="tiltDegrees">How far the camera looks down from the horizon.</param>
        /// <param name="fieldOfViewDegrees">The camera's vertical field of view.</param>
        /// <param name="aspect">Screen width over height.</param>
        /// <param name="margin">Metres of floor to keep around the points.</param>
        /// <param name="extraFar">More metres beyond the farthest point: what stands up from the floor
        /// reaches towards the top of the screen, and that's where a chef's view runs out first.</param>
        /// <returns>The camera's position: X across, Y up, Z along the floor.</returns>
        public static Vector3 Fit(IReadOnlyList<Vector2> points, float tiltDegrees, float fieldOfViewDegrees, float aspect, float margin, float minHeight, float maxHeight, float extraFar = 0f)
        {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X - margin);
                maxX = Math.Max(maxX, point.X + margin);
                minZ = Math.Min(minZ, point.Y - margin);
                maxZ = Math.Max(maxZ, point.Y + margin + extraFar);
            }

            // The top and bottom edges of the view are rays leaving the camera at fixed angles below
            // the horizon, and at height H they meet the floor H × cot(angle) ahead of it. So the
            // floor the view covers from front to back grows in proportion to H.
            var halfView = Radians(fieldOfViewDegrees) / 2f;
            var topAngle = Radians(tiltDegrees) - halfView;
            var bottomAngle = Radians(tiltDegrees) + halfView;
            var cotTop = 1f / (float)Math.Tan(topAngle);
            var cotBottom = 1f / (float)Math.Tan(bottomAngle);
            var heightForDepth = (maxZ - minZ) / (cotTop - cotBottom);

            // Across: perspective makes the bottom of the view the narrowest, nearest part of the
            // floor. Fit the width there, so it fits everywhere.
            var depthPerHeight = (float)(Math.Cos(halfView) / Math.Sin(bottomAngle));
            var widthPerHeight = depthPerHeight * (float)Math.Tan(halfView) * aspect;
            var heightForWidth = (maxX - minX) / 2f / widthPerHeight;

            var height = Math.Min(maxHeight, Math.Max(minHeight, Math.Max(heightForDepth, heightForWidth)));

            // Centred: the floor the view covers is halfway between those two rays.
            var cameraZ = (minZ + maxZ) / 2f - height * (cotTop + cotBottom) / 2f;
            return new Vector3((minX + maxX) / 2f, height, cameraZ);
        }

        private static float Radians(float degrees) => degrees * (float)Math.PI / 180f;
    }
}
