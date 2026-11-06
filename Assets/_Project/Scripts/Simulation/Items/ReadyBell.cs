using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// The bell: a plate on a counter is ready to go out once someone rings for it. A plate can wait on
    /// any counter, so ringing is what says it's finished rather than waiting for its onions. It works
    /// with or without a waiter to answer it. Taking the plate away cancels it.
    /// </summary>
    public sealed class ReadyBell
    {
        private readonly List<(ItemSlot Slot, Plate Plate)> _rung = new List<(ItemSlot, Plate)>();
        private readonly List<ItemSlot> _slots = new List<ItemSlot>();

        /// <summary>Raised when the bell is rung for a plate on this counter.</summary>
        public event Action<ItemSlot> Rang;

        /// <summary>The counters with a plate waiting to go out, the longest-waiting first.</summary>
        public IReadOnlyList<ItemSlot> Waiting
        {
            get
            {
                Forget();
                _slots.Clear();
                foreach (var rung in _rung)
                    _slots.Add(rung.Slot);
                return _slots;
            }
        }

        public bool IsRung(ItemSlot slot)
        {
            Forget();
            foreach (var rung in _rung)
            {
                if (rung.Slot == slot)
                    return true;
            }
            return false;
        }

        /// <summary>True if there's a clean plate with food here to ring for.</summary>
        public static bool CanRing(ItemSlot slot) => slot.Current is Plate plate && !plate.IsDirty && plate.Items.Count > 0;

        /// <returns>False if there's nothing to ring for, or it's already been rung.</returns>
        public bool Ring(ItemSlot slot)
        {
            if (!CanRing(slot) || IsRung(slot))
                return false;
            _rung.Add((slot, (Plate)slot.Current));
            Rang?.Invoke(slot);
            return true;
        }

        // Plates that have since been taken, by anyone, aren't waiting any more.
        private void Forget() => _rung.RemoveAll(rung => rung.Slot.Current != rung.Plate);
    }
}
