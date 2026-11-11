using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Where the street's wheelie bin can roll (decisions 2026-11-11): round the outside of the
    /// building, its back to the west, front (south) and east walls, never the north wall, where
    /// the grounds end too close behind. One open track, measured in metres from the north end of
    /// the west wall, through the two front corners, to the north end of the east wall, so the bin
    /// can always roll from one spot to another the long way round instead of through the building.
    ///
    /// Round each corner the bin's middle follows a quarter circle whose centre sits just inside the
    /// building's corner (<c>CornerRadius - WallGap - depth / 2</c> in from each wall). With a
    /// radius of about 1 m, the bin's inner face always passes outside the corner, so it never
    /// clips it while it turns. It can only be put down on a straight stretch with its whole length
    /// against a wall.
    /// </summary>
    public sealed class BinTrack
    {
        private readonly Vector2 _min;
        private readonly Vector2 _max;
        private readonly float _out;      // from a wall's outer face to the bin's middle
        private readonly float _half;     // half the bin's length, along the wall
        private readonly float _radius;
        private readonly float _inset;    // the corner circles' centres, in from each wall
        private readonly float[] _ends;   // where each of the five pieces of track ends

        /// <param name="buildingMin">The building's outer south-west corner (x, z).</param>
        /// <param name="buildingMax">The building's outer north-east corner (x, z).</param>
        /// <param name="wallGap">Metres left between a wall and the bin's back.</param>
        /// <param name="size">The bin's length along a wall and its depth out from it.</param>
        /// <param name="cornerRadius">The radius its middle turns round a corner on, at least
        /// <c>wallGap + size.Y / 2</c>. Too big and the building's corner pokes into the bin as it
        /// turns; BinTrackTests checks the value the game uses.</param>
        public BinTrack(Vector2 buildingMin, Vector2 buildingMax, float wallGap, Vector2 size, float cornerRadius)
        {
            _min = buildingMin;
            _max = buildingMax;
            _out = wallGap + size.Y / 2f;
            _half = size.X / 2f;
            _radius = Math.Max(cornerRadius, _out);
            _inset = _radius - _out;
            HalfSize = new Vector2(size.X / 2f, size.Y / 2f);

            var west = Math.Max(0f, _max.Y - _half - (_min.Y + _inset));
            var south = Math.Max(0f, _max.X - _inset - (_min.X + _inset));
            var arc = (float)Math.PI / 2f * _radius;
            _ends = new float[5];
            _ends[0] = west;
            _ends[1] = _ends[0] + arc;
            _ends[2] = _ends[1] + south;
            _ends[3] = _ends[2] + arc;
            _ends[4] = _ends[3] + west;
        }

        /// <summary>Half the bin's length (x, along the wall) and depth (y), for its footprint.</summary>
        public Vector2 HalfSize { get; }

        /// <summary>The whole track, in metres.</summary>
        public float Length => _ends[4];

        /// <summary>Halfway round a front corner (the east one, or the west), as a distance along the track.</summary>
        public float CornerMiddle(bool east) => east ? (_ends[2] + _ends[3]) / 2f : (_ends[0] + _ends[1]) / 2f;

        /// <summary>
        /// Where the bin's middle is at <paramref name="s"/> metres along the track, and how it's
        /// turned (degrees clockwise from above, as Unity's yaw: its front faces out from the wall).
        /// </summary>
        public (Vector2 Position, float Degrees) At(float s)
        {
            s = Math.Max(0f, Math.Min(Length, s));
            if (s <= _ends[0])
                return (new Vector2(_min.X - _out, _max.Y - _half - s), 270f);
            if (s <= _ends[1])
                return OnArc(new Vector2(_min.X + _inset, _min.Y + _inset), 180f + 90f * (s - _ends[0]) / (_ends[1] - _ends[0]));
            if (s <= _ends[2])
                return (new Vector2(_min.X + _inset + (s - _ends[1]), _min.Y - _out), 180f);
            if (s <= _ends[3])
                return OnArc(new Vector2(_max.X - _inset, _min.Y + _inset), 270f + 90f * (s - _ends[2]) / (_ends[3] - _ends[2]));
            return (new Vector2(_max.X + _out, _min.Y + _inset + (s - _ends[3])), 90f);
        }

        /// <summary>The bin's footprint at <paramref name="s"/>.</summary>
        public Footprint FootprintAt(float s)
        {
            var (position, degrees) = At(s);
            return new Footprint(position, degrees, HalfSize);
        }

        /// <summary>True if the bin stands with its whole length against a wall there, so it can be put down.</summary>
        public bool IsAgainstAWall(float s)
        {
            const float slack = 1e-3f;
            foreach (var (from, to) in Stretches())
            {
                if (s >= from - slack && s <= to + slack)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The spot nearest <paramref name="point"/> where the bin can stand, against one of the
        /// three walls, as a distance along the track.
        /// </summary>
        public float Snap(Vector2 point)
        {
            var best = 0f;
            var bestDistance = float.MaxValue;
            foreach (var (from, to) in Stretches())
            {
                // Each stretch is straight, so the nearest spot on it is the point's projection, clamped.
                var a = At(from).Position;
                var b = At(to).Position;
                var along = b - a;
                var t = along.LengthSquared() > 1e-8f ? Math.Max(0f, Math.Min(1f, Vector2.Dot(point - a, along) / along.LengthSquared())) : 0f;
                var distance = Vector2.Distance(point, a + along * t);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = from + (to - from) * t;
                }
            }
            return best;
        }

        /// <summary>The nearest distance along the track to a point (any spot, corners included).</summary>
        public float Nearest(Vector2 point)
        {
            // Fine enough steps for a bin: a few centimetres.
            const float step = 0.02f;
            var best = 0f;
            var bestDistance = float.MaxValue;
            for (var s = 0f; s <= Length; s += step)
            {
                var distance = Vector2.DistanceSquared(At(s).Position, point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>
        /// Where the bin mustn't stand, given the street door: the doorway and a margin either side,
        /// out as far as the bin reaches; and the guests' way from the doorstep straight south to the
        /// pavement, which runs along a side wall when the door is on one.
        /// </summary>
        /// <param name="door">The street door's footprint; a door faces in, into the room.</param>
        /// <param name="binReach">How far the bin reaches out from a wall (its gap plus its depth).</param>
        /// <param name="doorMargin">Metres kept clear either side of the door, on top of its width.</param>
        /// <param name="doorstep">How far out from the door guests step up to it.</param>
        /// <param name="pathWidth">The width of the guests' way to the pavement.</param>
        public static IReadOnlyList<Footprint> KeepClear(Footprint door, float binReach, float doorMargin, float doorstep, float pathWidth)
        {
            // Far enough to reach the pavement's walking line from any door on a side wall.
            const float toPavement = 30f;
            var outward = -door.Forward;
            var reach = binReach + 0.1f;
            var doorHalf = door.HalfSize(0.5f).X;
            var step = door.Centre + outward * doorstep;
            return new[]
            {
                new Footprint(door.Centre + outward * (reach / 2f), door.Degrees, new Vector2(doorHalf + doorMargin, reach / 2f)),
                new Footprint(step + new Vector2(0f, -toPavement / 2f), 0f, new Vector2(pathWidth / 2f, toPavement / 2f)),
            };
        }

        // The straight stretches with the bin's whole length against a wall: west, front, east.
        private IEnumerable<(float From, float To)> Stretches()
        {
            var sideRoom = _max.Y - _min.Y - 2f * _half;      // how far it can slide along a side wall
            var frontRoom = _max.X - _min.X - 2f * _half;
            if (sideRoom >= 0f)
                yield return (0f, sideRoom);
            if (frontRoom >= 0f)
            {
                var from = _ends[1] + (_min.X + _half - (_min.X + _inset));
                yield return (from, from + frontRoom);
            }
            if (sideRoom >= 0f)
                yield return (_ends[4] - sideRoom, _ends[4]);
        }

        private (Vector2, float) OnArc(Vector2 centre, float angle)
        {
            var radians = angle * (float)Math.PI / 180f;
            var outward = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians));
            // Unity's yaw for a front facing `outward`: forward is (sin yaw, cos yaw) on (x, z).
            var degrees = (float)(Math.Atan2(outward.X, outward.Y) * 180.0 / Math.PI);
            if (degrees < 0f)
                degrees += 360f;
            return (centre + outward * _radius, degrees);
        }
    }
}
