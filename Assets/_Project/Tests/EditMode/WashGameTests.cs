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
                // Continuous scrubbing, quicker nearer the centre, three shares; quicker plates
                // (2026-11-11): about 2.7 s a plate.
                Assert.That(plates, Is.GreaterThan(7.5f), $"seed {seed}: under 4 s a plate");
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
        public void ScrubbingIsQuickestAtTheCentreAndStopsAtTheOkRadius()
        {
            // The user's call (2026-11-10): the closer to the centre, the quicker.
            var settings = new WashGameSettings { Nudge = 0f, Instability = 0f, Damping = 0f, OkRadius = 0.6f, StartRadius = 0f };
            Assert.That(new WashGame(settings, 1).Rate, Is.EqualTo(1f), "full speed at the very centre");

            settings.StartRadius = 0.3f;
            Assert.That(new WashGame(settings, 1).Rate, Is.EqualTo(0.5f).Within(0.01f), "half speed halfway out");

            settings.StartRadius = 0.7f;
            Assert.That(new WashGame(settings, 1).Rate, Is.EqualTo(0f), "nothing beyond the OK radius");
        }

        [Test]
        public void ThePlateCleansAllTheTimeTheSoapIsClose()
        {
            // A still point (no drift, no stick): it washes every frame, more the closer it sits.
            float WashedAt(float radius)
            {
                var settings = new WashGameSettings { Nudge = 0f, Instability = 0f, Damping = 0f, StartRadius = radius, SecondsPerPlate = 2.4f };
                var game = new WashGame(settings, 1);
                var washed = 0f;
                for (var i = 0; i < 12; i++) // 0.2 s, well short of a share
                    washed += game.Tick(Frame, Vector2.Zero);
                return washed;
            }

            Assert.That(WashedAt(0.1f), Is.GreaterThan(WashedAt(0.4f)));
            Assert.That(WashedAt(0.4f), Is.GreaterThan(0f), "not only in the target");
            Assert.That(WashedAt(0.65f), Is.EqualTo(0f));
        }

        // A calm, still point in the centre with no drift: only the rules act on it.
        private static WashGameSettings StillSettings => new WashGameSettings
        {
            Nudge = 0f, Instability = 0f, Damping = 6f, StartRadius = 0f, SecondsPerPlate = 2.4f, Corrections = 2, PushSpeed = 3.5f,
        };

        [Test]
        public void EachThirdCleanPushesThePointAway()
        {
            // At full speed a third of a plate takes 2.4 / 3 = 0.8 s.
            var game = new WashGame(StillSettings, 1);
            var pushes = 0;
            game.Pushed += _ => pushes++;
            var washed = 0f;
            for (var i = 0; i < 45; i++) // 0.75 s
                washed += game.Tick(Frame, Vector2.Zero);
            Assert.That(pushes, Is.EqualTo(0), "the first share isn't clean yet");
            Assert.That(game.ShareProgress, Is.GreaterThan(0.9f));

            for (var i = 0; i < 6; i++) // past 0.8 s
                washed += game.Tick(Frame, Vector2.Zero);
            Assert.That(pushes, Is.EqualTo(1));
            Assert.That(washed, Is.EqualTo(1f / 3f).Within(0.02f), "a share's worth, nothing carried past it");
            for (var i = 0; i < 30; i++)
                game.Tick(Frame, Vector2.Zero);
            Assert.That(game.Point.Length(), Is.GreaterThan(game.GoodRadius), "kicked out of the centre");
        }

        [Test]
        public void APlateIsWashedInThreeSharesFromThreeSides()
        {
            // An ideal hand pulls the point straight back to the centre.
            var game = new WashGame(StillSettings, 7);
            var directions = new List<Vector2>();
            game.Pushed += directions.Add;
            var washed = 0f;
            for (var t = 0f; t < 20f && washed < 1f - 1e-4f; t += Frame)
                washed += game.Tick(Frame, Clamp(-game.Point * 4f));

            Assert.That(washed, Is.EqualTo(1f).Within(1e-4f), "a whole plate, exactly");
            Assert.That(game.StagesDone, Is.EqualTo(3), "three shares (the user's call)");
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
