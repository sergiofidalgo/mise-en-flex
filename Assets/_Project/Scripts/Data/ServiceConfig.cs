using System;
using System.Linq;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Tuning for a dinner service (decisions 2026-11-02: ~5 real minutes, waves with a rush).
    /// Times are in in-game minutes; at 60x that's also real seconds.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Service Config", fileName = "ServiceConfig")]
    public sealed class ServiceConfig : ScriptableObject
    {
        [Serializable]
        public sealed class TipCurveStop
        {
            [Range(0f, 1f)] [SerializeField] private float _score;
            [Tooltip("Tip as a fraction of the menu price.")]
            [SerializeField, Min(0f)] private float _tipFraction;

            public TipCurveStop() { }

            public TipCurveStop(float score, float tipFraction)
            {
                _score = score;
                _tipFraction = tipFraction;
            }

            public float Score => _score;
            public float TipFraction => _tipFraction;
        }

        [Serializable]
        public sealed class ReviewLines
        {
            [SerializeField] private ReviewTopic _topic;
            [SerializeField] private string[] _lines;

            public ReviewLines() { }

            public ReviewLines(ReviewTopic topic, params string[] lines)
            {
                _topic = topic;
                _lines = lines;
            }

            public ReviewTopic Topic => _topic;
            public string[] Lines => _lines;
        }

        [Serializable]
        public sealed class DonenessWeight
        {
            [Tooltip("A doneness level name of the main, e.g. \"Medium-rare\".")]
            [SerializeField] private string _doneness;
            [Tooltip("How often it's ordered, relative to the others.")]
            [SerializeField, Min(0f)] private float _weight = 1f;

            public DonenessWeight() { }

            public DonenessWeight(string doneness, float weight)
            {
                _doneness = doneness;
                _weight = weight;
            }

            public string Doneness => _doneness;
            public float Weight => _weight;
        }

        [Tooltip("In-game hours the service runs (5 ≈ 5 real minutes).")]
        [SerializeField, Min(0.1f)] private float _durationHours = 5f;

        [Tooltip("Clock time the service opens, for display (18 = 18:00).")]
        [SerializeField, Range(0f, 23.99f)] private float _opensAt = 18f;

        [Tooltip("In-game minutes before the end when the door closes to new customers.")]
        [SerializeField, Min(0f)] private float _lastOrdersMinutes = 30f;

        [Tooltip("Average in-game minutes between customers outside the rush.")]
        [SerializeField, Min(0.5f)] private float _arrivalIntervalMinutes = 25f;

        [Tooltip("± fraction of random variation in each gap between customers.")]
        [SerializeField, Range(0f, 0.9f)] private float _arrivalJitter = 0.35f;

        [Tooltip("When the rush starts and ends, as fractions of the service.")]
        [SerializeField, Range(0f, 1f)] private float _rushStart = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _rushEnd = 0.6f;

        [Tooltip("How many times more often customers arrive during the rush.")]
        [SerializeField, Min(1f)] private float _rushMultiplier = 2f;

        [Tooltip("In-game minutes a customer spends eating.")]
        [SerializeField, Min(0.5f)] private float _eatMinutes = 15f;

        [Tooltip("In-game minutes a seated customer waits for food before walking out (realism dial 1).")]
        [SerializeField, Min(0.5f)] private float _patienceMinutes = 35f;

        [Tooltip("How fast patience drains while queueing by the door, relative to seated.")]
        [SerializeField, Range(0f, 1f)] private float _queuePatienceRate = 0.5f;

        [Tooltip("Customers who wait by the door for a stool; more than that walk away.")]
        [SerializeField, Min(0)] private int _maxQueue = 3;

        [Tooltip("Random seed: the same seed plays the same service, which helps tuning.")]
        [SerializeField] private int _seed = 1;

        [Header("Menu (orders)")]
        [Tooltip("What every customer orders, cooked to a doneness.")]
        [SerializeField] private IngredientDefinition _main;
        [Tooltip("Donenesses on the menu and how often each is ordered. Names must match the main's levels.")]
        [SerializeField] private DonenessWeight[] _donenessWeights =
        {
            new DonenessWeight("Rare", 2f), new DonenessWeight("Medium-rare", 4f),
            new DonenessWeight("Medium", 3f), new DonenessWeight("Well done", 1f),
        };
        [Tooltip("Optional side some customers add, e.g. sautéed onions.")]
        [SerializeField] private IngredientDefinition _side;
        [Tooltip("Chance an order includes the side.")]
        [SerializeField, Range(0f, 1f)] private float _sideChance = 0.4f;

        [Header("Prices and payment")]
        [Tooltip("Shown before amounts of money.")]
        [SerializeField] private string _currency = "€";
        [SerializeField, Min(0f)] private float _mainPrice = 24f;
        [SerializeField, Min(0f)] private float _sidePrice = 4f;
        [Tooltip("Share of the price paid for a dish scoring 0; a score of 1 pays all of it.")]
        [SerializeField, Range(0f, 1f)] private float _minPayFraction = 0.5f;
        [Tooltip("Dish score → tip as a fraction of the menu price, ascending by score. No tip below the first stop.")]
        [SerializeField] private TipCurveStop[] _tipCurve =
        {
            new TipCurveStop(0.80f, 0f),
            new TipCurveStop(0.90f, 0.10f),
            new TipCurveStop(0.95f, 0.20f),
            new TipCurveStop(0.99f, 0.50f),
            new TipCurveStop(0.995f, 1.00f),
        };
        [Tooltip("Patience left at or above which the wait doesn't reduce the tip.")]
        [SerializeField, Range(0f, 1f)] private float _slowTipFrom = 0.5f;
        [Tooltip("Share of the tip left when served with no patience remaining.")]
        [SerializeField, Range(0f, 1f)] private float _slowTipFraction = 0.4f;
        [Tooltip("Money lost when a customer walks out, as a fraction of their order's menu price.")]
        [SerializeField, Min(0f)] private float _walkOutPenaltyFraction = 0.5f;

        [Header("Reviews")]
        [Tooltip("A part of the dish scoring below this is worth complaining about.")]
        [SerializeField, Range(0f, 1f)] private float _lowPartScore = 0.5f;
        [Tooltip("Served with less patience left than this, the wait gets mentioned.")]
        [SerializeField, Range(0f, 1f)] private float _lowPatience = 0.25f;
        [Tooltip("Stars from which a dish gets praise rather than a 'decent'.")]
        [SerializeField, Range(0f, 5f)] private float _praiseStars = 4.5f;
        [Tooltip("Stars left by someone who walked out.")]
        [SerializeField, Range(0f, 5f)] private float _walkOutStars = 1f;
        [Tooltip("What customers say. {ordered}, {got}, {main} and {side} are filled in.")]
        [SerializeField] private ReviewLines[] _reviewLines =
        {
            new ReviewLines(ReviewTopic.Praise, "Best {main} in town.", "Perfect. Coming back.", "Exactly how I like it."),
            new ReviewLines(ReviewTopic.Decent, "Good {main}. No complaints.", "Solid. Would eat again."),
            new ReviewLines(ReviewTopic.Undercooked, "Asked for {ordered}, got {got}.", "Too raw for me."),
            new ReviewLines(ReviewTopic.Overcooked, "Asked for {ordered}, got {got}.", "Overdone. Such a waste."),
            new ReviewLines(ReviewTopic.Burnt, "Tasted of charcoal.", "Burnt. Who sends that out?"),
            new ReviewLines(ReviewTopic.NoMain, "Where was my {main}?"),
            new ReviewLines(ReviewTopic.NoSide, "Forgot my {side}.", "No {side}, as if I hadn't asked."),
            new ReviewLines(ReviewTopic.Cold, "Arrived cold.", "Should have been hot."),
            new ReviewLines(ReviewTopic.PaleCrust, "Pale and sad. No sear at all.", "Steamed, not seared."),
            new ReviewLines(ReviewTopic.NotRested, "Bled all over the plate. Let it rest!"),
            new ReviewLines(ReviewTopic.NotFresh, "Didn't taste fresh."),
            new ReviewLines(ReviewTopic.BadCut, "Chunks of onion, all different sizes."),
            new ReviewLines(ReviewTopic.SideOff, "The {side} let it down."),
            new ReviewLines(ReviewTopic.SlowService, "Lovely, but what a wait.", "Good food, slow kitchen."),
            new ReviewLines(ReviewTopic.WalkedOut, "Waited ages. Never got my food.", "Gave up and left hungry."),
        };

        [Header("Customers (gameplay)")]
        [Tooltip("Real metres per second customers walk.")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 1.6f;

        public float OpensAt => _opensAt;
        public float WalkSpeed => _walkSpeed;
        public string Currency => _currency;

        public ServiceSettings ToSettings() => new ServiceSettings
        {
            DurationSeconds = _durationHours * 3600f,
            LastOrdersSeconds = _lastOrdersMinutes * 60f,
            ArrivalIntervalSeconds = _arrivalIntervalMinutes * 60f,
            ArrivalJitter = _arrivalJitter,
            RushStart = _rushStart,
            RushEnd = _rushEnd,
            RushMultiplier = _rushMultiplier,
            EatSeconds = _eatMinutes * 60f,
            MaxQueue = _maxQueue,
            PatienceSeconds = _patienceMinutes * 60f,
            QueuePatienceRate = _queuePatienceRate,
            Seed = _seed,
            Menu = BuildMenu(),
            Payment = new PaymentSettings
            {
                MinPayFraction = _minPayFraction,
                TipCurve = _tipCurve.Select(s => new TipStop(s.Score, s.TipFraction)).ToArray(),
                SlowTipFrom = _slowTipFrom,
                SlowTipFraction = _slowTipFraction,
                WalkOutPenaltyFraction = _walkOutPenaltyFraction,
            },
            Reviews = new ReviewSettings
            {
                LowPartScore = _lowPartScore,
                LowPatience = _lowPatience,
                PraiseStars = _praiseStars,
                WalkOutStars = _walkOutStars,
                Lines = _reviewLines.ToDictionary(l => l.Topic, l => l.Lines),
            },
        };

        // Order choices take the main's own doneness levels (with their order targets); unknown
        // names are skipped so a typo can't break a service, and WiringTests checks them.
        private OrderMenu BuildMenu()
        {
            if (_main == null)
                return null;
            var levels = _main.ToProperties().DonenessLevels;
            return new OrderMenu
            {
                MainTypeId = _main.TypeId,
                MainName = _main.DisplayName,
                MainPrice = _mainPrice,
                Choices = _donenessWeights
                    .Where(w => levels.Any(l => l.Name == w.Doneness))
                    .Select(w => new OrderChoice(levels.First(l => l.Name == w.Doneness), w.Weight))
                    .ToArray(),
                SideTypeId = _side != null ? _side.TypeId : null,
                SideName = _side != null ? _side.DisplayName : null,
                SidePrice = _sidePrice,
                SideChance = _sideChance,
            };
        }

        /// <summary>Menu doneness names that don't match a level of the main (should be none).</summary>
        public string[] UnknownDonenesses() => _main == null
            ? Array.Empty<string>()
            : _donenessWeights.Select(w => w.Doneness)
                .Where(n => _main.ToProperties().DonenessLevels.All(l => l.Name != n)).ToArray();
    }
}
