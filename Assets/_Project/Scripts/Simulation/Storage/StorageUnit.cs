using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Simulation.Storage
{
    /// <summary>
    /// A fridge or shelf with a few slots, each dedicated to one ingredient type with a limited
    /// capacity (decision 2026-11-02: storage with capacity is the game's main differentiator).
    /// Stock is real, simulated food kept at the unit's temperature, so a fridge keeps it cold and
    /// fresh and a shelf doesn't; leftovers put back keep their state.
    /// </summary>
    public sealed class StorageUnit : IItemContainer
    {
        public sealed class Slot
        {
            private readonly List<Ingredient> _items = new List<Ingredient>();

            public Slot(string typeId, int capacity)
            {
                TypeId = typeId;
                Capacity = capacity;
            }

            /// <summary>The ingredient this slot holds, or null/empty for an unused slot.</summary>
            public string TypeId { get; }

            public int Capacity { get; }

            public int Count => _items.Count;

            public bool IsUsed => !string.IsNullOrEmpty(TypeId) && Capacity > 0;

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

        /// <summary>Puts newly created stock into a slot, up to its capacity. Returns how many fitted.</summary>
        public int Stock(int slotIndex, Func<Ingredient> create, int count)
        {
            var slot = _slots[slotIndex];
            var added = 0;
            while (added < count && slot.IsUsed && slot.Count < slot.Capacity)
            {
                Store(slot, create());
                added++;
            }
            return added;
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

        public bool CanAccept(Item item) => item is Ingredient ingredient && FindSlotWithRoom(ingredient) != null;

        public Item Take() => throw new InvalidOperationException("Take from a specific slot with TryTake.");

        public void Put(Item item)
        {
            var ingredient = item as Ingredient;
            var slot = ingredient != null ? FindSlotWithRoom(ingredient) : null;
            if (slot == null)
                throw new InvalidOperationException("No slot for this item.");
            Store(slot, ingredient);
        }

        private Slot FindSlotWithRoom(Ingredient ingredient)
        {
            foreach (var slot in _slots)
            {
                if (slot.IsUsed && slot.TypeId == ingredient.TypeId && slot.Count < slot.Capacity)
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
