using System;
using System.Collections.Generic;

namespace Game.Simulation.Items
{
    /// <summary>
    /// Holds one item: a player's hands, a counter top. A counter top can also pile up dirty plates
    /// (decision 2026-11-08): a dirty plate goes on top of another, up to the slot's limit.
    /// <see cref="Current"/> is always the top one, so whatever looks at a counter sees what's on it.
    /// </summary>
    public sealed class ItemSlot : IItemContainer, IItemHolder
    {
        private readonly List<Plate> _below = new List<Plate>();

        /// <param name="dirtyPlates">How many dirty plates can pile up here; 1 for a plain slot.</param>
        public ItemSlot(int dirtyPlates = 1)
        {
            MaxDirtyPlates = Math.Max(1, dirtyPlates);
        }

        public int MaxDirtyPlates { get; }

        /// <summary>The top item, or null.</summary>
        public Item Current { get; private set; }

        public bool IsEmpty => Current == null;

        /// <summary>Items here: the top one and any dirty plates under it.</summary>
        public int Count => Current == null ? 0 : _below.Count + 1;

        /// <summary>Dirty plates under the top one, bottom first.</summary>
        public IReadOnlyList<Plate> Below => _below;

        public bool CanTake => Current != null;

        public event Action Changed;

        public bool CanAccept(Item item) => item != null && (Current == null || CanPileOn(item));

        /// <summary>True if this dirty plate could go on the dirty plates already here.</summary>
        public bool CanPileOn(Item item) =>
            item is Plate plate && plate.IsDirty && Current is Plate top && top.IsDirty && Count < MaxDirtyPlates
            && item != Current && !_below.Contains(plate);

        /// <summary>Where an item is in the pile: 0 at the bottom. -1 if it isn't here.</summary>
        public int IndexOf(Item item)
        {
            if (item == null)
                return -1;
            if (item == Current)
                return _below.Count;
            return item is Plate plate ? _below.IndexOf(plate) : -1;
        }

        /// <summary>Takes the top item; a dirty plate under it becomes the top.</summary>
        public Item Take()
        {
            if (Current == null)
                throw new InvalidOperationException("Slot is empty.");

            var item = Current;
            if (_below.Count > 0)
            {
                Current = _below[_below.Count - 1];
                _below.RemoveAt(_below.Count - 1);
            }
            else
            {
                Current = null;
            }
            Changed?.Invoke();
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Slot can't accept this item.");

            if (Current != null)
                _below.Add((Plate)Current);
            Current = item;
            Changed?.Invoke();
        }
    }
}
