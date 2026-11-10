using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Simulation.Service
{
    /// <summary>
    /// What one customer wants (decision 2026-11-02): a main cooked to a doneness, maybe with a
    /// side. The doneness's order target replaces the house standard when the dish is scored.
    /// </summary>
    public sealed class Order
    {
        private string _mainName;
        private string _sideName;

        public Order(string mainTypeId, DonenessLevel doneness, string sideTypeId = null)
        {
            MainTypeId = mainTypeId;
            Doneness = doneness;
            SideTypeId = sideTypeId;
        }

        public string MainTypeId { get; }

        public DonenessLevel Doneness { get; }

        /// <summary>The side they asked for, or null.</summary>
        public string SideTypeId { get; }

        /// <summary>What players read for the main; the type id unless the menu names it.</summary>
        public string MainName
        {
            get => _mainName ?? MainTypeId;
            set => _mainName = value;
        }

        /// <summary>What players read for the side, or null without one.</summary>
        public string SideName
        {
            get => SideTypeId == null ? null : _sideName ?? SideTypeId;
            set => _sideName = value;
        }

        public float MainPrice { get; set; }
        public float SidePrice { get; set; }

        /// <summary>The cookbook recipe it was ordered from, or null for a menu without recipes.</summary>
        public string RecipeId { get; set; }

        /// <summary>
        /// Stars added to (or taken off) what the dish alone would earn, from what it cost them: a
        /// dear dish has to be better to please (decision 2026-11-10). 0 at the suggested price.
        /// </summary>
        public float Expectation { get; set; }

        public bool Wants(string typeId) => typeId == MainTypeId || (SideTypeId != null && typeId == SideTypeId);

        public override string ToString() => SideTypeId == null
            ? $"{MainName} · {Doneness.Name}"
            : $"{MainName} · {Doneness.Name} + {SideName}";
    }

    /// <summary>One doneness on the menu and how often it's ordered (relative weight).</summary>
    public readonly struct OrderChoice
    {
        public OrderChoice(DonenessLevel doneness, float weight)
        {
            Doneness = doneness;
            Weight = weight;
        }

        public DonenessLevel Doneness { get; }
        public float Weight { get; }
    }

    /// <summary>
    /// One dish on the menu: a main cooked to a doneness, maybe with a side, at a price. A cookbook
    /// recipe becomes one of these once it's unlocked (decision 2026-11-10).
    /// </summary>
    public sealed class MenuDish
    {
        public string RecipeId { get; set; }
        public string MainTypeId { get; set; }
        public string MainName { get; set; }
        public float MainPrice { get; set; }
        public IReadOnlyList<OrderChoice> Choices { get; set; } = Array.Empty<OrderChoice>();

        /// <summary>Optional side, or null for none.</summary>
        public string SideTypeId { get; set; }
        public string SideName { get; set; }
        public float SidePrice { get; set; }

        /// <summary>0..1 chance an order includes the side (1 for a recipe that comes with it).</summary>
        public float SideChance { get; set; }

        /// <summary>How often it's ordered, relative to the menu's other dishes.</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>Stars its price adds to or takes off every rating (see <see cref="Order.Expectation"/>).</summary>
        public float Expectation { get; set; }

        /// <summary>A weighted pick of the doneness, then a roll for the side. Null without a main.</summary>
        public Order Pick(Random random)
        {
            var total = 0f;
            foreach (var choice in Choices)
                total += Math.Max(0f, choice.Weight);
            if (MainTypeId == null || total <= 0f)
                return null;

            var roll = (float)random.NextDouble() * total;
            var picked = Choices[Choices.Count - 1].Doneness;
            foreach (var choice in Choices)
            {
                roll -= Math.Max(0f, choice.Weight);
                if (roll < 0f)
                {
                    picked = choice.Doneness;
                    break;
                }
            }

            var side = SideTypeId != null && random.NextDouble() < SideChance ? SideTypeId : null;
            return new Order(MainTypeId, picked, side)
            {
                MainName = MainName,
                SideName = SideName,
                MainPrice = MainPrice,
                SidePrice = SidePrice,
                RecipeId = RecipeId,
                Expectation = Expectation,
            };
        }
    }

    /// <summary>
    /// What customers can order and how likely each order is. Either a list of dishes (the
    /// cookbook's menu), or the single dish described by its own properties (tests, and a service
    /// without a cookbook).
    /// </summary>
    public sealed class OrderMenu
    {
        public string MainTypeId { get; set; }
        public string MainName { get; set; }
        public float MainPrice { get; set; }
        public IReadOnlyList<OrderChoice> Choices { get; set; } = Array.Empty<OrderChoice>();

        /// <summary>Optional side, or null for none.</summary>
        public string SideTypeId { get; set; }
        public string SideName { get; set; }
        public float SidePrice { get; set; }

        /// <summary>0..1 chance an order includes the side.</summary>
        public float SideChance { get; set; }

        /// <summary>The dishes on the menu. When empty, the single dish above is the menu.</summary>
        public IReadOnlyList<MenuDish> Dishes { get; set; } = Array.Empty<MenuDish>();

        /// <summary>
        /// How many customers the menu's prices draw, against the suggested prices: 0.5 is half as
        /// many arrivals, 1.5 half as many again (decision 2026-11-10). 1 without a cookbook.
        /// </summary>
        public float Demand { get; set; } = 1f;

        /// <summary>A weighted pick of a dish, then its doneness and side. Null if the menu is empty.</summary>
        public Order Pick(Random random)
        {
            if (Dishes == null || Dishes.Count == 0)
                return Single().Pick(random);

            var total = 0f;
            foreach (var dish in Dishes)
                total += Math.Max(0f, dish.Weight);
            if (total <= 0f)
                return null;
            var roll = (float)random.NextDouble() * total;
            var picked = Dishes[Dishes.Count - 1];
            foreach (var dish in Dishes)
            {
                roll -= Math.Max(0f, dish.Weight);
                if (roll < 0f)
                {
                    picked = dish;
                    break;
                }
            }
            return picked.Pick(random);
        }

        private MenuDish Single() => new MenuDish
        {
            MainTypeId = MainTypeId,
            MainName = MainName,
            MainPrice = MainPrice,
            Choices = Choices,
            SideTypeId = SideTypeId,
            SideName = SideName,
            SidePrice = SidePrice,
            SideChance = SideChance,
        };
    }

    public enum OrderMatch
    {
        /// <summary>Everything ordered is on the plate, the main at the ordered doneness.</summary>
        Matches,
        /// <summary>The main is there but at another doneness (or raw).</summary>
        WrongDoneness,
        /// <summary>The side they asked for is missing.</summary>
        MissingSide,
        /// <summary>The main is missing.</summary>
        MissingMain,
    }

    /// <summary>Checks a plate against an order, for the pass prompt. Scoring is DishScorer's job.</summary>
    public static class OrderCheck
    {
        /// <summary>The worst mismatch first: no main, then wrong doneness, then a missing side.</summary>
        public static OrderMatch Check(Order order, Plate plate, out Ingredient main)
        {
            main = null;
            var hasSide = false;
            foreach (var item in plate.Items)
            {
                if (item is Ingredient ingredient && ingredient.TypeId == order.MainTypeId && main == null)
                    main = ingredient;
                else if (order.SideTypeId != null && item.TypeId == order.SideTypeId)
                    hasSide = true;
            }

            if (main == null)
                return OrderMatch.MissingMain;
            if (main.Doneness?.Name != order.Doneness.Name)
                return OrderMatch.WrongDoneness;
            if (order.SideTypeId != null && !hasSide)
                return OrderMatch.MissingSide;
            return OrderMatch.Matches;
        }
    }
}
