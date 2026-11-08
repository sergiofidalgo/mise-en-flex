using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Gameplay.Saving
{
    /// <summary>
    /// A station whose contents last overnight and go in the save (decision 2026-11-08: everything
    /// stays as it lies). Called after <see cref="Stations.Interactable.Bind"/>.
    /// </summary>
    public interface ISavedContents
    {
        /// <summary>What's on it now, each with its place on the station, bottom first.</summary>
        IEnumerable<(int Slot, Item Item)> SavedItems { get; }

        /// <summary>Takes off whatever binding put there to start with (the hob's own pan, the first knife).</summary>
        void ClearForLoad();

        /// <summary>Puts back an item from a save, with its view. False if it no longer fits.</summary>
        bool RestoreItem(int slot, Item item);
    }
}
