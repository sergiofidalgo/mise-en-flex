using System;
using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Simulation.Cooking
{
    public sealed class PanProperties
    {
        public string TypeId { get; set; } = "Pan";

        /// <summary>J/K. A 1.5 kg steel or iron pan is roughly 750 J/K.</summary>
        public float HeatCapacity { get; set; }

        /// <summary>W/K lost to the kitchen air. With the hob's power this sets the pan's top temperature.</summary>
        public float AirLossConductance { get; set; }

        /// <summary>How many ingredients fit in the pan at once.</summary>
        public int Capacity { get; set; }
    }

    /// <summary>
    /// A pan: holds ingredients and a temperature. Each ingredient lying in it draws heat
    /// through its contact surface, which is what makes a crowded or unheated pan sear badly.
    /// It can sit on a hob, stand on a counter or be carried; it keeps its heat wherever it is.
    /// </summary>
    public sealed class Pan : Item, IItemCollection
    {
        private readonly List<Ingredient> _contents = new List<Ingredient>();

        internal Pan(PanProperties properties, float startTemperature)
            : base(properties.TypeId)
        {
            Properties = properties;
            Temperature = startTemperature;
        }

        public PanProperties Properties { get; }

        public float Temperature { get; internal set; }

        // Heat flow (W) gathered during a simulation step before it's applied.
        internal float PendingHeat;

        public IReadOnlyList<Ingredient> Contents => _contents;

        public IReadOnlyList<Item> Items => _contents;

        public bool CanTake => _contents.Count > 0;

        public bool CanAccept(Item item) =>
            item is Ingredient ingredient && ingredient.Pan == null && _contents.Count < Properties.Capacity;

        /// <summary>Takes the ingredient added most recently.</summary>
        public Item Take()
        {
            if (_contents.Count == 0)
                throw new InvalidOperationException("Pan is empty.");

            var ingredient = _contents[_contents.Count - 1];
            _contents.RemoveAt(_contents.Count - 1);
            ingredient.Pan = null;
            return ingredient;
        }

        public void Put(Item item)
        {
            if (!CanAccept(item))
                throw new InvalidOperationException("Pan can't accept this item.");

            var ingredient = (Ingredient)item;
            ingredient.Pan = this;
            _contents.Add(ingredient);
        }
    }
}
