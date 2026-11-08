using System;

namespace Game.Simulation.Items
{
    /// <summary>
    /// Where plates get washed, one at a time (decision 2026-11-08). It holds no plates of its own: a
    /// dirty plate goes in, and whoever finishes washing it (a player scrubbing, a cleaner working)
    /// gets it back clean, to carry to the plate stack. Anyone washing adds to the same progress.
    /// </summary>
    public sealed class Sink : IPlateSource
    {
        /// <summary>The plate being washed, or null.</summary>
        public Plate Washing { get; private set; }

        public bool HasRoom => Washing == null;

        /// <summary>1 while a plate's waiting to be washed, otherwise 0.</summary>
        public int DirtyCount => Washing == null ? 0 : 1;

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

        /// <summary>Clean plates don't wait here: they go straight to whoever washed them.</summary>
        public bool CanTake => false;

        public bool CanAccept(Item item) => item is Plate plate && plate.IsDirty && HasRoom;

        public Item Take() => throw new InvalidOperationException("Washed plates go straight to whoever washed them.");

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Only a dirty plate goes in the sink, one at a time.");
            Washing = (Plate)item;
            WashProgress = 0f;
            Received?.Invoke(Washing);
        }

        /// <summary>Adds to the plate's progress.</summary>
        /// <returns>The plate, clean and out of the sink, if this finished it; otherwise null.</returns>
        public Plate Wash(float amount)
        {
            if (Washing == null || amount <= 0f)
                return null;

            WashProgress += amount;
            if (WashProgress < 1f)
                return null;

            WashProgress = 0f;
            var plate = Washing;
            Washing = null;
            plate.Clean();
            return plate;
        }

        /// <summary>A plate broken somewhere: it's replaced with the others in the morning.</summary>
        public void CountBroken() => Broken++;
    }
}
