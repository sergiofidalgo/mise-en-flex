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
                Assert.That(plates, Is.GreaterThan(5f), $"seed {seed}: under 6 s a plate");
            }
        }

        [Test]
        public void OverCorrectingIsMuchWorse()
        {
            var steady = 0f;
            var jumpy = 0f;
            for (var seed = 0; seed < 10; seed++)
            {
                steady += Plates(RealSettings, (point, velocity) => Clamp(-point - velocity * 0.35f), 30f, seed);
                jumpy += Plates(RealSettings, (point, velocity) => Clamp(-point * 3f - velocity * 0.2f), 30f, seed, reactionTime: 0.2f);
            }
            Assert.That(jumpy, Is.LessThan(steady * 0.6f));
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
