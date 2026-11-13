using Game.Simulation.Campaign;

namespace Game.Simulation.Characters
{
    /// <summary>
    /// A player naming themselves as they join (the user's calls, 2026-11-13), on the restaurant
    /// naming keyboard, trimmed for it: their name from last time on the top row (one press keeps
    /// it), then the letters, then Space. No suggestions and no Delete or Done keys: X deletes and
    /// Menu finishes (the user's calls, the same day: each is said once). Plain C#, so it's tested without Play mode.
    /// </summary>
    public sealed class ChefNaming
    {
        /// <summary>The longest name a player card fits.</summary>
        public const int MaxLength = 12;

        private readonly string _lastName;

        /// <param name="lastName">The name this slot used last time; empty for none.</param>
        public ChefNaming(string lastName)
        {
            _lastName = RestaurantNaming.Clean(lastName);
            Picker = new NamePicker(_lastName, null, MaxLength, shortcuts: true);
        }

        public NamePicker Picker { get; }

        /// <summary>The name chosen, once the kept name has been pressed, or Menu.</summary>
        public string Chosen { get; private set; }

        public bool Done => Chosen != null;

        /// <summary>Changes made: each press and move bumps it, so the card can redraw and pop.</summary>
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

        /// <summary>
        /// Menu: what's typed, or with nothing typed, last time's name.
        /// False (nothing happens) with neither.
        /// </summary>
        public bool Finish()
        {
            if (Done)
                return false;
            if (Picker.CanFinish)
                Chosen = RestaurantNaming.Clean(Picker.Text);
            else if (_lastName.Length > 0)
                Chosen = _lastName;
            else
                return false;
            Version++;
            return true;
        }

        /// <summary>X.</summary>
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
    }
}
