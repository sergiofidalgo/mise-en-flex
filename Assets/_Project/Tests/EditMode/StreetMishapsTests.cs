using System.Numerics;
using Game.Simulation.Street;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>A chef knocked down by a car, and a chef who walked off and is carried back (2026-11-12).</summary>
    public class StreetMishapsTests
    {
        private const float Step = 0.01f;

        [Test]
        public void ACarReachesAChefInItsWayAndNotOneBesideIt()
        {
            var car = new Vector2(0f, -13f);
            var east = new Vector2(1f, 0f);
            var size = new Vector2(1.05f, 2.15f); // half its width, half its length
            Assert.That(Knockdown.Reaches(new Vector2(2.3f, -13f), 0.3f, car, east, size), Is.True, "at its bumper");
            Assert.That(Knockdown.Reaches(new Vector2(0f, -11.8f), 0.3f, car, east, size), Is.True, "at its door");
            Assert.That(Knockdown.Reaches(new Vector2(2.6f, -13f), 0.3f, car, east, size), Is.False, "just ahead of it");
            Assert.That(Knockdown.Reaches(new Vector2(0f, -11.5f), 0.3f, car, east, size), Is.False, "on the kerb beside it");
        }

        [Test]
        public void ACarSoundsItsHornForAChefAheadInItsLaneOnly()
        {
            var car = new Vector2(0f, -13f);
            var east = new Vector2(1f, 0f);
            var size = new Vector2(1.05f, 2.15f);
            // At 6 m/s it warns anyone within a second's drive of its bumper.
            Assert.That(Knockdown.BearsDownOn(new Vector2(6f, -13f), 0.3f, car, east, size, 6f, 1f), Is.True);
            Assert.That(Knockdown.BearsDownOn(new Vector2(12f, -13f), 0.3f, car, east, size, 6f, 1f), Is.False, "too far ahead yet");
            Assert.That(Knockdown.BearsDownOn(new Vector2(-6f, -13f), 0.3f, car, east, size, 6f, 1f), Is.False, "behind it");
            Assert.That(Knockdown.BearsDownOn(new Vector2(6f, -16.2f), 0.3f, car, east, size, 6f, 1f), Is.False, "in the other lane");
        }

        [Test]
        public void TheyLandOnTheNearerPavementAlongTheCarsWayAndInsideTheScene()
        {
            // The grill's street: the near pavement at z −9.4, the far at −18.2; the scene from −17 to 13.
            Assert.That(Knockdown.LandingFor(new Vector2(0f, -11.5f), new Vector2(-1f, 0f), 4f, -9.4f, -18.2f, -17f, 13f), Is.EqualTo(new Vector2(-4f, -9.4f)));
            Assert.That(Knockdown.LandingFor(new Vector2(0f, -15.5f), new Vector2(1f, 0f), 4f, -9.4f, -18.2f, -17f, 13f), Is.EqualTo(new Vector2(4f, -18.2f)));
            Assert.That(Knockdown.LandingFor(new Vector2(11f, -11.5f), new Vector2(1f, 0f), 4f, -9.4f, -18.2f, -17f, 13f).X, Is.EqualTo(13f), "not thrown out of the scene");
        }

        [Test]
        public void TheyFlyInAnArcLieDazedAndGetUp()
        {
            var settings = new KnockdownSettings();
            var knock = new Knockdown(new Vector2(0f, -11.5f), new Vector2(-4f, -9.4f), settings);
            Assert.That(knock.State, Is.EqualTo(Knockdown.Phase.Flying));
            Assert.That(knock.Position, Is.EqualTo(new Vector3(0f, 0f, -11.5f)));

            var highest = 0f;
            for (var t = 0f; t < settings.FlightSeconds - Step; t += Step)
            {
                knock.Tick(Step);
                highest = System.Math.Max(highest, knock.Position.Y);
                Assert.That(knock.Position.Y, Is.GreaterThanOrEqualTo(0f));
            }
            Assert.That(highest, Is.EqualTo(settings.Height).Within(0.02f), "the top of the arc");
            Assert.That(knock.Lying, Is.EqualTo(0f), "upright in the air, tumbling");
            Assert.That(knock.Tumble, Is.GreaterThan(360f));

            knock.Tick(3f * Step);
            Assert.That(knock.State, Is.EqualTo(Knockdown.Phase.Dazed));
            Assert.That(knock.Position.X, Is.EqualTo(-4f).Within(1e-4f));
            Assert.That(knock.Position.Y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(knock.Position.Z, Is.EqualTo(-9.4f).Within(1e-4f));
            Assert.That(knock.Lying, Is.EqualTo(1f));
            Assert.That(knock.Tumble, Is.EqualTo(360f * settings.Tumbles).Within(1e-3f), "whole turns: they come down the right way round");

            knock.Tick(settings.DazedSeconds);
            Assert.That(knock.State, Is.EqualTo(Knockdown.Phase.GettingUp));
            Assert.That(knock.Lying, Is.InRange(0f, 1f));
            knock.Tick(settings.GetUpSeconds);
            Assert.That(knock.State, Is.EqualTo(Knockdown.Phase.Done));
            Assert.That(knock.Lying, Is.EqualTo(0f));
        }

        [Test]
        public void TheSceneEndsPastThePavementsEndsAndInThePark()
        {
            var edges = new SceneEdges(-17.5f, 13.5f, -22f);
            Assert.That(edges.HasLeft(new Vector2(0f, -9f)), Is.False, "on the pavement");
            Assert.That(edges.HasLeft(new Vector2(0f, -14f)), Is.False, "in the road");
            Assert.That(edges.HasLeft(new Vector2(-18f, -9f)), Is.True);
            Assert.That(edges.HasLeft(new Vector2(14f, -14f)), Is.True);
            Assert.That(edges.HasLeft(new Vector2(0f, -23f)), Is.True, "off into the park");
            Assert.That(edges.Outward(new Vector2(-18f, -9f)), Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(edges.Outward(new Vector2(14f, -9f)), Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(edges.Outward(new Vector2(0f, -23f)), Is.EqualTo(new Vector2(0f, -1f)));
        }

        [Test]
        public void ABodyguardFetchesThemCarriesThemToTheDoorstepAndGoesBack()
        {
            var settings = new EscortSettings();
            var chefAt = new Vector2(-18f, -9f);
            var doorstep = new Vector2(2f, -8f);
            var escort = new Escort(chefAt, doorstep, new Vector2(-1f, 0f), settings);
            Assert.That(escort.State, Is.EqualTo(Escort.Phase.Waiting));
            Assert.That(escort.GuardShown, Is.False);
            Assert.That(escort.Guard, Is.EqualTo(new Vector2(-22f, -9f)), "he comes in from further out");

            Run(escort, settings.WaitSeconds + 0.05f);
            Assert.That(escort.State, Is.EqualTo(Escort.Phase.Fetching));
            Assert.That(escort.Chef, Is.EqualTo(chefAt), "still standing where they stopped");

            Run(escort, settings.ComesFrom / settings.Speed + 0.05f);
            Assert.That(escort.State, Is.EqualTo(Escort.Phase.Carrying));
            Run(escort, 1f);
            Assert.That(escort.Carried, Is.True);
            Assert.That(escort.Chef, Is.EqualTo(escort.Guard), "under his arm");
            Assert.That(escort.Chef.X, Is.GreaterThan(chefAt.X + 4f), "on the way back");
            Assert.That(escort.ChefFree, Is.False);

            // However long the rest of the way takes (about 20 m at his pace), and no longer than that.
            var carried = 0f;
            for (; escort.State == Escort.Phase.Carrying && carried < 10f; carried += Step)
                escort.Tick(Step);
            Assert.That(carried, Is.LessThan(Vector2.Distance(chefAt, doorstep) / settings.Speed));
            Assert.That(escort.State, Is.EqualTo(Escort.Phase.PuttingDown));
            Assert.That(escort.Chef, Is.EqualTo(doorstep));
            Run(escort, settings.PutDownSeconds + 0.05f);
            Assert.That(escort.ChefFree, Is.True);
            Assert.That(escort.Chef, Is.EqualTo(doorstep), "the chef stays; only he goes");

            Run(escort, 10f);
            Assert.That(escort.State, Is.EqualTo(Escort.Phase.Done));
            Assert.That(escort.GuardShown, Is.False);
            Assert.That(escort.Guard, Is.EqualTo(new Vector2(-22f, -9f)));
        }

        private static void Run(Escort escort, float seconds)
        {
            for (var t = 0f; t < seconds; t += Step)
                escort.Tick(Step);
        }
    }
}
