using System;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The pacer on a spinning ring (the sink and the cloth; the user's call, 2026-11-14): a pale
    /// bubble with a short tapering trail that runs round the rim at the pace that cleans fastest,
    /// the way the player is turning (clockwise until they've turned). Chasing it with the soap or
    /// the cloth shows both the way and the speed, right where the player is looking. It shows at
    /// the start and whenever the circling falls short of full speed, and fades once they keep up.
    /// The pieces are made by the view that owns the ring (they share its kit, order and material).
    /// Angles are in the kit's frame: +x screen right, +z screen up, anticlockwise positive.
    /// </summary>
    public sealed class SpinPacer
    {
        // The lead bubble's size, then the trail's, as shares of the dot size.
        private static readonly float[] Sizes = { 1f, 0.78f, 0.6f, 0.45f, 0.32f };

        private const float TrailStep = 0.24f;  // radians between the trail's bubbles
        private const float Lead = 0.7f;        // radians ahead of the soap when it appears
        private const float FadeSeconds = 0.2f;

        private readonly Renderer[] _dots;
        private readonly Action<Renderer, Color> _paint;
        private float _angle;
        private int _way = -1;

        /// <param name="make">Makes one of its bubbles, under the ring's kit.</param>
        public SpinPacer(Func<Renderer> make, Action<Renderer, Color> paint)
        {
            _paint = paint;
            _dots = new Renderer[Sizes.Length];
            for (var i = 0; i < _dots.Length; i++)
            {
                _dots[i] = make();
                _dots[i].gameObject.SetActive(false);
            }
        }

        /// <summary>0..1: how much it shows.</summary>
        public float Shown { get; private set; }

        /// <summary>The way it runs: +1 anticlockwise, -1 clockwise.</summary>
        public int Way => _way;

        /// <summary>
        /// 0..1: how much a pace (the game's, smoothed) wants the pacer: all of it well short of
        /// full speed, none at it. Between the two it fades, so it doesn't flicker on and off.
        /// </summary>
        public static float WantFor(float pace) => Mathf.InverseLerp(0.92f, 0.65f, pace);

        /// <param name="want">0..1: how much it should show (the stick cue's wish, or a short pace).</param>
        /// <param name="loopsPerSecond">Full speed: the pace it runs at.</param>
        /// <param name="turning">The player's way round (+1, -1), or 0 before they've turned.</param>
        /// <param name="toolAngle">Where the soap or cloth is, so it appears just ahead of it.</param>
        public void Update(float dt, float want, float loopsPerSecond, int turning, float toolAngle,
            float radius, float height, float dotSize, Color color)
        {
            if (turning != 0)
                _way = turning;
            if (Shown <= 0f && want > 0f)
                _angle = toolAngle + _way * Lead;
            // No time passing (a capture): shown as wanted at once.
            Shown = dt > 0f ? Mathf.MoveTowards(Shown, Mathf.Clamp01(want), dt / FadeSeconds) : Mathf.Clamp01(want);
            _angle += _way * loopsPerSecond * Mathf.PI * 2f * dt;
            _angle = Mathf.Repeat(_angle, Mathf.PI * 2f);

            for (var i = 0; i < _dots.Length; i++)
            {
                var dot = _dots[i];
                if (dot == null)
                    continue;
                var show = Shown > 0.01f;
                dot.gameObject.SetActive(show);
                if (!show)
                    continue;
                var at = _angle - _way * i * TrailStep;
                dot.transform.localPosition = new Vector3(Mathf.Cos(at) * radius, height, Mathf.Sin(at) * radius);
                // Pops in from nothing; the trail fades towards the ring's own colour.
                dot.transform.localScale = Vector3.one * (dotSize * Sizes[i] * Shown);
                _paint(dot, color);
            }
        }

        /// <summary>Hides it at once (the game ended).</summary>
        public void Hide()
        {
            Shown = 0f;
            foreach (var dot in _dots)
            {
                if (dot != null)
                    dot.gameObject.SetActive(false);
            }
        }
    }
}
