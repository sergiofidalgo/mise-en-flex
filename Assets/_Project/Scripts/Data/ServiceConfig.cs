using System;
using System.Linq;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Tuning for a dinner service (decision 2026-11-02: ~6 real minutes, waves with a rush).
    /// Times are in in-game minutes; at 60x that's also real seconds.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Service Config", fileName = "ServiceConfig")]
    public sealed class ServiceConfig : ScriptableObject
    {
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

        [Tooltip("In-game hours the service runs (6 ≈ 6 real minutes).")]
        [SerializeField, Min(0.1f)] private float _durationHours = 6f;

        [Tooltip("Clock time the service opens, for display (18 = 18:00).")]
        [SerializeField, Range(0f, 23.99f)] private float _opensAt = 18f;

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

        [Header("Customers (gameplay)")]
        [Tooltip("Real metres per second customers walk.")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 1.6f;

        public float OpensAt => _opensAt;
        public float WalkSpeed => _walkSpeed;

        public ServiceSettings ToSettings() => new ServiceSettings
        {
            DurationSeconds = _durationHours * 3600f,
            ArrivalIntervalSeconds = _arrivalIntervalMinutes * 60f,
            ArrivalJitter = _arrivalJitter,
            RushStart = _rushStart,
            RushEnd = _rushEnd,
            RushMultiplier = _rushMultiplier,
            EatSeconds = _eatMinutes * 60f,
            MaxQueue = _maxQueue,
            Seed = _seed,
            Menu = BuildMenu(),
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
                Choices = _donenessWeights
                    .Where(w => levels.Any(l => l.Name == w.Doneness))
                    .Select(w => new OrderChoice(levels.First(l => l.Name == w.Doneness), w.Weight))
                    .ToArray(),
                SideTypeId = _side != null ? _side.TypeId : null,
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
