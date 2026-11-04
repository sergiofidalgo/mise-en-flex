using System;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// The rectangle a station takes up on the floor, at any rotation. Most furniture is the standard
    /// square, whose size the methods are given; anything else carries its own size.
    /// </summary>
    public readonly struct Footprint
    {
        /// <param name="halfSize">Half its width and depth, along its own right and forward. Zero: the standard square.</param>
        public Footprint(Vector2 centre, float degrees = 0f, Vector2 halfSize = default)
        {
            Centre = centre;
            Degrees = degrees;
            OwnHalfSize = halfSize;
        }

        public Vector2 Centre { get; }

        /// <summary>Clockwise from above, as Unity's yaw.</summary>
        public float Degrees { get; }

        /// <summary>Its own half width and depth, or zero if it's the standard square.</summary>
        public Vector2 OwnHalfSize { get; }

        /// <summary>Half its width and depth, given how big the standard square is.</summary>
        public Vector2 HalfSize(float standard) => OwnHalfSize != Vector2.Zero ? OwnHalfSize : new Vector2(standard, standard);

        /// <summary>How far it reaches from its centre along a direction on the floor.</summary>
        public float Extent(Vector2 axis, float standard)
        {
            var half = HalfSize(standard);
            return Math.Abs(Vector2.Dot(Right, axis)) * half.X + Math.Abs(Vector2.Dot(Forward, axis)) * half.Y;
        }

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
            var half = HalfSize(halfSize);
            var outsideRight = Math.Abs(Vector2.Dot(offset, Right)) - half.X;
            var outsideForward = Math.Abs(Vector2.Dot(offset, Forward)) - half.Y;
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
                var reach = Extent(axis, halfSize) + other.Extent(axis, halfSize);
                if (gap >= reach - touching)
                    return false;
            }
            return true;
        }

        /// <summary>The corners, for checking it fits in the room.</summary>
        public Vector2[] Corners(float halfSize)
        {
            var half = HalfSize(halfSize);
            var right = Right * half.X;
            var forward = Forward * half.Y;
            return new[] { Centre + right + forward, Centre + right - forward, Centre - right + forward, Centre - right - forward };
        }
    }
}
