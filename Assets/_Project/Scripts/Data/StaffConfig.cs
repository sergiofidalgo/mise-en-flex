using System;
using System.Linq;
using Game.Simulation.Staff;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Who can be hired and how staff work (decision 2026-11-04). Times are in in-game seconds; at
    /// 60x, 60 is one real second.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Staff Config", fileName = "StaffConfig")]
    public sealed class StaffConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Template
        {
            [SerializeField] private StaffRole _role;
            [Tooltip("What players read: words, not numbers.")]
            [SerializeField] private string _description;
            [Tooltip("0..1: the chance a job goes right when they're fresh.")]
            [SerializeField, Range(0f, 1f)] private float _skill = 0.95f;
            [Tooltip("1 is an average pace.")]
            [SerializeField, Min(0.1f)] private float _speed = 1f;
            [SerializeField, Min(0f)] private float _dailyWage = 40f;

            public Template() { }

            public Template(StaffRole role, string description, float skill, float speed, float wage)
            {
                _role = role;
                _description = description;
                _skill = skill;
                _speed = speed;
                _dailyWage = wage;
            }

            public Candidate ToCandidate() => new Candidate(string.Empty, _role, _description, _skill, _speed, _dailyWage);
        }

        [Header("Money")]
        [Tooltip("Balance on day 1, so a first hire is possible.")]
        [SerializeField, Min(0f)] private float _startingMoney = 100f;

        [Header("Candidates")]
        [Tooltip("How many people are looking for work each day.")]
        [SerializeField, Min(1)] private int _candidatesPerDay = 3;
        [SerializeField] private int _seed = 1;
        [SerializeField] private string[] _names = { "Ana", "Tomás", "Marta", "Ben", "Lucía", "Omar", "Nina", "Pau" };
        [SerializeField] private Template[] _templates =
        {
            new Template(StaffRole.Dishwasher, "quick but careless", 0.90f, 1.4f, 50f),
            new Template(StaffRole.Dishwasher, "steady, nothing fancy", 0.96f, 1f, 40f),
            new Template(StaffRole.Dishwasher, "slow, very reliable", 0.995f, 0.7f, 30f),
            new Template(StaffRole.Dishwasher, "fast and careful, and knows it", 0.98f, 1.4f, 70f),
        };

        [Header("Work")]
        [Tooltip("Real metres per second staff walk.")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 2f;
        [Tooltip("In-game seconds to wash a plate at speed 1.")]
        [SerializeField, Min(1f)] private float _washSecondsPerPlate = 150f;
        [Tooltip("Fatigue (0..1) gained per plate washed.")]
        [SerializeField, Range(0f, 1f)] private float _fatiguePerPlate = 0.05f;
        [Tooltip("Share of speed lost when fully fatigued.")]
        [SerializeField, Range(0f, 1f)] private float _fatigueSlowdown = 0.4f;
        [Tooltip("Added to the chance of a mistake when fully fatigued.")]
        [SerializeField, Range(0f, 1f)] private float _fatigueMistakes = 0.1f;

        public float StartingMoney => _startingMoney;
        public float WalkSpeed => _walkSpeed;
        public int Seed => _seed;

        public StaffSettings ToSettings() => new StaffSettings
        {
            WashSecondsPerPlate = _washSecondsPerPlate,
            FatiguePerPlate = _fatiguePerPlate,
            FatigueSlowdown = _fatigueSlowdown,
            FatigueMistakes = _fatigueMistakes,
        };

        public System.Collections.Generic.IReadOnlyList<Candidate> CandidatesFor(int day) =>
            StaffMarket.CandidatesFor(day, _seed, _candidatesPerDay, _names, _templates.Select(t => t.ToCandidate()).ToArray());
    }
}
