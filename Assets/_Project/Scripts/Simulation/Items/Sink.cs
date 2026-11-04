using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// Where plates get washed. It holds only a couple of plates at a time, dirty or clean (decision
    /// 2026-11-04): one is washed at a time, and clean ones wait to be taken to the stack, so a sink
    /// full of clean plates has to be emptied before washing more. Whoever washes (a player scrubbing, a dishwasher working)
    /// adds to the progress of the plate in hand.
    /// </summary>
    public sealed class Sink : IPlateSource
    {
        private readonly Queue<Plate> _dirty = new Queue<Plate>();
        private readonly Stack<Plate> _clean = new Stack<Plate>();

        /// <param name="capacity">Plates it holds, dirty and clean together.</param>
        public Sink(int capacity = int.MaxValue)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public bool HasRoom => _dirty.Count + _clean.Count < Capacity;

        public int DirtyCount => _dirty.Count;
        public int CleanCount => _clean.Count;

        /// <summary>0..1 through the plate being washed.</summary>
        public float WashProgress { get; private set; }

        /// <summary>Plates broken at this sink and not yet replaced.</summary>
        public int Broken { get; private set; }

        /// <summary>The broken count, cleared because the plates are being replaced.</summary>
        public int TakeBrokenCount()
        {
            var broken = Broken;
            Broken = 0;
            return broken;
        }

        /// <summary>Raised when a plate goes in, so its view can go.</summary>
        public event Action<Plate> Received;

        public bool CanTake => _clean.Count > 0;

        public bool CanAccept(Item item) => item is Plate plate && plate.IsDirty && HasRoom;

        public Item Take()
        {
            if (_clean.Count == 0)
                throw new InvalidOperationException("No clean plates in the sink.");
            return _clean.Pop();
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Only dirty plates go in the sink, up to its limit.");
            var plate = (Plate)item;
            _dirty.Enqueue(plate);
            Received?.Invoke(plate);
        }

        /// <summary>Adds to the current plate's progress.</summary>
        /// <returns>The plate if this finished it (it's now with the clean ones), otherwise null.</returns>
        public Plate Wash(float amount)
        {
            if (_dirty.Count == 0 || amount <= 0f)
                return null;

            WashProgress += amount;
            if (WashProgress < 1f)
                return null;

            WashProgress = 0f;
            var plate = _dirty.Dequeue();
            plate.Clean();
            _clean.Push(plate);
            return plate;
        }

        /// <summary>A clean plate gets dropped: the kitchen has one plate fewer for good.</summary>
        public void BreakClean()
        {
            if (_clean.Count == 0)
                return;
            _clean.Pop();
            Broken++;
        }
    }
}
