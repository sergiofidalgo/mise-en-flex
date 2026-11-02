using System;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>A colour at a value; a list of these is sampled with linear blending between stops.</summary>
    [Serializable]
    public struct ColorStop
    {
        [SerializeField] private float _at;
        [SerializeField] private Color _color;

        public ColorStop(float at, Color color)
        {
            _at = at;
            _color = color;
        }

        public float At => _at;
        public Color Color => _color;

        /// <summary>
        /// Samples ascending <paramref name="stops"/> at <paramref name="value"/>. Below the first
        /// stop the result is <paramref name="start"/> (the item's own colour), blending into the
        /// first stop from <paramref name="startAt"/>.
        /// </summary>
        public static Color Sample(ColorStop[] stops, float value, Color start, float startAt)
        {
            var previousAt = startAt;
            var previousColor = start;
            foreach (var stop in stops)
            {
                if (value <= stop.At)
                    return Color.Lerp(previousColor, stop.Color, Mathf.InverseLerp(previousAt, stop.At, value));
                previousAt = stop.At;
                previousColor = stop.Color;
            }
            return previousColor;
        }
    }
}
