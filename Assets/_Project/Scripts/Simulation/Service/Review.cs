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
        public Review(Customer customer, float stars, ReviewTopic topic, string line, Persona persona = Persona.Regular, KnownGuest returning = null)
        {
            Customer = customer;
            Stars = stars;
            Topic = topic;
            Line = line;
            Persona = persona;
            Returning = returning;
        }

        public Review(Customer customer, float stars, ReviewTopic topic, Said said, ReviewFacts facts, Persona persona, KnownGuest returning)
            : this(customer, stars, topic, said.Text, persona, returning)
        {
            Templates = said.Templates;
            Facts = facts;
        }

        /// <summary>The templates the line was written from (empty for the fixed lines), saved so it can be shown in any language.</summary>
        public IReadOnlyList<string> Templates { get; } = new List<string>();

        /// <summary>The facts the line was filled with.</summary>
        public ReviewFacts Facts { get; } = new ReviewFacts();

        public Customer Customer { get; }
        public float Stars { get; }
        public ReviewTopic Topic { get; }
        public string Line { get; }

        /// <summary>Who wrote it, which shapes how they write and how they take a reply.</summary>
        public Persona Persona { get; }

        /// <summary>The guest who came back, if this is someone who has posted before; null for a first visit.</summary>
        public KnownGuest Returning { get; }
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

        /// <summary>The restaurant style's voice (ideas.md, 2026-11-09); null for none.</summary>
        public Voice Style { get; set; }

        /// <summary>The voice every style shares. When it or <see cref="Style"/> is set, lines come from the voices.</summary>
        public Voice Generic { get; set; }

        /// <summary>Lines used lately, kept with the campaign so words don't repeat across days.</summary>
        public LineMemory Memory { get; set; }

        /// <summary>Guests who posted before and might come back; null for none (ideas.md, 2026-11-09).</summary>
        public GuestBook Guests { get; set; }

        public string Restaurant { get; set; } = "";

        /// <summary>The hour the doors open, for times such as when a guest left.</summary>
        public float OpensAtHour { get; set; } = 18f;

        // Not every guest posts (user's call, 2026-11-09): a night worth talking about is likelier to
        // be reviewed than a forgettable one. 1 means everyone posts.
        public float PraiseShare { get; set; } = 1f;
        public float DecentShare { get; set; } = 1f;
        public float ComplaintShare { get; set; } = 1f;
        public float WalkOutShare { get; set; } = 1f;

        /// <summary>How much likelier each kind of guest is to post, by <see cref="Persona"/> order; empty means 1.</summary>
        public float[] PersonaShare { get; set; } = new float[0];

        /// <summary>The chance this guest posts about this night, from 0 to 1.</summary>
        public float ShareChance(ReviewTopic topic, Persona persona)
        {
            var chance = topic switch
            {
                ReviewTopic.Praise => PraiseShare,
                ReviewTopic.Decent => DecentShare,
                ReviewTopic.WalkedOut => WalkOutShare,
                _ => ComplaintShare,
            };
            var index = (int)persona;
            if (PersonaShare != null && index < PersonaShare.Length)
                chance *= PersonaShare[index];
            return Math.Max(0f, Math.Min(1f, chance));
        }
    }

    /// <summary>Picks what a customer says about their visit (decision 2026-11-03).</summary>
    public static class ReviewWriter
    {
        /// <summary>
        /// The worst thing wins: something missing or burnt, then the wrong doneness, then the
        /// weakest part of the dish, then a long wait. With nothing to complain about, the stars
        /// decide between praise and "decent".
        /// </summary>
        /// <param name="stars">What the guest rates it, if their expectations moved it off the dish's own stars.</param>
        public static ReviewTopic ChooseTopic(DishScore score, float patienceWhenServed, ReviewSettings settings, float? stars = null)
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
            return (stars ?? score.Stars) >= settings.PraiseStars ? ReviewTopic.Praise : ReviewTopic.Decent;
        }

        /// <summary>
        /// What this guest posts about their night, or null if they keep it to themselves: not every
        /// guest reviews, and how memorable the night was and who they are decide how likely it is.
        /// </summary>
        public static Review Write(Customer customer, ReviewSettings settings, Random random)
        {
            var stars = customer.RatedStars ?? settings.WalkOutStars;
            var topic = customer.Satisfaction == null ? ReviewTopic.WalkedOut : ChooseTopic(customer.Satisfaction, customer.PatienceWhenServed ?? 1f, settings, stars);
            var voiced = settings.Style != null || settings.Generic != null;
            // Sometimes it's someone who's been before, and who they are carries over.
            var returning = voiced ? settings.Guests?.Draw(random) : null;
            var persona = returning?.Persona ?? (voiced ? ReviewVoices.PickPersona(settings.Style, settings.Generic, random) : Persona.Regular);
            // The dice are only rolled when someone might stay quiet, so settings where everyone
            // posts give the same reviews as before. A returning guest has more to say.
            var chance = settings.ShareChance(topic, persona);
            if (returning != null)
                chance = Math.Min(1f, chance * settings.Guests.PostBoost);
            if (chance < 1f && random.NextDouble() >= chance)
                return null;
            if (!voiced)
                return new Review(customer, stars, topic, Line(topic, customer, settings, random));

            if (returning != null)
            {
                var facts = Facts(customer, settings);
                var back = ReviewVoices.SayReturn(returning.Memory, topic, persona, facts, settings.Style, settings.Generic, settings.Memory, random);
                if (back.Text.Length > 0)
                    return new Review(customer, stars, topic, back, facts, persona, returning);
            }

            var visit = Facts(customer, settings);
            var said = ReviewVoices.SayLine(topic, persona, visit, settings.Style, settings.Generic, settings.Memory, random);
            return said.Text.Length > 0
                ? new Review(customer, stars, topic, said, visit, persona, returning)
                : new Review(customer, stars, topic, Line(topic, customer, settings, random), persona, returning);
        }

        /// <summary>What a review can mention about this visit.</summary>
        public static ReviewFacts Facts(Customer customer, ReviewSettings settings)
        {
            var order = customer.Order;
            var got = customer.Satisfaction != null ? Main(customer.Satisfaction)?.DonenessName : null;
            // Service time runs a second to the game minute, as the dashboard's clock reads it.
            var left = settings.OpensAtHour * 60f + (customer.ArrivedAt + customer.SecondsInRestaurant) / 60f;
            return new ReviewFacts
            {
                Main = order?.MainName.ToLowerInvariant() ?? "",
                Side = order?.SideName?.ToLowerInvariant() ?? "",
                Ordered = order?.Doneness.Name.ToLowerInvariant() ?? "",
                Got = got?.ToLowerInvariant() ?? "",
                Restaurant = settings.Restaurant ?? "",
                LeftAt = $"{(int)(left / 60f) % 24:00}:{(int)left % 60:00}",
                WaitMinutes = customer.WalkedOut ? (int)Math.Round(customer.SecondsInRestaurant / 60f) : 0,
            };
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
