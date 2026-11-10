using System.Collections.Generic;
using System.Linq;

namespace Game.Simulation.Campaign
{
    /// <summary>What a piece of furniture is to a working kitchen: one of each kept role is never taken.</summary>
    public enum ApplianceRole
    {
        Other = 0,
        Hob,
        Board,
        Pass,
        Storage,

        /// <summary>Somewhere to seat a guest: without one, nobody can be served.</summary>
        Table,
    }

    /// <summary>One piece in the room, as the bodyguards see it.</summary>
    public readonly struct Appliance
    {
        public Appliance(int index, ApplianceRole role, float resale, bool forSale = true)
        {
            Index = index;
            Role = role;
            Resale = resale;
            ForSale = forSale;
        }

        /// <summary>Whatever the caller uses to find it again (its place in the station list).</summary>
        public int Index { get; }

        public ApplianceRole Role { get; }

        /// <summary>What selling it would fetch: what taking it knocks off the debt.</summary>
        public float Resale { get; }

        /// <summary>Only catalogue pieces are taken: the sink and the like are part of the building.</summary>
        public bool ForSale { get; }
    }

    /// <summary>
    /// Which appliances the bodyguards carry out (§15.3; the user's call, 2026-11-10, "comic menace").
    /// A repossession takes the most valuable pieces first until what they'd fetch covers the
    /// shortfall; each is credited at resale value, as selling it would have been. The bare minimum a
    /// kitchen needs to keep trading (one hob, board, pass, storage and table) is never taken, so a
    /// restaurant can always earn its way back. Stripping (step 3 with one restaurant) takes
    /// everything else, keeping the cheapest of each.
    /// </summary>
    public static class Repossession
    {
        private static readonly ApplianceRole[] Kept =
        {
            ApplianceRole.Hob, ApplianceRole.Board, ApplianceRole.Pass, ApplianceRole.Storage, ApplianceRole.Table,
        };

        /// <summary>The pieces taken for a shortfall, in the order they're carried out.</summary>
        public static IReadOnlyList<Appliance> ForShortfall(IEnumerable<Appliance> room, float shortfall)
        {
            var taken = new List<Appliance>();
            if (shortfall <= 0f)
                return taken;
            var credited = 0f;
            foreach (var piece in Takeable(room).OrderByDescending(a => a.Resale).ThenBy(a => a.Index))
            {
                if (credited >= shortfall)
                    break;
                taken.Add(piece);
                credited += piece.Resale;
            }
            return taken;
        }

        /// <summary>Everything but the bare minimum, most valuable first.</summary>
        public static IReadOnlyList<Appliance> StripToMinimum(IEnumerable<Appliance> room) =>
            Takeable(room).OrderByDescending(a => a.Resale).ThenBy(a => a.Index).ToList();

        /// <summary>What the pieces knock off the debt.</summary>
        public static float Credit(IEnumerable<Appliance> taken) => taken.Sum(a => a.Resale);

        // Every for-sale piece except the cheapest one of each kept role.
        private static IEnumerable<Appliance> Takeable(IEnumerable<Appliance> room)
        {
            var pieces = room.ToList();
            var spared = new HashSet<int>();
            foreach (var role in Kept)
            {
                // The building's own piece of a role (a pass that isn't for sale) already covers it.
                var ofRole = pieces.Where(a => a.Role == role).OrderBy(a => a.ForSale ? 1 : 0).ThenBy(a => a.Resale).ThenBy(a => a.Index);
                foreach (var keep in ofRole.Take(1))
                    spared.Add(keep.Index);
            }
            return pieces.Where(a => a.ForSale && !spared.Contains(a.Index));
        }
    }
}
