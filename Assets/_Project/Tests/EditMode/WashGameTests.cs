using System;
using System.Numerics;
using Game.Data;
using Game.Simulation.Items;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode
{
    /// <summary>Washing by circling the stick (decision 2026-11-11, playtest round).</summary>
    public class WashGameTests
    {
        private const float Frame = 1f / 60f;

        private static WashGameSettings RealSettings =>
            AssetDatabase.LoadAssetAtPath<TuningConfig>("Assets/_Project/Data/Settings/TuningConfig.asset").WashGame;

        // Plates washed in some seconds by a hand that puts the stick at hand(t).
        private static float Plates(WashGameSettings settings, Func<float, Vector2> hand, float seconds)
        {
            var game = new WashGame(settings);
            var washed = 0f;
            for (var t = 0f; t < seconds; t += Frame)
                washed += game.Tick(Frame, hand(t));
            return washed;
        }

        // A round circle, `loops` a second; negative goes the other way.
        private static Func<float, Vector2> Circle(float loops, float radius = 0.9f) => t =>
        {
            var angle = t * loops * Math.PI * 2.0;
            return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
        };

        // A rough square: the thumb runs along the stick's edges, corner to corner.
        private static Func<float, Vector2> Square(float loops) => t =>
        {
            var along = (t * loops) % 1f * 4f;
            var side = (int)along;
            var f = along - side;
            Vector2[] corners = { new Vector2(0.75f, 0.75f), new Vector2(-0.75f, 0.75f), new Vector2(-0.75f, -0.75f), new Vector2(0.75f, -0.75f) };
            return Vector2.Lerp(corners[side], corners[(side + 1) % 4], f);
        };

        [Test]
        public void DoingNothingNeverWashesAPlate()
        {
            Assert.That(Plates(RealSettings, _ => Vector2.Zero, 120f), Is.Zero);
        }

        [Test]
        public void HoldingTheStickOneWayNeverWashes()
        {
            Assert.That(Plates(RealSettings, _ => new Vector2(0.9f, 0f), 60f), Is.Zero);
        }

        [Test]
        public void WaggingBackAndForthNeverWashes()
        {
            // Wagging through half a turn and back: only new turning counts.
            Assert.That(Plates(RealSettings, t => new Vector2((float)Math.Cos(Math.Sin(t * 6.0) * 1.5), (float)Math.Sin(Math.Sin(t * 6.0) * 1.5)) * 0.9f, 30f),
                Is.LessThan(0.15f), "at most the first sweep counts");
        }

        [Test]
        public void ASteadyCircleWashesAPlateInAboutASecond()
        {
            // The user's call (2026-11-13): washing was too slow. 2 circles a plate at up to 2 a second.
            var settings = RealSettings;
            var plates = Plates(settings, Circle(settings.CircleSpeed), 30f);
            Assert.That(30f / plates, Is.InRange(0.8f, 1.4f), "seconds a plate");
        }

        [Test]
        public void EitherDirectionWashesTheSame()
        {
            var clockwise = Plates(RealSettings, Circle(-1.2f), 20f);
            var anticlockwise = Plates(RealSettings, Circle(1.2f), 20f);
            Assert.That(clockwise, Is.GreaterThan(4f));
            Assert.That(clockwise, Is.EqualTo(anticlockwise).Within(0.1f));
        }

        [Test]
        public void ChangingDirectionMidwayKeepsWashing()
        {
            var game = new WashGame(RealSettings);
            var washed = 0f;
            for (var t = 0f; t < 10f; t += Frame)
                washed += game.Tick(Frame, Circle(t < 5f ? 1.2f : -1.2f)(t));
            var steady = Plates(RealSettings, Circle(1.2f), 10f);
            Assert.That(washed, Is.GreaterThan(steady * 0.8f), "only the turn back is lost");
        }

        [Test]
        public void RoughShapesCountAsCircles()
        {
            var round = Plates(RealSettings, Circle(1.2f), 20f);
            Assert.That(Plates(RealSettings, Square(1.2f), 20f), Is.GreaterThan(round * 0.9f), "a square");
            Assert.That(Plates(RealSettings, t => Circle(1.2f)(t) * new Vector2(1f, 0.55f), 20f), Is.GreaterThan(round * 0.9f), "a squashed oval");
            Assert.That(Plates(RealSettings, Circle(1.2f, radius: 0.5f), 20f), Is.GreaterThan(round * 0.9f), "a small circle, past the push needed");
        }

        [Test]
        public void CirclingFasterThanTheCapWashesNoFaster()
        {
            var settings = RealSettings;
            var atCap = Plates(settings, Circle(settings.CircleSpeed), 20f);
            Assert.That(Plates(settings, Circle(settings.CircleSpeed * 2f), 20f), Is.LessThanOrEqualTo(atCap * 1.02f));
        }

        [Test]
        public void SlammingTheStickAboutWashesLittle()
        {
            var random = new Random(3);
            var slammed = Plates(RealSettings, _ =>
            {
                var angle = random.NextDouble() * Math.PI * 2.0;
                return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }, 30f);
            Assert.That(slammed, Is.LessThan(Plates(RealSettings, Circle(1.2f), 30f) * 0.25f));
        }

        [Test]
        public void TheSoapFollowsTheStick()
        {
            var game = new WashGame(RealSettings);
            game.Tick(Frame, new Vector2(0.3f, -0.6f));
            Assert.That(game.Point.X, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(game.Point.Y, Is.EqualTo(-0.6f).Within(1e-5f));
        }

        [Test]
        public void TheSinkSharesTheClothsPushAndTurnBack()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>("Assets/_Project/Data/Settings/TuningConfig.asset");
            Assert.That(tuning.WashGame.MinPush, Is.EqualTo(tuning.TraceGame.MinPush), "one control for every cleaning job");
            Assert.That(tuning.WashGame.TurnBack, Is.EqualTo(tuning.TraceGame.TurnBack));
        }

        // The pace and direction the view's pacer and foam follow (the user's call, 2026-11-14).

        private static WashGame After(WashGameSettings settings, Func<float, Vector2> hand, float seconds)
        {
            var game = new WashGame(settings);
            for (var t = 0f; t < seconds; t += Frame)
                game.Tick(Frame, hand(t));
            return game;
        }

        [Test]
        public void CirclingAtFullSpeedReadsAsFullPace()
        {
            var settings = RealSettings;
            var game = After(settings, Circle(settings.CircleSpeed), 2f);
            Assert.That(game.Pace, Is.GreaterThan(0.95f));
        }

        [Test]
        public void CirclingAtHalfSpeedReadsAsHalfPace()
        {
            var settings = RealSettings;
            var game = After(settings, Circle(settings.CircleSpeed * 0.5f), 2f);
            Assert.That(game.Pace, Is.EqualTo(0.5f).Within(0.1f));
        }

        [Test]
        public void CirclingFasterStillReadsAsFullPace()
        {
            var settings = RealSettings;
            var game = After(settings, Circle(settings.CircleSpeed * 1.5f), 2f);
            Assert.That(game.Pace, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void StoppingDropsThePace()
        {
            var settings = RealSettings;
            var circle = Circle(settings.CircleSpeed);
            var game = After(settings, t => t < 1f ? circle(t) : circle(1f), 2f);
            Assert.That(game.Pace, Is.Zero);
        }

        [Test]
        public void TurningFollowsTheWayRound()
        {
            var settings = RealSettings;
            Assert.That(After(settings, Circle(1f), 1f).Turning, Is.EqualTo(1), "anticlockwise");
            Assert.That(After(settings, Circle(-1f), 1f).Turning, Is.EqualTo(-1), "clockwise");
            Assert.That(new WashGame(settings).Turning, Is.Zero, "not turned yet");
        }
    }
}
