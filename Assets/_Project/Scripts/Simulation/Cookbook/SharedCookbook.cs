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
        /// Chefs the boss erased (§15.3), oldest first: a dusty portrait each in the hall of fame. Kept
        /// here because, like the cookbook, the hall outlives campaigns. Empty in older files.
        /// </summary>
        public List<ErasedChef> Erased = new List<ErasedChef>();

        /// <summary>
        /// Brings an older file up to date; false for one from a newer game, which is then left alone.
        /// Version 1 is the first, so there are no steps yet.
        /// </summary>
        public bool Upgrade()
        {
            if (Version > CurrentVersion)
                return false;
            Unlocked ??= new List<string>();
            Erased ??= new List<ErasedChef>();
            Version = CurrentVersion;
            return true;
        }
    }

    /// <summary>A portrait in the hall of fame: who was erased, and how far they got.</summary>
    [Serializable]
    public sealed class ErasedChef
    {
        /// <summary>The restaurant they were last running, for the plaque.</summary>
        public string Restaurant = "";

        /// <summary>Days played across all their restaurants.</summary>
        public int Days;

        /// <summary>Player numbers who cooked, for the portrait's colours.</summary>
        public List<int> Cooks = new List<int>();

        /// <summary>When, as <see cref="DateTime.Ticks"/> in local time.</summary>
        public long Ticks;
    }
}
