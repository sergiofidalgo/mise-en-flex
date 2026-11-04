using System;
using System.Collections.Generic;
using Game.Gameplay.Stations;

namespace Game.Gameplay
{
    /// <summary>
    /// The stations in the kitchen right now. Furniture can be bought and sold between services, so
    /// anything that needs the stations reads this list or listens to it, rather than keeping its own.
    /// </summary>
    public sealed class StationRegistry
    {
        private readonly List<Interactable> _all;

        public StationRegistry(IEnumerable<Interactable> stations)
        {
            _all = new List<Interactable>(stations);
        }

        public IReadOnlyList<Interactable> All => _all;

        public event Action<Interactable> Added;

        public event Action<Interactable> Removed;

        public void Add(Interactable station)
        {
            _all.Add(station);
            Added?.Invoke(station);
        }

        public void Remove(Interactable station)
        {
            if (_all.Remove(station))
                Removed?.Invoke(station);
        }
    }
}
