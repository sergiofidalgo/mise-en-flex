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
