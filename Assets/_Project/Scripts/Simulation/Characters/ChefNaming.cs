using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Campaign;

namespace Game.Simulation.Characters
{
    /// <summary>
    /// A player naming themselves as they join (the user's calls, 2026-11-13), on the restaurant
    /// naming keyboard: their name from last time on the top row (one press keeps it), two
    /// suggested names (Y rolls others), then the letters and Space, Delete, Done. Plain C#, so
    /// it's tested without Play mode.
    /// </summary>
    public sealed class ChefNaming
    {
        /// <summary>The longest name a player card fits.</summary>
        public const int MaxLength = 12;

        /// <summary>Suggested names shown at once.</summary>
        public const int Suggestions = 2;

        private readonly IReadOnlyList<string> _pool;
        private readonly IReadOnlyList<string> _taken;
        private int _rolls;

        /// <param name="lastName">The name this slot used last time; empty for none.</param>
        /// <param name="pool">Names to suggest from (the guests' first names, in the player's language).</param>
        /// <param name="taken">Other players' names, never suggested.</param>
        /// <param name="seed">Varies the suggestions between joins.</param>
        public ChefNaming(string lastName, IEnumerable<string> pool, IEnumerable<string> taken, int seed)
        {
            _pool = (pool ?? Enumerable.Empty<string>()).ToList();
            var taken2 = (taken ?? Enumerable.Empty<string>()).ToList();
            if (!string.IsNullOrWhiteSpace(lastName))
                taken2.Add(lastName);
            _taken = taken2;
            Seed = seed;
            Picker = new NamePicker(lastName, Suggest(), MaxLength);
        }

        public NamePicker Picker { get; }

        public int Seed { get; private set; }

        /// <summary>The name chosen, once Done (or a kept or suggested name) has been pressed.</summary>
        public string Chosen { get; private set; }

        public bool Done => Chosen != null;

        /// <summary>Changes made: each press, move or roll bumps it, so the card can pop.</summary>
        public int Version { get; private set; }

        /// <summary>A press of A: types, or finishes. Returns true if it did anything.</summary>
        public bool Press()
        {
            if (Done)
                return false;
            var before = Picker.Text;
            var name = Picker.Press();
            if (name != null)
            {
                Chosen = name;
                Version++;
                return true;
            }
            var changed = Picker.Text != before;
            if (changed)
                Version++;
            return changed;
        }

        public bool Delete()
        {
            if (Done || !Picker.Delete())
                return false;
            Version++;
            return true;
        }

        public bool Move(int dx, int dy)
        {
            if (Done || !Picker.Move(dx, dy))
                return false;
            Version++;
            return true;
        }

        /// <summary>Y: two other suggestions.</summary>
        public void Roll()
        {
            if (Done)
                return;
            _rolls++;
            Picker.SetSuggestions(Suggest());
            Version++;
        }

        private IReadOnlyList<string> Suggest() =>
            RestaurantNaming.Suggest(_pool, unchecked(Seed * 7919 + _rolls * 104729), Suggestions, _taken);
    }
}
