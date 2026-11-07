using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Timing for small presentation motions that follow an on/off state, like a fridge door or a
    /// chef bending: an amount that moves between 0 and 1 at its own speed each way, and easings
    /// that turn it into motion that feels physical rather than mechanical.
    /// </summary>
    public static class Swing
    {
        /// <summary>Moves <paramref name="amount"/> towards 1 when on, or 0 when off, over the given seconds.</summary>
        public static float Step(float amount, bool on, float onSeconds, float offSeconds, float deltaTime)
        {
            var seconds = Mathf.Max(on ? onSeconds : offSeconds, 0.0001f);
            return Mathf.MoveTowards(amount, on ? 1f : 0f, deltaTime / seconds);
        }

        /// <summary>
        /// Ease-out with a small overshoot: fast at first, swinging slightly past 1 before settling,
        /// like a door thrown open. 0 and 1 map exactly to themselves.
        /// </summary>
        public static float EaseOutBack(float t, float overshoot = 1.2f)
        {
            t = Mathf.Clamp01(t);
            var c3 = overshoot + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }
    }
}
