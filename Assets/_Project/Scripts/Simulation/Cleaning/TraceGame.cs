using System;
using System.Numerics;

namespace Game.Simulation.Cleaning
{
    /// <summary>The motion a tool wants (decision 2026-11-11: spin and sweep).</summary>
    public enum TraceShape
    {
        /// <summary>The cloth, on tables and counters: spin the stick round, quickly.</summary>
        Circle,

        /// <summary>The mop, on the floor: wag the stick from side to side (the user's call, 2026-11-12), a little slower than the extinguisher.</summary>
        MopSweep,

        /// <summary>The extinguisher: wag the stick from side to side across the flames.</summary>
        Sweep,
    }

    /// <summary>Tuning for <see cref="TraceGame"/>. Distances are in stick units (1 = pushed all the way).</summary>
    public sealed class TraceGameSettings
    {
        /// <summary>How far the stick must be pushed for its turning to count (cloth and mop). A thumb resting, or a worn stick's drift, does nothing.</summary>
        public float MinPush { get; set; } = 0.4f;

        /// <summary>
        /// Radians the stick must turn back the other way before spinning that way counts instead
        /// (cloth and mop). Large, so wagging back and forth never adds up: going back 270° is
        /// clearly spinning the other way.
        /// </summary>
        public float TurnBack { get; set; } = 4.7f;

        /// <summary>How far to each side the stick must go for a sweep to reach that side (extinguisher).</summary>
        public float SweepEdge { get; set; } = 0.55f;

        /// <summary>
        /// Fastest a hand turns or sweeps the stick: radians a second (spinning) and stick units a
        /// second (sweeping). A stick that jumps faster than this between two frames was slammed,
        /// not moved, and doesn't count. A brisk 2.5 turns a second is about 16 rad/s.
        /// </summary>
        public float MaxTurnRate { get; set; } = 30f;
        public float MaxSweepRate { get; set; } = 25f;

        /// <summary>
        /// Loops of motion held back to be counted at the tool's speed: a sweep crosses the middle
        /// far faster than its average, and this keeps that burst rather than throwing it away.
        /// </summary>
        public float Buffer { get; set; } = 0.5f;

        /// <summary>
        /// Fastest loops a second that count, per tool: spinning (or sweeping) faster than this
        /// cleans no faster, so slamming the stick isn't rewarded. A loop is a full turn, or a sweep
        /// there and back. The cloth is quickest, the mop a little slower than the extinguisher.
        /// </summary>
        public float CircleSpeed { get; set; } = 1.1f;
        public float MopSpeed { get; set; } = 0.8f;
        public float SweepSpeed { get; set; } = 0.9f;

        public float SpeedFor(TraceShape shape) => shape switch
        {
            TraceShape.MopSweep => MopSpeed,
            TraceShape.Sweep => SweepSpeed,
            _ => CircleSpeed,
        };
    }

    /// <summary>
    /// Cleaning and putting out fires by hand (game design §15.8; decisions 2026-11-10 and
    /// 2026-11-11). It's about moving, where the sink is about holding still, but with the sink's
    /// forgiveness (the user's call, after the traced path proved near impossible at times): no
    /// path to stay on. The cloth and the mop clean while the stick is spun round, either way;
    /// the mop and the extinguisher while it's wagged from side to side. Faster cleans faster, up to the tool's
    /// speed. Each loop wipes one layer.
    ///
    /// Only new progress counts: turning the stick a little back and forth, or jiggling it in the
    /// middle, adds nothing.
    /// </summary>
    public sealed class TraceGame
    {
        private readonly TraceGameSettings _settings;
        private double _loops;
        private double _pending;

        // Spinning: the stick's last angle, the way round it's going, and how far it has turned
        // that way since it started (or turned back), and the furthest it has been.
        private bool _hasAngle;
        private double _lastAngle;
        private int _turning;
        private double _turned;
        private double _furthestTurned;

        // Sweeping: where the stick first was, the side it's heading for (+1 right, -1 left, 0 until
        // it first moves) and the furthest across this leg.
        private bool _hasSweep;
        private double _sweepStart;
        private double _lastX;
        private int _heading;
        private double _furthestAcross;

        /// <param name="start">Where on the loop to start (0..1), so each patch begins somewhere new.</param>
        public TraceGame(TraceShape shape, TraceGameSettings settings, float start = 0f)
        {
            Shape = shape;
            _settings = settings;
            Start = start - (float)Math.Floor(start);
        }

        public TraceShape Shape { get; }

        /// <summary>Where on the loop it is, 0..1: the start plus the loops done so far.</summary>
        public float Along => (float)((Start + _loops) - Math.Floor(Start + _loops));

        /// <summary>Where the tool's head is, in stick units: it follows the stick.</summary>
        public Vector2 Marker => Stick;

        /// <summary>Loops done since it started (wipes given).</summary>
        public float Loops => (float)_loops;

        /// <summary>True while the stick's motion is cleaning.</summary>
        public bool OnTrack { get; private set; }

        /// <summary>The stick as last read.</summary>
        public Vector2 Stick { get; private set; }

        private float Start { get; }

        /// <returns>Wipes done this tick (loops of new motion).</returns>
        public float Tick(float seconds, Vector2 stick)
        {
            if (stick.LengthSquared() > 1f)
                stick = Vector2.Normalize(stick);
            Stick = stick;
            OnTrack = false;
            if (seconds <= 0f)
                return 0f;

            var moved = Shape == TraceShape.Circle ? Spin(stick, seconds) : Sweep(stick, seconds);
            // Counted at up to the tool's speed: motion faster than that waits in a small buffer
            // (so a sweep's quick middle isn't lost) and what overflows it cleans no faster.
            _pending = Math.Min(_pending + moved, Math.Max(_settings.Buffer, 0.0));
            var counted = Math.Min(_pending, _settings.SpeedFor(Shape) * seconds);
            if (counted <= 0.0)
                return 0f;
            _pending -= counted;
            _loops += counted;
            OnTrack = true;
            return (float)counted;
        }

        // Loops of new turning: only past the furthest the stick has turned this way counts.
        private double Spin(Vector2 stick, float seconds)
        {
            if (stick.Length() < _settings.MinPush)
            {
                _hasAngle = false; // let go: picking it up again elsewhere isn't a turn
                return 0.0;
            }
            var angle = Math.Atan2(stick.Y, stick.X);
            if (!_hasAngle)
            {
                _hasAngle = true;
                _lastAngle = angle;
                return 0.0;
            }

            var change = angle - _lastAngle;
            change -= Math.Round(change / (2.0 * Math.PI)) * 2.0 * Math.PI; // the short way round
            _lastAngle = angle;
            if (Math.Abs(change) > _settings.MaxTurnRate * seconds)
                return 0.0; // slammed, not turned
            if (_turning == 0)
            {
                if (Math.Abs(change) < 1e-6)
                    return 0.0;
                _turning = Math.Sign(change);
            }

            _turned += change * _turning;
            if (_turned > _furthestTurned)
            {
                var gained = _turned - _furthestTurned;
                _furthestTurned = _turned;
                return gained / (2.0 * Math.PI);
            }
            if (_turned < _furthestTurned - _settings.TurnBack)
            {
                // Clearly spinning the other way now: that way counts from here.
                _turning = -_turning;
                _turned = 0.0;
                _furthestTurned = 0.0;
            }
            return 0.0;
        }

        // Loops of new sweeping: each leg runs from one side to the other (half a loop); only
        // ground past the furthest reached on this leg counts.
        private double Sweep(Vector2 stick, float seconds)
        {
            var edge = Math.Max(0.05, _settings.SweepEdge);
            var jumped = _hasSweep && Math.Abs(stick.X - _lastX) > _settings.MaxSweepRate * seconds;
            _lastX = stick.X;
            if (jumped)
                return 0.0; // slammed, not swept
            var x = Math.Clamp(stick.X, -edge, edge);
            if (!_hasSweep)
            {
                _hasSweep = true;
                _sweepStart = x;
                return 0.0;
            }
            if (_heading == 0)
            {
                // The first push either way sets the first leg's way, counting from where it began.
                if (Math.Abs(x - _sweepStart) < 0.05)
                    return 0.0;
                _heading = Math.Sign(x - _sweepStart);
                _furthestAcross = Across(_sweepStart, edge);
            }

            var across = Across(x, edge);
            if (across <= _furthestAcross)
                return 0.0;
            var gained = across - _furthestAcross;
            _furthestAcross = across;
            if (across >= 1.0)
            {
                // Reached the side: the next leg heads back.
                _heading = -_heading;
                _furthestAcross = 0.0;
            }
            return gained * 0.5;
        }

        // 0 at the side it's leaving, 1 at the side it's heading for.
        private double Across(double x, double edge) => (x * _heading + edge) / (2.0 * edge);

        /// <summary>
        /// The motion as a path in stick units, for hints and tests: up on the stick is +Y. A
        /// circle for the cloth, side to side for the mop and the extinguisher.
        /// </summary>
        public static Vector2 PointAt(TraceShape shape, float along)
        {
            var angle = along * Math.PI * 2.0;
            switch (shape)
            {
                case TraceShape.MopSweep:
                    return new Vector2((float)(0.82 * Math.Sin(angle)), (float)(0.08 * Math.Cos(angle)));
                case TraceShape.Sweep:
                    return new Vector2((float)(0.8 * Math.Sin(angle)), (float)(0.1 * Math.Cos(angle)));
                default:
                    return new Vector2((float)(0.72 * Math.Cos(angle)), (float)(0.72 * Math.Sin(angle)));
            }
        }
    }
}
