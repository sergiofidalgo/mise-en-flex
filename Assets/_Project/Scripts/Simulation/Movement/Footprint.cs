using System;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>The square a station takes up on the floor, at any rotation.</summary>
    public readonly struct Footprint
    {
        public Footprint(Vector2 centre, float degrees = 0f)
        {
            Centre = centre;
            Degrees = degrees;
        }

        public Vector2 Centre { get; }

        /// <summary>Clockwise from above, as Unity's yaw.</summary>
        public float Degrees { get; }

        // The station's own right and forward directions, on the floor.
        public Vector2 Right
        {
            get
            {
                var radians = Degrees * (float)Math.PI / 180f;
                return new Vector2((float)Math.Cos(radians), -(float)Math.Sin(radians));
            }
        }

        public Vector2 Forward
        {
            get
            {
                var right = Right;
                return new Vector2(-right.Y, right.X);
            }
        }

        /// <summary>True if the point is within this square, grown by <paramref name="margin"/>.</summary>
        public bool Contains(Vector2 point, float halfSize, float margin = 0f)
        {
            var offset = point - Centre;
            var reach = halfSize + margin;
            return Math.Abs(Vector2.Dot(offset, Right)) < reach && Math.Abs(Vector2.Dot(offset, Forward)) < reach;
        }

        /// <summary>
        /// True if two squares overlap, by the separating axis test: two rectangles are apart exactly
        /// when one of their four edge directions has a gap between them. Touching isn't overlapping.
        /// </summary>
        public bool Overlaps(Footprint other, float halfSize)
        {
            const float touching = 1e-3f;
            foreach (var axis in new[] { Right, Forward, other.Right, other.Forward })
            {
                var gap = Math.Abs(Vector2.Dot(other.Centre - Centre, axis));
                var reach = Reach(axis, halfSize) + other.Reach(axis, halfSize);
                if (gap >= reach - touching)
                    return false;
            }
            return true;
        }

        /// <summary>The corners, for checking it fits in the room.</summary>
        public Vector2[] Corners(float halfSize)
        {
            var right = Right * halfSize;
            var forward = Forward * halfSize;
            return new[] { Centre + right + forward, Centre + right - forward, Centre - right + forward, Centre - right - forward };
        }

        private float Reach(Vector2 axis, float halfSize) =>
            (Math.Abs(Vector2.Dot(Right, axis)) + Math.Abs(Vector2.Dot(Forward, axis))) * halfSize;
    }
}
