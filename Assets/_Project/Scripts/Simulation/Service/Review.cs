using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Quality;

namespace Game.Simulation.Service
{
    /// <summary>What a review line is about: the one thing the customer remembers most.</summary>
    public enum ReviewTopic
    {
        Praise,
        Decent,
        Undercooked,
        Overcooked,
        Burnt,
        NoMain,
        NoSide,
        Cold,
        PaleCrust,
        NotRested,
        NotFresh,
        BadCut,
        SideOff,
        SlowService,
        WalkedOut,
    }

    /// <summary>One customer's review.</summary>
    public sealed class Review
    {
        public Review(Customer customer, float stars, ReviewTopic topic, string line)
        {
            Customer = customer;
            Stars = stars;
            Topic = topic;
            Line = line;
        }

        public Customer Customer { get; }
        public float Stars { get; }
        public ReviewTopic Topic { get; }
        public string Line { get; }
    }

    /// <summary>Tuning and text for reviews. Filled from the ServiceConfig asset.</summary>
    public sealed class ReviewSettings
    {
        /// <summary>A part of the dish scoring below this is worth complaining about.</summary>
        public float LowPartScore { get; set; } = 0.5f;

        /// <summary>Served with less patience than this, the wait gets mentioned.</summary>
        public float LowPatience { get; set; } = 0.25f;

        /// <summary>Stars from which a dish gets praise rather than "decent".</summary>
        public float PraiseStars { get; set; } = 4.5f;

        /// <summary>Stars left by someone who walked out.</summary>
        public float WalkOutStars { get; set; } = 1f;

        /// <summary>
        /// Lines per topic. {ordered} is the doneness they asked for, {got} what they got, {main}
        /// and {side} the dish names.
        /// </summary>
        public IReadOnlyDictionary<ReviewTopic, string[]> Lines { get; set; } = new Dictionary<ReviewTopic, string[]>();
    }

    /// <summary>Picks what a customer says about their visit (decision 2026-11-03).</summary>
    public static class ReviewWriter
    {
        /// <summary>
        /// The worst thing wins: something missing or burnt, then the wrong doneness, then the
        /// weakest part of the dish, then a long wait. With nothing to complain about, the stars
        /// decide between praise and "decent".
        /// </summary>
        public static ReviewTopic ChooseTopic(DishScore score, float patienceWhenServed, ReviewSettings settings)
        {
            if (score.Match == OrderMatch.MissingMain)
                return ReviewTopic.NoMain;
            if (Burnt(score))
                return ReviewTopic.Burnt;
            if (score.Match == OrderMatch.WrongDoneness)
                return Main(score).Ingredient.PeakCoreTemperature < score.Order.Doneness.OrderTargetCoreTemperature
                    ? ReviewTopic.Undercooked
                    : ReviewTopic.Overcooked;
            if (score.Match == OrderMatch.MissingSide)
                return ReviewTopic.NoSide;

            var weakest = WeakestPart(score, settings);
            if (weakest.HasValue)
                return weakest.Value;
            if (patienceWhenServed < settings.LowPatience)
                return ReviewTopic.SlowService;
            return score.Stars >= settings.PraiseStars ? ReviewTopic.Praise : ReviewTopic.Decent;
        }

        public static Review Write(Customer customer, ReviewSettings settings, Random random)
        {
            if (customer.Satisfaction == null)
                return new Review(customer, settings.WalkOutStars, ReviewTopic.WalkedOut, Line(ReviewTopic.WalkedOut, customer, settings, random));
            var topic = ChooseTopic(customer.Satisfaction, customer.PatienceWhenServed ?? 1f, settings);
            return new Review(customer, customer.Satisfaction.Stars, topic, Line(topic, customer, settings, random));
        }

        private static string Line(ReviewTopic topic, Customer customer, ReviewSettings settings, Random random)
        {
            if (!settings.Lines.TryGetValue(topic, out var lines) || lines == null || lines.Length == 0)
                return string.Empty;

            var line = lines[random.Next(lines.Length)];
            var order = customer.Order;
            if (order == null)
                return line;
            var got = customer.Satisfaction != null ? Main(customer.Satisfaction)?.DonenessName : null;
            return line
                .Replace("{ordered}", order.Doneness.Name.ToLowerInvariant())
                .Replace("{got}", (got ?? "raw").ToLowerInvariant())
                .Replace("{main}", order.MainName.ToLowerInvariant())
                .Replace("{side}", (order.SideName ?? string.Empty).ToLowerInvariant());
        }

        private static ItemScore Main(DishScore score)
        {
            foreach (var item in score.Items)
            {
                if (score.Order != null && item.Ingredient.TypeId == score.Order.MainTypeId)
                    return item;
            }
            return null;
        }

        private static bool Burnt(DishScore score)
        {
            foreach (var item in score.Items)
            {
                if (item.Ingredient.IsBurnt)
                    return true;
            }
            return false;
        }

        // The lowest-scoring part below the complaint threshold. Doneness only counts for the side
        // here: the main's doneness was already checked against the order.
        private static ReviewTopic? WeakestPart(DishScore score, ReviewSettings settings)
        {
            ReviewTopic? topic = null;
            var lowest = settings.LowPartScore;
            foreach (var item in score.Items)
            {
                var isMain = score.Order != null && item.Ingredient.TypeId == score.Order.MainTypeId;
                if (!isMain)
                    Check(item.Doneness, ReviewTopic.SideOff);
                Check(item.Temperature, ReviewTopic.Cold);
                Check(item.Crust, isMain ? ReviewTopic.PaleCrust : ReviewTopic.SideOff);
                Check(item.Rest, ReviewTopic.NotRested);
                Check(item.Freshness, ReviewTopic.NotFresh);
                Check(item.Cut, ReviewTopic.BadCut);
            }
            return topic;

            void Check(float? part, ReviewTopic candidate)
            {
                if (part.HasValue && part.Value < lowest)
                {
                    lowest = part.Value;
                    topic = candidate;
                }
            }
        }
    }
}
