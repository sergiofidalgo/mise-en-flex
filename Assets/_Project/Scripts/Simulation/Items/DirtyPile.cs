using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// Where dirty plates wait for the sink (decision 2026-11-04). It has a limit, so falling behind
    /// on washing up shows: with the pile full, plates stay on the counters and the stools stay blocked.
    /// </summary>
    public sealed class DirtyPile : IPlateSource
    {
        private readonly Queue<Plate> _plates = new Queue<Plate>();

        public DirtyPile(int capacity)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public int Count => _plates.Count;

        /// <summary>Raised when a plate goes on the pile, so its view can go.</summary>
        public event Action<Plate> Received;

        public bool CanTake => _plates.Count > 0;

        public bool CanAccept(Item item) => item is Plate plate && plate.IsDirty && _plates.Count < Capacity;

        /// <summary>The plate that's been waiting longest.</summary>
        public Item Take()
        {
            if (_plates.Count == 0)
                throw new InvalidOperationException("No dirty plates here.");
            return _plates.Dequeue();
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Only dirty plates go on the pile, up to its limit.");
            Add((Plate)item);
        }

        /// <summary>For someone leaving with a plate and nowhere to put it: it can't just vanish.</summary>
        public void PutOverLimit(Plate plate) => Add(plate);

        private void Add(Plate plate)
        {
            _plates.Enqueue(plate);
            Received?.Invoke(plate);
        }
    }
}
