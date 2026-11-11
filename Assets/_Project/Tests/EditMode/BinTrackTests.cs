using System;
using System.Linq;
using System.Numerics;
using Game.Simulation.Movement;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The wheelie bin's way round the building (decisions 2026-11-11).</summary>
    public class BinTrackTests
    {
        // The grill's building (outer faces) and the bin as GreyboxStreet builds them.
        private static readonly Vector2 Min = new Vector2(-4.1f, -7.1f);
        private static readonly Vector2 Max = new Vector2(4.1f, 2.1f);
        private const float Gap = 0.15f;
        private static readonly Vector2 Size = new Vector2(1.8f, 1.2f);
        private const float Radius = 1f;

        private static BinTrack Track() => new BinTrack(Min, Max, Gap, Size, Radius);

        private static bool InsideBuilding(Vector2 p, float slack = 1e-3f) =>
            p.X > Min.X + slack && p.X < Max.X - slack && p.Y > Min.Y + slack && p.Y < Max.Y - slack;

        [Test]
        public void ItNeverReachesIntoTheBuildingAllTheWayRound()
        {
            var track = Track();
            var corners = new[] { Min, new Vector2(Min.X, Max.Y), new Vector2(Max.X, Min.Y), Max };
            for (var s = 0f; s <= track.Length; s += 0.01f)
            {
                var footprint = track.FootprintAt(s);
                foreach (var corner in footprint.Corners(0f))
                    Assert.IsFalse(InsideBuilding(corner), $"a corner of the bin is in the building at {s:0.00} m");
                foreach (var corner in corners)
                    Assert.IsFalse(footprint.Contains(corner, 0f, -1e-3f), $"the building's corner is in the bin at {s:0.00} m");
                // The building's walls between its corners can't cross the bin either: sample along them.
                for (var t = 0f; t <= 1f; t += 0.02f)
                {
                    foreach (var point in new[]
                             {
                                 Vector2.Lerp(Min, new Vector2(Max.X, Min.Y), t),
                                 Vector2.Lerp(Min, new Vector2(Min.X, Max.Y), t),
                                 Vector2.Lerp(new Vector2(Max.X, Min.Y), Max, t),
                             })
                        Assert.IsFalse(footprint.Contains(point, 0f, -1e-3f), $"a wall runs through the bin at {s:0.00} m");
                }
            }
        }

        [Test]
        public void ItRollsSmoothlyWithNoJumps()
        {
            var track = Track();
            var (last, lastDegrees) = track.At(0f);
            for (var s = 0.01f; s <= track.Length; s += 0.01f)
            {
                var (position, degrees) = track.At(s);
                Assert.Less(Vector2.Distance(position, last), 0.0101f + 1e-4f, $"a jump at {s:0.00} m");
                Assert.Less(Math.Abs(DeltaAngle(degrees, lastDegrees)), 1.5f, $"a sudden turn at {s:0.00} m");
                (last, lastDegrees) = (position, degrees);
            }
        }

        [Test]
        public void OnEachWallItStandsFlushWithItsFrontOut()
        {
            var track = Track();
            var east = track.Snap(new Vector2(Max.X + 3f, -2f));
            var (eastAt, eastDegrees) = track.At(east);
            Assert.AreEqual(Max.X + Gap + Size.Y / 2f, eastAt.X, 1e-4f);
            Assert.AreEqual(-2f, eastAt.Y, 1e-3f);
            Assert.AreEqual(90f, eastDegrees, 1e-3f);

            var (westAt, westDegrees) = track.At(track.Snap(new Vector2(Min.X - 3f, -2f)));
            Assert.AreEqual(Min.X - Gap - Size.Y / 2f, westAt.X, 1e-4f);
            Assert.AreEqual(270f, westDegrees, 1e-3f);

            var (frontAt, frontDegrees) = track.At(track.Snap(new Vector2(1f, Min.Y - 3f)));
            Assert.AreEqual(Min.Y - Gap - Size.Y / 2f, frontAt.Y, 1e-4f);
            Assert.AreEqual(1f, frontAt.X, 1e-3f);
            Assert.AreEqual(180f, frontDegrees, 1e-3f);
        }

        [Test]
        public void ItCanOnlyBePutDownWithItsWholeLengthAgainstAWall()
        {
            var track = Track();
            // Past the front corner, the nearest spot keeps it on the wall rather than round the corner.
            var (cornerAt, _) = track.At(track.Snap(new Vector2(Max.X + 2f, Min.Y - 2f)));
            var footprint = new Footprint(cornerAt, track.At(track.Snap(new Vector2(Max.X + 2f, Min.Y - 2f))).Degrees, track.HalfSize);
            Assert.IsTrue(footprint.Corners(0f).All(c => c.X <= Max.X + Gap + Size.Y + 1e-3f && c.Y >= Min.Y - Gap - Size.Y - 1e-3f));
            Assert.IsTrue(track.IsAgainstAWall(track.Snap(new Vector2(Max.X + 2f, Min.Y - 2f))));

            // Behind the building there's no wall for it: it stops at the side wall's north end.
            var (behind, _) = track.At(track.Snap(new Vector2(Max.X - 1f, Max.Y + 2f)));
            Assert.AreEqual(Max.Y - Size.X / 2f, behind.Y, 1e-3f);

            // Halfway round a corner it's only passing.
            var corner = track.Nearest(new Vector2(Max.X + 0.6f, Min.Y - 0.6f));
            Assert.IsFalse(track.IsAgainstAWall(corner));
        }

        [Test]
        public void ItRollsRoundTheCornersRatherThanThroughTheBuilding()
        {
            var track = Track();
            var east = track.Snap(new Vector2(Max.X + 1f, 0f));
            var west = track.Snap(new Vector2(Min.X - 1f, 0f));
            // From the east wall to the west, the way passes in front of the building.
            for (var s = Math.Min(east, west); s <= Math.Max(east, west); s += 0.05f)
                Assert.IsFalse(InsideBuilding(track.At(s).Position));
            Assert.Greater(Math.Abs(east - west), Max.X - Min.X);
        }

        [Test]
        public void TheDoorwayAndTheGuestsWayInAreKeptClear()
        {
            var track = Track();
            // A door on the east wall, halfway along: it faces in, west (270°).
            var door = new Footprint(new Vector2(Max.X, -2.5f), 270f, new Vector2(0.6f, 0.1f));
            var clear = BinTrack.KeepClear(door, Gap + Size.Y, 0.5f, 0.9f, 0.9f);

            bool Blocked(float s) => clear.Any(zone => track.FootprintAt(s).Overlaps(zone, 0f));

            Assert.IsTrue(Blocked(track.Snap(new Vector2(Max.X + 1f, -2.5f))), "right in front of the door");
            Assert.IsTrue(Blocked(track.Snap(new Vector2(Max.X + 1f, -5.5f))), "between the door and the street, where guests walk");
            Assert.IsFalse(Blocked(track.Snap(new Vector2(Max.X + 1f, 0.5f))), "further up the same wall");
            Assert.IsFalse(Blocked(track.Snap(new Vector2(3f, Min.Y - 1f))), "on the front wall, round the corner");
            Assert.IsFalse(Blocked(track.Snap(new Vector2(Min.X - 1f, -2.5f))), "on the far wall");
        }

        [Test]
        public void ADoorInTheFrontWallKeepsItsDoorwayClearButNotTheWholeWall()
        {
            var track = Track();
            var door = new Footprint(new Vector2(0f, Min.Y), 0f, new Vector2(0.6f, 0.1f));
            var clear = BinTrack.KeepClear(door, Gap + Size.Y, 0.5f, 0.9f, 0.9f);

            bool Blocked(float s) => clear.Any(zone => track.FootprintAt(s).Overlaps(zone, 0f));

            Assert.IsTrue(Blocked(track.Snap(new Vector2(0.5f, Min.Y - 1f))));
            Assert.IsTrue(Blocked(track.Snap(new Vector2(-1.8f, Min.Y - 1f))), "its end would be in the doorway");
            Assert.IsFalse(Blocked(track.Snap(new Vector2(2.2f, Min.Y - 1f))), "beside the doorway, by the door (it'll smell)");
            Assert.IsFalse(Blocked(track.Snap(new Vector2(Max.X + 1f, -5f))));
        }

        [Test]
        public void ItRollsPastTheSignPoleAtTheFrontCorner()
        {
            // GreyboxStreet's sign pole: 1.5 m east and 1.7 m south of the building's corner, 0.14 m thick.
            var track = Track();
            var pole = new Vector2(Max.X + 1.5f, Min.Y - 1.7f);
            for (var s = 0f; s <= track.Length; s += 0.01f)
                Assert.IsFalse(track.FootprintAt(s).Contains(pole, 0f, 0.07f), $"the bin hits the sign pole at {s:0.00} m");
        }

        private static float DeltaAngle(float a, float b)
        {
            var d = (a - b) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
}
