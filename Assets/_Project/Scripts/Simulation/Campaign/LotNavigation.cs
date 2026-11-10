using System;
using System.Collections.Generic;

namespace Game.Simulation.Campaign
{
    /// <summary>
    /// Moving the miniature's highlight from lot to lot with the stick: a push goes to the nearest
    /// lot that lies roughly that way on the table, as seen from the camera (x right, z away).
    /// </summary>
    public static class LotNavigation
    {
        /// <summary>
        /// The lot a push in (<paramref name="dx"/>, <paramref name="dz"/>) moves to from
        /// <paramref name="current"/>, or <paramref name="current"/> if none lies that way.
        /// </summary>
        /// <param name="cone">How far off the push a lot may lie and still count, in degrees either side.</param>
        public static int Next(IReadOnlyList<(float X, float Z)> lots, int current, float dx, float dz, float cone = 65f)
        {
            if (lots == null || lots.Count == 0)
                return -1;
            if (current < 0 || current >= lots.Count)
                return 0;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < 1e-4f)
                return current;
            dx /= length;
            dz /= length;
            var minCos = (float)Math.Cos(cone * Math.PI / 180.0);

            var best = current;
            var bestScore = float.MaxValue;
            var from = lots[current];
            for (var i = 0; i < lots.Count; i++)
            {
                if (i == current)
                    continue;
                var ox = lots[i].X - from.X;
                var oz = lots[i].Z - from.Z;
                var distance = (float)Math.Sqrt(ox * ox + oz * oz);
                if (distance < 1e-4f)
                    continue;
                var cos = (ox * dx + oz * dz) / distance;
                if (cos < minCos)
                    continue;
                // Nearer wins, and straighter ahead wins among similar distances: a lot straight
                // across is "right" before one a little nearer but well off to the side.
                var score = distance * (2f - cos);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}
