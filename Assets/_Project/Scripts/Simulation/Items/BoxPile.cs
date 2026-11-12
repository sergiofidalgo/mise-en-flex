using System;

namespace Game.Simulation.Items
{
    /// <summary>Somewhere food in hand goes straight onto a fresh plate or box (ItemTransfer's last rule).</summary>
    public interface IFreshPlateSource : IItemContainer
    {
        /// <summary>True if the next one would take <paramref name="item"/>, without taking it.</summary>
        bool PeekAccepts(Item item);
    }

    /// <summary>
    /// The pile of delivery boxes (decision 2026-11-12): free and never runs out, so a delivery costs
    /// no plate and nothing to wash. An empty box can go back on it.
    /// </summary>
    public sealed class BoxPile : IPlateSource, IFreshPlateSource
    {
        private readonly int _boxCapacity;

        public BoxPile(int boxCapacity)
        {
            _boxCapacity = boxCapacity;
        }

        /// <summary>Raised when a box goes back on the pile, so its view can go.</summary>
        public event Action<Plate> Returned;

        public bool CanTake => true;

        public bool CanAccept(Item item) => item is Plate plate && plate.IsBox && plate.Items.Count == 0;

        public bool PeekAccepts(Item item) => item != null && !(item is IItemCollection) && _boxCapacity > 0;

        public Item Take() => Plate.NewBox(_boxCapacity);

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Only empty boxes go back on the pile.");
            Returned?.Invoke((Plate)item);
        }
    }
}
