using System;
using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class WindowLeanTests
    {
        // The office's side walls (OfficeBuilder): glass at x = ±17.5.
        private const float West = -17.5f;
        private const float East = 17.5f;
        private const float Zone = 2f;
        private const float Slack = 0.5f;
        private const float Tilt = 55f;
        private const float Fov = 45f;
        private const float Aspect = 16f / 9f;

        private static int Side(int previous, params Vector2[] points) => WindowLean.Side(points, West, East, Zone, Slack, previous);

        [Test]
        public void APlayerByTheWestGlassLeansWest()
        {
            Assert.That(Side(0, new Vector2(-16f, 0f)), Is.EqualTo(-1));
            Assert.That(Side(0, new Vector2(16f, 0f)), Is.EqualTo(1));
            Assert.That(Side(0, new Vector2(0f, 0f)), Is.EqualTo(0));
        }

        [Test]
        public void StandingOnTheLineDoesntFlicker()
        {
            // 2.2 m from the glass: not yet in, if it wasn't leaning; still in, if it was.
            var onTheLine = new Vector2(West + Zone + 0.2f, 0f);
            Assert.That(Side(0, onTheLine), Is.EqualTo(0));
            Assert.That(Side(-1, onTheLine), Is.EqualTo(-1));
            Assert.That(Side(-1, new Vector2(West + Zone + Slack + 0.1f, 0f)), Is.EqualTo(0));
        }

        [Test]
        public void OppositeWindowsCancelOut()
        {
            Assert.That(Side(0, new Vector2(-16f, 0f), new Vector2(16.5f, 15f)), Is.EqualTo(0));
            Assert.That(Side(-1, new Vector2(-16f, 0f), new Vector2(16.5f, 15f)), Is.EqualTo(0));
        }

        [Test]
        public void TwoAtTheSameSideLeanOnce()
        {
            var camera = new Vector3(-10f, 12f, -5f);
            var points = new[] { new Vector2(-16f, 3f), new Vector2(-16.5f, 4f) };
            var leaned = WindowLean.Apply(camera, Side(0, points), 4f, points, Tilt, Fov, Aspect, 1f);

            Assert.That(leaned.X, Is.EqualTo(-14f).Within(0.001f));
        }

        [Test]
        public void ItSlidesAcrossWithoutZoomingOrMovingAlong()
        {
            var camera = new Vector3(-8f, 11f, -4f);
            var points = new[] { new Vector2(-16f, 4f) };
            var leaned = WindowLean.Apply(camera, -1, 4f, points, Tilt, Fov, Aspect, 1f);

            Assert.That(leaned.X, Is.EqualTo(-12f).Within(0.001f));
            Assert.That(leaned.Y, Is.EqualTo(camera.Y));
            Assert.That(leaned.Z, Is.EqualTo(camera.Z));
        }

        [Test]
        public void ALeanNeverPushesAnyoneOutOfView()
        {
            // One by the west glass, one well across the room: the full lean would lose the second.
            var points = new[] { new Vector2(-16f, 4f), new Vector2(-1f, 4f) };
            var camera = CameraFraming.Fit(points, Tilt, Fov, Aspect, 3f, 10f, 55f);
            var leaned = WindowLean.Apply(camera, -1, 4f, points, Tilt, Fov, Aspect, 1f);

            Assert.That(leaned.X, Is.LessThan(camera.X), "still leans some way");
            Assert.That(leaned.X, Is.GreaterThan(camera.X - 4f), "but not all of it");
            var kept = CameraFraming.KeepInView(points, leaned, Tilt, Fov, Aspect, 1f, 55f);
            Assert.That(Math.Abs(kept.X - leaned.X), Is.LessThan(0.001f));
        }

        [Test]
        public void NoLeanLeavesTheCameraAlone()
        {
            var camera = new Vector3(3f, 12f, -5f);
            Assert.That(WindowLean.Apply(camera, 0, 4f, new[] { new Vector2(0f, 0f) }, Tilt, Fov, Aspect, 1f), Is.EqualTo(camera));
        }
    }
}
