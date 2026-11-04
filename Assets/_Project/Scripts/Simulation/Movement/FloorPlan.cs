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
    }
}
