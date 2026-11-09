using System.Collections.Generic;

namespace Game.Simulation.Characters
{
    /// <summary>
    /// One player dressing at the tailor (decision 2026-11-09). Up and down pick a module, left and
    /// right an item in it. The chef shows whatever is picked (<see cref="Preview"/>), locked items
    /// too, so players can see what's to come; what they wear (<see cref="Worn"/>) takes only items
    /// that are open, and is what saves. Leaving puts the preview back to what's worn.
    /// </summary>
    public sealed class TailorSession
    {
        private readonly ICollection<string> _unlocked;
        private int _module;

        public TailorSession(ChefLook worn, ICollection<string> unlocked)
        {
            _unlocked = unlocked;
            Worn = worn.Copy();
            Worn.Repair(unlocked);
            Preview = Worn.Copy();
        }

        /// <summary>What the chef wears: saved, and seen everywhere else.</summary>
        public ChefLook Worn { get; }

        /// <summary>What the chef shows while dressing, which may include a locked item.</summary>
        public ChefLook Preview { get; }

        public WardrobeModule Module => Wardrobe.Modules[_module];

        public WardrobeItem Item => Wardrobe.Find(Module, Preview.Get(Module)) ?? Wardrobe.Default(Module);

        /// <summary>Where the picked item is in its module, and how many there are, for the card.</summary>
        public int ItemIndex => Wardrobe.IndexOf(Module, Preview.Get(Module));
        public int ItemCount => Wardrobe.Options(Module).Count;

        public bool ItemLocked => Wardrobe.IsLocked(Item, _unlocked);

        /// <summary>Moves to the next module (+1) or the one before (-1), wrapping round.</summary>
        public void StepModule(int direction)
        {
            var count = Wardrobe.Modules.Count;
            _module = ((_module + direction) % count + count) % count;
        }

        /// <summary>
        /// Shows the next item (+1) or the one before (-1) in the module, wrapping round. Returns true
        /// if what's worn changed (an open item), so the caller saves.
        /// </summary>
        public bool StepItem(int direction)
        {
            var items = Wardrobe.Options(Module);
            var index = ((ItemIndex + direction) % items.Count + items.Count) % items.Count;
            var item = items[index];
            Preview.Set(Module, item.Id);
            if (Wardrobe.IsLocked(item, _unlocked) || Worn.Get(Module) == item.Id)
                return false;
            Worn.Set(Module, item.Id);
            return true;
        }

        /// <summary>Done dressing: the chef goes back to what they wear, dropping any locked preview.</summary>
        public void Leave()
        {
            foreach (var module in Wardrobe.Modules)
                Preview.Set(module, Worn.Get(module));
        }
    }
}
