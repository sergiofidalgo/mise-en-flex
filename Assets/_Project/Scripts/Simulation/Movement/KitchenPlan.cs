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
        NotOnAWall,
        NoRoomForChairs,
        NoFreeCounter,
    }

    /// <summary>The building's two rooms. Pieces may stand in either (decisions 2026-11-11).</summary>
    public enum Room
    {
        Kitchen,
        DiningRoom,
    }

    /// <summary>
    /// The rules for rearranging the restaurant (decisions 2026-11-04): pieces go exactly where the
    /// player wants, snapping to the furniture around them when close, at any rotation. They can't
    /// overlap, land on someone, or reach into a wall: a carried piece stops flush against one, and
    /// jumps the dividing wall once the cursor passes its middle (decisions 2026-11-11). Since then
    /// appliances may stand in the dining room and tables in the kitchen: each piece has a usual
    /// room, and <see cref="OutOfTheirRooms"/> lists those that aren't in it, for the mess and the
    /// guests' reaction. Bad layouts are the player's to discover. Positions are on the floor, in metres.
    /// </summary>
    public sealed class KitchenPlan
    {
        private readonly Dictionary<object, Footprint> _pieces = new Dictionary<object, Footprint>();
        private readonly Dictionary<object, Vector2> _sizes = new Dictionary<object, Vector2>();
        private readonly Dictionary<object, (Vector2 Min, Vector2 Max)> _otherAreas = new Dictionary<object, (Vector2, Vector2)>();
        private readonly Dictionary<object, Room> _usualRooms = new Dictionary<object, Room>();
        private readonly List<(Room Room, Vector2 Min, Vector2 Max)> _rooms = new List<(Room, Vector2, Vector2)>();
        private readonly float _snapRange;
        private readonly float _stationHalfSize;
        private readonly float _walkerRadius;

        /// <param name="areaMin">The kitchen: the corner of where a standard piece's centre can go when square
        /// to the room, so half a standard piece in from the walls' faces. Other rooms come with <see cref="AddRoom"/>.</param>
        /// <param name="snapRange">How close, in metres, a piece has to get before it snaps into line.</param>
        public KitchenPlan(Vector2 areaMin, Vector2 areaMax, float snapRange, float stationHalfSize, float walkerRadius)
        {
            _rooms.Add((Room.Kitchen, areaMin, areaMax));
            _snapRange = snapRange;
            _stationHalfSize = stationHalfSize;
            _walkerRadius = walkerRadius;
        }

        public IEnumerable<object> Pieces => _pieces.Keys;

        /// <param name="halfSize">Half its width and depth, if it isn't the standard square.</param>
        public void Add(object piece, Vector2 position, float degrees = 0f, Vector2 halfSize = default)
        {
            GiveSize(piece, halfSize);
            _pieces[piece] = FootprintOf(piece, position, degrees);
        }

        /// <summary>Half this piece's width and depth, for one not yet placed, if it isn't the standard square.</summary>
        public void GiveSize(object piece, Vector2 halfSize) => _sizes[piece] = halfSize;

        private Footprint FootprintOf(object piece, Vector2 position, float degrees) =>
            new Footprint(position, degrees, _sizes.TryGetValue(piece, out var size) ? size : default);

        /// <summary>
        /// Opens another room to furniture, measured as the kitchen is: where a standard piece's centre
        /// can go when square to it. Rooms don't overlap; what's between them is wall.
        /// </summary>
        public void AddRoom(Room room, Vector2 areaMin, Vector2 areaMax) => _rooms.Add((room, areaMin, areaMax));

        /// <summary>This piece has its own rule (the street door): its centre stays between these corners, whatever room.</summary>
        public void GiveArea(object piece, Vector2 min, Vector2 max) => _otherAreas[piece] = (min, max);

        /// <summary>Where this piece usually stands. Pieces not told otherwise are the kitchen's.</summary>
        public void SetUsualRoom(object piece, Room room) => _usualRooms[piece] = room;

        public Room UsualRoomOf(object piece) => _usualRooms.TryGetValue(piece, out var room) ? room : Room.Kitchen;

        /// <summary>The room a point on the floor is in, between its walls' faces, or null inside a wall or outside.</summary>
        public Room? RoomAt(Vector2 point)
        {
            foreach (var room in _rooms)
            {
                if (Within(room.Min, room.Max, point))
                    return room.Room;
            }
            return null;
        }

        private bool Within(Vector2 min, Vector2 max, Vector2 point) =>
            point.X >= min.X - _stationHalfSize - 1e-3f && point.X <= max.X + _stationHalfSize + 1e-3f
            && point.Y >= min.Y - _stationHalfSize - 1e-3f && point.Y <= max.Y + _stationHalfSize + 1e-3f;

        /// <summary>The room this piece stands in now, or null for one with its own area (the door).</summary>
        public Room? RoomOf(object piece) => _otherAreas.ContainsKey(piece) ? null : RoomAt(_pieces[piece].Centre);

        /// <summary>Whether this piece would stand outside its usual room at this position: an appliance in the dining room, a table in the kitchen.</summary>
        public bool IsOutOfItsRoom(object piece, Vector2 position)
        {
            if (_otherAreas.ContainsKey(piece))
                return false;
            var room = RoomAt(position);
            return room.HasValue && room.Value != UsualRoomOf(piece);
        }

        /// <summary>
        /// Every placed piece standing outside its usual room (an open kitchen, decisions 2026-11-11):
        /// appliances in the dining room and tables in the kitchen. Pieces with their own area never count.
        /// </summary>
        public IReadOnlyList<object> OutOfTheirRooms()
        {
            var found = new List<object>();
            foreach (var pair in _pieces)
            {
                if (IsOutOfItsRoom(pair.Key, pair.Value.Centre))
                    found.Add(pair.Key);
            }
            return found;
        }

        // The area a piece carried to this point is kept in: its own, or the room the point is in,
        // or, inside a wall, the nearer room, so a piece jumps the dividing wall at its middle.
        private (Vector2 Min, Vector2 Max) AreaFor(object piece, Vector2 point)
        {
            if (_otherAreas.TryGetValue(piece, out var own))
                return own;
            var best = (_rooms[0].Min, _rooms[0].Max);
            var bestDistance = float.MaxValue;
            foreach (var room in _rooms)
            {
                var dx = Math.Max(0f, Math.Max(room.Min.X - _stationHalfSize - point.X, point.X - room.Max.X - _stationHalfSize));
                var dy = Math.Max(0f, Math.Max(room.Min.Y - _stationHalfSize - point.Y, point.Y - room.Max.Y - _stationHalfSize));
                var distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (room.Min, room.Max);
                }
            }
            return best;
        }

        // The area a piece standing here must fit in, or null if its centre isn't in a room.
        private (Vector2 Min, Vector2 Max)? AreaAt(object piece, Vector2 centre)
        {
            if (_otherAreas.TryGetValue(piece, out var own))
                return own;
            foreach (var room in _rooms)
            {
                if (Within(room.Min, room.Max, centre))
                    return (room.Min, room.Max);
            }
            return null;
        }

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

            var frame = FootprintOf(piece, point, degrees);
            var alongRight = BestCorrection(piece, frame, frame.Right, frame.Forward);
            var alongForward = BestCorrection(piece, frame, frame.Forward, frame.Right);
            var snapped = point + frame.Right * alongRight + frame.Forward * alongForward;

            // Against the walls, when it's square to the room.
            var area = AreaFor(piece, point);
            var extentX = frame.Extent(Vector2.UnitX, _stationHalfSize);
            var extentY = frame.Extent(Vector2.UnitY, _stationHalfSize);
            if (IsSquare(degrees))
            {
                snapped.X = ToEdge(snapped.X, area.Min.X, area.Max.X, extentX);
                snapped.Y = ToEdge(snapped.Y, area.Min.Y, area.Max.Y, extentY);
            }

            // Never into a wall, at any turn: pushed further, it stays flush against it. The extents
            // are how far its corners reach along each axis, so a turned piece stops by its corner.
            snapped.X = Inside(snapped.X, area.Min.X, area.Max.X, extentX);
            snapped.Y = Inside(snapped.Y, area.Min.Y, area.Max.Y, extentY);
            return snapped;
        }

        // Keeps a centre where something reaching this far either side stays between the walls'
        // faces (half a standard piece beyond the area). Too big for the room, it's centred in it.
        private float Inside(float value, float min, float max, float extent)
        {
            var low = min - _stationHalfSize + extent;
            var high = max + _stationHalfSize - extent;
            if (low > high)
                return (min + max) / 2f;
            return Math.Min(Math.Max(value, low), high);
        }

        // The smallest nudge along one direction that lines it up with a neighbour turned the same
        // way: centres level, or edges touching. Only neighbours close on the other direction count.
        private float BestCorrection(object piece, Footprint moving, Vector2 axis, Vector2 otherAxis)
        {
            var point = moving.Centre;
            var ownAlong = moving.Extent(axis, _stationHalfSize);
            var ownAcross = moving.Extent(otherAxis, _stationHalfSize);
            var best = 0f;
            var bestSize = _snapRange;
            foreach (var pair in _pieces)
            {
                if (pair.Key == piece || !SameAxes(pair.Value, axis))
                    continue;

                var offset = point - pair.Value.Centre;
                if (Math.Abs(Vector2.Dot(offset, otherAxis)) > ownAcross + pair.Value.Extent(otherAxis, _stationHalfSize) + _snapRange)
                    continue;

                var along = Vector2.Dot(offset, axis);
                var touching = ownAlong + pair.Value.Extent(axis, _stationHalfSize);
                foreach (var target in new[] { 0f, touching, -touching })
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

        // The area is where a standard piece's centre can go: a smaller one sits with its edge at
        // the same wall, so its centre is closer in.
        private float ToEdge(float value, float min, float max, float extent)
        {
            var low = min - _stationHalfSize + extent;
            var high = max + _stationHalfSize - extent;
            if (Math.Abs(value - low) < _snapRange)
                return low;
            if (Math.Abs(value - high) < _snapRange)
                return high;
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

        /// <param name="people">Where everyone is standing, each taken to be the plan's walker radius wide.</param>
        public Placement Check(object piece, Vector2 position, float degrees, IEnumerable<Vector2> people) =>
            Check(piece, position, degrees, WithWalkerRadius(people));

        /// <param name="people">Where everyone is standing, and how far their body reaches from there.</param>
        public Placement Check(object piece, Vector2 position, float degrees, IEnumerable<(Vector2 Position, float Radius)> people)
        {
            var moved = FootprintOf(piece, position, degrees);

            // All of it has to fit in the room its centre is in, so a turned station can't poke
            // through a wall, and nothing straddles the dividing wall.
            var found = AreaAt(piece, position);
            if (!found.HasValue)
                return Placement.OutsideTheKitchen;
            var area = found.Value;
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
                if (moved.Contains(person.Position, _stationHalfSize, person.Radius))
                    return Placement.SomeoneInTheWay;
            }

            return Placement.Ok;
        }

        private IEnumerable<(Vector2, float)> WithWalkerRadius(IEnumerable<Vector2> people)
        {
            foreach (var person in people)
                yield return (person, _walkerRadius);
        }

        public void Remove(object piece)
        {
            _pieces.Remove(piece);
            _otherAreas.Remove(piece);
            _usualRooms.Remove(piece);
            _sizes.Remove(piece);
        }

        public void Move(object piece, Vector2 position, float degrees) => _pieces[piece] = FootprintOf(piece, position, degrees);
    }
}
