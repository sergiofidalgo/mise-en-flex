using System;
using System.Collections.Generic;

namespace Game.Simulation.Cookbook
{
    /// <summary>
    /// Every recipe any campaign has unlocked (decision 2026-11-10). Campaigns each buy their own
    /// menu; this outlives them all and is what the Free kitchen sandbox will offer once it's built.
    /// Plain data with public fields, saved beside the campaign slots.
    /// </summary>
    [Serializable]
    public sealed class SharedCookbook
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>Recipe ids, in the order they were first unlocked.</summary>
        public List<string> Unlocked = new List<string>();

        /// <summary>Adds a recipe; true if it's new here.</summary>
        public bool Add(string id)
        {
            if (string.IsNullOrEmpty(id) || Unlocked.Contains(id))
                return false;
            Unlocked.Add(id);
            return true;
        }

        public bool Has(string id) => Unlocked.Contains(id);

        /// <summary>
        /// Brings an older file up to date; false for one from a newer game, which is then left alone.
        /// Version 1 is the first, so there are no steps yet.
        /// </summary>
        public bool Upgrade()
        {
            if (Version > CurrentVersion)
                return false;
            Unlocked ??= new List<string>();
            Version = CurrentVersion;
            return true;
        }
    }
}
