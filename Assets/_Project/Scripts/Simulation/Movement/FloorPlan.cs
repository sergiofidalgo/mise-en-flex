using System;
using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// The building people walk around in: where they can be, and the walls inside it. Positions
    /// are on the floor, in metres. Furniture is separate, because that moves.
    /// </summary>
    public sealed class FloorPlan
    {
        // Snapped furniture sits exactly against a wall; a little slack for what's placed by hand.
        private const float TouchingDistance = 0.06f;

        public FloorPlan(Vector2 roomMin, Vector2 roomMax, IReadOnlyList<(Vector2 Min, Vector2 Max)> walls, float stationHalfSize, float walkerRadius, float standDistance)
        {
            RoomMin = roomMin;
            RoomMax = roomMax;
            Walls = walls;
            StationHalfSize = stationHalfSize;
            WalkerRadius = walkerRadius;
            StandDistance = standDistance;
        }

        /// <summary>The corner of where someone's centre can be, clear of the outer walls.</summary>
        public Vector2 RoomMin { get; }

        public Vector2 RoomMax { get; }

        /// <summary>Walls inside the building, as rectangles; a door is a gap between two of them.</summary>
        public IReadOnlyList<(Vector2 Min, Vector2 Max)> Walls { get; }

        public float StationHalfSize { get; }

        public float WalkerRadius { get; }

        public float StandDistance { get; }

        /// <summary>True if someone could stand here, as far as the building goes: furniture aside.</summary>
        public bool CanStand(Vector2 point)
        {
            if (point.X < RoomMin.X || point.X > RoomMax.X || point.Y < RoomMin.Y || point.Y > RoomMax.Y)
                return false;
            foreach (var wall in Walls)
            {
                if ((Closest(wall, point) - point).LengthSquared() < WalkerRadius * WalkerRadius)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// True if someone here can reach this station. A wall is in the way, unless the station is
        /// right up against that wall: then it works like a hatch, from both sides.
        /// </summary>
        public bool CanReach(Vector2 from, Footprint station)
        {
            foreach (var wall in Walls)
            {
                if (Crosses(wall, from, station.Centre) && !station.Contains(Closest(wall, station.Centre), StationHalfSize, TouchingDistance))
                    return false;
            }
            return true;
        }

        private static Vector2 Closest((Vector2 Min, Vector2 Max) wall, Vector2 point) =>
            new Vector2(Math.Max(wall.Min.X, Math.Min(wall.Max.X, point.X)), Math.Max(wall.Min.Y, Math.Min(wall.Max.Y, point.Y)));

        // Whether a straight line passes through a rectangle: where it's inside the rectangle's
        // range on each axis has to overlap, somewhere along the line (the slab test).
        private static bool Crosses((Vector2 Min, Vector2 Max) wall, Vector2 from, Vector2 to)
        {
            var enter = 0f;
            var leave = 1f;
            return Narrow(from.X, to.X - from.X, wall.Min.X, wall.Max.X, ref enter, ref leave)
                && Narrow(from.Y, to.Y - from.Y, wall.Min.Y, wall.Max.Y, ref enter, ref leave);
        }

        private static bool Narrow(float start, float along, float min, float max, ref float enter, ref float leave)
        {
            if (Math.Abs(along) < 1e-6f)
                return start >= min && start <= max;
            var first = (min - start) / along;
            var second = (max - start) / along;
            enter = Math.Max(enter, Math.Min(first, second));
            leave = Math.Min(leave, Math.Max(first, second));
            return enter <= leave;
        }
    }
}
