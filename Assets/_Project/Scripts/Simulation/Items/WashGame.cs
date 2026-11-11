using System;
using System.Numerics;
using Game.Simulation.Cleaning;

namespace Game.Simulation.Items
{
    /// <summary>Tuning for <see cref="WashGame"/>. Distances are in stick units (1 = pushed all the way).</summary>
    public sealed class WashGameSettings
    {
        /// <summary>Circles of the stick that wash one plate.</summary>
        public float CirclesPerPlate { get; set; } = 4f;

        /// <summary>Fastest circles a second that count: scrubbing faster than this washes no faster.</summary>
        public float CircleSpeed { get; set; } = 1.5f;

        /// <summary>How far the stick must be pushed for circling to count (shared with the cloth and mop).</summary>
        public float MinPush { get; set; } = 0.4f;

        /// <summary>Radians the stick must turn back before circling the other way counts (shared with the cloth and mop).</summary>
        public float TurnBack { get; set; } = 4.7f;

        /// <summary>A stick turning faster than this (radians a second) between frames was slammed, not circled.</summary>
        public float MaxTurnRate { get; set; } = 30f;

        /// <summary>Circles of motion held back to be counted at <see cref="CircleSpeed"/>.</summary>
        public float Buffer { get; set; } = 0.5f;
    }

    /// <summary>
    /// Washing a plate by hand (decision 2026-11-11, playtest round, replacing the balancing game of
    /// 2026-11-04): circle the stick, as if scrubbing the plate round. It's counted exactly as the
    /// cloth's spinning (<see cref="TraceGame"/>), so the sink and the cleaning tools are one family:
    /// either direction counts, rough shapes count (only the stick's angle matters, once it's pushed
    /// past <see cref="WashGameSettings.MinPush"/>), wagging back and forth and doing nothing never
    /// wash, and circling faster than <see cref="WashGameSettings.CircleSpeed"/> doesn't wash faster.
    /// </summary>
    public sealed class WashGame
    {
        private readonly WashGameSettings _settings;
        private readonly TraceGame _circles;

        public WashGame(WashGameSettings settings)
        {
            _settings = settings;
            _circles = new TraceGame(TraceShape.Circle, new TraceGameSettings
            {
                MinPush = settings.MinPush,
                TurnBack = settings.TurnBack,
                MaxTurnRate = settings.MaxTurnRate,
                Buffer = settings.Buffer,
                CircleSpeed = settings.CircleSpeed,
            });
        }

        /// <summary>Where the soap is, in stick units: it follows the stick round the basin.</summary>
        public Vector2 Point => _circles.Stick;

        /// <summary>True while the stick's circling is washing.</summary>
        public bool OnTrack => _circles.OnTrack;

        /// <summary>Circles counted since washing started.</summary>
        public float Circles => _circles.Loops;

        /// <summary>The push the stick needs for its circling to count, for the view.</summary>
        public float MinPush => _settings.MinPush;

        /// <returns>How much of a plate this washed (for <see cref="Sink.Wash"/>).</returns>
        public float Tick(float seconds, Vector2 stick)
        {
            var circles = _circles.Tick(seconds, stick);
            return circles / Math.Max(0.1f, _settings.CirclesPerPlate);
        }
    }
}
