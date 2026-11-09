using System;
using System.Collections.Generic;

namespace Game.Simulation.Characters
{
    /// <summary>
    /// The players' chefs, kept apart from any campaign so they outlive it, like the cookbook
    /// (decision 2026-11-09): one look per player slot (Player 1's chef, Player 2's...), and what's
    /// been earned. Plain data; the gameplay layer writes the file.
    /// </summary>
    [Serializable]
    public sealed class ChefProfile
    {
        /// <summary>Bumped whenever the shape changes in a way a default can't cover.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Slots kept: the stretch goal's four players.</summary>
        public const int Slots = 4;

        public int Version = CurrentVersion;

        public List<ChefLook> Looks = new List<ChefLook>();

        /// <summary>Crazy items earned, by id. Empty until earning exists (Phase 7).</summary>
        public List<string> Unlocked = new List<string>();

        /// <summary>The look for a player slot; a default chef if that slot has never dressed.</summary>
        public ChefLook Look(int slot)
        {
            Fill();
            return Looks[Clamp(slot)];
        }

        public void SetLook(int slot, ChefLook look)
        {
            Fill();
            Looks[Clamp(slot)] = look.Copy();
        }

        /// <summary>
        /// Brings a profile read from disk up to date: a newer version than this game knows is
        /// refused (false), missing slots get the default chef, and every look is repaired.
        /// </summary>
        public bool Upgrade()
        {
            if (Version > CurrentVersion)
                return false;
            // No changes of shape yet: version 1 is current. Each future version adds a step here.
            Version = CurrentVersion;
            Looks ??= new List<ChefLook>();
            Unlocked ??= new List<string>();
            Fill();
            for (var i = 0; i < Looks.Count; i++)
            {
                Looks[i] ??= new ChefLook();
                Looks[i].Repair(Unlocked);
            }
            return true;
        }

        private void Fill()
        {
            while (Looks.Count < Slots)
                Looks.Add(new ChefLook());
        }

        private static int Clamp(int slot) => Math.Max(0, Math.Min(Slots - 1, slot));
    }
}
