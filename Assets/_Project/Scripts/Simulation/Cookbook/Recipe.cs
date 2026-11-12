using System;
using System.Collections.Generic;
using Game.Simulation.Service;

namespace Game.Simulation.Cookbook
{
    /// <summary>
    /// A dish in the cookbook (game design §15.5): what it's made of, what it needs, what it costs to
    /// unlock and what it's worth. Filled from the CookbookConfig asset.
    /// </summary>
    public sealed class Recipe
    {
        /// <summary>Stable id, kept in saves. Never rename one that's shipped.</summary>
        public string Id { get; set; } = "";

        /// <summary>What players read, in English (translated when shown).</summary>
        public string Name { get; set; } = "";

        public string MainTypeId { get; set; }
        public string MainName { get; set; }

        /// <summary>The donenesses a guest may ask for, and how often.</summary>
        public IReadOnlyList<OrderChoice> Choices { get; set; } = Array.Empty<OrderChoice>();

        /// <summary>The side that comes with it, or null.</summary>
        public string SideTypeId { get; set; }
        public string SideName { get; set; }

        /// <summary>The price the cookbook suggests, in whole money.</summary>
        public int SuggestedPrice { get; set; } = 10;

        /// <summary>The part of the suggested price that's for the side: what's not paid if it's missing.</summary>
        public int SuggestedSidePrice { get; set; }

        /// <summary>What it costs to unlock. 0 for the one the boss wants (decision 2026-11-06).</summary>
        public int UnlockCost { get; set; }

        /// <summary>On every new campaign's menu from the first day.</summary>
        public bool StartsUnlocked { get; set; }

        /// <summary>1 (simple) to 3 (demanding), for the cookbook page.</summary>
        public int Complexity { get; set; } = 1;

        /// <summary>The appliances it depends on, as players read them ("Hob", "Board").</summary>
        public IReadOnlyList<string> Appliances { get; set; } = Array.Empty<string>();

        /// <summary>The ingredients it's made from, as players read them.</summary>
        public IReadOnlyList<string> Ingredients { get; set; } = Array.Empty<string>();

        /// <summary>The same ingredients as item type ids, for their pictures.</summary>
        public IReadOnlyList<string> IngredientTypeIds { get; set; } = Array.Empty<string>();

        /// <summary>About what the ingredients for one serving cost.</summary>
        public float IngredientCost { get; set; }

        /// <summary>How often it's ordered against the menu's other dishes, at the suggested price.</summary>
        public float Popularity { get; set; } = 1f;

        /// <summary>The value it brings per serving at the suggested price: price less ingredients.</summary>
        public float Value => SuggestedPrice - IngredientCost;

        /// <summary>How it's cooked, step by step, as the Restaurant tab explains it (2026-11-11).</summary>
        public IReadOnlyList<CookStep> Steps { get; set; } = Array.Empty<CookStep>();
    }

    /// <summary>One step of cooking a dish: where it happens (a station, as players read it) and what to do.</summary>
    public sealed class CookStep
    {
        public CookStep(string station, string text)
        {
            Station = station ?? "";
            Text = text ?? "";
        }

        /// <summary>"Fridge", "Board", "Hob", "Counter", "Pass": translated where shown.</summary>
        public string Station { get; }

        /// <summary>What to do there, in English (translated where shown).</summary>
        public string Text { get; }
    }
}
