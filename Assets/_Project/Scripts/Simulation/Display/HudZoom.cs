using System;

namespace Game.Simulation.Display
{
    /// <summary>How high the camera is, as far as the world-anchored HUD cares (decision 2026-11-14).</summary>
    public enum HudBand
    {
        /// <summary>The kitchen's shot, about 8 m: everything at full size.</summary>
        Near,
        /// <summary>The whole restaurant, about 10 m: shared pieces shrink with the scene and simplify.</summary>
        Middle,
        /// <summary>The street and the road, 14 m and up: shared pieces fade out.</summary>
        Far,
    }

    /// <summary>The camera heights of the bands and how pieces change in them. Tuned in <c>TuningConfig</c>.</summary>
    public sealed class HudZoomSettings
    {
        /// <summary>Metres up at which the near band becomes the middle one.</summary>
        public float MiddleFrom { get; set; } = 9f;
        /// <summary>Metres up below which the middle band is the near one again (lower than <see cref="MiddleFrom"/>, so a camera hovering between doesn't flick).</summary>
        public float NearBelow { get; set; } = 8.6f;
        /// <summary>Metres up at which the middle band becomes the far one.</summary>
        public float FarFrom { get; set; } = 13.5f;
        /// <summary>Metres up below which the far band is the middle one again.</summary>
        public float MiddleBelow { get; set; } = 12.5f;
        /// <summary>Camera height at which shared pieces are at their full size (the kitchen's shot).</summary>
        public float FullSizeAt { get; set; } = 7.9f;
        /// <summary>The smallest a shared piece shrinks to, as a share of its full size.</summary>
        public float SmallestScale { get; set; } = 0.7f;
        /// <summary>Real seconds a piece takes to change form or fade when the band (or its urgency) changes.</summary>
        public float ChangeSeconds { get; set; } = 0.25f;
        /// <summary>A guest's patience (0..1) below which their order is urgent: full size at every height.</summary>
        public float UrgentPatience { get; set; } = 0.35f;
    }

    /// <summary>
    /// How one piece of world-anchored HUD should look: its size as a share of full, how far it has
    /// gone to its simple form (0 full, 1 simple) and how visible it is (0..1).
    /// </summary>
    public readonly struct HudLook : IEquatable<HudLook>
    {
        public readonly float Scale;
        public readonly float Simple;
        public readonly float Alpha;

        public HudLook(float scale, float simple, float alpha)
        {
            Scale = scale;
            Simple = simple;
            Alpha = alpha;
        }

        /// <summary>Full size, full form, fully shown: a chef's own pieces, and anything urgent.</summary>
        public static HudLook Full => new HudLook(1f, 0f, 1f);

        public static HudLook Lerp(HudLook a, HudLook b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new HudLook(a.Scale + (b.Scale - a.Scale) * t, a.Simple + (b.Simple - a.Simple) * t, a.Alpha + (b.Alpha - a.Alpha) * t);
        }

        public bool Equals(HudLook other) => Scale.Equals(other.Scale) && Simple.Equals(other.Simple) && Alpha.Equals(other.Alpha);
        public override bool Equals(object obj) => obj is HudLook other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Scale, Simple, Alpha);
        public override string ToString() => $"(scale {Scale:0.##}, simple {Simple:0.##}, alpha {Alpha:0.##})";
    }

    /// <summary>
    /// Keeps the world-anchored HUD readable as the camera rises (decision 2026-11-14): pieces that
    /// are everyone's (orders, the cooking strips, the bell, reviews) shrink with the scene down to a
    /// smallest size, take a simple form in the middle band and fade out in the far one. A chef's
    /// own pieces and anything about to go wrong stay as they are. One of these per HUD piece, ticked
    /// with the camera's height each frame.
    ///
    /// The band changes at once, with a gap between the heights going up and coming down, and the
    /// form and fade then ease over <see cref="HudZoomSettings.ChangeSeconds"/>: a camera easing
    /// between shots crosses a band once, and one hovering at the edge doesn't flick. Size follows
    /// the height smoothly, as the scene does.
    /// </summary>
    public sealed class HudZoom
    {
        private bool _started;

        public HudBand Band { get; private set; } = HudBand.Near;

        /// <summary>How far shared pieces have gone to their simple form, 0..1.</summary>
        public float Simple { get; private set; }

        /// <summary>How far shared pieces have faded out, 0..1.</summary>
        public float Hidden { get; private set; }

        /// <summary>Size of shared pieces, as a share of full.</summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>How a shared piece looks now.</summary>
        public HudLook Shared => new HudLook(Scale, Simple, 1f - Hidden);

        /// <summary>
        /// How a shared piece looks now given how urgent it is (0..1, eased by its owner with
        /// <see cref="Ease"/>): at 1 it's as a chef's own piece, full size at every height.
        /// </summary>
        public HudLook For(float urgency) => HudLook.Lerp(Shared, HudLook.Full, urgency);

        /// <param name="height">The camera's height above the floor, in metres.</param>
        /// <param name="deltaTime">Real seconds since the last tick.</param>
        public void Tick(float height, float deltaTime, HudZoomSettings settings)
        {
            Band = BandAt(height, _started ? Band : FreshBand(height, settings), settings);
            Scale = ScaleAt(height, settings);
            var simple = Band == HudBand.Near ? 0f : 1f;
            var hidden = Band == HudBand.Far ? 1f : 0f;
            // The first tick lands at once: a scene that opens high shouldn't fade its HUD out.
            Simple = _started ? Ease(Simple, simple, deltaTime, settings.ChangeSeconds) : simple;
            Hidden = _started ? Ease(Hidden, hidden, deltaTime, settings.ChangeSeconds) : hidden;
            _started = true;
        }

        /// <summary>The band at <paramref name="height"/>, coming from <paramref name="previous"/>: each change up has a higher threshold than the one back down.</summary>
        public static HudBand BandAt(float height, HudBand previous, HudZoomSettings settings)
        {
            switch (previous)
            {
                case HudBand.Near:
                    if (height >= settings.FarFrom)
                        return HudBand.Far;
                    return height >= settings.MiddleFrom ? HudBand.Middle : HudBand.Near;
                case HudBand.Middle:
                    if (height >= settings.FarFrom)
                        return HudBand.Far;
                    return height < settings.NearBelow ? HudBand.Near : HudBand.Middle;
                default:
                    if (height >= settings.MiddleBelow)
                        return HudBand.Far;
                    return height < settings.NearBelow ? HudBand.Near : HudBand.Middle;
            }
        }

        // With no band to come from, the upward thresholds decide.
        private static HudBand FreshBand(float height, HudZoomSettings settings) =>
            height >= settings.FarFrom ? HudBand.Far : height >= settings.MiddleFrom ? HudBand.Middle : HudBand.Near;

        /// <summary>
        /// Size of a shared piece at <paramref name="height"/>: the pieces are drawn a fixed size on
        /// screen, while the scene shrinks as one over the height (the camera's depth to the floor
        /// grows in step with its height at a fixed tilt), so full size × <c>FullSizeAt / height</c>
        /// shrinks them with it. Never above full, never below the smallest readable share.
        /// </summary>
        public static float ScaleAt(float height, HudZoomSettings settings)
        {
            if (height <= 0f)
                return 1f;
            var smallest = Math.Clamp(settings.SmallestScale, 0f, 1f);
            return Math.Clamp(settings.FullSizeAt / height, smallest, 1f);
        }

        /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> at a steady rate that takes <paramref name="seconds"/> for the whole way.</summary>
        public static float Ease(float current, float target, float deltaTime, float seconds)
        {
            if (seconds <= 0f)
                return target;
            var step = deltaTime / seconds;
            return current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
        }
    }
}
