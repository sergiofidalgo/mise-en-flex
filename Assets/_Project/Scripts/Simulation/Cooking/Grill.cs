using System;
using Game.Simulation.Items;

namespace Game.Simulation.Cooking
{
    /// <summary>
    /// One zone of a charcoal grill: a grate that's always at its temperature, holding up to two
    /// pieces of food, or a pan or pot taking the whole zone (decisions 2026-11-07).
    /// </summary>
    public sealed class GrillZone : IItemContainer, IItemHolder
    {
        internal GrillZone(bool isHot, Pan grate, Hob source)
        {
            IsHot = isHot;
            Grate = grate;
            Source = source;
        }

        public bool IsHot { get; }

        /// <summary>Food on the bars. It never moves and is never carried.</summary>
        public Pan Grate { get; }

        /// <summary>The coals under the zone, as a heat source for a pan put on it.</summary>
        public Hob Source { get; }

        public Pan StandingPan => Source.Pan;

        public bool IsEmpty => StandingPan == null && Grate.Contents.Count == 0;

        /// <summary>The hotter piece on the grate: what A and X act on when there are two.</summary>
        public Ingredient Hottest
        {
            get
            {
                Ingredient hottest = null;
                foreach (var piece in Grate.Contents)
                {
                    if (hottest == null || piece.AverageTemperature > hottest.AverageTemperature)
                        hottest = piece;
                }
                return hottest;
            }
        }

        /// <summary>Room for one more piece on the grate, wherever that piece is now.</summary>
        public bool HasRoomOnGrate => StandingPan == null && Grate.Contents.Count < Grate.Properties.Capacity;

        /// <summary>The pan on it, or the hotter piece on the grate.</summary>
        public Item Current => (Item)StandingPan ?? Hottest;

        public bool CanTake => Current != null;

        /// <summary>A pan needs the zone to itself; food needs no pan there and room on the grate.</summary>
        public bool CanAccept(Item item)
        {
            if (StandingPan != null)
                return false;
            if (item is Pan)
                return Grate.Contents.Count == 0;
            return Grate.CanAccept(item);
        }

        public Item Take()
        {
            if (StandingPan != null)
                return Source.RemovePan();
            var piece = Hottest ?? throw new InvalidOperationException("Nothing on this zone.");
            Grate.TakeOut(piece);
            return piece;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("This zone can't take that.");
            if (item is Pan pan)
                Source.PlacePan(pan);
            else
                Grate.Put(item);
        }
    }

    /// <summary>
    /// A charcoal grill: a hot zone and a cool zone side by side, always lit (decisions 2026-11-07).
    /// Its heat isn't turned up or down; food goes hotter or cooler by moving, and X flips a piece
    /// over onto the other zone.
    /// </summary>
    public sealed class Grill
    {
        public const int PiecesPerZone = 2;

        public Grill(CookingSimulation cooking, PanProperties hotGrate, PanProperties coolGrate, HobProperties hotSource, HobProperties coolSource)
        {
            Hot = new GrillZone(true, cooking.AddPan(hotGrate), cooking.AddHob(hotSource));
            Cool = new GrillZone(false, cooking.AddPan(coolGrate), cooking.AddHob(coolSource));
        }

        public GrillZone Hot { get; }

        public GrillZone Cool { get; }

        public GrillZone Other(GrillZone zone) => zone == Hot ? Cool : Hot;

        /// <summary>
        /// X: the hotter piece on <paramref name="from"/> turns over onto the other zone. In a pan,
        /// it's the pan's food that turns, and nothing moves.
        /// </summary>
        /// <returns>False if there's nothing to flip, or no room on the other side.</returns>
        public bool TryFlip(GrillZone from)
        {
            if (from.StandingPan != null)
            {
                if (from.StandingPan.Contents.Count == 0)
                    return false;
                from.StandingPan.Contents[0].Flip();
                return true;
            }

            var piece = from.Hottest;
            var to = Other(from);
            if (piece == null || !to.HasRoomOnGrate)
                return false;

            from.Grate.TakeOut(piece);
            piece.Flip();
            to.Grate.Put(piece);
            return true;
        }

        /// <summary>Sold: the grates and coals stop simulating. It has to be empty.</summary>
        public void Remove(CookingSimulation cooking)
        {
            foreach (var zone in new[] { Hot, Cool })
            {
                cooking.Remove(zone.Grate);
                cooking.Remove(zone.Source);
            }
        }
    }
}
