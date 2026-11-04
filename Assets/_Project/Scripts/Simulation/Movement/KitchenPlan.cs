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
    /// The rules for rearranging the kitchen (decisions 2026-11-04): pieces go exactly where the
    /// player wants, snapping to the furniture around them when close, at any rotation. They can't
    /// overlap, land on someone, or be placed so that anything can no longer be reached.
    /// Positions are on the floor, in metres.
    /// </summary>
    public sealed class KitchenPlan
    {
        private readonly Dictionary<object, Footprint> _pieces = new Dictionary<object, Footprint>();
        private readonly HashSet<object> _rowLocked = new HashSet<object>();
        private readonly Dictionary<object, Vector2> _fronts = new Dictionary<object, Vector2>();
        private readonly Vector2 _roomHalfSize;
        private readonly Vector2 _areaMin;
        private readonly Vector2 _areaMax;
        private readonly float _snapRange;
        private readonly float _stationHalfSize;
        private readonly float _walkerRadius;
        private readonly float _standDistance;

        /// <param name="areaMin">The corner of where stations' centres can go when square to the room.</param>
        /// <param name="snapRange">How close, in metres, a piece has to get before it snaps into line.</param>
        public KitchenPlan(Vector2 roomHalfSize, Vector2 areaMin, Vector2 areaMax, float snapRange, float stationHalfSize, float walkerRadius, float standDistance)
        {
            _roomHalfSize = roomHalfSize;
            _areaMin = areaMin;
            _areaMax = areaMax;
            _snapRange = snapRange;
            _stationHalfSize = stationHalfSize;
            _walkerRadius = walkerRadius;
            _standDistance = standDistance;
        }

        public IEnumerable<object> Pieces => _pieces.Keys;

        /// <param name="rowLocked">It stays in its row, square on, and only slides along it (the pass, facing the stools).</param>
        public void Add(object piece, Vector2 position, float degrees = 0f, bool rowLocked = false)
        {
            _pieces[piece] = new Footprint(position, degrees);
            if (rowLocked)
                _rowLocked.Add(piece);
        }

        /// <summary>
        /// Gives a piece a front: the side a chef works it from, which has to stay clear wherever it
        /// goes. A hob boxed in on that side can't be used (decision 2026-11-04), whereas a plain
        /// counter can be reached from any side. It's the side facing the room right now, and it
        /// turns with the piece.
        /// </summary>
        public void GiveFront(object piece)
        {
            var footprint = _pieces[piece];
            var spot = Layout(_pieces.Values).StandingSpot(footprint);
            if (!spot.HasValue)
                return;
            var offset = (spot.Value - footprint.Centre) / _standDistance;
            _fronts[piece] = new Vector2(Vector2.Dot(offset, footprint.Right), Vector2.Dot(offset, footprint.Forward));
        }

        /// <summary>Where a chef stands to work a piece that has a front; null if it hasn't one.</summary>
        public Vector2? FrontOf(object piece)
        {
            if (!_fronts.TryGetValue(piece, out var front))
                return null;
            var footprint = _pieces[piece];
            return footprint.Centre + footprint.Direction(front) * _standDistance;
        }

        public Vector2 PositionOf(object piece) => _pieces[piece].Centre;

        public float DegreesOf(object piece) => _pieces[piece].Degrees;

        public bool CanRotate(object piece) => !_rowLocked.Contains(piece);

        /// <summary>
        /// Where this piece lands if carried to <paramref name="point"/>: exactly there, unless it's
        /// close to lining up with something. Each direction snaps on its own, so it can sit against
        /// one neighbour and in line with another, and <paramref name="home"/> (where it came from)
        /// is caught too, so it can always go back exactly.
        /// </summary>
        public Vector2 Snap(object piece, Vector2 point, float degrees, Vector2? home = null)
        {
            if (_rowLocked.Contains(piece))
                point.Y = _pieces[piece].Centre.Y;

            if (home.HasValue && (home.Value - point).Length() <= _snapRange)
                return home.Value;

            var frame = new Footprint(point, degrees);
            var alongRight = BestCorrection(piece, point, frame.Right, frame.Forward);
            var alongForward = BestCorrection(piece, point, frame.Forward, frame.Right);
            var snapped = point + frame.Right * alongRight + frame.Forward * alongForward;

            // Against the walls, when it's square to the room.
            if (IsSquare(degrees))
            {
                snapped.X = ToEdge(snapped.X, _areaMin.X, _areaMax.X);
                if (!_rowLocked.Contains(piece))
                    snapped.Y = ToEdge(snapped.Y, _areaMin.Y, _areaMax.Y);
            }
            if (_rowLocked.Contains(piece))
                snapped.Y = _pieces[piece].Centre.Y;
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
        /// <param name="blocked">If given, is filled with the pieces this placement would cut off.</param>
        public Placement Check(object piece, Vector2 position, float degrees, IEnumerable<Vector2> people, List<object> blocked = null)
        {
            blocked?.Clear();
            var moved = new Footprint(position, degrees);

            // All of it has to fit, so a turned station can't poke through a wall. A piece in a
            // locked row is held by the row instead, front to back.
            var locked = _rowLocked.Contains(piece);
            foreach (var corner in moved.Corners(_stationHalfSize))
            {
                var fitsX = corner.X >= _areaMin.X - _stationHalfSize - 1e-3f && corner.X <= _areaMax.X + _stationHalfSize + 1e-3f;
                var fitsY = locked || (corner.Y >= _areaMin.Y - _stationHalfSize - 1e-3f && corner.Y <= _areaMax.Y + _stationHalfSize + 1e-3f);
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

            var footprints = new List<Footprint>();
            foreach (var pair in _pieces)
                footprints.Add(pair.Key == piece ? moved : pair.Value);
            var layout = Layout(footprints);

            // Everything needs somewhere to stand: its front if it has one, otherwise any side.
            var needs = new List<IReadOnlyList<Vector2>>();
            var owners = new List<object>();
            var inTheWay = blocked ?? new List<object>();
            foreach (var pair in _pieces)
            {
                var footprint = pair.Key == piece ? moved : pair.Value;
                List<Vector2> places;
                if (_fronts.TryGetValue(pair.Key, out var front))
                {
                    var point = footprint.Centre + footprint.Direction(front) * _standDistance;
                    places = layout.IsFree(point) ? new List<Vector2> { point } : new List<Vector2>();
                }
                else
                {
                    places = layout.FreeSides(footprint);
                }

                if (places.Count == 0)
                {
                    inTheWay.Add(pair.Key);
                    continue;
                }
                needs.Add(places);
                owners.Add(pair.Key);
            }

            // Nobody can be walled in either; a person isn't a piece, so has no owner to report.
            foreach (var person in people)
            {
                needs.Add(new List<Vector2> { person });
                owners.Add(null);
            }

            var walledIn = false;
            foreach (var index in layout.CutOff(needs))
            {
                walledIn = true;
                if (owners[index] != null)
                    inTheWay.Add(owners[index]);
            }
            return inTheWay.Count > 0 || walledIn ? Placement.BlocksAStation : Placement.Ok;
        }

        private StationLayout Layout(IEnumerable<Footprint> footprints) =>
            new StationLayout(footprints, _roomHalfSize, _stationHalfSize, _walkerRadius, _standDistance);

        public void Move(object piece, Vector2 position, float degrees) => _pieces[piece] = new Footprint(position, degrees);
    }
}
