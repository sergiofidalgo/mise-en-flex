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
        /// <param name="buildingMin">The building's corner, if the view should stay on it: once all of it fits it's
        /// centred on the building rather than on people, and otherwise it doesn't show beyond its walls.</param>
        /// <param name="buildingMax">The opposite corner.</param>
        /// <param name="areaMin">The corner of an area to show as it is, with no margins, as well as the points.</param>
        /// <param name="areaMax">Its opposite corner.</param>
        /// <returns>The camera's position: X across, Y up, Z along the floor.</returns>
        public static Vector3 Fit(
            IReadOnlyList<Vector2> points, float tiltDegrees, float fieldOfViewDegrees, float aspect, float margin, float minHeight, float maxHeight,
            float extraFar = 0f, float? nearMargin = null, float screenTop = 0f, float screenBottom = 0f, Vector2? buildingMin = null, Vector2? buildingMax = null,
            Vector2? areaMin = null, Vector2? areaMax = null)
        {
            var near = nearMargin ?? margin;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            if (areaMin.HasValue && areaMax.HasValue)
            {
                minX = areaMin.Value.X;
                minZ = areaMin.Value.Y;
                maxX = areaMax.Value.X;
                maxZ = areaMax.Value.Y;
            }
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
            var widthPerHeight = depthPerHeight * tanHalfView * aspect;
            var heightForWidth = (maxX - minX) / 2f / widthPerHeight;

            var height = Math.Min(maxHeight, Math.Max(minHeight, Math.Max(heightForDepth, heightForWidth)));
            var cameraX = (minX + maxX) / 2f;
            var cameraZ = CentredAlongTheFloor(minZ, maxZ, height, tilt, tanHalfView, top, bottom);
            if (buildingMin.HasValue && buildingMax.HasValue)
            {
                var low = buildingMin.Value;
                var high = buildingMax.Value;
                cameraX = KeepOnBuilding(cameraX, height * widthPerHeight, low.X, high.X);

                var depth = height * (Cot(topAngle) - Cot(bottomAngle));
                if (depth >= high.Y - low.Y)
                    cameraZ = CentredAlongTheFloor(low.Y, high.Y, height, tilt, tanHalfView, top, bottom);
                else
                    cameraZ = Math.Max(low.Y - height * Cot(bottomAngle), Math.Min(high.Y - height * Cot(topAngle), cameraZ));
            }
            return new Vector3(cameraX, height, cameraZ);
        }

        /// <summary>
        /// The least a camera has to move from where it is to keep every point in view: up, if they've
        /// spread further than it can see, and across or along, if one has gone past an edge. A camera
        /// that already shows them all stays where it is. This is the hard limit under the eased moves,
        /// so nobody walks off screen however fast they split up.
        /// </summary>
        public static Vector3 KeepInView(
            IReadOnlyList<Vector2> points, Vector3 camera, float tiltDegrees, float fieldOfViewDegrees, float aspect, float margin, float maxHeight,
            float screenTop = 0f, float screenBottom = 0f)
        {
            if (points.Count == 0)
                return camera;

            var fit = Fit(points, tiltDegrees, fieldOfViewDegrees, aspect, margin, 0f, maxHeight, 0f, null, screenTop, screenBottom);
            var height = Math.Max(camera.Y, fit.Y);

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X - margin);
                maxX = Math.Max(maxX, point.X + margin);
                minZ = Math.Min(minZ, point.Y - margin);
                maxZ = Math.Max(maxZ, point.Y + margin);
            }

            // The same rays as in Fit. Along the floor, the camera can be anywhere that has the far
            // edge below the top ray and the near edge above the bottom one.
            var tanHalfView = (float)Math.Tan(Radians(fieldOfViewDegrees) / 2f);
            var tilt = Radians(tiltDegrees);
            var topAngle = tilt - (float)Math.Atan((1f - 2f * screenTop) * tanHalfView);
            var bottomAngle = tilt - (float)Math.Atan((-1f + 2f * screenBottom) * tanHalfView);
            var cameraZ = Within(camera.Z, maxZ - height * Cot(topAngle), minZ - height * Cot(bottomAngle));

            // Across, measured where the view is narrowest, as in Fit.
            var halfWidth = height * (float)(Math.Cos(bottomAngle - tilt) / Math.Sin(bottomAngle)) * tanHalfView * aspect;
            var cameraX = Within(camera.X, maxX - halfWidth, minX + halfWidth);
            return new Vector3(cameraX, height, cameraZ);
        }

        // Clamped between two limits; halfway if they cross, which only the top height can cause.
        private static float Within(float value, float low, float high) =>
            low > high ? (low + high) / 2f : Math.Max(low, Math.Min(high, value));

        // Centred if all of it fits across; otherwise as near the wanted place as stays on the building.
        private static float KeepOnBuilding(float wanted, float halfWidth, float low, float high)
        {
            if (halfWidth >= (high - low) / 2f)
                return (low + high) / 2f;
            return Math.Max(low + halfWidth, Math.Min(high - halfWidth, wanted));
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

        /// <summary>
        /// The floor the kitchen's still shot shows, exactly: its working area, a little past it at the
        /// sides and back so the walls and the units on them show, and the floor in front of it where its
        /// customers are served. The front is the side towards the dining room, the low end of the second
        /// coordinate. Kept within the building's outer walls.
        /// </summary>
        public static (Vector2 Min, Vector2 Max) KitchenShot(Vector2 kitchenMin, Vector2 kitchenMax, float inFront, float atSides, float behind, Vector2 buildingMin, Vector2 buildingMax)
        {
            var min = new Vector2(kitchenMin.X - atSides, kitchenMin.Y - inFront);
            var max = new Vector2(kitchenMax.X + atSides, kitchenMax.Y + behind);
            return (Vector2.Clamp(min, buildingMin, buildingMax), Vector2.Clamp(max, buildingMin, buildingMax));
        }

        /// <summary>
        /// How far out of the building anyone is, for the outside's still shots: the street in front of it
        /// (pavement, plaza, parking), or the road beyond the kerb, which needs a shot further out. The
        /// road runs along the building's front, the low end of the second coordinate. Out counts from
        /// <paramref name="outBy"/> past a line, back from <paramref name="backBy"/>, so standing in the
        /// doorway or on the kerb doesn't flick it.
        /// </summary>
        /// <param name="buildingMin">The corner of the building's outer walls.</param>
        /// <param name="buildingMax">The opposite corner.</param>
        /// <param name="kerb">Where the road starts, along the second coordinate.</param>
        /// <param name="was">What it said last time.</param>
        public static Outside WhereOutside(IReadOnlyList<Vector2> people, Outside was, Vector2 buildingMin, Vector2 buildingMax, float kerb, float outBy, float backBy)
        {
            var result = Outside.No;
            foreach (var person in people)
            {
                if (person.Y < kerb - (was == Outside.Road ? backBy : outBy))
                    return Outside.Road;
                if (!Inside(person, buildingMin, buildingMax, was == Outside.No ? outBy : backBy))
                    result = Outside.Street;
            }
            return result;
        }

        /// <summary>
        /// One still shot of a stretch of floor, centred on it, with some floor kept all round so anyone
        /// standing at its edge is clear of the screen's edge. It doesn't depend on where anyone is, so
        /// moving about in it never moves the camera.
        /// </summary>
        public static Vector3 StillShot(
            Vector2 min, Vector2 max, float margin, float tiltDegrees, float fieldOfViewDegrees, float aspect, float minHeight, float maxHeight,
            float screenTop = 0f, float screenBottom = 0f)
        {
            var around = new Vector2(margin, margin);
            return Fit(Array.Empty<Vector2>(), tiltDegrees, fieldOfViewDegrees, aspect, 0f, minHeight, maxHeight, 0f, null, screenTop, screenBottom,
                null, null, min - around, max + around);
        }

        /// <summary>Whether a point is inside a rectangle, or within some slack of it.</summary>
        public static bool Inside(Vector2 point, Vector2 min, Vector2 max, float slack = 0f) =>
            point.X >= min.X - slack && point.X <= max.X + slack && point.Y >= min.Y - slack && point.Y <= max.Y + slack;

        /// <summary>
        /// Where along its way from one place to another a camera has to be, at the least, to keep every
        /// point in view: so when someone outruns it, it hurries along the arc it's already on instead
        /// of being pushed off it. Where it is, if they're all in view already; where it's going, if
        /// even that doesn't show them.
        /// </summary>
        public static Vector3 HurryAlong(
            IReadOnlyList<Vector2> points, Vector3 camera, Vector3 goal, float tiltDegrees, float fieldOfViewDegrees, float aspect, float margin, float maxHeight,
            float screenTop = 0f, float screenBottom = 0f)
        {
            bool Shows(Vector3 at) => KeepInView(points, at, tiltDegrees, fieldOfViewDegrees, aspect, margin, maxHeight, screenTop, screenBottom) == at;
            if (Shows(camera) || !Shows(goal))
                return Shows(camera) ? camera : goal;

            var behind = 0f;
            var ahead = 1f;
            for (var i = 0; i < SearchSteps; i++)
            {
                var t = (behind + ahead) / 2f;
                if (Shows(Vector3.Lerp(camera, goal, t)))
                    ahead = t;
                else
                    behind = t;
            }
            return Vector3.Lerp(camera, goal, ahead);
        }

        /// <summary>
        /// Whether, once someone leaves the kitchen, to show the whole restaurant rather than grow the
        /// kitchen's shot after them: when it's not much bigger, so the move out starts at once and goes
        /// to one place instead of creeping. Never when the whole restaurant doesn't fit.
        /// </summary>
        public static bool ShowWhole(float kitchenHeight, float wholeHeight, float maxHeight, float mostTimesKitchen) =>
            wholeHeight < maxHeight && wholeHeight <= kitchenHeight * mostTimesKitchen;

        private static float Cot(float radians) => 1f / (float)Math.Tan(radians);

        /// <summary>Where anyone furthest out of the building is: nowhere, the street, or the road.</summary>
        public enum Outside
        {
            No,
            Street,
            Road,
        }

        private static float Radians(float degrees) => degrees * (float)Math.PI / 180f;
    }
}
