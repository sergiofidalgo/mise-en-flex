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

        /// <summary>A direction given relative to the station (x along its right, y along its forward), on the floor.</summary>
        public Vector2 Direction(Vector2 local) => Right * local.X + Forward * local.Y;

        /// <summary>
        /// True if the point is within this square, or closer to it than <paramref name="margin"/>.
        /// That keeps the corners round: someone round slips past a corner they'd hit if the margin
        /// were simply a bigger square, 1.4 times further out on the diagonal.
        /// </summary>
        public bool Contains(Vector2 point, float halfSize, float margin = 0f)
        {
            var offset = point - Centre;
            var outsideRight = Math.Abs(Vector2.Dot(offset, Right)) - halfSize;
            var outsideForward = Math.Abs(Vector2.Dot(offset, Forward)) - halfSize;
            if (outsideRight >= margin || outsideForward >= margin)
                return false;
            var pastACorner = outsideRight > 0f && outsideForward > 0f;
            return !pastACorner || outsideRight * outsideRight + outsideForward * outsideForward < margin * margin;
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
