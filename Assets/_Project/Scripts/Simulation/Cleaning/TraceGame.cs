using System;
using System.Numerics;

namespace Game.Simulation.Cleaning
{
    /// <summary>The shape a tool traces (decision 2026-11-10).</summary>
    public enum TraceShape
    {
        /// <summary>The cloth, on tables and counters: small circles.</summary>
        Circle,

        /// <summary>The mop, on the floor: a wide figure-eight.</summary>
        FigureEight,

        /// <summary>The extinguisher: side-to-side sweeps across the base of the flames.</summary>
        Sweep,
    }

    /// <summary>Tuning for <see cref="TraceGame"/>. Distances are in stick units (1 = pushed all the way).</summary>
    public sealed class TraceGameSettings
    {
        /// <summary>
        /// Below this push the stick reads as centred (a thumb resting, or a worn stick's drift). A
        /// centred stick can only pull the marker where the path runs near the middle, such as the
        /// crossing of the mop's eight: everywhere else the path is out of its reach.
        /// </summary>
        public float DeadZone { get; set; } = 0.15f;

        /// <summary>
        /// Stick units a second. The stick moving faster than this (slammed about rather than
        /// steered) doesn't pull the marker. Circling the rim at a loop a second is about 6.
        /// </summary>
        public float MaxStickSpeed { get; set; } = 15f;

        /// <summary>How far from the path the stick can be and still pull the marker along. Wide = forgiving.</summary>
        public float Tolerance { get; set; } = 0.4f;

        /// <summary>How far ahead along the path (in loops) the marker can be pulled from where it is.</summary>
        public float Reach { get; set; } = 0.15f;

        /// <summary>Fastest loops a second, per shape: the cloth's circles are quickest, the mop's eights slowest.</summary>
        public float CircleSpeed { get; set; } = 1.1f;
        public float FigureEightSpeed { get; set; } = 0.7f;
        public float SweepSpeed { get; set; } = 0.9f;

        public float SpeedFor(TraceShape shape) => shape switch
        {
            TraceShape.FigureEight => FigureEightSpeed,
            TraceShape.Sweep => SweepSpeed,
            _ => CircleSpeed,
        };
    }

    /// <summary>
    /// Cleaning and putting out fires by hand (game design §15.8, decision 2026-11-10): a path lies
    /// on the dirt, and the stick, held where the path is, pulls a marker round it. Each loop wipes
    /// one layer. It's about moving, where the sink is about holding still: the stick's position is
    /// the point (not its speed), so circling the stick draws the circle.
    ///
    /// The marker only goes forward, and only work past the furthest point reached counts, so
    /// wiggling at one spot does nothing. Pulled too far off the path, or let go, it waits.
    /// </summary>
    public sealed class TraceGame
    {
        // Points tested along the reachable stretch when finding where the stick is on the path.
        private const int SearchSteps = 24;

        private readonly TraceGameSettings _settings;
        private double _along;
        private double _furthest;
        private bool _hasStick;

        /// <param name="start">Where on the loop to start (0..1), so each patch begins somewhere new.</param>
        public TraceGame(TraceShape shape, TraceGameSettings settings, float start = 0f)
        {
            Shape = shape;
            _settings = settings;
            _along = _furthest = start - Math.Floor(start);
            StartedAt = _along;
        }

        public TraceShape Shape { get; }

        /// <summary>Where the marker is along the loop, 0..1.</summary>
        public float Along => (float)(_along - Math.Floor(_along));

        /// <summary>The marker, in stick units.</summary>
        public Vector2 Marker => PointAt(Shape, Along);

        /// <summary>Loops done since it started (wipes given).</summary>
        public float Loops => (float)(_furthest - StartedAt);

        /// <summary>True while the stick is pulling the marker along.</summary>
        public bool OnTrack { get; private set; }

        /// <summary>The stick as last read, for the view's hint.</summary>
        public Vector2 Stick { get; private set; }

        private double StartedAt { get; }

        /// <summary>Moves the marker towards where the stick is on the path.</summary>
        /// <returns>Wipes done this tick (loops of new path traced).</returns>
        public float Tick(float seconds, Vector2 stick)
        {
            if (stick.LengthSquared() > 1f)
                stick = Vector2.Normalize(stick);
            if (stick.Length() < _settings.DeadZone)
                stick = Vector2.Zero;
            var jumped = _hasStick && seconds > 0f && Vector2.Distance(stick, Stick) / seconds > _settings.MaxStickSpeed;
            Stick = stick;
            _hasStick = true;
            OnTrack = false;
            if (seconds <= 0f || jumped)
                return 0f;

            // The point on the path nearest the stick, from a little behind the marker to its reach
            // ahead: looking only nearby is what tells the two crossings of an eight apart.
            var behind = _settings.Reach * 0.5;
            var bestAt = _along;
            var bestDistance = float.MaxValue;
            for (var i = 0; i <= SearchSteps; i++)
            {
                var at = _along - behind + (behind + _settings.Reach) * i / SearchSteps;
                var distance = Vector2.Distance(PointAt(Shape, (float)(at - Math.Floor(at))), stick);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAt = at;
                }
            }
            if (bestDistance > _settings.Tolerance)
                return 0f;

            OnTrack = true;
            var step = Math.Min(bestAt - _along, _settings.SpeedFor(Shape) * seconds);
            _along += step;
            if (_along <= _furthest)
                return 0f;
            var gained = _along - _furthest;
            _furthest = _along;
            return (float)gained;
        }

        /// <summary>
        /// A point on the shape, in stick units: up on the stick is +Y. Each shape fits the stick's
        /// circle with room to spare, so it can be traced with the stick near its rim.
        /// </summary>
        public static Vector2 PointAt(TraceShape shape, float along)
        {
            var angle = along * Math.PI * 2.0;
            switch (shape)
            {
                case TraceShape.FigureEight:
                    // A lemniscate of Gerono laid on its side: two lobes left and right, crossing in the middle.
                    return new Vector2((float)(0.8 * Math.Sin(angle)), (float)(0.45 * Math.Sin(2.0 * angle)));
                case TraceShape.Sweep:
                {
                    // Left to right and back along a shallow arc that bows upwards, as a nozzle sweeps
                    // the base of a fire. The way back runs a little lower, so the loop has a width.
                    var x = 0.8 * Math.Sin(angle);
                    var bow = 1.0 - (x / 0.8) * (x / 0.8);
                    var lane = 0.08 * Math.Cos(angle);
                    return new Vector2((float)x, (float)(-0.2 + 0.35 * bow + lane));
                }
                default:
                    return new Vector2((float)(0.72 * Math.Cos(angle)), (float)(0.72 * Math.Sin(angle)));
            }
        }
    }
}
