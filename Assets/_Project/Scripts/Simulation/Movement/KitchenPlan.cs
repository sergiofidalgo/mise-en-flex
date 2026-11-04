using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    public enum Placement
    {
        Ok,
        OutsideTheKitchen,
        OnAnotherStation,
        SomeoneInTheWay,
        CantAfford,
    }

    /// <summary>
    /// The rules for rearranging the restaurant (decisions 2026-11-04): pieces go exactly where the
    /// player wants, snapping to the furniture around them when close, at any rotation. They can't
    /// overlap, land on someone, or leave their own room. Bad layouts are the player's to discover.
    /// Positions are on the floor, in metres.
    /// </summary>
    public sealed class KitchenPlan
    {
        private readonly Dictionary<object, Footprint> _pieces = new Dictionary<object, Footprint>();
        private readonly Dictionary<object, (Vector2 Min, Vector2 Max)> _otherAreas = new Dictionary<object, (Vector2, Vector2)>();
        private readonly Vector2 _areaMin;
        private readonly Vector2 _areaMax;
        private readonly float _snapRange;
        private readonly float _stationHalfSize;
        private readonly float _walkerRadius;

        /// <param name="areaMin">The corner of where pieces' centres can go when square to the room, unless given their own area.</param>
        /// <param name="snapRange">How close, in metres, a piece has to get before it snaps into line.</param>
        public KitchenPlan(Vector2 areaMin, Vector2 areaMax, float snapRange, float stationHalfSize, float walkerRadius)
        {
            _areaMin = areaMin;
            _areaMax = areaMax;
            _snapRange = snapRange;
            _stationHalfSize = stationHalfSize;
            _walkerRadius = walkerRadius;
        }

        public IEnumerable<object> Pieces => _pieces.Keys;

        public void Add(object piece, Vector2 position, float degrees = 0f) => _pieces[piece] = new Footprint(position, degrees);

        /// <summary>This piece belongs in another room: its centre stays between these corners instead.</summary>
        public void GiveArea(object piece, Vector2 min, Vector2 max) => _otherAreas[piece] = (min, max);

        private (Vector2 Min, Vector2 Max) AreaOf(object piece) =>
            _otherAreas.TryGetValue(piece, out var area) ? area : (_areaMin, _areaMax);

        public Vector2 PositionOf(object piece) => _pieces[piece].Centre;

        public float DegreesOf(object piece) => _pieces[piece].Degrees;

        /// <summary>
        /// Where this piece lands if carried to <paramref name="point"/>: exactly there, unless it's
        /// close to lining up with something. Each direction snaps on its own, so it can sit against
        /// one neighbour and in line with another, and <paramref name="home"/> (where it came from)
        /// is caught too, so it can always go back exactly.
        /// </summary>
        public Vector2 Snap(object piece, Vector2 point, float degrees, Vector2? home = null)
        {
            if (home.HasValue && (home.Value - point).Length() <= _snapRange)
                return home.Value;

            var frame = new Footprint(point, degrees);
            var alongRight = BestCorrection(piece, point, frame.Right, frame.Forward);
            var alongForward = BestCorrection(piece, point, frame.Forward, frame.Right);
            var snapped = point + frame.Right * alongRight + frame.Forward * alongForward;

            // Against the walls, when it's square to the room.
            if (IsSquare(degrees))
            {
                var area = AreaOf(piece);
                snapped.X = ToEdge(snapped.X, area.Min.X, area.Max.X);
                snapped.Y = ToEdge(snapped.Y, area.Min.Y, area.Max.Y);
            }
            return snapped;
        }

        // The smallest nudge along one direction that lines it up with a neighbour turned the same
        // way: centres level, or edges touching. Only neighbours close on the other direction count.
        private float BestCorrection(object piece, Vector2 point, Vector2 axis, Vector2 otherAxis)
        {
            var size = _stationHalfSize * 2f;
            var best = 0f;
            var bestSize = _snapRange;
            foreach (var pair in _pieces)
            {
                if (pair.Key == piece || !SameAxes(pair.Value, axis))
                    continue;

                var offset = point - pair.Value.Centre;
                if (Math.Abs(Vector2.Dot(offset, otherAxis)) > size + _snapRange)
                    continue;

                var along = Vector2.Dot(offset, axis);
                foreach (var target in new[] { 0f, size, -size })
                {
                    var correction = target - along;
                    if (Math.Abs(correction) < bestSize)
                    {
                        bestSize = Math.Abs(correction);
                        best = correction;
                    }
                }
            }
            return best;
        }

        // Whether a neighbour's edges run along this direction, whichever way it's turned.
        private static bool SameAxes(Footprint other, Vector2 axis)
        {
            var dot = Math.Abs(Vector2.Dot(other.Right, axis));
            return dot > 0.999f || dot < 0.001f;
        }

        private static bool IsSquare(float degrees)
        {
            var remainder = Math.Abs(degrees % 90f);
            return remainder < 0.01f || remainder > 89.99f;
        }

        private float ToEdge(float value, float min, float max)
        {
            if (Math.Abs(value - min) < _snapRange)
                return min;
            if (Math.Abs(value - max) < _snapRange)
                return max;
            return value;
        }

        /// <summary>The piece under a point on the floor, or null.</summary>
        public object PieceAt(Vector2 point)
        {
            object best = null;
            var bestDistance = float.MaxValue;
            foreach (var pair in _pieces)
            {
                if (!pair.Value.Contains(point, _stationHalfSize))
                    continue;
                var distance = (pair.Value.Centre - point).LengthSquared();
                if (distance < bestDistance)
                {
                    best = pair.Key;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <param name="people">Where everyone is standing.</param>
        public Placement Check(object piece, Vector2 position, float degrees, IEnumerable<Vector2> people)
        {
            var moved = new Footprint(position, degrees);

            // All of it has to fit, so a turned station can't poke through a wall.
            var area = AreaOf(piece);
            foreach (var corner in moved.Corners(_stationHalfSize))
            {
                var fitsX = corner.X >= area.Min.X - _stationHalfSize - 1e-3f && corner.X <= area.Max.X + _stationHalfSize + 1e-3f;
                var fitsY = corner.Y >= area.Min.Y - _stationHalfSize - 1e-3f && corner.Y <= area.Max.Y + _stationHalfSize + 1e-3f;
                if (!fitsX || !fitsY)
                    return Placement.OutsideTheKitchen;
            }

            foreach (var pair in _pieces)
            {
                if (pair.Key != piece && moved.Overlaps(pair.Value, _stationHalfSize))
                    return Placement.OnAnotherStation;
            }

            foreach (var person in people)
            {
                if (moved.Contains(person, _stationHalfSize, _walkerRadius))
                    return Placement.SomeoneInTheWay;
            }

            return Placement.Ok;
        }

        public void Remove(object piece)
        {
            _pieces.Remove(piece);
            _otherAreas.Remove(piece);
        }

        public void Move(object piece, Vector2 position, float degrees) => _pieces[piece] = new Footprint(position, degrees);
    }
}
