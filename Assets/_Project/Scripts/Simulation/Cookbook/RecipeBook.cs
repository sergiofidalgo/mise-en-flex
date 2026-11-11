using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Service;

namespace Game.Simulation.Cookbook
{
    public enum UnlockResult
    {
        Unlocked,
        AlreadyUnlocked,
        CantAfford,
        UnknownRecipe,
    }

    /// <summary>A dish's price as saved with the campaign.</summary>
    [Serializable]
    public sealed class PriceRecord
    {
        public string Recipe = "";
        // Whole money before 2026-11-11; a file with whole prices reads the same.
        public float Price;
    }

    /// <summary>
    /// One campaign's cookbook (decision 2026-11-10): the recipes it has paid to unlock, which are its
    /// menu, and the price set for each. Each campaign buys its own, at its own restaurant; what any
    /// campaign has unlocked also goes into the shared <see cref="SharedCookbook"/> for the sandbox.
    /// </summary>
    public sealed class RecipeBook
    {
        private readonly List<Recipe> _recipes;
        private readonly HashSet<string> _unlocked = new HashSet<string>();
        private readonly Dictionary<string, float> _prices = new Dictionary<string, float>();

        /// <param name="recipes">Every recipe the cookbook offers here, in the order it lists them.</param>
        public RecipeBook(IEnumerable<Recipe> recipes, PricingSettings pricing)
        {
            _recipes = recipes.Where(r => r != null).ToList();
            Pricing = pricing ?? new PricingSettings();
            foreach (var recipe in _recipes.Where(r => r.StartsUnlocked))
                _unlocked.Add(recipe.Id);
        }

        public PricingSettings Pricing { get; }

        public IReadOnlyList<Recipe> Recipes => _recipes;

        /// <summary>
        /// The recipes as the Restaurant tab lists them (2026-11-11): those on the menu first, then
        /// those still to unlock, each group in the cookbook's order.
        /// </summary>
        public IReadOnlyList<Recipe> OnMenuFirst() =>
            _recipes.Where(r => IsUnlocked(r.Id)).Concat(_recipes.Where(r => !IsUnlocked(r.Id))).ToList();

        /// <summary>The menu: unlocked recipes, in the cookbook's order.</summary>
        public IEnumerable<Recipe> Menu => _recipes.Where(r => _unlocked.Contains(r.Id));

        /// <summary>Raised when a recipe is unlocked or a price changes.</summary>
        public event Action Changed;

        /// <summary>Raised with a recipe just unlocked, so it can join the shared cookbook.</summary>
        public event Action<Recipe> Unlocked;

        public Recipe Find(string id) => _recipes.FirstOrDefault(r => r.Id == id);

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        /// <summary>Pays for a recipe and puts it on the menu, through <paramref name="trySpend"/>.</summary>
        public UnlockResult TryUnlock(string id, Func<float, bool> trySpend)
        {
            var recipe = Find(id);
            if (recipe == null)
                return UnlockResult.UnknownRecipe;
            if (_unlocked.Contains(id))
                return UnlockResult.AlreadyUnlocked;
            if (recipe.UnlockCost > 0 && !trySpend(recipe.UnlockCost))
                return UnlockResult.CantAfford;
            _unlocked.Add(id);
            Unlocked?.Invoke(recipe);
            Changed?.Invoke();
            return UnlockResult.Unlocked;
        }

        /// <summary>The price set for a dish, or its suggested price until one is.</summary>
        public float PriceOf(string id)
        {
            if (_prices.TryGetValue(id, out var price))
                return price;
            return Find(id)?.SuggestedPrice ?? 0;
        }

        public float MaxPriceOf(Recipe recipe) =>
            Math.Max(Pricing.MinPrice, Step * (float)Math.Ceiling(recipe.SuggestedPrice * Pricing.MaxPriceFactor / Step));

        // What every price is a multiple of: 50 cents (2026-11-11).
        private float Step => Pricing.PriceStep > 0f ? Pricing.PriceStep : 1f;

        // A price on the step, so floating-point drift can't leave $25.4999.
        private float OnStep(float price) => Step * (float)Math.Round(price / Step, MidpointRounding.AwayFromZero);

        /// <summary>Sets a price, on the step and kept between the lowest and highest allowed.</summary>
        public void SetPrice(string id, float price)
        {
            var recipe = Find(id);
            if (recipe == null)
                return;
            price = Math.Clamp(OnStep(price), Pricing.MinPrice, MaxPriceOf(recipe));
            if (Math.Abs(PriceOf(id) - price) < 0.001f)
                return;
            _prices[id] = price;
            Changed?.Invoke();
        }

        /// <summary>Moves a price by <paramref name="steps"/> of the price step (50 cents since 2026-11-11; 1 before).</summary>
        public void Nudge(string id, int steps) => SetPrice(id, PriceOf(id) + steps * Step);

        /// <summary>Price over suggested price, for the cookbook page and the formulas.</summary>
        public float RatioOf(Recipe recipe) => PriceResponse.Ratio(PriceOf(recipe.Id), recipe.SuggestedPrice);

        /// <summary>
        /// Today's menu at today's prices. Each dish is ordered in proportion to its popularity,
        /// moved by its price; how many guests come follows the menu's prices on average, weighted
        /// by how often each is ordered at its suggestion.
        /// </summary>
        public OrderMenu BuildMenu()
        {
            var dishes = new List<MenuDish>();
            var weighted = 0f;
            var weights = 0f;
            foreach (var recipe in Menu)
            {
                var price = PriceOf(recipe.Id);
                var ratio = RatioOf(recipe);
                // The side keeps its share of the price, so a missing side costs what it would have.
                var sidePrice = recipe.SideTypeId != null && recipe.SuggestedPrice > 0
                    ? OnStep(price * (float)recipe.SuggestedSidePrice / recipe.SuggestedPrice)
                    : 0f;
                dishes.Add(new MenuDish
                {
                    RecipeId = recipe.Id,
                    MainTypeId = recipe.MainTypeId,
                    MainName = recipe.MainName,
                    MainPrice = price - sidePrice,
                    Choices = recipe.Choices,
                    SideTypeId = recipe.SideTypeId,
                    SideName = recipe.SideName,
                    SidePrice = sidePrice,
                    SideChance = recipe.SideTypeId != null ? 1f : 0f,
                    Weight = Math.Max(0f, recipe.Popularity) * PriceResponse.ChoiceFactor(ratio, Pricing),
                    Expectation = PriceResponse.Expectation(ratio, Pricing),
                });
                weighted += Math.Max(0f, recipe.Popularity) * ratio;
                weights += Math.Max(0f, recipe.Popularity);
            }
            return new OrderMenu
            {
                Dishes = dishes,
                Demand = weights > 0f ? PriceResponse.Demand(weighted / weights, Pricing) : 1f,
            };
        }

        /// <summary>The unlocked recipes' ids, for the save.</summary>
        public List<string> UnlockedIds() => _recipes.Where(r => _unlocked.Contains(r.Id)).Select(r => r.Id).ToList();

        /// <summary>Only prices players have set, for the save.</summary>
        public List<PriceRecord> PriceRecords() =>
            _prices.Select(p => new PriceRecord { Recipe = p.Key, Price = p.Value }).OrderBy(p => p.Recipe).ToList();

        /// <summary>
        /// Back as saved. Starter recipes stay unlocked even in a file from before the cookbook, and
        /// ids the cookbook no longer has are dropped rather than breaking the load.
        /// </summary>
        public void Load(IEnumerable<string> unlocked, IEnumerable<PriceRecord> prices)
        {
            foreach (var id in unlocked ?? Enumerable.Empty<string>())
            {
                if (Find(id) != null)
                    _unlocked.Add(id);
            }
            foreach (var record in prices ?? Enumerable.Empty<PriceRecord>())
            {
                var recipe = record != null ? Find(record.Recipe) : null;
                if (recipe != null)
                    _prices[recipe.Id] = Math.Clamp(OnStep(record.Price), Pricing.MinPrice, MaxPriceOf(recipe));
            }
        }
    }
}
