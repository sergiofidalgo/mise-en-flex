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

        /// <summary>
        /// Where along a lane of <paramref name="length"/> metres something is at <paramref name="time"/>,
        /// and whether it's heading forward. Driving (no ping-pong) runs to the end and starts again;
        /// walking (ping-pong) goes to the end, waits <paramref name="pause"/> seconds, comes back,
        /// waits again.
        /// </summary>
        public static (float Along, bool Forward) Lane(float time, float length, float speed, bool pingPong, float pause, float phase)
        {
            length = Mathf.Max(length, 0.01f);
            speed = Mathf.Max(speed, 0.01f);
            var leg = length / speed;
            if (!pingPong)
            {
                var t = Mathf.Repeat(time / leg + phase, 1f);
                return (t * length, true);
            }
            var cycle = 2f * (leg + pause);
            var at = Mathf.Repeat(time + phase * cycle, cycle);
            if (at < leg)
                return (at * speed, true);
            if (at < leg + pause)
                return (length, true);
            at -= leg + pause;
            if (at < leg)
                return (length - at * speed, false);
            return (0f, false);
        }

        /// <summary>
        /// A camera flash going off (the avenue's selfies and photographers): once in each
        /// <paramref name="period"/> seconds, at a different moment each time, a bright pop that dies
        /// away in a fifth of a second; about one in three is a phone's double flash. 0 between.
        /// </summary>
        public static float Flash(float time, float period, float seed)
        {
            period = Mathf.Max(period, 1f);
            var cycle = Mathf.FloorToInt(time / period);
            var into = time - cycle * period;
            var start = Hash01(cycle, seed) * (period - 0.6f);
            var pop = Pop(into - start);
            if (Hash01(cycle + 7, seed + 3f) < 0.35f)
                pop = Mathf.Max(pop, Pop(into - start - 0.28f)); // the photo after the pre-flash
            return pop;
        }

        // A flash from its first instant: full at once, fading over about 0.2 s.
        private static float Pop(float since) => since < 0f || since > 0.22f ? 0f : Mathf.Exp(-since / 0.05f);

        /// <summary>
        /// A social-media pop over a phone (user's note, 2026-11-10: the game is about social media
        /// and reviews): once in each <paramref name="period"/> seconds, at a different moment each
        /// time, a heart or star rises for <paramref name="duration"/> seconds. Returns how far
        /// through its rise it is (0–1), or −1 between pops.
        /// </summary>
        public static float Pop(float time, float period, float duration, float seed)
        {
            period = Mathf.Max(period, duration + 0.1f);
            var cycle = Mathf.FloorToInt(time / period);
            var into = time - cycle * period;
            var start = Hash01(cycle, seed + 2f) * (period - duration);
            var progress = (into - start) / Mathf.Max(duration, 0.01f);
            return progress < 0f || progress >= 1f ? -1f : progress;
        }

        /// <summary>Which of <paramref name="kinds"/> icons this period's pop shows.</summary>
        public static int PopKind(float time, float period, float seed, int kinds)
        {
            var cycle = Mathf.FloorToInt(time / Mathf.Max(period, 0.1f));
            return Mathf.Min(kinds - 1, Mathf.FloorToInt(Hash01(cycle + 17, seed) * kinds));
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
