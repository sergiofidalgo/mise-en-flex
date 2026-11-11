using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// How food shows its age (the user's call, 2026-11-11). Fresh food looks as it is. Below the
    /// stale line (35%) it dulls, a greyish brown creeping in as freshness falls. Below the spoiled
    /// line (10%, the same line the overnight clean-up throws food out at) it goes a mouldy grey-green
    /// and grows fuzzy mould spots. Pure colour maths, so it's tested without Play mode; the views
    /// apply it to their renderers.
    /// </summary>
    public static class FreshnessLook
    {
        public enum Stage
        {
            Fresh,
            Stale,
            Mouldy,
        }

        // What old food fades towards: dull, a little brown, a little dark.
        private static readonly Color StaleColour = new Color(0.42f, 0.38f, 0.30f);
        // How far it fades: a clear step as soon as it's stale, so crossing the line shows, and more
        // by the time it's spoiled. Enough to read from the overhead camera, while it still reads as
        // what it is.
        private const float FirstStale = 0.3f;
        private const float MostStale = 0.6f;
        // The mouldy cast on top of that.
        private static readonly Color MouldColour = new Color(0.46f, 0.52f, 0.36f);
        private const float Mould = 0.35f;

        public static Stage StageOf(float freshness, float staleBelow, float mouldyBelow) =>
            freshness < mouldyBelow ? Stage.Mouldy : freshness < staleBelow ? Stage.Stale : Stage.Fresh;

        /// <summary><paramref name="raw"/> as food at <paramref name="freshness"/> shows it.</summary>
        public static Color Tint(Color raw, float freshness, float staleBelow, float mouldyBelow)
        {
            if (freshness >= staleBelow)
                return raw;
            // 0 just under the stale line, 1 at the spoiled line and beyond.
            var gone = Mathf.InverseLerp(staleBelow, mouldyBelow, freshness);
            var colour = Color.Lerp(raw, StaleColour, Mathf.Lerp(FirstStale, MostStale, gone));
            if (freshness < mouldyBelow)
                colour = Color.Lerp(colour, MouldColour, Mould);
            colour.a = raw.a;
            return colour;
        }
    }
}
