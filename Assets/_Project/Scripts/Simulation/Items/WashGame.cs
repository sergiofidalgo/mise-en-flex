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
        public float Instability { get; set; } = 3.2f;

        /// <summary>How fast speed dies away, per second.</summary>
        public float Damping { get; set; } = 4.2f;

        /// <summary>
        /// Size of the random nudge. Must stay below Instability × OkRadius, or a player doing
        /// nothing could drift back into range: not reacting must never clean a plate.
        /// </summary>
        public float Nudge { get; set; } = 1.0f;

        /// <summary>Seconds between new nudge directions.</summary>
        public float NudgeInterval { get; set; } = 0.5f;

        /// <summary>The centre the point must be held in.</summary>
        public float GoodRadius { get; set; } = 0.25f;

        /// <summary>How far out the point still counts as close, for its colour (0 beyond, 1 inside GoodRadius).</summary>
        public float OkRadius { get; set; } = 0.55f;

        /// <summary>Seconds the point must stay in the centre for it to count: passing through doesn't.</summary>
        public float HoldSeconds { get; set; } = 0.3f;

        /// <summary>Pushes per plate after the first centring; each must be corrected. A plate takes this + 1 centrings.</summary>
        public int Corrections { get; set; } = 2;

        /// <summary>Speed the point is kicked away with once it's been held in the centre (circle radii per second).</summary>
        public float PushSpeed { get; set; } = 3.5f;

        /// <summary>Random spread, in degrees either way, around the sides the plate is tipped to, evenly spaced round the circle.</summary>
        public float PushSpreadDegrees { get; set; } = 20f;

        /// <summary>Where the point starts for each plate: outside OkRadius, so you have to bring it in.</summary>
        public float StartRadius { get; set; } = 0.7f;
    }

    /// <summary>
    /// Washing a plate by hand (decisions 2026-11-04 and 2026-11-09): balance a point in the centre
    /// of a circle with the stick. It behaves like balancing a stick on your palm: the further it's
    /// off centre, the harder it runs away, so doing nothing loses it, and over-correcting does too.
    /// Holding it in the centre for a moment scrubs one part of the plate; then the plate gets
    /// kicked off in a new direction to be brought back (3 centrings a plate, the user's call).
    /// The start and the kicks go evenly round the circle (with a little randomness), so every
    /// plate is tipped every way before it's clean.
    /// </summary>
    public sealed class WashGame
    {
        private readonly WashGameSettings _settings;
        private readonly Random _random;
        private Vector2 _velocity;
        private Vector2 _nudge;
        private float _nudgeLeft;
        private float _held;
        private float _pushAngle;
        private int _pushes;

        public WashGame(WashGameSettings settings, int seed)
        {
            _settings = settings;
            _random = new Random(seed);
            NewPlate();
        }

        /// <summary>The point, with 1 as the circle's edge.</summary>
        public Vector2 Point { get; private set; }

        /// <summary>Inside this distance from the centre, washing goes at full speed.</summary>
        public float GoodRadius => _settings.GoodRadius;

        /// <summary>Beyond this distance from the centre, nothing gets clean.</summary>
        public float OkRadius => _settings.OkRadius;

        /// <summary>0..1: how close to the centre the point is (1 inside it), for its colour.</summary>
        public float Rate => RateAt(Point.Length());

        /// <summary>0..1: how long it has been held in the centre, towards <see cref="WashGameSettings.HoldSeconds"/>.</summary>
        public float HoldFraction => _settings.HoldSeconds > 0f ? Math.Clamp(_held / _settings.HoldSeconds, 0f, 1f) : 0f;

        /// <summary>Centrings a plate takes.</summary>
        public int Stages => Math.Max(1, _settings.Corrections + 1);

        /// <summary>Centrings done on this plate.</summary>
        public int StagesDone { get; private set; }

        /// <summary>Raised when the point is kicked away, with the direction it goes.</summary>
        public event Action<Vector2> Pushed;

        /// <summary>Starts the next plate, with the point off centre again.</summary>
        public void NewPlate()
        {
            // The plate starts tipped to one side, and each push goes to the next, evenly round the
            // circle: with 3 centrings, three sides 120° apart.
            _pushAngle = (float)(_random.NextDouble() * Math.PI * 2.0);
            var start = _pushAngle - SideSpacing;
            Point = new Vector2((float)Math.Cos(start), (float)Math.Sin(start)) * _settings.StartRadius;
            _velocity = Vector2.Zero;
            _held = 0f;
            StagesDone = 0;
            _pushes = 0;
        }

        // The angle between the sides a plate is tipped to: one per centring.
        private double SideSpacing => 2.0 * Math.PI / Stages;

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

            // Held in the centre long enough: that part's scrubbed. Leaving starts the hold again.
            if (Point.Length() > _settings.GoodRadius)
            {
                _held = 0f;
                return 0f;
            }
            _held += seconds;
            if (_held < _settings.HoldSeconds)
                return 0f;

            _held = 0f;
            StagesDone++;
            if (StagesDone < Stages)
                Push();
            return 1f / Stages;
        }

        // Kicks the point off to the plate's next side, nudged a little, so over a plate it's
        // tipped every way.
        private void Push()
        {
            var spread = _settings.PushSpreadDegrees * Math.PI / 180.0;
            var angle = _pushAngle + _pushes * SideSpacing + (_random.NextDouble() * 2.0 - 1.0) * spread;
            _pushes++;
            var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            _velocity += direction * _settings.PushSpeed;
            Pushed?.Invoke(direction);
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
