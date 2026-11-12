using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Cookbook;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The recipes the cookbook offers and how price moves customers (decisions 2026-11-10). Mains,
    /// sides and ingredients are item type ids, looked up in the item catalog, so the defaults here
    /// need no asset references. Real content is Phase 7; these four come from what can already be cooked.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Cookbook Config", fileName = "CookbookConfig")]
    public sealed class CookbookConfig : ScriptableObject
    {
        /// <summary>One step of how a dish is cooked, for the Restaurant tab.</summary>
        [Serializable]
        public sealed class Step
        {
            [Tooltip("Where it happens, as players read it: Fridge, Board, Hob, Counter, Pass.")]
            [SerializeField] private string _station;
            [Tooltip("What to do there, short (about 35 characters), in English.")]
            [SerializeField] private string _text;

            public Step() { }

            public Step(string station, string text)
            {
                _station = station;
                _text = text;
            }

            public CookStep ToStep() => new CookStep(_station, _text);
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Stable id kept in saves. Never rename one that's shipped.")]
            [SerializeField] private string _id;
            [Tooltip("What players read, in English.")]
            [SerializeField] private string _name;
            [Tooltip("Item type id of the main, e.g. Steak.")]
            [SerializeField] private string _main;
            [Tooltip("Donenesses a guest may ask for and how often. Names must match the main's levels.")]
            [SerializeField] private ServiceConfig.DonenessWeight[] _donenesses = Array.Empty<ServiceConfig.DonenessWeight>();
            [Tooltip("Item type id of the side that comes with it, or empty.")]
            [SerializeField] private string _side;
            [Tooltip("The price the cookbook suggests.")]
            [SerializeField, Min(1)] private int _suggestedPrice = 10;
            [Tooltip("The part of the suggested price that's the side's: not paid when it's missing.")]
            [SerializeField, Min(0)] private int _suggestedSidePrice;
            [Tooltip("What unlocking it costs. The boss's dish is free (decision 2026-11-06).")]
            [SerializeField, Min(0)] private int _unlockCost;
            [Tooltip("On every new campaign's menu from day 1.")]
            [SerializeField] private bool _startsUnlocked;
            [Tooltip("1 simple to 3 demanding, as the cookbook page shows it.")]
            [SerializeField, Range(1, 3)] private int _complexity = 1;
            [Tooltip("Appliances it needs, as players read them.")]
            [SerializeField] private string[] _appliances = Array.Empty<string>();
            [Tooltip("Item type ids of what's bought to make one serving; their prices make its cost.")]
            [SerializeField] private string[] _ingredients = Array.Empty<string>();
            [Tooltip("How often it's ordered against the menu's others, at the suggested price.")]
            [SerializeField, Min(0f)] private float _popularity = 1f;
            [Tooltip("How it's cooked, step by step, as the Restaurant tab explains it (2026-11-11).")]
            [SerializeField] private Step[] _steps = Array.Empty<Step>();

            public Entry() { }

            public Entry(string id, string name, string main, ServiceConfig.DonenessWeight[] donenesses, string side,
                int suggestedPrice, int suggestedSidePrice, int unlockCost, bool startsUnlocked, int complexity,
                string[] appliances, string[] ingredients, float popularity, Step[] steps = null)
            {
                _steps = steps ?? Array.Empty<Step>();
                _id = id;
                _name = name;
                _main = main;
                _donenesses = donenesses;
                _side = side;
                _suggestedPrice = suggestedPrice;
                _suggestedSidePrice = suggestedSidePrice;
                _unlockCost = unlockCost;
                _startsUnlocked = startsUnlocked;
                _complexity = complexity;
                _appliances = appliances;
                _ingredients = ingredients;
                _popularity = popularity;
            }

            public string Id => _id;
            public string Name => _name;
            public string Main => _main;
            public string Side => string.IsNullOrEmpty(_side) ? null : _side;
            public IEnumerable<string> Ingredients => _ingredients ?? Array.Empty<string>();
            public IReadOnlyList<Step> Steps => _steps ?? Array.Empty<Step>();
            public IEnumerable<string> Donenesses => (_donenesses ?? Array.Empty<ServiceConfig.DonenessWeight>()).Select(d => d.Doneness);

            public Recipe ToRecipe(ItemCatalog catalog)
            {
                var main = catalog != null ? catalog.Find(_main) : null;
                var side = catalog != null && Side != null ? catalog.Find(Side) : null;
                var levels = main != null ? main.ToProperties().DonenessLevels : null;
                return new Recipe
                {
                    Id = _id,
                    Name = _name,
                    MainTypeId = _main,
                    MainName = main != null ? main.DisplayName : _main,
                    // Unknown doneness names are skipped so a typo can't break a service; CookbookTests checks them.
                    Choices = levels == null
                        ? Array.Empty<OrderChoice>()
                        : (_donenesses ?? Array.Empty<ServiceConfig.DonenessWeight>())
                            .Where(w => levels.Any(l => l.Name == w.Doneness))
                            .Select(w => new OrderChoice(levels.First(l => l.Name == w.Doneness), w.Weight))
                            .ToArray(),
                    SideTypeId = Side,
                    SideName = side != null ? side.DisplayName : Side,
                    SuggestedPrice = _suggestedPrice,
                    SuggestedSidePrice = Side != null ? Mathf.Min(_suggestedSidePrice, _suggestedPrice) : 0,
                    UnlockCost = _unlockCost,
                    StartsUnlocked = _startsUnlocked,
                    Complexity = _complexity,
                    Appliances = _appliances ?? Array.Empty<string>(),
                    Ingredients = Ingredients.Select(id => catalog != null && catalog.Find(id) != null ? catalog.Find(id).DisplayName : id).ToArray(),
                    IngredientTypeIds = Ingredients.ToArray(),
                    IngredientCost = Ingredients.Sum(id => catalog != null && catalog.Find(id) != null ? catalog.Find(id).Price : 0f),
                    Popularity = _popularity,
                    Steps = (_steps ?? Array.Empty<Step>()).Where(s => s != null).Select(s => s.ToStep()).ToArray(),
                };
            }
        }

        // How each starting dish is cooked (2026-11-11), the same as in the asset.
        private static Step[] SteakSteps => new[]
        {
            new Step("Fridge", "Take a steak"),
            new Step("Hob", "Sear it in a hot pan, flipping it"),
            new Step("Counter", "Let it rest a moment"),
            new Step("Pass", "Plate it and send it out"),
        };

        private static Step[] RingsSteps => new[]
        {
            new Step("Board", "Cut an onion into rings"),
            new Step("Hob", "Fry the rings golden"),
            new Step("Hob", "Sear the steak to order"),
            new Step("Pass", "Rest the steak, plate both"),
        };

        private static Step[] SauteedSteps => new[]
        {
            new Step("Board", "Dice an onion"),
            new Step("Hob", "Sauté it soft and golden"),
            new Step("Hob", "Sear the steak to order"),
            new Step("Pass", "Rest the steak, plate both"),
        };

        private static Step[] OnionSteps => new[]
        {
            new Step("Fridge", "Take an onion"),
            new Step("Board", "Dice it"),
            new Step("Hob", "Sauté it as ordered"),
            new Step("Pass", "Plate it and send it out"),
        };

        private static ServiceConfig.DonenessWeight[] SteakDonenesses() => new[]
        {
            new ServiceConfig.DonenessWeight("Rare", 3f), new ServiceConfig.DonenessWeight("Medium", 5f), new ServiceConfig.DonenessWeight("Well done", 2f),
        };

        [Tooltip("Every recipe on offer, in the order the cookbook lists them.")]
        [SerializeField] private Entry[] _recipes =
        {
            new Entry("steak", "Steak", "Steak", SteakDonenesses(), null, 20, 0, 0, true, 1,
                new[] { "Hob" }, new[] { "Steak" }, 1f, SteakSteps),
            new Entry("steak-rings", "Steak with onion rings", "Steak", SteakDonenesses(), "OnionRings", 25, 4, 40, false, 2,
                new[] { "Hob", "Board" }, new[] { "Steak", "Onion" }, 1f, RingsSteps),
            new Entry("steak-onions", "Steak with sautéed onions", "Steak", SteakDonenesses(), "DicedOnion", 27, 5, 60, false, 3,
                new[] { "Hob", "Board" }, new[] { "Steak", "Onion" }, 1f, SauteedSteps),
            new Entry("onions", "Sautéed onions", "DicedOnion",
                new[] { new ServiceConfig.DonenessWeight("Softened", 1f), new ServiceConfig.DonenessWeight("Cooked", 1f) },
                null, 8, 0, 30, false, 2, new[] { "Hob", "Board" }, new[] { "Onion" }, 0.5f, OnionSteps),
        };

        [Header("Prices (decision 2026-11-10)")]
        [Tooltip("Lowest price a dish can be set to.")]
        [SerializeField, Min(1)] private int _minPrice = 1;
        [Tooltip("What one step of the price dial moves a price by (the user's call, 2026-11-11: 10 cents; the dial shows whole numbers and its needle the tenths).")]
        [SerializeField, Min(0.01f)] private float _priceStep = 0.1f;
        [Tooltip("Highest price, as a multiple of the suggested price.")]
        [SerializeField, Min(1f)] private float _maxPriceFactor = 5f;
        [Tooltip("Arrivals scale by (price ÷ suggested)^-this. 1.5: 20% over brings ~24% fewer guests.")]
        [SerializeField, Min(0f)] private float _demandElasticity = 1.5f;
        [Tooltip("Fewest guests, as a share of the suggested-price crowd, however dear.")]
        [SerializeField, Range(0f, 1f)] private float _minDemand = 0.15f;
        [Tooltip("Most guests, as a multiple of the suggested-price crowd, however cheap.")]
        [SerializeField, Min(1f)] private float _maxDemand = 1.5f;
        [Tooltip("How much a dish's price moves it against the menu's other dishes.")]
        [SerializeField, Min(0f)] private float _choiceElasticity = 1f;
        [Tooltip("Stars a guest takes off per 100% over the suggested price (adds when under).")]
        [SerializeField, Min(0f)] private float _starsPerRatio = 2.5f;
        [Tooltip("Most stars price can add or take off a rating.")]
        [SerializeField, Range(0f, 5f)] private float _maxStarShift = 1f;

        public IReadOnlyList<Entry> Entries => _recipes;

        public IEnumerable<Recipe> ToRecipes(ItemCatalog catalog) => (_recipes ?? Array.Empty<Entry>()).Select(e => e.ToRecipe(catalog));

        public PricingSettings ToPricing() => new PricingSettings
        {
            MinPrice = _minPrice,
            PriceStep = _priceStep,
            MaxPriceFactor = _maxPriceFactor,
            DemandElasticity = _demandElasticity,
            MinDemand = _minDemand,
            MaxDemand = _maxDemand,
            ChoiceElasticity = _choiceElasticity,
            StarsPerRatio = _starsPerRatio,
            MaxStarShift = _maxStarShift,
        };

        /// <summary>A fresh campaign's cookbook: the starter recipes unlocked, every price suggested.</summary>
        public RecipeBook NewBook(ItemCatalog catalog) => new RecipeBook(ToRecipes(catalog), ToPricing());
    }
}
