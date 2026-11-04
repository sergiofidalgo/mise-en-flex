using System;
using System.Numerics;

namespace Game.Simulation.Movement
{
    /// <summary>
    /// Where the street door can go: on the dining room's outer walls, facing in. Close to a wall it
    /// snaps onto it, and anywhere else it can't be put down.
    /// </summary>
    public static class DoorPlacement
    {
        /// <param name="point">Where the player is carrying it.</param>
        /// <param name="min">The corner where the south and west walls' door lines meet.</param>
        /// <param name="max">The other end: X is the east wall's line, Y how far north the side walls go.</param>
        /// <param name="snapRange">How close to a wall it has to be, in metres.</param>
        /// <param name="endMargin">How far from a corner it has to stay, for the width of the door.</param>
        public static (Vector2 Position, float Degrees, bool OnAWall) Fit(Vector2 point, Vector2 min, Vector2 max, float snapRange, float endMargin)
        {
            var toSouth = Math.Abs(point.Y - min.Y);
            var toWest = point.Y <= max.Y + snapRange ? Math.Abs(point.X - min.X) : float.MaxValue;
            var toEast = point.Y <= max.Y + snapRange ? Math.Abs(point.X - max.X) : float.MaxValue;
            var nearest = Math.Min(toSouth, Math.Min(toWest, toEast));
            if (nearest > snapRange)
                return (point, 0f, false);

            var alongSouth = Clamp(point.X, min.X + endMargin, max.X - endMargin);
            var alongSides = Clamp(point.Y, min.Y + endMargin, max.Y);

            // Degrees are clockwise from above, and an unturned door faces north, into the room.
            if (nearest == toSouth)
                return (new Vector2(alongSouth, min.Y), 0f, true);
            return nearest == toWest
                ? (new Vector2(min.X, alongSides), 90f, true)
                : (new Vector2(max.X, alongSides), 270f, true);
        }

        private static float Clamp(float value, float low, float high) => Math.Max(low, Math.Min(high, value));
    }
}
