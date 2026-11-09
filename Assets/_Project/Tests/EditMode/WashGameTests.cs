using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Data;
using Game.Simulation.Items;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    public class WashGameTests
    {
        private const float Frame = 1f / 60f;

        private static WashGameSettings RealSettings =>
            AssetDatabase.LoadAssetAtPath<TuningConfig>("Assets/_Project/Data/Settings/TuningConfig.asset").WashGame;

        // Plays for some seconds, reacting to where the point was `reactionTime` ago, like a person.
        private static float Plates(WashGameSettings settings, Func<Vector2, Vector2, Vector2> player, float seconds, int seed, float reactionTime = 0.15f)
        {
            var game = new WashGame(settings, seed);
            var seen = new Queue<(Vector2 Point, Vector2 Velocity)>();
            var delay = (int)(reactionTime / Frame);
            var washed = 0f;
            var last = game.Point;
            for (var t = 0f; t < seconds; t += Frame)
            {
                seen.Enqueue((game.Point, (game.Point - last) / Frame));
                last = game.Point;
                var stick = Vector2.Zero;
                if (seen.Count > delay)
                {
                    var (point, velocity) = seen.Dequeue();
                    stick = player(point, velocity);
                }
                washed += game.Tick(Frame, stick);
            }
            return washed;
        }

        private static Vector2 Clamp(Vector2 stick) => stick.LengthSquared() > 1f ? Vector2.Normalize(stick) : stick;

        [Test]
        public void DoingNothingNeverCleansAPlate()
        {
            for (var seed = 0; seed < 20; seed++)
                Assert.That(Plates(RealSettings, (_, _) => Vector2.Zero, 120f, seed), Is.EqualTo(0f), $"seed {seed}");
        }

        [Test]
        public void HoldingTheStickOneWayDoesntCleanOne()
        {
            for (var seed = 0; seed < 20; seed++)
                Assert.That(Plates(RealSettings, (_, _) => new Vector2(0.3f, 0f), 60f, seed), Is.LessThan(0.5f), $"seed {seed}");
        }

        [Test]
        public void ASteadyHandCleansPlatesAtAGoodPace()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var plates = Plates(RealSettings, (point, velocity) => Clamp(-point - velocity * 0.35f), 30f, seed);
                // Holding the centre, then three corrections (2026-11-09): about 5 s a plate.
                Assert.That(plates, Is.GreaterThan(4f), $"seed {seed}: under 7.5 s a plate");
            }
        }

        // Retuned 2026-11-04 (playtest: too hard, too much inertia): ordinary over-correcting is now
        // forgiven, but slamming the stick around with slow reactions still costs a lot.
        [Test]
        public void PanickingIsMuchWorseThanASteadyHand()
        {
            var steady = 0f;
            var panicked = 0f;
            for (var seed = 0; seed < 10; seed++)
            {
                steady += Plates(RealSettings, (point, velocity) => Clamp(-point - velocity * 0.35f), 30f, seed);
                panicked += Plates(RealSettings, (point, _) => Clamp(-point * 20f), 30f, seed, reactionTime: 0.35f);
            }
            Assert.That(panicked, Is.LessThan(steady * 0.6f));
        }

        [Test]
        public void TheNudgeCantPushAPointBackIntoRange()
        {
            var settings = RealSettings;
            Assert.That(settings.Nudge, Is.LessThan(settings.Instability * settings.OkRadius), "keeps doing nothing from ever working");
            Assert.That(settings.StartRadius, Is.GreaterThan(settings.OkRadius));
        }

        [Test]
        public void WashingRateFollowsTheDistance()
        {
            var settings = new WashGameSettings { Nudge = 0f, Instability = 0f, Damping = 0f, StartRadius = 0.1f };
            Assert.That(new WashGame(settings, 1).Rate, Is.EqualTo(1f), "in the target");

            settings.StartRadius = 0.4f;
            Assert.That(new WashGame(settings, 1).Rate, Is.EqualTo(0.5f).Within(0.01f), "halfway out");

            settings.StartRadius = 0.7f;
            Assert.That(new WashGame(settings, 1).Rate, Is.EqualTo(0f));
        }

        // A calm, still point in the centre with no drift: only the rules act on it.
        private static WashGameSettings StillSettings => new WashGameSettings
        {
            Nudge = 0f, Instability = 0f, Damping = 6f, StartRadius = 0f, HoldSeconds = 0.3f, Corrections = 2, PushSpeed = 3.5f,
        };

        [Test]
        public void HoldingTheCentreScrubsAPartThenPushesThePointAway()
        {
            var game = new WashGame(StillSettings, 1);
            var pushes = 0;
            game.Pushed += _ => pushes++;
            var washed = 0f;
            for (var i = 0; i < 15; i++) // 0.25 s
                washed += game.Tick(Frame, Vector2.Zero);
            Assert.That(washed, Is.EqualTo(0f), "not held long enough yet");

            for (var i = 0; i < 6; i++) // past 0.3 s
                washed += game.Tick(Frame, Vector2.Zero);
            Assert.That(washed, Is.EqualTo(1f / 3f).Within(1e-5f), "one of three parts");
            Assert.That(pushes, Is.EqualTo(1));
            for (var i = 0; i < 30; i++)
                game.Tick(Frame, Vector2.Zero);
            Assert.That(game.Point.Length(), Is.GreaterThan(game.GoodRadius), "kicked out of the centre");
        }

        [Test]
        public void PassingThroughTheCentreDoesntCount()
        {
            var settings = StillSettings;
            settings.StartRadius = 0.9f;
            settings.Damping = 0f;
            var game = new WashGame(settings, 3);
            var washed = 0f;
            // Fling it straight across: it crosses the centre in well under the hold time.
            var across = -Vector2.Normalize(game.Point);
            for (var t = 0f; t < 1f; t += Frame)
                washed += game.Tick(Frame, across);
            Assert.That(washed, Is.EqualTo(0f));
        }

        [Test]
        public void APlateTakesThreeCentringsFromThreeSides()
        {
            // An ideal hand pulls the point straight back to the centre.
            var game = new WashGame(StillSettings, 7);
            var directions = new List<Vector2>();
            game.Pushed += directions.Add;
            var washed = 0f;
            for (var t = 0f; t < 20f && washed < 1f - 1e-4f; t += Frame)
                washed += game.Tick(Frame, Clamp(-game.Point * 4f));

            Assert.That(washed, Is.EqualTo(1f).Within(1e-4f), "a whole plate");
            Assert.That(game.StagesDone, Is.EqualTo(3), "the first centring and two corrections (the user's call)");
            Assert.That(directions.Count, Is.EqualTo(2), "no push after the last one");
            // Evenly spread with a little randomness: the pushes about 120° apart, at least 80°.
            for (var i = 0; i < directions.Count; i++)
            for (var j = i + 1; j < directions.Count; j++)
            {
                var angle = Math.Acos(Math.Clamp(Vector2.Dot(directions[i], directions[j]), -1f, 1f)) * 180.0 / Math.PI;
                Assert.That(angle, Is.GreaterThan(80.0), $"pushes {i} and {j}");
            }
        }

        [Test]
        public void LeavingTheCentreStartsTheHoldAgain()
        {
            var game = new WashGame(StillSettings, 1);
            for (var t = 0f; t < 0.2f; t += Frame)
                game.Tick(Frame, Vector2.Zero);
            Assert.That(game.HoldFraction, Is.GreaterThan(0.5f));
            for (var i = 0; i < 40; i++)
                game.Tick(Frame, new Vector2(1f, 0f));
            Assert.That(game.HoldFraction, Is.EqualTo(0f));
        }

        [Test]
        public void ThePointStaysInsideTheCircle()
        {
            var game = new WashGame(new WashGameSettings(), 1);
            for (var i = 0; i < 600; i++)
            {
                game.Tick(Frame, new Vector2(1f, 0f));
                Assert.That(game.Point.Length(), Is.LessThanOrEqualTo(1.0001f));
            }
        }

        [Test]
        public void ANewPlateStartsOffCentre()
        {
            var settings = new WashGameSettings();
            var game = new WashGame(settings, 1);
            game.NewPlate();
            Assert.That(game.Point.Length(), Is.EqualTo(settings.StartRadius).Within(0.001f));
        }
    }
}
