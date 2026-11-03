using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// The stack of clean plates (decision 2026-11-04: limited, washed plates come back). Takes
    /// back only clean, empty plates, and running out stops service until someone washes up.
    /// </summary>
    public sealed class PlateStack : IItemContainer
    {
        private readonly Stack<Plate> _plates = new Stack<Plate>();

        public PlateStack(int count, int plateCapacity)
        {
            for (var i = 0; i < count; i++)
                _plates.Push(new Plate(plateCapacity));
        }

        public int Count => _plates.Count;

        /// <summary>Raised when a plate goes back on the stack, so its view can go.</summary>
        public event Action<Plate> Returned;

        public bool CanTake => _plates.Count > 0;

        /// <summary>New plates join the stack.</summary>
        public void AddNew(int count, int plateCapacity)
        {
            for (var i = 0; i < count; i++)
                _plates.Push(new Plate(plateCapacity));
        }

        public bool CanAccept(Item item) => item is Plate plate && !plate.IsDirty && plate.Items.Count == 0;

        public Item Take()
        {
            if (_plates.Count == 0)
                throw new InvalidOperationException("No plates left.");
            return _plates.Pop();
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Only clean, empty plates go back on the stack.");
            var plate = (Plate)item;
            _plates.Push(plate);
            Returned?.Invoke(plate);
        }
    }
}
