using System;
using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class CameraFramingTests
    {
        private const float Tilt = 55f;
        private const float Fov = 45f;
        private const float Aspect = 16f / 9f;

        private static Vector3 Fit(float margin, params Vector2[] points) =>
            CameraFraming.Fit(points, Tilt, Fov, Aspect, margin, 1f, 200f);

        // Projects a floor point the way a camera with this tilt does, and checks it's on screen.
        private static void AssertInView(Vector3 camera, Vector2 point)
        {
            var radians = Tilt * (float)Math.PI / 180f;
            var forward = new Vector3(0f, -(float)Math.Sin(radians), (float)Math.Cos(radians));
            var up = new Vector3(0f, (float)Math.Cos(radians), (float)Math.Sin(radians));
            var relative = new Vector3(point.X, 0f, point.Y) - camera;
            var depth = Vector3.Dot(relative, forward);
            var halfHeight = depth * (float)Math.Tan(Fov * Math.PI / 360f);

            Assert.That(Math.Abs(Vector3.Dot(relative, up)), Is.LessThanOrEqualTo(halfHeight + 0.001f), $"{point} up and down");
            Assert.That(Math.Abs(relative.X), Is.LessThanOrEqualTo(halfHeight * Aspect + 0.001f), $"{point} across");
        }

        [Test]
        public void EveryoneStaysInView()
        {
            var points = new[] { new Vector2(-4f, 3f), new Vector2(4f, -7f), new Vector2(-4f, -7f), new Vector2(4f, 3f), new Vector2(0f, 0f) };
            var camera = Fit(0f, points);
            foreach (var point in points)
                AssertInView(camera, point);
        }

        [Test]
        public void AWideRoomStaysInViewAcross()
        {
            var points = new[] { new Vector2(-30f, 0f), new Vector2(30f, 0f) };
            var camera = Fit(0f, points);
            foreach (var point in points)
                AssertInView(camera, point);
        }

        [Test]
        public void ItBacksAwayAsPeopleSpreadApart()
        {
            var close = Fit(1f, new Vector2(0f, 0f), new Vector2(1f, 0f));
            var apart = Fit(1f, new Vector2(-6f, 0f), new Vector2(6f, 0f));
            var alongTheRoom = Fit(1f, new Vector2(0f, -6f), new Vector2(0f, 6f));

            Assert.That(apart.Y, Is.GreaterThan(close.Y));
            Assert.That(alongTheRoom.Y, Is.GreaterThan(close.Y));
        }

        [Test]
        public void ItCentresOnThem()
        {
            Assert.That(Fit(0f, new Vector2(6f, 0f), new Vector2(10f, 0f)).X, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void TheMarginKeepsFloorAroundThem()
        {
            var camera = Fit(2f, new Vector2(-5f, -3f), new Vector2(5f, 3f));
            AssertInView(camera, new Vector2(-7f, -5f));
            AssertInView(camera, new Vector2(7f, 5f));
        }

        [Test]
        public void ItStaysWithinItsLimits()
        {
            var one = new[] { Vector2.Zero };
            Assert.That(CameraFraming.Fit(one, Tilt, Fov, Aspect, 0f, 8f, 40f).Y, Is.EqualTo(8f));
            var far = new[] { new Vector2(-500f, 0f), new Vector2(500f, 0f) };
            Assert.That(CameraFraming.Fit(far, Tilt, Fov, Aspect, 0f, 8f, 40f).Y, Is.EqualTo(40f));
        }

        [Test]
        public void ItCanKeepMoreRoomAtTheFarSide()
        {
            var points = new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f) };
            var camera = CameraFraming.Fit(points, Tilt, Fov, Aspect, 2f, 1f, 200f, 2f);
            AssertInView(camera, new Vector2(0f, 4f)); // the margin and the extra
            AssertInView(camera, new Vector2(0f, -2f));
            Assert.That(camera.Y, Is.GreaterThan(Fit(2f, points).Y));
        }

        // Where a floor point lands on screen: -1 at the bottom, 1 at the top.
        private static float ScreenHeight(Vector3 camera, Vector2 point)
        {
            var radians = Tilt * (float)Math.PI / 180f;
            var forward = new Vector3(0f, -(float)Math.Sin(radians), (float)Math.Cos(radians));
            var up = new Vector3(0f, (float)Math.Cos(radians), (float)Math.Sin(radians));
            var relative = new Vector3(point.X, 0f, point.Y) - camera;
            return Vector3.Dot(relative, up) / (Vector3.Dot(relative, forward) * (float)Math.Tan(Fov * Math.PI / 360f));
        }

        [Test]
        public void ItLeavesTheShareOfTheScreenItsAskedTo()
        {
            var points = new[] { new Vector2(0f, 3f), new Vector2(3f, -7.5f) };
            var camera = CameraFraming.Fit(points, Tilt, Fov, Aspect, 0f, 1f, 200f, 0f, 0f, 0.2f, 0.05f);

            Assert.That(ScreenHeight(camera, points[0]), Is.EqualTo(0.6f).Within(0.01f), "a fifth of the screen clear at the top");
            Assert.That(ScreenHeight(camera, points[1]), Is.EqualTo(-0.9f).Within(0.01f), "a twentieth at the bottom");
        }

        [Test]
        public void SpareRoomIsSharedEquallyOnScreen()
        {
            // Well above what they need: near floor mustn't get all the empty space.
            var points = new[] { new Vector2(0f, 1f), new Vector2(0f, -1f) };
            var camera = CameraFraming.Fit(points, Tilt, Fov, Aspect, 0f, 20f, 200f);

            Assert.That(ScreenHeight(camera, points[0]), Is.EqualTo(-ScreenHeight(camera, points[1])).Within(0.01f));
        }

        private static readonly Vector2 BuildingMin = new Vector2(-4f, -7f);
        private static readonly Vector2 BuildingMax = new Vector2(4f, 2f);

        private static Vector3 OnBuilding(float minHeight, params Vector2[] points) =>
            CameraFraming.Fit(points, Tilt, Fov, Aspect, 2f, minHeight, 200f, 0f, 0.5f, 0.2f, 0.02f, BuildingMin, BuildingMax);

        [Test]
        public void WhenAllOfTheBuildingFitsItStaysCentredOnIt()
        {
            var inACorner = OnBuilding(15f, new Vector2(3f, 1f));
            var inTheOther = OnBuilding(15f, new Vector2(-3f, -6f));

            Assert.That(inACorner.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(inTheOther.Z, Is.EqualTo(inACorner.Z).Within(0.001f), "it doesn't follow anyone");
            Assert.That(ScreenHeight(inACorner, new Vector2(0f, 2f)), Is.LessThanOrEqualTo(0.6f + 0.01f));
        }

        [Test]
        public void WhenItDoesntFitItStaysOnTheBuilding()
        {
            var camera = OnBuilding(6f, new Vector2(3.5f, 1f));
            AssertInView(camera, new Vector2(3.5f, 1f));
            Assert.That(ScreenHeight(camera, new Vector2(0f, 2f)), Is.GreaterThanOrEqualTo(0.59f), "no further than the north wall");
        }

        [Test]
        public void SomeoneOutsideTheBuildingsWidthDoesntPullItOffCentre()
        {
            var camera = OnBuilding(15f, new Vector2(3.9f, 0f), new Vector2(2f, -6f));
            Assert.That(camera.X, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ItEasesBackAsSomeoneWalksAwayFromTheKitchenUpToALimit()
        {
            Assert.That(CameraFraming.ClosestHeight(0f, 6.5f, 0.5f, 2f), Is.EqualTo(6.5f));
            Assert.That(CameraFraming.ClosestHeight(2f, 6.5f, 0.5f, 2f), Is.EqualTo(7.5f));
            Assert.That(CameraFraming.ClosestHeight(20f, 6.5f, 0.5f, 2f), Is.EqualTo(8.5f));
        }

        [Test]
        public void DistanceOutsideARectangle()
        {
            var min = new Vector2(-1f, -1f);
            var max = new Vector2(1f, 1f);
            Assert.That(CameraFraming.DistanceOutside(Vector2.Zero, min, max), Is.EqualTo(0f));
            Assert.That(CameraFraming.DistanceOutside(new Vector2(0f, -4f), min, max), Is.EqualTo(3f));
        }
    }
}
