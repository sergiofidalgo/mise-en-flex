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
        BlocksAStation,
    }

    /// <summary>
    /// The rules for rearranging the kitchen (decisions 2026-11-04): pieces snap to a grid, can't
    /// overlap or land on someone, and can't be placed so that any station can no longer be reached.
    /// Positions are on the floor, in metres.
    /// </summary>
    public sealed class KitchenPlan
    {
        private readonly Dictionary<object, Vector2> _pieces = new Dictionary<object, Vector2>();
        private readonly HashSet<object> _rowLocked = new HashSet<object>();
        private readonly Vector2 _roomHalfSize;
        private readonly Vector2 _areaMin;
        private readonly Vector2 _areaMax;
        private readonly float _gridSize;
        private readonly float _stationHalfSize;
        private readonly float _walkerRadius;
        private readonly float _standDistance;

        /// <param name="areaMin">The corner of where pieces can go, as the centre of the piece.</param>
        public KitchenPlan(Vector2 roomHalfSize, Vector2 areaMin, Vector2 areaMax, float gridSize, float stationHalfSize, float walkerRadius, float standDistance)
        {
            _roomHalfSize = roomHalfSize;
            _areaMin = areaMin;
            _areaMax = areaMax;
            _gridSize = gridSize;
            _stationHalfSize = stationHalfSize;
            _walkerRadius = walkerRadius;
            _standDistance = standDistance;
        }

        public IEnumerable<object> Pieces => _pieces.Keys;

        /// <param name="rowLocked">It stays in its row and only slides along it (the pass, facing the stools).</param>
        public void Add(object piece, Vector2 position, bool rowLocked = false)
        {
            _pieces[piece] = position;
            if (rowLocked)
                _rowLocked.Add(piece);
        }

        public Vector2 PositionOf(object piece) => _pieces[piece];

        /// <summary>Where this piece would land if carried to <paramref name="point"/>.</summary>
        public Vector2 Snap(object piece, Vector2 point)
        {
            var snapped = new Vector2(
                (float)Math.Round(point.X / _gridSize) * _gridSize,
                (float)Math.Round(point.Y / _gridSize) * _gridSize);
            if (_rowLocked.Contains(piece))
                snapped.Y = _pieces[piece].Y;
            return snapped;
        }

        /// <summary>The piece under a point on the floor, or null.</summary>
        public object PieceAt(Vector2 point)
        {
            object best = null;
            var bestDistance = float.MaxValue;
            foreach (var pair in _pieces)
            {
                var offset = pair.Value - point;
                if (Math.Abs(offset.X) > _stationHalfSize || Math.Abs(offset.Y) > _stationHalfSize)
                    continue;
                if (offset.LengthSquared() < bestDistance)
                {
                    best = pair.Key;
                    bestDistance = offset.LengthSquared();
                }
            }
            return best;
        }

        /// <param name="people">Where everyone is standing.</param>
        public Placement Check(object piece, Vector2 position, IEnumerable<Vector2> people)
        {
            // A piece in a locked row may be outside the free area; it's the row that holds it.
            var withinX = position.X >= _areaMin.X && position.X <= _areaMax.X;
            var withinY = _rowLocked.Contains(piece) || (position.Y >= _areaMin.Y && position.Y <= _areaMax.Y);
            if (!withinX || !withinY)
                return Placement.OutsideTheKitchen;

            var size = _stationHalfSize * 2f;
            foreach (var pair in _pieces)
            {
                if (pair.Key == piece)
                    continue;
                if (Math.Abs(pair.Value.X - position.X) < size - 1e-3f && Math.Abs(pair.Value.Y - position.Y) < size - 1e-3f)
                    return Placement.OnAnotherStation;
            }

            var reach = _stationHalfSize + _walkerRadius;
            foreach (var person in people)
            {
                if (Math.Abs(person.X - position.X) < reach && Math.Abs(person.Y - position.Y) < reach)
                    return Placement.SomeoneInTheWay;
            }

            var points = new List<Vector2>();
            foreach (var pair in _pieces)
                points.Add(pair.Key == piece ? position : pair.Value);
            var layout = new StationLayout(points, _roomHalfSize, _stationHalfSize, _walkerRadius, _standDistance);
            return layout.EveryStationCanBeReached(points, people) ? Placement.Ok : Placement.BlocksAStation;
        }

        public void Move(object piece, Vector2 position) => _pieces[piece] = position;
    }
}
