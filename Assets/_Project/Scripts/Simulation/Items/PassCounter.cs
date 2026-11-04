using System;

namespace Game.Simulation.Items
{
    /// <summary>Somewhere a dirty plate can be collected from, for whoever clears up.</summary>
    public interface IDirtyPlateSource
    {
        bool HasDirtyPlate { get; }

        Plate TakeDirtyPlate();
    }

    /// <summary>
    /// The counter in front of a stool (decision 2026-11-04). It holds one plate with food, which
    /// can be taken back until a customer starts on it; then it's theirs until they've finished,
    /// and what's left is a dirty plate that can be cleared.
    /// </summary>
    public sealed class PassCounter : IItemContainer, IDirtyPlateSource
    {
        private bool _beingEaten;

        public Plate Current { get; private set; }

        /// <summary>Raised when a plate is put down.</summary>
        public event Action<Plate> Placed;

        public bool HasDirtyPlate => Current != null && Current.IsDirty;

        /// <summary>A plate with food, waiting for a customer.</summary>
        public bool IsWaitingForCustomer => Current != null && !Current.IsDirty && !_beingEaten;

        public bool CanTake => Current != null && !_beingEaten;

        public bool CanAccept(Item item) => Current == null && item is Plate plate && !plate.IsDirty && plate.Items.Count > 0;

        public Item Take()
        {
            if (!CanTake)
                throw new InvalidOperationException("A customer is eating from that plate.");
            var plate = Current;
            Current = null;
            return plate;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Only a clean plate with food goes on the pass.");
            Current = (Plate)item;
            Placed?.Invoke(Current);
        }

        /// <summary>A customer has started on the plate: it can't be taken back now.</summary>
        public void StartEating() => _beingEaten = true;

        /// <summary>The customer has finished: the plate is dirty and can be cleared.</summary>
        public void FinishEating()
        {
            _beingEaten = false;
            Current?.MarkDirty();
        }

        public Plate TakeDirtyPlate()
        {
            if (!HasDirtyPlate)
                throw new InvalidOperationException("No dirty plate here.");
            return (Plate)Take();
        }
    }
}
