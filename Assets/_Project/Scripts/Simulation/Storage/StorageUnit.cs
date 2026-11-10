using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Simulation.Storage
{
    /// <summary>
    /// A fridge or shelf with a few slots of limited capacity (decision 2026-11-02: storage with
    /// capacity is the game's main differentiator). A slot holds one ingredient at a time and is
    /// free again when empty, and any ingredient can go in any unit (decision 2026-11-04). Stock
    /// is real, simulated food kept at the unit's temperature, so meat on a shelf simply spoils
    /// faster; leftovers put back keep their state.
    /// </summary>
    public sealed class StorageUnit : IItemContainer
    {
        public sealed class Slot
        {
            private readonly List<Ingredient> _items = new List<Ingredient>();

            public Slot(int capacity)
            {
                Capacity = capacity;
            }

            /// <summary>The ingredient this slot holds right now, or null when empty and free.</summary>
            public string TypeId => _items.Count > 0 ? _items[0].TypeId : null;

            /// <summary>
            /// True if what's here has been cooked. Cooked food never shares a slot with raw stock
            /// of the same ingredient: taking is oldest first, so a cooked steak put behind raw
            /// ones would otherwise be unreachable, and the bin would only show raw steak.
            /// </summary>
            public bool HoldsCooked => _items.Count > 0 && _items[0].HasBeenCooked;

            public int Capacity { get; }

            public int Count => _items.Count;

            public bool IsUsed => Capacity > 0;

            /// <summary>The portions in the slot, oldest first.</summary>
            public IReadOnlyList<Ingredient> Stock => _items;

            internal List<Ingredient> Items => _items;
        }

        private readonly Slot[] _slots;

        /// <param name="temperature">°C the unit keeps its contents at (fridge ≈ 4, shelf = kitchen air).</param>
        public StorageUnit(float temperature, params Slot[] slots)
        {
            Temperature = temperature;
            _slots = slots;
        }

        public float Temperature { get; }

        public IReadOnlyList<Slot> Slots => _slots;

        /// <summary>Puts newly created stock away, while there's room. Returns how many fitted.</summary>
        public int Stock(string typeId, Func<Ingredient> create, int count)
        {
            var added = 0;
            while (added < count)
            {
                // New stock is raw; checked before it's created, so nothing is made that won't fit.
                var slot = FindSlotWithRoom(typeId, cooked: false);
                if (slot == null)
                    break;
                Store(slot, create());
                added++;
            }
            return added;
        }

        /// <summary>
        /// How many raw portions of <paramref name="typeId"/> could still go in: the room left in
        /// slots already holding it raw, plus every free slot.
        /// </summary>
        public int RoomFor(string typeId)
        {
            var room = 0;
            foreach (var slot in _slots)
            {
                if (!slot.IsUsed)
                    continue;
                if (slot.Count == 0)
                    room += slot.Capacity;
                else if (slot.TypeId == typeId && !slot.HoldsCooked)
                    room += slot.Capacity - slot.Count;
            }
            return room;
        }

        /// <summary>Takes one portion from a slot (the oldest first), or returns false if it's empty.</summary>
        public bool TryTake(int slotIndex, out Ingredient ingredient)
        {
            ingredient = null;
            if (slotIndex < 0 || slotIndex >= _slots.Length)
                return false;

            var items = _slots[slotIndex].Items;
            if (items.Count == 0)
                return false;

            ingredient = items[0];
            items.RemoveAt(0);
            ingredient.EnvironmentTemperature = null;
            return true;
        }

        // Taking is done per slot with the face buttons (TryTake), not through Interact.
        public bool CanTake => false;

        public bool CanAccept(Item item) => item is Ingredient ingredient && FindSlotWithRoom(ingredient.TypeId, ingredient.HasBeenCooked) != null;

        public Item Take() => throw new InvalidOperationException("Take from a specific slot with TryTake.");

        public void Put(Item item)
        {
            var ingredient = item as Ingredient;
            var slot = ingredient != null ? FindSlotWithRoom(ingredient.TypeId, ingredient.HasBeenCooked) : null;
            if (slot == null)
                throw new InvalidOperationException("No slot for this item.");
            Store(slot, ingredient);
        }

        /// <summary>The slot holding <paramref name="ingredient"/>, or -1.</summary>
        public int SlotOf(Ingredient ingredient)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Items.Contains(ingredient))
                    return i;
            }
            return -1;
        }

        /// <summary>Throws out everything below <paramref name="freshness"/>.</summary>
        /// <returns>The food thrown out, for whoever keeps track of it.</returns>
        public List<Ingredient> RemoveSpoiled(float freshness)
        {
            var spoiled = new List<Ingredient>();
            foreach (var slot in _slots)
            {
                for (var i = slot.Items.Count - 1; i >= 0; i--)
                {
                    if (slot.Items[i].Freshness >= freshness)
                        continue;
                    spoiled.Add(slot.Items[i]);
                    slot.Items.RemoveAt(i);
                }
            }
            return spoiled;
        }

        // A slot already holding this ingredient, cooked or raw alike, with room, or else the first free one.
        private Slot FindSlotWithRoom(string typeId, bool cooked)
        {
            foreach (var slot in _slots)
            {
                if (slot.IsUsed && slot.Count > 0 && slot.Count < slot.Capacity
                    && slot.TypeId == typeId && slot.HoldsCooked == cooked)
                    return slot;
            }
            foreach (var slot in _slots)
            {
                if (slot.IsUsed && slot.Count == 0)
                    return slot;
            }
            return null;
        }

        private void Store(Slot slot, Ingredient ingredient)
        {
            ingredient.EnvironmentTemperature = Temperature;
            slot.Items.Add(ingredient);
        }
    }
}
