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
        [Tooltip("In-game hours the service runs (6 ≈ 6 real minutes).")]
        [SerializeField, Min(0.1f)] private float _durationHours = 6f;

        [Tooltip("Clock time the service opens, for display (18 = 18:00).")]
        [SerializeField, Range(0f, 23.99f)] private float _opensAt = 18f;

        [Tooltip("Average in-game minutes between customers outside the rush.")]
        [SerializeField, Min(0.5f)] private float _arrivalIntervalMinutes = 12f;

        [Tooltip("± fraction of random variation in each gap between customers.")]
        [SerializeField, Range(0f, 0.9f)] private float _arrivalJitter = 0.35f;

        [Tooltip("When the rush starts and ends, as fractions of the service.")]
        [SerializeField, Range(0f, 1f)] private float _rushStart = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _rushEnd = 0.6f;

        [Tooltip("How many times more often customers arrive during the rush.")]
        [SerializeField, Min(1f)] private float _rushMultiplier = 2.5f;

        [Tooltip("In-game minutes a customer spends eating.")]
        [SerializeField, Min(0.5f)] private float _eatMinutes = 15f;

        [Tooltip("Customers who wait by the door for a stool; more than that walk away.")]
        [SerializeField, Min(0)] private int _maxQueue = 3;

        [Tooltip("Random seed: the same seed plays the same service, which helps tuning.")]
        [SerializeField] private int _seed = 1;

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
        };
    }
}
