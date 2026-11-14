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

        // The camera eases its height on its own, then asks where to be at the height it has reached.
        [Test]
        public void AGivenHeightIsKeptAndTheFloorPositionSuitsIt()
        {
            var chef = new[] { new Vector2(1f, -2f) };
            var low = CameraFraming.Fit(chef, Tilt, Fov, Aspect, 1f, 7f, 7f);
            var high = CameraFraming.Fit(chef, Tilt, Fov, Aspect, 1f, 9f, 9f);

            Assert.That(low.Y, Is.EqualTo(7f));
            Assert.That(high.Y, Is.EqualTo(9f));
            Assert.That(high.Z, Is.LessThan(low.Z), "further up, it stands further back to keep looking at them");
            AssertInView(low, chef[0]);
            AssertInView(high, chef[0]);
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
        public void ACameraThatShowsEveryoneIsLeftWhereItIs()
        {
            var points = new[] { new Vector2(0f, 0f), new Vector2(1f, 1f) };
            var camera = Fit(1f, points) + new Vector3(0.1f, 2f, -0.1f);
            var kept = CameraFraming.KeepInView(points, camera, Tilt, Fov, Aspect, 0.5f, 40f);

            Assert.That(kept, Is.EqualTo(camera));
        }

        [Test]
        public void PlayersSplittingFasterThanItZoomsPushItUpAtOnce()
        {
            var points = new[] { new Vector2(-9f, 1f), new Vector2(9f, -6f) };
            var tooLow = new Vector3(0f, 6.5f, -8f);
            var kept = CameraFraming.KeepInView(points, tooLow, Tilt, Fov, Aspect, 0.5f, 40f, 0.1f, 0.02f);

            Assert.That(kept.Y, Is.GreaterThan(tooLow.Y));
            foreach (var point in points)
                AssertInView(kept, point);
        }

        [Test]
        public void SomeoneRunningPastAnEdgeDragsItAlongWithoutZooming()
        {
            var runner = new[] { new Vector2(12f, 9f) };
            var behind = new Vector3(0f, 8f, -6f);
            var kept = CameraFraming.KeepInView(runner, behind, Tilt, Fov, Aspect, 0.5f, 40f, 0.1f, 0.02f);

            Assert.That(kept.Y, Is.EqualTo(8f));
            AssertInView(kept, runner[0]);
            Assert.That(ScreenHeight(kept, runner[0]), Is.InRange(0.5f, 0.8f), "only as far as needed: they're near the edge it leaves clear");
        }

        private static readonly Vector2 KitchenMin = new Vector2(-3.4f, -2.4f);
        private static readonly Vector2 KitchenMax = new Vector2(3.4f, 1.4f);
        private static readonly Vector2 RoomMin = new Vector2(-3.6f, -6.6f);
        private static readonly Vector2 RoomMax = new Vector2(3.6f, 1.6f);

        private static readonly Vector2 Walls = new Vector2(0.5f);
        private static (Vector2 Min, Vector2 Max) Shot => CameraFraming.KitchenShot(KitchenMin, KitchenMax, 2f, 0.5f, 0.5f, RoomMin - Walls, RoomMax + Walls);

        // As the camera does: the shot as it is, plus anyone outside it, with the usual margins.
        private static Vector3 KitchenFraming(params Vector2[] people)
        {
            var shot = Shot;
            var outside = new System.Collections.Generic.List<Vector2>();
            foreach (var person in people)
                if (!CameraFraming.Inside(person, shot.Min, shot.Max))
                    outside.Add(person);
            return CameraFraming.Fit(outside, Tilt, Fov, Aspect, 2f, 6.5f, 40f, -0.5f, 0.6f, 0.27f, 0.06f, RoomMin - Walls, RoomMax + Walls, shot.Min, shot.Max);
        }

        [Test]
        public void TheKitchenShotTakesInItsCustomersAndStaysInsideTheBuilding()
        {
            var shot = Shot;

            Assert.That(shot.Min.Y, Is.EqualTo(-4.4f).Within(0.001f), "past the bar seats at the wall");
            Assert.That(shot.Min.X, Is.EqualTo(-3.9f).Within(0.001f), "the side wall");
            Assert.That(shot.Max.Y, Is.EqualTo(1.9f).Within(0.001f), "the back wall");
        }

        [Test]
        public void TheKitchenShotShowsAllOfItsArea()
        {
            var camera = KitchenFraming();
            var shot = Shot;
            foreach (var corner in new[] { shot.Min, shot.Max, new Vector2(shot.Min.X, shot.Max.Y), new Vector2(shot.Max.X, shot.Min.Y) })
                AssertInView(camera, corner);
        }

        // The point of the shot: chefs moving about the kitchen, even pressed into its corners, don't
        // move the camera at all.
        [Test]
        public void WalkingAroundTheKitchenDoesntMoveIt()
        {
            var still = KitchenFraming();
            foreach (var chef in new[] { new Vector2(0f, 0f), new Vector2(-3.3f, 1.3f), new Vector2(3.3f, -2.5f), new Vector2(-3.3f, -2.4f) })
                Assert.That(KitchenFraming(chef, new Vector2(1f, 1f)), Is.EqualTo(still), $"a chef at {chef}");
        }

        [Test]
        public void LeavingTheKitchenGrowsTheShotAndKeepsTheKitchen()
        {
            var still = KitchenFraming();
            var diner = new Vector2(2f, -6f);
            var out_ = KitchenFraming(new Vector2(0f, 0f), diner);

            Assert.That(out_.Y, Is.GreaterThan(still.Y));
            AssertInView(out_, diner);
            AssertInView(out_, new Vector2(0f, KitchenMax.Y));
        }

        [Test]
        public void SomeoneLeavingShowsTheWholeRestaurantWhenItsNotMuchBigger()
        {
            Assert.That(CameraFraming.ShowWhole(8f, 10.5f, 40f, 1.4f), Is.True);
            Assert.That(CameraFraming.ShowWhole(8f, 20f, 40f, 1.4f), Is.False, "much bigger: grow the kitchen's shot instead");
            Assert.That(CameraFraming.ShowWhole(30f, 40f, 40f, 1.4f), Is.False, "never when it doesn't all fit");
        }

        [Test]
        public void SomeoneOutrunningItHurriesItAlongItsArcInsteadOfOffIt()
        {
            var shot = Shot;
            var from = KitchenFraming();
            var to = CameraFraming.Fit(new[] { RoomMin, RoomMax }, Tilt, Fov, Aspect, 2f, 6.5f, 40f, -0.5f, 0.6f, 0.14f, 0.06f, RoomMin - Walls, RoomMax + Walls);
            var walkingOut = new[] { new Vector2(-3f, -5.6f) };
            var hurried = CameraFraming.HurryAlong(walkingOut, from, to, Tilt, Fov, Aspect, 0.6f, 40f, 0.1f, 0.02f);

            AssertInView(hurried, walkingOut[0]);
            var along = Vector3.Dot(hurried - from, to - from) / (to - from).LengthSquared();
            Assert.That(along, Is.InRange(0.01f, 1f), "partway along");
            Assert.That(Vector3.Distance(hurried, Vector3.Lerp(from, to, along)), Is.LessThan(0.001f), "on the line, not off it");
        }

        [Test]
        public void ACameraThatShowsEveryoneIsntHurried()
        {
            var from = KitchenFraming();
            var to = from + new Vector3(0f, 3f, -2f);
            Assert.That(CameraFraming.HurryAlong(new[] { new Vector2(0f, 0f) }, from, to, Tilt, Fov, Aspect, 0.6f, 40f, 0.1f, 0.02f), Is.EqualTo(from));
        }

        // The first restaurant's street (GreyboxStreet): the building's outer walls, the street in front
        // of it with the plaza, up to the kerb, and all the grounds, road and far pavement included.
        private static readonly Vector2 OuterWallsMin = new Vector2(-4.1f, -7.1f);
        private static readonly Vector2 OuterWallsMax = new Vector2(4.1f, 2.1f);
        private static readonly Vector2 StreetMin = new Vector2(-14f, -10.3f);
        private static readonly Vector2 StreetMax = new Vector2(4.1f, 3f);
        private static readonly Vector2 GroundsMin = new Vector2(-14f, -19.4f);
        private static readonly Vector2 GroundsMax = new Vector2(10f, 3f);
        private const float Kerb = -10.3f;
        private const float OutBy = 0.3f;

        // As FramingCamera asks it: the road counts from the kerb itself.
        private static CameraFraming.Outside Where(CameraFraming.Outside was, params Vector2[] people) =>
            CameraFraming.WhereOutside(people, was, OuterWallsMin, OuterWallsMax, Kerb + OutBy, OutBy, OutBy / 2f);

        private static Vector3 StillShot(Vector2 min, Vector2 max) =>
            CameraFraming.StillShot(min, max, 0.6f, Tilt, Fov, Aspect, 6.5f, 40f, 0.14f, 0.06f);

        [Test]
        public void LeavingTheBuildingCountsOnceClearOfTheDoorwayAndComingBackFromHalfThat()
        {
            var no = CameraFraming.Outside.No;
            var street = CameraFraming.Outside.Street;
            Assert.That(Where(no, new Vector2(0f, -7.3f)), Is.EqualTo(no), "in the doorway");
            Assert.That(Where(no, new Vector2(0f, -7.5f)), Is.EqualTo(street));
            Assert.That(Where(street, new Vector2(0f, -7.3f)), Is.EqualTo(street), "on the way back, the doorway's still out");
            Assert.That(Where(street, new Vector2(0f, -7.2f)), Is.EqualTo(no));
            Assert.That(Where(no, new Vector2(-8f, -2f)), Is.EqualTo(street), "the plaza, at the side");
        }

        [Test]
        public void SteppingOffTheKerbIsTheRoadAndBackOnThePavementFromALittleUp()
        {
            var street = CameraFraming.Outside.Street;
            var road = CameraFraming.Outside.Road;
            Assert.That(Where(street, new Vector2(0f, -10.2f)), Is.EqualTo(street));
            Assert.That(Where(street, new Vector2(0f, -10.35f)), Is.EqualTo(road), "just off the kerb");
            Assert.That(Where(road, new Vector2(0f, -10.2f)), Is.EqualTo(road), "standing on the kerb on the way back");
            Assert.That(Where(road, new Vector2(0f, -10.1f)), Is.EqualTo(street));
            Assert.That(Where(street, new Vector2(0f, -18.5f)), Is.EqualTo(road), "the far pavement is the road's shot too");
        }

        [Test]
        public void TheFurthestOutDecidesForEveryone()
        {
            var inTheKitchen = new Vector2(0f, 0f);
            Assert.That(Where(CameraFraming.Outside.No, inTheKitchen, new Vector2(-8f, -5f)), Is.EqualTo(CameraFraming.Outside.Street));
            Assert.That(Where(CameraFraming.Outside.No, inTheKitchen, new Vector2(-8f, -5f), new Vector2(2f, -13f)), Is.EqualTo(CameraFraming.Outside.Road));
            Assert.That(Where(CameraFraming.Outside.Road, inTheKitchen, new Vector2(1f, -5f)), Is.EqualTo(CameraFraming.Outside.No));
        }

        // The bug (2026-11-14): one chef a metre onto the road made it frame them alone, dropping from
        // about 15 m to 6.5 m. Now each outside shot is the same wherever anyone is in it, and shows
        // everyone there, all at once, without the hard edge limit having to move it.
        [Test]
        public void TheOutsideShotsHoldStillWithEveryoneInThemOnScreen()
        {
            foreach (var (min, max, lowestZ) in new[] { (StreetMin, StreetMax, Kerb), (GroundsMin, GroundsMax, GroundsMin.Y) })
            {
                var shot = StillShot(min, max);
                var corners = new[] { new Vector2(min.X, lowestZ), new Vector2(max.X, lowestZ), new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), new Vector2((min.X + max.X) / 2f, (lowestZ + max.Y) / 2f) };
                Assert.That(CameraFraming.KeepInView(corners, shot, Tilt, Fov, Aspect, 0.6f, 40f, 0.1f, 0.02f), Is.EqualTo(shot), $"everyone in {min}..{max} at once");
                foreach (var corner in corners)
                    Assert.That(CameraFraming.KeepInView(new[] { corner }, shot, Tilt, Fov, Aspect, 0.6f, 40f, 0.1f, 0.02f), Is.EqualTo(shot), $"{corner} alone");
            }
        }

        [Test]
        public void TheRoadIsOneStepFurtherOutThanTheStreetWhichIsOneStepOutFromTheDiningRoom()
        {
            var dining = CameraFraming.Fit(new[] { RoomMin, RoomMax }, Tilt, Fov, Aspect, 2f, 6.5f, 40f, -1.1f, 0.6f, 0.14f, 0.06f, RoomMin - Walls, RoomMax + Walls);
            var street = StillShot(StreetMin, StreetMax);
            var road = StillShot(GroundsMin, GroundsMax);
            TestContext.WriteLine($"dining room {dining.Y:F2} m, street {street.Y:F2} m, road {road.Y:F2} m");
            Assert.That(street.Y, Is.GreaterThan(dining.Y));
            Assert.That(road.Y, Is.GreaterThan(street.Y + 3f));
            Assert.That(road.Y, Is.LessThan(40f), "the road's shot fits under the top height, so it's never cut");
        }
    }
}
