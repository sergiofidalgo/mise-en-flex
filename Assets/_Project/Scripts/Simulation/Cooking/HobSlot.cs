using System;
using Game.Simulation.Items;

namespace Game.Simulation.Cooking
{
    /// <summary>
    /// The place on a hob where a pan stands. Only pans go here; putting one down or lifting
    /// it off moves it on or off the hob's heat.
    /// </summary>
    public sealed class HobSlot : IItemContainer, IItemHolder
    {
        public HobSlot(Hob hob)
        {
            Hob = hob;
        }

        public Hob Hob { get; }

        public Item Current => Hob.Pan;

        public bool CanTake => Hob.Pan != null;

        public bool CanAccept(Item item) => item is Pan && Hob.Pan == null;

        public Item Take()
        {
            if (Hob.Pan == null)
                throw new InvalidOperationException("No pan on the hob.");
            return Hob.RemovePan();
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Only a pan can go on an empty hob.");
            Hob.PlacePan((Pan)item);
        }
    }
}
