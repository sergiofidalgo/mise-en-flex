using System.Collections.Generic;
using System.Numerics;
using Game.Simulation.Office;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class TerraceBirdsTests
    {
        private const float Step = 0.05f;
        // A few steps more than a timing needs, for rounding over many small steps.
        private const float Margin = 0.2f;
        private static readonly Vector3 Perch = new Vector3(0f, 1f, 19.5f);

        private static BirdSettings Settings() => new BirdSettings();

        private static TerraceBirds Flock(BirdSettings settings, params Vector3[] perches) => new TerraceBirds(perches, settings, seed: 7);

        private static void Run(TerraceBirds birds, float seconds, params Vector2[] chefs)
        {
            for (var t = 0f; t < seconds; t += Step)
                birds.Tick(Step, chefs);
        }

        [Test]
        public void ItStaysPutWhileChefsKeepTheirDistance()
        {
            var birds = Flock(Settings(), Perch);
            Run(birds, 5f, new Vector2(0f, 16.5f));

            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Perched));
            Assert.That(birds.Birds[0].Position, Is.EqualTo(Perch));
        }

        [Test]
        public void AChefSteppingCloseSendsItOffWithinTheStartleDelay()
        {
            var settings = Settings();
            var birds = Flock(settings, Perch);
            birds.Tick(Step, new[] { new Vector2(0f, 17.5f) });
            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Startled));

            Run(birds, settings.StartleDelayMax + Margin, new Vector2(0f, 17.5f));
            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.FlyingOff));
        }

        [Test]
        public void ItFliesUpAndAwayFromTheChefThenOutOfSight()
        {
            var settings = Settings();
            var birds = Flock(settings, Perch);
            var chef = new Vector2(-2f, 19f);
            Run(birds, settings.StartleDelayMax + settings.FlyOffSeconds * 0.4f, chef);
            var bird = birds.Birds[0];

            Assert.That(bird.Position.Y, Is.GreaterThan(Perch.Y + 3f), "climbs");
            Assert.That(bird.Position.X, Is.GreaterThan(Perch.X), "away from the chef");
            Assert.That(bird.Position.Z, Is.GreaterThan(Perch.Z), "out over the rail");

            Run(birds, settings.FlyOffSeconds, chef);
            Assert.That(bird.State, Is.EqualTo(TerraceBird.Phase.Away));
            Assert.That(bird.Visible, Is.False);
            Assert.That(bird.Position.Y, Is.LessThan(Perch.Y), "dropped towards the roofs");
        }

        [Test]
        public void ItLeansOutEvenWhenTheChefComesFromBeyond()
        {
            // A chef between the bird and the rail: straight away would be into the office.
            var way = TerraceBird.FleeDirection(new Vector2(0f, 15f), new Vector2(0f, 16f), new Vector2(0f, 1f), 1f);
            Assert.That(way, Is.EqualTo(new Vector2(0f, 1f)));

            var sideways = TerraceBird.FleeDirection(new Vector2(0f, 15f), new Vector2(-1f, 15f), new Vector2(0f, 1f), 1f);
            Assert.That(sideways.X, Is.GreaterThan(0f));
            Assert.That(sideways.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void NeighboursLeaveWithIt()
        {
            var birds = Flock(Settings(), Perch, Perch + new Vector3(2f, 0f, 0f), Perch + new Vector3(4f, 0f, 0f), Perch + new Vector3(12f, 0f, 0f));
            birds.Tick(Step, new[] { new Vector2(-1f, 19f) });

            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Startled));
            Assert.That(birds.Birds[1].State, Is.EqualTo(TerraceBird.Phase.Startled), "within flock distance");
            Assert.That(birds.Birds[2].State, Is.EqualTo(TerraceBird.Phase.Startled), "a neighbour's neighbour");
            Assert.That(birds.Birds[3].State, Is.EqualTo(TerraceBird.Phase.Perched), "too far");
        }

        [Test]
        public void ItComesBackToItsPerchOnceNobodysThere()
        {
            var settings = Settings();
            var birds = Flock(settings, Perch);
            Run(birds, settings.StartleDelayMax + settings.FlyOffSeconds + Margin, new Vector2(0f, 18f));
            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Away));

            Run(birds, settings.AwayMax + settings.ReturnSeconds + 1f);
            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Perched));
            Assert.That(Vector3.Distance(birds.Birds[0].Position, Perch), Is.LessThan(0.001f));
        }

        [Test]
        public void ItWontLandWhileAChefStandsByItsPerch()
        {
            var settings = Settings();
            var birds = Flock(settings, Perch);
            Run(birds, settings.StartleDelayMax + settings.FlyOffSeconds + Margin, new Vector2(0f, 18f));
            // Further than would startle it, nearer than it would land by.
            var standingBy = new Vector2(0f, Perch.Z - settings.LandClearDistance + 0.5f);
            Run(birds, settings.AwayMax + 5f, standingBy);

            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Away));
        }

        [Test]
        public void ItStaysAwayForAtLeastTheShortestTime()
        {
            var settings = Settings();
            var birds = Flock(settings, Perch);
            Run(birds, settings.StartleDelayMax + settings.FlyOffSeconds + Margin, new Vector2(0f, 18f));
            Run(birds, settings.AwayMin - 1f);

            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Away));
        }

        [Test]
        public void ANegativeShortestTimeKeepsItAway()
        {
            var settings = Settings();
            settings.AwayMin = -1f;
            var birds = Flock(settings, Perch);
            Run(birds, settings.StartleDelayMax + settings.FlyOffSeconds + 200f, new Vector2(0f, 18f));
            Run(birds, 200f);

            Assert.That(birds.Birds[0].State, Is.EqualTo(TerraceBird.Phase.Away));
        }

        [Test]
        public void AChefMeetingItAsItLandsSendsItOffAgainFromWhereItIs()
        {
            var settings = Settings();
            var birds = Flock(settings, Perch);
            Run(birds, settings.StartleDelayMax + settings.FlyOffSeconds + settings.AwayMax + Margin, new Vector2(0f, 18f), new Vector2(40f, 0f));
            // Gone from the perch, nobody near it any more: it's on its way back by now.
            Run(birds, settings.ReturnSeconds * 0.8f, new Vector2(40f, 0f));
            var bird = birds.Birds[0];
            Assume.That(bird.State, Is.EqualTo(TerraceBird.Phase.Returning));
            var before = bird.Position;

            birds.Tick(Step, new[] { new Vector2(bird.Position.X, bird.Position.Z) });
            Assert.That(bird.State, Is.EqualTo(TerraceBird.Phase.FlyingOff));
            Assert.That(Vector3.Distance(bird.Position, before), Is.LessThan(1f), "no jump");
        }
    }
}
