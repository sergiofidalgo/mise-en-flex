using System;
using System.Collections.Generic;
using Game.Simulation.Cleaning;

namespace Game.Simulation.Saving
{
    /// <summary>Dirt to and from the save: spots on furniture point at its station record.</summary>
    public static class DirtRecords
    {
        /// <param name="stationOf">The station record index of a spot's host, or -1 if it isn't saved.</param>
        public static void Write(DirtMap dirt, Func<object, int> stationOf, List<DirtRecord> into)
        {
            foreach (var spot in dirt.Spots)
            {
                var station = spot.Host != null ? stationOf(spot.Host) : -1;
                // Dirt on a piece that isn't kept goes with it.
                if (spot.Host != null && station < 0)
                    continue;
                into.Add(new DirtRecord
                {
                    Kind = (int)spot.Kind,
                    Surface = (int)spot.Surface,
                    Station = station,
                    X = spot.X,
                    Z = spot.Z,
                    Amount = spot.Amount,
                    Peak = spot.Peak,
                });
            }
        }

        /// <param name="hostAt">The furniture rebuilt for a station record, or null if it's gone.</param>
        public static void Read(IEnumerable<DirtRecord> records, Func<int, object> hostAt, DirtMap into)
        {
            if (records == null)
                return;
            foreach (var record in records)
            {
                if (record == null || !Enum.IsDefined(typeof(DirtSurface), record.Surface) || !Enum.IsDefined(typeof(DirtKind), record.Kind))
                    continue;
                var surface = (DirtSurface)record.Surface;
                object host = null;
                if (surface != DirtSurface.Floor)
                {
                    host = record.Station >= 0 ? hostAt(record.Station) : null;
                    if (host == null)
                        continue;
                }
                into.Restore((DirtKind)record.Kind, surface, host, record.X, record.Z, record.Amount, record.Peak);
            }
        }
    }
}
