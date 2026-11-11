using System;

namespace Game.Simulation.Furniture
{
    /// <summary>
    /// Moving through the shop card's grid of pictures (the user's design, 2026-11-11): a page's
    /// pieces in rows of <see cref="Columns"/>, filled left to right. Left and right step one piece,
    /// running on into the next or previous row and round the ends; up and down move a whole row,
    /// staying put at the top and bottom (a short last row is reached by the nearest piece in it).
    /// </summary>
    public static class ShopGrid
    {
        public const int Columns = 3;

        /// <param name="index">The chosen piece.</param>
        /// <param name="count">Pieces on the page.</param>
        /// <param name="dx">-1 left, 1 right, 0 neither.</param>
        /// <param name="dy">-1 up, 1 down, 0 neither.</param>
        public static int Step(int index, int count, int dx, int dy)
        {
            if (count <= 0)
                return 0;
            index = Math.Clamp(index, 0, count - 1);
            if (dx != 0)
                return ((index + Math.Sign(dx)) % count + count) % count;
            if (dy == 0)
                return index;
            var row = index / Columns + Math.Sign(dy);
            var rows = (count + Columns - 1) / Columns;
            if (row < 0 || row >= rows)
                return index;
            return Math.Min(row * Columns + index % Columns, count - 1);
        }

        /// <summary>The first row to show when only <paramref name="shown"/> rows fit: the chosen piece's row always in view.</summary>
        public static int FirstRow(int index, int count, int shown)
        {
            var rows = (count + Columns - 1) / Columns;
            if (rows <= shown)
                return 0;
            var row = Math.Max(0, index) / Columns;
            return Math.Clamp(row - (shown - 1), 0, rows - shown);
        }
    }
}
