using System;
using System.Collections.Generic;

namespace Game.Simulation.Cleaning
{
    /// <summary>Where dirt lies (game design §15.8). The floor takes a mop; tables and counters a cloth.</summary>
    public enum DirtSurface
    {
        Floor,
        Table,
        Counter,
    }

    /// <summary>What made it, for how it looks. Every kind is cleaned the same way.</summary>
    public enum DirtKind
    {
        /// <summary>Crumbs and sauce where a guest ate.</summary>
        Smudge,

        /// <summary>A dropped plate: food and shards on the floor.</summary>
        Spill,

        /// <summary>Soot and grease by a pan that burnt on the heat.</summary>
        Scorch,

        /// <summary>Footprints where guests come and go.</summary>
        Scuff,

        /// <summary>What the extinguisher leaves behind.</summary>
        Foam,
    }

    /// <summary>
    /// One patch of dirt. On a table or counter it belongs to that piece (<see cref="Host"/>) and its
    /// position is relative to it, so it moves when the piece is moved; on the floor it's in the room.
    /// </summary>
    public sealed class DirtSpot
    {
        internal DirtSpot(int id, DirtKind kind, DirtSurface surface, object host, float x, float z, float amount)
        {
            Id = id;
            Kind = kind;
            Surface = surface;
            Host = host;
            X = x;
            Z = z;
            Amount = amount;
            Peak = amount;
        }

        public int Id { get; }
        public DirtKind Kind { get; }
        public DirtSurface Surface { get; }

        /// <summary>The table or counter it's on, or null on the floor. Opaque to the simulation.</summary>
        public object Host { get; }

        /// <summary>Metres: in the room on the floor, from the host's centre otherwise (in its own axes).</summary>
        public float X { get; }
        public float Z { get; }

        /// <summary>Wipes of work left: one wipe is one loop of the cleaning game.</summary>
        public float Amount { get; internal set; }

        /// <summary>The most it has held since it last came clean, so a view can fade it as it's wiped.</summary>
        public float Peak { get; internal set; }

        /// <summary>0..1: how much of it is still there, against <see cref="Peak"/>.</summary>
        public float Remaining => Peak > 0f ? Math.Clamp(Amount / Peak, 0f, 1f) : 0f;
    }

    /// <summary>Tuning for dirt: how much each event leaves, and what dirt costs in reviews.</summary>
    public sealed class DirtSettings
    {
        /// <summary>Wipes a diner leaves on their table when they finish.</summary>
        public float DinerSmudge { get; set; } = 1f;

        /// <summary>Wipes a dropped or broken plate leaves on the floor.</summary>
        public float PlateSpill { get; set; } = 3f;

        /// <summary>Wipes a pan burnt on the heat leaves beside its hob.</summary>
        public float BurntScorch { get; set; } = 2f;

        /// <summary>Guests through the dining room per floor scuff.</summary>
        public int GuestsPerScuff { get; set; } = 6;

        /// <summary>Wipes each floor scuff takes.</summary>
        public float Scuff { get; set; } = 1f;

        /// <summary>Wipes the foam from a fire put out takes.</summary>
        public float FireFoam { get; set; } = 2f;

        /// <summary>Metres: new dirt this close to a spot on the same surface adds to it instead of making another.</summary>
        public float MergeRadius { get; set; } = 0.35f;

        /// <summary>The most one spot can hold, so a table used all night isn't a five-minute job.</summary>
        public float MaxPerSpot { get; set; } = 4f;

        /// <summary>Score (0..1) a guest loses for a full wipe's dirt on their own table; less for less.</summary>
        public float TablePenalty { get; set; } = 0.1f;

        /// <summary>Score lost per wipe of floor dirt within <see cref="FloorRadius"/> of their seat.</summary>
        public float FloorPenalty { get; set; } = 0.04f;

        /// <summary>Metres from the seat that floor dirt is noticed.</summary>
        public float FloorRadius { get; set; } = 1.5f;

        /// <summary>The most dirt can take off a dish's score.</summary>
        public float MaxPenalty { get; set; } = 0.2f;

        /// <summary>A penalty at least this big makes the dirt what the guest remembers, if nothing worse happened.</summary>
        public float NoticedFrom { get; set; } = 0.05f;
    }

    /// <summary>
    /// The restaurant's dirt (decisions 2026-11-08 and 2026-11-10): what service left on floors,
    /// tables and counters. Kept overnight with the restaurant and cleaned by hand. Plain data: the
    /// gameplay layer says where things happened and shows the spots.
    /// </summary>
    public sealed class DirtMap
    {
        // Under this much a spot counts as clean and goes.
        private const float CleanBelow = 0.001f;

        private readonly List<DirtSpot> _spots = new List<DirtSpot>();
        private int _nextId = 1;

        public DirtMap(DirtSettings settings = null)
        {
            Settings = settings ?? new DirtSettings();
        }

        /// <summary>Read live, so tuning edits apply in play.</summary>
        public DirtSettings Settings { get; set; }

        public IReadOnlyList<DirtSpot> Spots => _spots;

        /// <summary>A new spot appeared.</summary>
        public event Action<DirtSpot> Added;

        /// <summary>A spot got dirtier or was partly wiped.</summary>
        public event Action<DirtSpot> Changed;

        /// <summary>A spot came clean, or went with the furniture it was on.</summary>
        public event Action<DirtSpot> Removed;

        /// <summary>All the wipes of work in the restaurant.</summary>
        public float Total
        {
            get
            {
                var total = 0f;
                foreach (var spot in _spots)
                    total += spot.Amount;
                return total;
            }
        }

        /// <summary>
        /// Leaves dirt. Near a spot of the same kind already on the same surface (and the same piece),
        /// it adds to that one, up to <see cref="DirtSettings.MaxPerSpot"/>, so a busy table gets dirtier rather than
        /// collecting dozens of crumbs.
        /// </summary>
        public DirtSpot Add(DirtKind kind, DirtSurface surface, object host, float x, float z, float amount)
        {
            if (amount <= 0f)
                return null;
            if (surface == DirtSurface.Floor)
                host = null;

            var near = Nearest(surface, host, x, z, Settings.MergeRadius, kind);
            if (near != null)
            {
                near.Amount = Math.Min(Settings.MaxPerSpot, near.Amount + amount);
                near.Peak = Math.Max(near.Peak, near.Amount);
                Changed?.Invoke(near);
                return near;
            }

            var spot = new DirtSpot(_nextId++, kind, surface, host, x, z, Math.Min(Settings.MaxPerSpot, amount));
            _spots.Add(spot);
            Added?.Invoke(spot);
            return spot;
        }

        /// <summary>A spot from a save, as it was left (partly wiped included).</summary>
        public DirtSpot Restore(DirtKind kind, DirtSurface surface, object host, float x, float z, float amount, float peak)
        {
            if (amount <= CleanBelow)
                return null;
            var spot = new DirtSpot(_nextId++, kind, surface, surface == DirtSurface.Floor ? null : host, x, z, Math.Min(Settings.MaxPerSpot, amount));
            spot.Peak = Math.Max(spot.Amount, Math.Min(Settings.MaxPerSpot, peak));
            _spots.Add(spot);
            Added?.Invoke(spot);
            return spot;
        }

        /// <summary>Wipes some of a spot away.</summary>
        /// <returns>True if that left it clean (and it's gone).</returns>
        public bool Clean(DirtSpot spot, float wipes)
        {
            if (spot == null || wipes <= 0f || !_spots.Contains(spot))
                return false;
            spot.Amount -= wipes;
            if (spot.Amount > CleanBelow)
            {
                Changed?.Invoke(spot);
                return false;
            }
            spot.Amount = 0f;
            _spots.Remove(spot);
            Removed?.Invoke(spot);
            return true;
        }

        /// <summary>The furniture went (sold, or the layout was rebuilt): its dirt goes with it.</summary>
        public void RemoveOn(object host)
        {
            for (var i = _spots.Count - 1; i >= 0; i--)
            {
                var spot = _spots[i];
                if (spot.Host == null || !ReferenceEquals(spot.Host, host))
                    continue;
                _spots.RemoveAt(i);
                Removed?.Invoke(spot);
            }
        }

        /// <summary>Everything goes, e.g. before a save is loaded over it.</summary>
        public void Clear()
        {
            for (var i = _spots.Count - 1; i >= 0; i--)
            {
                var spot = _spots[i];
                _spots.RemoveAt(i);
                Removed?.Invoke(spot);
            }
        }

        /// <summary>Spots on one table or counter.</summary>
        public IEnumerable<DirtSpot> On(object host)
        {
            foreach (var spot in _spots)
            {
                if (spot.Host != null && ReferenceEquals(spot.Host, host))
                    yield return spot;
            }
        }

        /// <summary>Wipes of dirt on one table or counter.</summary>
        public float AmountOn(object host)
        {
            var total = 0f;
            foreach (var spot in On(host))
                total += spot.Amount;
            return total;
        }

        /// <summary>The nearest floor spot within <paramref name="radius"/> metres, or null.</summary>
        public DirtSpot NearestOnFloor(float x, float z, float radius) => Nearest(DirtSurface.Floor, null, x, z, radius);

        /// <summary>Wipes of floor dirt within <paramref name="radius"/> metres.</summary>
        public float FloorAmountNear(float x, float z, float radius)
        {
            var total = 0f;
            foreach (var spot in _spots)
            {
                if (spot.Surface == DirtSurface.Floor && Distance(spot.X, spot.Z, x, z) <= radius)
                    total += spot.Amount;
            }
            return total;
        }

        /// <summary>
        /// What dirt takes off a guest's dish score (0..<see cref="DirtSettings.MaxPenalty"/>): the
        /// dirt on their table, plus floor dirt near their seat. Guests notice a mess; a crumb or two
        /// costs little.
        /// </summary>
        /// <param name="table">Their table or counter, or null.</param>
        public float PenaltyFor(object table, float seatX, float seatZ)
        {
            var s = Settings;
            var onTable = table != null ? Math.Min(1f, AmountOn(table)) : 0f;
            var penalty = onTable * s.TablePenalty + FloorAmountNear(seatX, seatZ, s.FloorRadius) * s.FloorPenalty;
            return Math.Clamp(penalty, 0f, s.MaxPenalty);
        }

        private DirtSpot Nearest(DirtSurface surface, object host, float x, float z, float radius, DirtKind? kind = null)
        {
            DirtSpot best = null;
            var bestDistance = radius;
            foreach (var spot in _spots)
            {
                if (spot.Surface != surface || !ReferenceEquals(spot.Host, host) || (kind.HasValue && spot.Kind != kind.Value))
                    continue;
                var distance = Distance(spot.X, spot.Z, x, z);
                if (distance > bestDistance)
                    continue;
                best = spot;
                bestDistance = distance;
            }
            return best;
        }

        private static float Distance(float ax, float az, float bx, float bz)
        {
            var dx = ax - bx;
            var dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
