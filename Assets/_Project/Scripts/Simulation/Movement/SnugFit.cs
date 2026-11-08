using System.Collections.Generic;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Where something smaller than its floor slot sits within it: pushed up against whatever is
    /// beside it, a wall or another station, so the bin stands snug in a corner or against the
    /// counters instead of floating in the middle (the user's note, 2026-11-08). Its slot, and so
    /// the layout, stays the same; only where it shows changes.
    /// </summary>
    public static class SnugFit
    {
        // How far past the slot's edge to look for something beside it.
        private const float Probe = 0.06f;

        private static readonly Vector2[] Sides = { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };

        /// <summary>
        /// The offset from the slot's centre, on the floor: <paramref name="slack"/> towards each
        /// side that has a wall or a station beside it. Blocked on opposite sides, it stays centred
        /// along that axis; with something on two adjacent sides it goes into the corner.
        /// </summary>
        public static Vector2 Offset(FloorPlan building, Vector2 centre, float slotHalfSize, float slack, IEnumerable<Footprint> neighbours)
        {
            var offset = Vector2.Zero;
            var others = new List<Footprint>(neighbours);
            foreach (var side in Sides)
            {
                if (IsBlocked(building, centre + side * (slotHalfSize + Probe), others))
                    offset += side * slack;
            }
            return offset;
        }

        private static bool IsBlocked(FloorPlan building, Vector2 point, List<Footprint> others)
        {
            // Outer walls stand a walker's radius beyond the room's bounds.
            var min = building.RoomMin - new Vector2(building.WalkerRadius);
            var max = building.RoomMax + new Vector2(building.WalkerRadius);
            if (point.X < min.X || point.Y < min.Y || point.X > max.X || point.Y > max.Y)
                return true;

            foreach (var wall in building.Walls)
            {
                if (point.X >= wall.Min.X && point.X <= wall.Max.X && point.Y >= wall.Min.Y && point.Y <= wall.Max.Y)
                    return true;
            }

            foreach (var other in others)
            {
                if (other.Contains(point, building.StationHalfSize))
                    return true;
            }
            return false;
        }
    }
}
