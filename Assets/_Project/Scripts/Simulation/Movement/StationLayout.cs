using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Where people can walk among the stations: finds a place to stand at a station, and a route
    /// there around the furniture (decision 2026-11-04). Positions are on the floor, in metres.
    /// A* on a grid, then shortened to straight lines wherever they stay clear of the furniture.
    /// </summary>
    public sealed class StationLayout
    {
        private const float CellSize = 0.2f;

        private readonly List<Footprint> _stations;
        private readonly Vector2 _roomHalfSize;
        private readonly float _stationHalfSize;
        private readonly float _radius;
        private readonly float _standDistance;
        private readonly int _width;
        private readonly int _height;

        public StationLayout(IEnumerable<Vector2> stations, Vector2 roomHalfSize, float stationHalfSize, float walkerRadius, float standDistance)
            : this(stations.Select(centre => new Footprint(centre)), roomHalfSize, stationHalfSize, walkerRadius, standDistance)
        {
        }

        public StationLayout(IEnumerable<Footprint> stations, Vector2 roomHalfSize, float stationHalfSize, float walkerRadius, float standDistance)
        {
            _stations = new List<Footprint>(stations);
            _roomHalfSize = roomHalfSize;
            _stationHalfSize = stationHalfSize;
            _radius = walkerRadius;
            _standDistance = standDistance;
            _width = (int)(roomHalfSize.X * 2f / CellSize) + 1;
            _height = (int)(roomHalfSize.Y * 2f / CellSize) + 1;
        }

        /// <summary>True if someone could stand here without touching furniture or leaving the room.</summary>
        public bool IsFree(Vector2 point)
        {
            if (Math.Abs(point.X) > _roomHalfSize.X || Math.Abs(point.Y) > _roomHalfSize.Y)
                return false;
            foreach (var station in _stations)
            {
                if (station.Contains(point, _stationHalfSize, _radius))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Where someone ends up moved by <paramref name="shift"/>: if furniture is in the way they
        /// slide along it, keeping whichever part of the move still fits, like a chef does.
        /// </summary>
        public Vector2 Slide(Vector2 from, Vector2 shift)
        {
            if (IsFree(from + shift))
                return from + shift;
            var alongX = new Vector2(from.X + shift.X, from.Y);
            if (shift.X != 0f && IsFree(alongX))
                return alongX;
            var alongY = new Vector2(from.X, from.Y + shift.Y);
            if (shift.Y != 0f && IsFree(alongY))
                return alongY;
            return from;
        }

        /// <summary>The free side of a station nearest the middle of the room, or null if it's boxed in.</summary>
        public Vector2? StandingSpot(Vector2 station) => StandingSpot(new Footprint(station));

        /// <summary>The same for a station at any rotation: its sides turn with it.</summary>
        public Vector2? StandingSpot(Footprint station)
        {
            Vector2? best = null;
            var bestDistance = float.MaxValue;
            foreach (var side in new[] { station.Forward, -station.Forward, -station.Right, station.Right })
            {
                var point = station.Centre + side * _standDistance;
                var distance = point.LengthSquared();
                if (IsFree(point) && distance < bestDistance)
                {
                    best = point;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// True if every station has somewhere to stand, and all those places connect, along with
        /// where everyone is, so nobody can be walled in. One flood over the floor rather than a
        /// route per station, since it runs as a station is carried.
        /// </summary>
        public bool EveryStationCanBeReached(IReadOnlyList<Footprint> stations, IEnumerable<Vector2> people = null)
        {
            var spots = new List<int>();
            foreach (var station in stations)
            {
                var spot = StandingSpot(station);
                if (!spot.HasValue)
                    return false;
                var cell = NearestFreeCell(spot.Value);
                if (cell < 0)
                    return false;
                spots.Add(cell);
            }
            if (people != null)
            {
                foreach (var person in people)
                {
                    var cell = NearestFreeCell(person);
                    if (cell < 0)
                        return false;
                    spots.Add(cell);
                }
            }
            if (spots.Count == 0)
                return true;

            var reached = new HashSet<int> { spots[0] };
            var frontier = new Queue<int>();
            frontier.Enqueue(spots[0]);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                var x = cell % _width;
                var y = cell / _width;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= _width || ny >= _height)
                        continue;
                    var neighbour = ny * _width + nx;
                    if (reached.Contains(neighbour) || !IsClear(Centre(cell), Centre(neighbour)))
                        continue;
                    reached.Add(neighbour);
                    frontier.Enqueue(neighbour);
                }
            }

            foreach (var cell in spots)
            {
                if (!reached.Contains(cell))
                    return false;
            }
            return true;
        }

        /// <summary>A route from one point to another around the furniture, ending at <paramref name="to"/>.</summary>
        /// <returns>The points to walk to in order, or null if there's no way there.</returns>
        public List<Vector2> FindPath(Vector2 from, Vector2 to)
        {
            if (IsClear(from, to))
                return new List<Vector2> { to };

            var start = NearestFreeCell(from);
            var goal = NearestFreeCell(to);
            if (start < 0 || goal < 0)
                return null;

            var cells = Search(start, goal);
            if (cells == null)
                return null;

            var points = new List<Vector2>();
            foreach (var cell in cells)
                points.Add(Centre(cell));
            points.Add(to);
            return Shorten(from, points);
        }

        // True if a straight walk stays clear: sampled finer than the walker's size.
        public bool IsClear(Vector2 from, Vector2 to)
        {
            var steps = Math.Max(1, (int)Math.Ceiling((to - from).Length() / (CellSize / 2f)));
            for (var i = 0; i <= steps; i++)
            {
                if (!IsFree(Vector2.Lerp(from, to, i / (float)steps)))
                    return false;
            }
            return true;
        }

        private List<Vector2> Shorten(Vector2 from, List<Vector2> points)
        {
            var result = new List<Vector2>();
            var current = from;
            var index = 0;
            while (index < points.Count)
            {
                // The furthest point still in a clear straight line from here.
                var next = index;
                for (var i = points.Count - 1; i > index; i--)
                {
                    if (IsClear(current, points[i]))
                    {
                        next = i;
                        break;
                    }
                }
                result.Add(points[next]);
                current = points[next];
                index = next + 1;
            }
            return result;
        }

        private List<int> Search(int start, int goal)
        {
            var cost = new Dictionary<int, float> { [start] = 0f };
            var cameFrom = new Dictionary<int, int>();
            var open = new List<int> { start };
            var closed = new HashSet<int>();
            var goalPoint = Centre(goal);

            while (open.Count > 0)
            {
                // The promising cell first: cost so far plus the straight line left.
                var bestIndex = 0;
                var bestScore = float.MaxValue;
                for (var i = 0; i < open.Count; i++)
                {
                    var score = cost[open[i]] + (Centre(open[i]) - goalPoint).Length();
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                var cell = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (cell == goal)
                    return Rebuild(cameFrom, cell);
                closed.Add(cell);

                var x = cell % _width;
                var y = cell / _width;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= _width || ny >= _height)
                            continue;

                        var neighbour = ny * _width + nx;
                        if (closed.Contains(neighbour) || !IsClear(Centre(cell), Centre(neighbour)))
                            continue;

                        var step = cost[cell] + (dx != 0 && dy != 0 ? CellSize * 1.414f : CellSize);
                        if (cost.TryGetValue(neighbour, out var known) && known <= step)
                            continue;
                        cost[neighbour] = step;
                        cameFrom[neighbour] = cell;
                        if (!open.Contains(neighbour))
                            open.Add(neighbour);
                    }
                }
            }
            return null;
        }

        private static List<int> Rebuild(Dictionary<int, int> cameFrom, int cell)
        {
            var path = new List<int> { cell };
            while (cameFrom.TryGetValue(cell, out var previous))
            {
                path.Insert(0, previous);
                cell = previous;
            }
            return path;
        }

        private Vector2 Centre(int cell) =>
            new Vector2(-_roomHalfSize.X + (cell % _width) * CellSize, -_roomHalfSize.Y + (cell / _width) * CellSize);

        private int NearestFreeCell(Vector2 point)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var cell = 0; cell < _width * _height; cell++)
            {
                var centre = Centre(cell);
                var distance = (centre - point).LengthSquared();
                if (distance < bestDistance && IsFree(centre))
                {
                    best = cell;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
