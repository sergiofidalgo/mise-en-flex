using System;
using Game.Simulation.Cleaning;
using Game.Simulation.Items;

namespace Game.Simulation.Realism
{
    /// <summary>How strongly the minigame complexity dial bends the stick games.</summary>
    public sealed class MinigameComplexitySettings
    {
        /// <summary>
        /// Doublings of forgiveness from the middle of the dial to its end: 1 means complexity 0 is
        /// √2 times as forgiving as 0.5, and complexity 1 is 1/√2 as forgiving.
        /// </summary>
        public float Strength { get; set; } = 1f;

    }

    /// <summary>
    /// The minigame complexity dial (§12; decision 2026-11-10) applied to the stick games: the sink's
    /// circles and the cleaning/extinguisher spin and sweep. 0.5 leaves the tuning exactly as designed.
    /// Everything scales by one "ease" factor, so a dial step feels the same in every game: the push
    /// needed shrinks by it and the speed that counts grows by it.
    /// </summary>
    public static class MinigameComplexity
    {
        /// <summary>
        /// The forgiveness factor: 1 at 0.5, above 1 easier, below 1 harder. Exponential, so going
        /// one step easier and one step harder cancel out.
        /// </summary>
        public static float Ease(float complexity, MinigameComplexitySettings settings)
        {
            var c = Math.Clamp(complexity, 0f, 1f);
            return (float)Math.Pow(2.0, (0.5f - c) * settings.Strength);
        }

        public static WashGameSettings Scale(WashGameSettings wash, float complexity, MinigameComplexitySettings settings)
        {
            var ease = Ease(complexity, settings);
            return new WashGameSettings
            {
                // As the cloth (below): a lighter push counts and faster circling counts. The circles a
                // plate takes stay the same, so easier is quicker, never different.
                CirclesPerPlate = wash.CirclesPerPlate,
                CircleSpeed = wash.CircleSpeed * ease,
                MinPush = wash.MinPush / ease,
                TurnBack = wash.TurnBack,
                MaxTurnRate = wash.MaxTurnRate,
                Buffer = wash.Buffer,
            };
        }

        /// <summary>
        /// How much of each ingredient's sweet-spot step the cutting dial uses (decision 2026-11-11):
        /// none up to 0.25 (relaxed: pure cadence), rising to all of it at 0.5 (as tuned) and half as
        /// much again at 1.
        /// </summary>
        public static float SweetSpotStepScale(float complexity)
        {
            var c = Math.Clamp(complexity, 0f, 1f);
            return c <= 0.5f ? Math.Clamp((c - 0.25f) / 0.25f, 0f, 1f) : 1f + (c - 0.5f);
        }

        public static TraceGameSettings Scale(TraceGameSettings trace, float complexity, MinigameComplexitySettings settings)
        {
            var ease = Ease(complexity, settings);
            return new TraceGameSettings
            {
                // A lighter push and a shorter sweep count, and faster loops count: quicker, sloppier cleaning.
                MinPush = trace.MinPush / ease,
                TurnBack = trace.TurnBack,
                MaxTurnRate = trace.MaxTurnRate,
                MaxSweepRate = trace.MaxSweepRate,
                Buffer = trace.Buffer,
                SweepEdge = Math.Min(0.95f, trace.SweepEdge / ease),
                CircleSpeed = trace.CircleSpeed * ease,
                WideCircleSpeed = trace.WideCircleSpeed * ease,
                SweepSpeed = trace.SweepSpeed * ease,
            };
        }
    }
}
