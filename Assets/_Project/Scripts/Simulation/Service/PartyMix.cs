using System;
using System.Collections.Generic;

namespace Game.Simulation.Service
{
    /// <summary>
    /// How big a group walks in, going by the seating: the biggest table sets the biggest group, and
    /// each table votes. A stool votes for someone alone; a table for four splits its vote between
    /// groups of one, two, three and four. So the mix follows the layout: three stools and one table
    /// for four mostly bring people alone, instead of sending most groups to that one table.
    /// </summary>
    public static class PartyMix
    {
        /// <summary>How likely a group of this size is, given how many chairs each table has.</summary>
        public static float Chance(int size, IReadOnlyList<int> tables)
        {
            if (tables.Count == 0)
                return 0f;
            var votes = 0f;
            foreach (var chairs in tables)
            {
                if (chairs >= size)
                    votes += 1f / chairs;
            }
            return votes / tables.Count;
        }

        /// <summary>Rolls a group's size. With only single seats it's always one, and rolls nothing.</summary>
        public static int PickSize(IReadOnlyList<int> tables, Random random)
        {
            var biggest = 1;
            foreach (var chairs in tables)
                biggest = Math.Max(biggest, chairs);
            if (biggest <= 1)
                return 1;

            var roll = (float)random.NextDouble();
            for (var size = 1; size < biggest; size++)
            {
                roll -= Chance(size, tables);
                if (roll < 0f)
                    return size;
            }
            return biggest;
        }
    }
}
