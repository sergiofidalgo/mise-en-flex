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

    /// <summary>What customers can order and how likely each order is.</summary>
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

        /// <summary>A weighted pick of the doneness, then a roll for the side. Null if the menu is empty.</summary>
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
            };
        }
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
