using System;
using System.Numerics;

namespace Game.Simulation.Items
{
    /// <summary>Tuning for <see cref="WashGame"/>. Radii are fractions of the circle; times in real seconds.</summary>
    public sealed class WashGameSettings
    {
        /// <summary>Acceleration at full stick: high, so small movements shift the point a lot.</summary>
        public float StickAcceleration { get; set; } = 10f;

        /// <summary>Acceleration away from the centre per unit of distance: the point won't stay by itself.</summary>
        public float Instability { get; set; } = 4f;

        /// <summary>How fast speed dies away, per second.</summary>
        public float Damping { get; set; } = 5f;

        /// <summary>
        /// Size of the random nudge. Must stay below Instability × OkRadius, or a player doing
        /// nothing could drift back into range: not reacting must never clean a plate.
        /// </summary>
        public float Nudge { get; set; } = 1.4f;

        /// <summary>Seconds between new nudge directions.</summary>
        public float NudgeInterval { get; set; } = 0.5f;

        /// <summary>Inside this radius, washing goes at full speed.</summary>
        public float GoodRadius { get; set; } = 0.25f;

        /// <summary>Beyond this radius, nothing gets clean; between the two it fades.</summary>
        public float OkRadius { get; set; } = 0.55f;

        /// <summary>Seconds of full-speed washing per plate.</summary>
        public float SecondsPerPlate { get; set; } = 2.5f;

        /// <summary>Where the point starts for each plate: outside OkRadius, so you have to bring it in.</summary>
        public float StartRadius { get; set; } = 0.7f;
    }

    /// <summary>
    /// Washing a plate by hand (decision 2026-11-04): balance a point in the centre of a circle
    /// with the stick. It behaves like balancing a stick on your palm: the further it's off
    /// centre, the harder it runs away, so doing nothing loses it, and over-correcting does too.
    /// Plates only get clean while it's near the centre.
    /// </summary>
    public sealed class WashGame
    {
        private readonly WashGameSettings _settings;
        private readonly Random _random;
        private Vector2 _velocity;
        private Vector2 _nudge;
        private float _nudgeLeft;

        public WashGame(WashGameSettings settings, int seed)
        {
            _settings = settings;
            _random = new Random(seed);
            NewPlate();
        }

        /// <summary>The point, with 1 as the circle's edge.</summary>
        public Vector2 Point { get; private set; }

        /// <summary>0..1: how fast it's washing right now, from where the point is.</summary>
        public float Rate => RateAt(Point.Length());

        /// <summary>Starts the next plate, with the point off centre again.</summary>
        public void NewPlate()
        {
            Point = RandomDirection() * _settings.StartRadius;
            _velocity = Vector2.Zero;
        }

        /// <summary>Moves the point.</summary>
        /// <returns>How much of a plate this washed (for <see cref="Sink.Wash"/>).</returns>
        public float Tick(float seconds, Vector2 stick)
        {
            if (seconds <= 0f)
                return 0f;

            _nudgeLeft -= seconds;
            if (_nudgeLeft <= 0f)
            {
                _nudgeLeft = _settings.NudgeInterval;
                _nudge = RandomDirection() * _settings.Nudge;
            }

            if (stick.LengthSquared() > 1f)
                stick = Vector2.Normalize(stick);

            var acceleration = stick * _settings.StickAcceleration + Point * _settings.Instability + _nudge - _velocity * _settings.Damping;
            _velocity += acceleration * seconds;
            Point += _velocity * seconds;

            // The rim stops the point, so a lost point can always be brought back.
            var distance = Point.Length();
            if (distance > 1f)
            {
                var outward = Point / distance;
                Point = outward;
                var along = Vector2.Dot(_velocity, outward);
                if (along > 0f)
                    _velocity -= outward * along;
            }

            return Rate * seconds / Math.Max(0.01f, _settings.SecondsPerPlate);
        }

        private float RateAt(float distance)
        {
            if (distance <= _settings.GoodRadius)
                return 1f;
            var span = _settings.OkRadius - _settings.GoodRadius;
            return span > 0f ? Math.Clamp(1f - (distance - _settings.GoodRadius) / span, 0f, 1f) : 0f;
        }

        private Vector2 RandomDirection()
        {
            var angle = _random.NextDouble() * Math.PI * 2.0;
            return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }
    }
}
