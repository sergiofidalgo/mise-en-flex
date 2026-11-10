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

        /// <summary>Extra pushes per plate over the whole dial (wash game): 4 means 0 → two fewer, 1 → two more.</summary>
        public float CorrectionsSpan { get; set; } = 4f;
    }

    /// <summary>
    /// The minigame complexity dial (§12; decision 2026-11-10) applied to the stick games: the sink's
    /// balance game and the cleaning/extinguisher trace. 0.5 leaves the tuning exactly as designed.
    /// Everything scales by one "ease" factor, so a dial step feels the same in every game:
    /// targets grow by it and the forces that pull the point away shrink by it.
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
            var okRadius = Math.Min(0.9f, wash.OkRadius * ease);
            return new WashGameSettings
            {
                StickAcceleration = wash.StickAcceleration,
                // Less run-away force while the targets grow by the same factor, so Instability × OkRadius
                // (what the random nudge must stay under, see WashGameSettings.Nudge) doesn't change.
                Instability = wash.Instability / ease,
                Damping = wash.Damping,
                Nudge = wash.Nudge,
                NudgeInterval = wash.NudgeInterval,
                GoodRadius = Math.Min(okRadius, wash.GoodRadius * ease),
                OkRadius = okRadius,
                HoldSeconds = wash.HoldSeconds,
                Corrections = Math.Max(0, wash.Corrections + (int)Math.Round((Math.Clamp(complexity, 0f, 1f) - 0.5f) * settings.CorrectionsSpan, MidpointRounding.AwayFromZero)),
                PushSpeed = wash.PushSpeed,
                PushSpreadDegrees = wash.PushSpreadDegrees,
                // Still outside the bigger OK ring, so every plate starts with bringing the point in.
                StartRadius = Math.Max(wash.StartRadius, Math.Min(0.95f, okRadius + 0.1f)),
            };
        }

        public static TraceGameSettings Scale(TraceGameSettings trace, float complexity, MinigameComplexitySettings settings)
        {
            var ease = Ease(complexity, settings);
            return new TraceGameSettings
            {
                DeadZone = trace.DeadZone,
                // A wider band round the path, and faster loops allowed: quicker, sloppier cleaning.
                Tolerance = trace.Tolerance * ease,
                MaxStickSpeed = trace.MaxStickSpeed * ease,
                Reach = trace.Reach,
                CircleSpeed = trace.CircleSpeed * ease,
                FigureEightSpeed = trace.FigureEightSpeed * ease,
                SweepSpeed = trace.SweepSpeed * ease,
            };
        }
    }
}
