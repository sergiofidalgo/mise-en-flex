using System.Collections.Generic;
using System.Numerics;
using Game.Simulation.Street;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The people walking past the restaurant, its lamps and its extractor (2026-11-11).</summary>
    public class StreetWalkersTests
    {
        private const float Step = 0.02f;
        private static readonly Vector2[] Nobody = new Vector2[0];

        private static WalkerSettings Settings() => new WalkerSettings
        {
            West = -10f,
            East = 10f,
            Lanes = new[] { 0f, -1.5f },
            SpeedMin = 1.5f,
            SpeedMax = 1.5f,
            AwayMin = 2f,
            AwayMax = 2f,
            MinZ = -2f,
            MaxZ = 1f,
        };

        private static StreetWalker Walker(WalkerSettings settings, int lane = 0)
        {
            var walker = new StreetWalker(lane, settings, new System.Random(3));
            walker.StartAway(0f);
            walker.Tick(Step, Nobody, true);
            return walker;
        }

        [Test]
        public void TheyWalkTheirLaneFromOneEndToTheOtherAndComeRoundAgain()
        {
            var settings = Settings();
            var east = Walker(settings, 0);
            var west = Walker(settings, 1);
            Assert.That(east.Position.X, Is.EqualTo(-10f).Within(0.1f));
            Assert.That(west.Position.X, Is.EqualTo(10f).Within(0.1f));

            for (var t = 0f; t < 6f; t += Step)
            {
                east.Tick(Step, Nobody, true);
                west.Tick(Step, Nobody, true);
            }
            Assert.That(east.Position.X, Is.EqualTo(-1f).Within(0.2f));
            Assert.That(east.Position.Y, Is.EqualTo(0f), "on its lane");
            Assert.That(east.Facing, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(west.Position.X, Is.EqualTo(1f).Within(0.2f));
            Assert.That(west.Facing, Is.EqualTo(new Vector2(-1f, 0f)));

            // 20 m at 1.5 m/s is about 13.3 s; then 2 s away; then in again as someone new.
            for (var t = 6f; t < 14f; t += Step)
                east.Tick(Step, Nobody, true);
            Assert.That(east.Visible, Is.False);
            Assert.That(east.Arrivals, Is.EqualTo(1));
            for (var t = 0f; t < 2.5f; t += Step)
                east.Tick(Step, Nobody, true);
            Assert.That(east.Visible, Is.True);
            Assert.That(east.Arrivals, Is.EqualTo(2));
            Assert.That(east.Position.X, Is.LessThan(-8f));
        }

        [Test]
        public void TheyStepAsideForSomeoneInTheirLaneAndNeverStop()
        {
            var settings = Settings();
            var walker = Walker(settings);
            var chef = new[] { new Vector2(0f, 0f) };

            var nearest = float.MaxValue;
            var lastX = walker.Position.X;
            for (var t = 0f; t < 10f; t += Step)
            {
                walker.Tick(Step, chef, true);
                nearest = System.Math.Min(nearest, Vector2.Distance(walker.Position, chef[0]));
                Assert.That(walker.Position.X, Is.GreaterThan(lastX), "keeps walking");
                Assert.That(walker.Position.Y, Is.InRange(settings.MinZ, settings.MaxZ));
                lastX = walker.Position.X;
            }
            Assert.That(nearest, Is.GreaterThan(0.6f), "clear of the chef, body to body");
            Assert.That(walker.Position.X, Is.GreaterThan(3f), "past them");
            Assert.That(walker.Position.Y, Is.EqualTo(0f).Within(0.01f), "back on its lane");
        }

        [Test]
        public void TheyStepAwayFromWhicheverSideSomeoneStands()
        {
            var settings = Settings();
            var above = Walker(settings);
            var below = Walker(settings);
            for (var t = 0f; t < 6.6f; t += Step)
            {
                above.Tick(Step, new[] { new Vector2(0f, 0.4f) }, true);
                below.Tick(Step, new[] { new Vector2(0f, -0.4f) }, true);
            }
            Assert.That(above.Position.Y, Is.LessThan(-0.2f));
            Assert.That(below.Position.Y, Is.GreaterThan(0.2f));
        }

        [Test]
        public void TheyNeverStepOffThePavement()
        {
            var settings = Settings();
            settings.MinZ = -0.1f;
            settings.MaxZ = 0.2f;
            var walker = Walker(settings);
            for (var t = 0f; t < 10f; t += Step)
            {
                walker.Tick(Step, new[] { new Vector2(0f, 0.1f) }, true);
                Assert.That(walker.Position.Y, Is.InRange(-0.1f - 1e-4f, 0.2f + 1e-4f));
            }
        }

        [Test]
        public void SomeoneStopsAtTheMenuBoardReadsItAndWalksOn()
        {
            var settings = Settings();
            settings.HasBoard = true;
            settings.Board = new Vector2(2f, 1f);
            settings.BoardSpot = new Vector2(2f, 0.3f);
            settings.BoardLane = 0;
            settings.ReadChance = 1f;
            settings.ReadMin = settings.ReadMax = 4f;
            var walker = Walker(settings);

            // 12 m to the board at 1.5 m/s: 8 s.
            for (var t = 0f; t < 9f; t += Step)
                walker.Tick(Step, Nobody, true);
            Assert.That(walker.State, Is.EqualTo(StreetWalker.Phase.Reading));
            Assert.That(Vector2.Distance(walker.Position, settings.BoardSpot), Is.LessThan(0.02f));
            Assert.That(walker.Facing.Y, Is.GreaterThan(0.9f), "facing the board");

            for (var t = 0f; t < 4f; t += Step)
                walker.Tick(Step, Nobody, true);
            Assert.That(walker.State, Is.EqualTo(StreetWalker.Phase.Walking));
            Assert.That(walker.Position.X, Is.GreaterThan(2.5f));
        }

        [Test]
        public void OnlyTheBoardsLaneStopsAndOnlyWhileItsFree()
        {
            var settings = Settings();
            settings.HasBoard = true;
            settings.Board = new Vector2(2f, 1f);
            settings.BoardSpot = new Vector2(2f, 0.3f);
            settings.BoardLane = 0;
            settings.ReadChance = 1f;
            var otherLane = Walker(settings, 1);
            var busy = Walker(settings);
            for (var t = 0f; t < 13f; t += Step)
            {
                otherLane.Tick(Step, Nobody, true);
                busy.Tick(Step, Nobody, false);
                Assert.That(otherLane.State, Is.Not.EqualTo(StreetWalker.Phase.Reading));
                Assert.That(busy.State, Is.Not.EqualTo(StreetWalker.Phase.Reading));
            }
        }

        [Test]
        public void TheStreetHasOneReaderAtATimeAndIsTheSameForTheSameSeed()
        {
            var settings = Settings();
            settings.HasBoard = true;
            settings.Board = new Vector2(2f, 1f);
            settings.BoardSpot = new Vector2(2f, 0.3f);
            settings.ReadChance = 1f;
            settings.SpeedMin = 1f;
            settings.SpeedMax = 1.7f;
            var street = new StreetWalkers(8, settings, seed: 11);
            var twin = new StreetWalkers(8, settings, seed: 11);
            var everRead = false;
            for (var t = 0f; t < 120f; t += Step)
            {
                street.Tick(Step, Nobody);
                twin.Tick(Step, Nobody);
                var readers = 0;
                foreach (var walker in street.Walkers)
                    if (walker.State == StreetWalker.Phase.Reading)
                        readers++;
                Assert.That(readers, Is.LessThanOrEqualTo(1));
                everRead |= readers == 1;
            }
            Assert.That(everRead, Is.True);
            for (var i = 0; i < 8; i++)
                Assert.That(street.Walkers[i].Position, Is.EqualTo(twin.Walkers[i].Position));
        }

        [Test]
        public void TheLampsAreLitFromDuskUntilDawn()
        {
            Assert.That(StreetHours.Dark(13f), Is.False, "lunch");
            Assert.That(StreetHours.Dark(18.9f), Is.False);
            Assert.That(StreetHours.Dark(19f), Is.True);
            Assert.That(StreetHours.Dark(23.5f), Is.True);
            Assert.That(StreetHours.Dark(25f), Is.True, "one in the morning, on a clock past 24");
            Assert.That(StreetHours.Dark(31.5f), Is.False, "half past seven the next morning");
        }

        [Test]
        public void TheLightIsWhiteByDayBlueByNightAndTurnsBetween()
        {
            Assert.That(Daylight.At(13f), Is.EqualTo(Daylight.Day), "lunch");
            Assert.That(Daylight.At(22f), Is.EqualTo(Daylight.Night), "dinner");
            Assert.That(Daylight.At(25f), Is.EqualTo(Daylight.Night), "past midnight, on a clock past 24");
            Assert.That(Daylight.At(18.25f), Is.EqualTo(Daylight.Golden));
            // Through an all-day service it only ever gets darker, and never jumps.
            var before = Daylight.At(12f);
            for (var hour = 12f; hour <= 24f; hour += 0.05f)
            {
                var now = Daylight.At(hour);
                Assert.That(now.X + now.Y + now.Z, Is.LessThanOrEqualTo(before.X + before.Y + before.Z + 1e-4f), $"darker by {hour:0.00}");
                Assert.That(Vector3.Distance(now, before), Is.LessThan(0.03f), $"no jump at {hour:0.00}");
                before = now;
            }
            // Night is blue: more blue in it than red.
            Assert.That(Daylight.Night.Z, Is.GreaterThan(Daylight.Night.X));
        }

        [Test]
        public void IndoorsItNeverGoesDark()
        {
            Assert.That(Daylight.Nightfall(13f), Is.EqualTo(0f));
            Assert.That(Daylight.Nightfall(22f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Daylight.Indoors(13f), Is.EqualTo(Daylight.Day));
            for (var hour = 0f; hour <= 24f; hour += 0.25f)
            {
                var light = Daylight.Indoors(hour);
                Assert.That((light.X + light.Y + light.Z) / 3f, Is.GreaterThan(0.9f), $"the kitchen reads the same at {hour:0.00}");
            }
        }

        [Test]
        public void TheExtractorWorksHarderTheMoreIsCooking()
        {
            var off = new List<(float, bool)> { (0f, false), (0f, false) };
            var idle = new List<(float, bool)> { (1f, false), (0f, false) };
            var one = new List<(float, bool)> { (1f, true), (0f, false) };
            var both = new List<(float, bool)> { (1f, true), (1f, true), (1f, true) };

            Assert.That(StreetHours.Extraction(off), Is.EqualTo(0f));
            Assert.That(StreetHours.Extraction(idle), Is.GreaterThan(0f));
            Assert.That(StreetHours.Extraction(one), Is.GreaterThan(StreetHours.Extraction(idle)));
            Assert.That(StreetHours.Extraction(both), Is.EqualTo(1f), "never more than full");
        }
    }
}
