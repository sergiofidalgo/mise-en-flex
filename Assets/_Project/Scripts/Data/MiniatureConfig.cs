using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Campaign;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The city miniature in the boss's back room (Phase 6, milestone 2): which restaurants are on
    /// it, what a new campaign borrows, and what buying offers (suggested names, styles).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Miniature Config", fileName = "MiniatureConfig")]
    public sealed class MiniatureConfig : ScriptableObject
    {
        [Tooltip("Every restaurant on the miniature, cheapest first. The first is where a campaign starts.")]
        [SerializeField] private RestaurantDefinition[] _lots = Array.Empty<RestaurantDefinition>();

        [Header("A new campaign")]
        [Tooltip("What the boss lends a new campaign (decision 2026-11-10): the grill's price plus a first morning's stock. Kept as the campaign's loan.")]
        [SerializeField, Min(0f)] private float _startingLoan = 500f;

        [Header("Buying")]
        [Tooltip("Names offered over the letter grid; a few at a time, Y for others.")]
        [SerializeField] private string[] _suggestedNames = Array.Empty<string>();
        [SerializeField, Min(1)] private int _suggestionsShown = 3;
        [Tooltip("The longest name the card and the sign fit.")]
        [SerializeField, Min(4)] private int _maxNameLength = 18;
        [Tooltip("Styles offered besides keeping the previous one. Labels only until service styles (6b).")]
        [SerializeField] private string[] _styles = { "Fast casual", "Bistro", "Fine dining" };

        public IReadOnlyList<RestaurantDefinition> Lots => _lots ?? Array.Empty<RestaurantDefinition>();
        public float StartingLoan => _startingLoan;
        public IReadOnlyList<string> SuggestedNames => _suggestedNames ?? Array.Empty<string>();
        public int SuggestionsShown => _suggestionsShown;
        public int MaxNameLength => _maxNameLength;
        public IReadOnlyList<string> Styles => _styles ?? Array.Empty<string>();

        /// <summary>Each lot as plain settings, in the miniature's order; missing entries are left out.</summary>
        public List<RestaurantSettings> LotSettings() => Lots.Where(l => l != null).Select(l => l.ToSettings()).ToList();

        /// <summary>For the builder.</summary>
        public void Set(RestaurantDefinition[] lots, float startingLoan, string[] suggestedNames)
        {
            _lots = lots ?? Array.Empty<RestaurantDefinition>();
            _startingLoan = startingLoan;
            _suggestedNames = suggestedNames ?? Array.Empty<string>();
        }
    }
}
