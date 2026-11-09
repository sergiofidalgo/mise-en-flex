using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// The maths behind the office's ambient life (user's note, 2026-11-09: birds, flickering
    /// windows, people on their terraces). Everything is a pure function of time and a phase, so
    /// many things share one clock without moving in step, and it can be tested without Play mode.
    /// </summary>
    public static class AmbientMotion
    {
        /// <summary>A smooth swing between −1 and 1, once every <paramref name="period"/> seconds.</summary>
        public static float Wave(float time, float period, float phase) =>
            Mathf.Sin(2f * Mathf.PI * (time / Mathf.Max(period, 0.01f) + phase));

        /// <summary>
        /// A short bump from 0 up to 1 and back, taking <paramref name="duty"/> of each period, and
        /// 0 for the rest: for a peck, a glance, an arm raised to a mouth. A half sine, so it starts
        /// and ends gently.
        /// </summary>
        public static float Pulse(float time, float period, float duty, float phase)
        {
            var cycle = Mathf.Repeat(time / Mathf.Max(period, 0.01f) + phase, 1f);
            duty = Mathf.Clamp(duty, 0.001f, 1f);
            return cycle < duty ? Mathf.Sin(Mathf.PI * cycle / duty) : 0f;
        }

        /// <summary>Whether a blinking light is on: for the first <paramref name="onFraction"/> of each period.</summary>
        public static bool BlinkOn(float time, float period, float onFraction, float phase) =>
            Mathf.Repeat(time / Mathf.Max(period, 0.01f) + phase, 1f) < onFraction;

        /// <summary>A fire's brightness: a restless 0.65–1.15, never still, never out.</summary>
        public static float Fire(float time, float seed) =>
            0.65f + 0.5f * Mathf.Clamp01(0.6f * Mathf.PerlinNoise(time * 2.3f, seed) + 0.4f * Mathf.PerlinNoise(time * 7.1f, seed + 13.7f));

        /// <summary>
        /// A television's light: a new brightness between 0.35 and 1 every <paramref name="step"/>
        /// seconds, jumping rather than easing, as cuts between shots do.
        /// </summary>
        public static float Television(float time, float step, float seed)
        {
            var shot = Mathf.FloorToInt(time / Mathf.Max(step, 0.01f));
            return 0.35f + 0.65f * Hash01(shot, seed);
        }

        /// <summary>
        /// A failing fluorescent tube: on, except for a short burst once every
        /// <paramref name="period"/> seconds when it stutters on and off.
        /// </summary>
        public static bool FaultyOn(float time, float period, float seed)
        {
            period = Mathf.Max(period, 0.5f);
            var cycle = Mathf.FloorToInt(time / period);
            var into = time - cycle * period;
            // Each cycle's burst starts somewhere different and lasts 0.6 s.
            var burstStart = Hash01(cycle, seed) * (period - 0.6f);
            if (into < burstStart || into > burstStart + 0.6f)
                return true;
            // In the burst: off and on in uneven slices.
            var slice = Mathf.FloorToInt((into - burstStart) / 0.07f);
            return Hash01(slice + cycle * 31, seed + 5f) > 0.55f;
        }

        // A repeatable 0–1 from a whole number and a seed.
        private static float Hash01(int n, float seed)
        {
            var x = Mathf.Sin(n * 12.9898f + seed * 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        /// <summary>
        /// A point on a loop round <paramref name="centre"/>: an ellipse across the ground with the
        /// given radii, rising and dipping by <paramref name="lift"/> twice a lap, as a flock wheels.
        /// </summary>
        public static Vector3 Loop(Vector3 centre, Vector2 radii, float lift, float time, float period, float phase, bool clockwise)
        {
            var angle = 2f * Mathf.PI * (time / Mathf.Max(period, 0.01f) + phase) * (clockwise ? -1f : 1f);
            return centre + new Vector3(Mathf.Cos(angle) * radii.x, Mathf.Sin(2f * angle) * lift, Mathf.Sin(angle) * radii.y);
        }
    }
}
