using UnityEngine;

namespace Game.Presentation
{
    /// <summary>The rules behind how sounds are mixed, apart from the engine so they can be tested.</summary>
    public static class SoundMath
    {
        /// <summary>0 below <paramref name="from"/> °C, rising to 1 at <paramref name="full"/> °C.</summary>
        public static float SizzleLevel(float temperature, float from, float full) =>
            Mathf.InverseLerp(from, full, temperature);

        /// <summary>
        /// Silent at level 0, then starts at <paramref name="quiet"/> rather than rising from nothing:
        /// players need to tell "barely sizzling" apart from "not sizzling", as a real pan does.
        /// </summary>
        public static float SizzleVolume(float level, float quiet, float loud) =>
            level <= 0f ? 0f : Mathf.Lerp(quiet, loud, level);

        /// <summary>
        /// Stereo position from a place on screen. Deliberately not 3D sound: under a camera that
        /// frames everyone, distance fade would hide a burning pan just when it's far from the
        /// action, so everything stays audible and only pans left and right.
        /// </summary>
        /// <param name="viewportX">0 at the screen's left edge, 1 at its right; may be off screen.</param>
        /// <param name="width">0 keeps everything centred; 1 reaches hard left and right at the edges.</param>
        public static float StereoPan(float viewportX, float width) =>
            Mathf.Clamp((viewportX - 0.5f) * 2f, -1f, 1f) * width;

        /// <summary>True if a cue last played at <paramref name="lastPlayed"/> may play again now.</summary>
        public static bool CanPlay(float now, float? lastPlayed, float minInterval) =>
            lastPlayed == null || now - lastPlayed.Value >= minInterval;

        /// <summary>
        /// Picks a take other than the last one, so two chops in a row always differ.
        /// </summary>
        /// <param name="roll">A random number in 0..1.</param>
        /// <param name="last">The last take played, or a negative number for none.</param>
        public static int NextTake(int count, int last, float roll)
        {
            if (count <= 1)
                return 0;

            var hasLast = last >= 0 && last < count;
            var choices = hasLast ? count - 1 : count;
            var pick = Mathf.Min(Mathf.FloorToInt(roll * choices), choices - 1);
            // Skip over the last take by numbering the remaining ones around it.
            return hasLast && pick >= last ? pick + 1 : pick;
        }
    }
}
