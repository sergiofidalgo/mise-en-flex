using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Simulation.Prep
{
    /// <summary>
    /// The mise en place counter (decision 2026-11-02): a cutting board in the middle and four
    /// slots for prepared ingredients. A whole ingredient goes on the board, is chopped with the
    /// knife in rhythm, and the cut product goes into a slot (one ingredient per slot; a slot is
    /// assigned when the first portion arrives and freed when it empties). If no slot can take
    /// it, the cut stays on the board.
    /// </summary>
    public sealed class PrepCounter : IItemContainer, IItemHolder
    {
        public const int SlotCount = 4;

        public sealed class Slot
        {
            private readonly List<Ingredient> _items = new List<Ingredient>();

            /// <summary>The ingredient this slot currently holds, or null when empty and free.</summary>
            public string TypeId => _items.Count > 0 ? _items[0].TypeId : null;

            public IReadOnlyList<Ingredient> Items => _items;

            internal List<Ingredient> List => _items;
        }

        private readonly Slot[] _slots = new Slot[SlotCount];
        private readonly Func<Ingredient, Ingredient> _cut;
        private readonly float _goodWindow;
        private readonly float _okWindow;

        /// <param name="slotCapacity">Portions each slot holds.</param>
        /// <param name="cut">Turns a finished whole ingredient into its cut form (via the cooking simulation).</param>
        /// <param name="goodWindow">± real seconds around a beat that count as a good chop.</param>
        /// <param name="okWindow">± real seconds that count as early/late.</param>
        public PrepCounter(int slotCapacity, Func<Ingredient, Ingredient> cut, float goodWindow, float okWindow)
        {
            SlotCapacity = slotCapacity;
            _cut = cut;
            _goodWindow = goodWindow;
            _okWindow = okWindow;
            for (var i = 0; i < SlotCount; i++)
                _slots[i] = new Slot();
        }

        public int SlotCapacity { get; }

        public IReadOnlyList<Slot> Slots => _slots;

        /// <summary>What's on the board, or null.</summary>
        public Item Current { get; private set; }

        /// <summary>The rhythm of the cut in progress, or null.</summary>
        public ChopRhythm Rhythm { get; private set; }

        /// <summary>Chops done on the item on the board.</summary>
        public int Cuts { get; private set; }

        public bool CanChop => Current is Ingredient ingredient && IsCuttable(ingredient);

        public int CutsNeeded => (Current as Ingredient)?.Properties.CutsNeeded ?? 0;

        /// <summary>Raised when a cut finishes, with the cut product and whether it went into a slot.</summary>
        public event Action<Ingredient, bool> CutFinished;

        public static bool IsCuttable(Ingredient ingredient) =>
            !string.IsNullOrEmpty(ingredient.Properties.CutIntoTypeId) && ingredient.Properties.CutsNeeded > 0;

        /// <summary>One knife chop at <paramref name="realTime"/> (seconds). Null if there's nothing to cut.</summary>
        public ChopGrade? Chop(double realTime)
        {
            if (!CanChop)
                return null;

            var whole = (Ingredient)Current;
            Rhythm ??= new ChopRhythm(whole.Properties.ChopInterval, _goodWindow, _okWindow);
            var grade = Rhythm.Hit(realTime);
            Cuts++;

            if (Cuts >= whole.Properties.CutsNeeded)
            {
                var evenness = Rhythm.Evenness;
                var product = _cut(whole);
                product.CutEvenness = evenness;
                ClearBoard();

                var stored = TryStore(product);
                if (!stored)
                    Current = product; // no room in the slots: the cut stays on the board
                CutFinished?.Invoke(product, stored);
            }

            return grade;
        }

        public bool TryTakeFromSlot(int index, out Ingredient ingredient)
        {
            ingredient = null;
            if (index < 0 || index >= SlotCount || _slots[index].List.Count == 0)
                return false;
            ingredient = _slots[index].List[0];
            _slots[index].List.RemoveAt(0);
            return true;
        }

        public bool CanTake => Current != null;

        /// <summary>Whole ingredients go on an empty board; prepared ones into a slot.</summary>
        public bool CanAccept(Item item) =>
            item is Ingredient ingredient && (IsCuttable(ingredient) ? Current == null : FindSlotFor(ingredient) != null);

        public Item Take()
        {
            if (Current == null)
                throw new InvalidOperationException("Nothing on the board.");
            var item = Current;
            ClearBoard();
            return item;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("The prep counter can't take this.");

            var ingredient = (Ingredient)item;
            if (IsCuttable(ingredient))
            {
                Current = ingredient;
                Cuts = 0;
                Rhythm = null;
            }
            else
            {
                TryStore(ingredient);
            }
        }

        private bool TryStore(Ingredient ingredient)
        {
            var slot = FindSlotFor(ingredient);
            if (slot == null)
                return false;
            slot.List.Add(ingredient);
            return true;
        }

        // Prefer a slot already holding this ingredient with room; otherwise the first free one.
        private Slot FindSlotFor(Ingredient ingredient)
        {
            foreach (var slot in _slots)
            {
                if (slot.TypeId == ingredient.TypeId && slot.List.Count < SlotCapacity)
                    return slot;
            }
            foreach (var slot in _slots)
            {
                if (slot.List.Count == 0)
                    return slot;
            }
            return null;
        }

        private void ClearBoard()
        {
            Current = null;
            Cuts = 0;
            Rhythm = null;
        }
    }
}
