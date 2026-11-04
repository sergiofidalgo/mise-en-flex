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
            new Template(StaffRole.PrepAssistant, "neat but slow", 0.98f, 0.7f, 40f),
            new Template(StaffRole.PrepAssistant, "quick, a bit sloppy", 0.85f, 1.3f, 45f),
            new Template(StaffRole.PrepAssistant, "steady hands", 0.94f, 1f, 50f),
            new Template(StaffRole.PrepAssistant, "trained, and expensive", 0.99f, 1.3f, 80f),
        };

        [Header("Work")]
        [Tooltip("Real metres per second staff walk.")]
        [SerializeField, Min(0.1f)] private float _walkSpeed = 2f;
        [Tooltip("In-game seconds to wash a plate at speed 1.")]
        [SerializeField, Min(1f)] private float _washSecondsPerPlate = 150f;
        [Tooltip("In-game seconds to cut one item at speed 1.")]
        [SerializeField, Min(1f)] private float _cutSeconds = 240f;
        [Tooltip("Fatigue (0..1) gained per portion a prep assistant moves or cuts.")]
        [SerializeField, Range(0f, 1f)] private float _fatiguePerJob = 0.02f;
        [Tooltip("A container below this share of full gets refilled.")]
        [SerializeField, Range(0f, 1f)] private float _refillBelow = 0.5f;
        [Tooltip("In-game seconds a prep assistant waits for a busy board before giving up.")]
        [SerializeField, Min(0f)] private float _boardPatienceSeconds = 300f;
        [Tooltip("Fatigue (0..1) gained per plate washed.")]
        [SerializeField, Range(0f, 1f)] private float _fatiguePerPlate = 0.05f;
        [Tooltip("Share of speed lost when fully fatigued.")]
        [SerializeField, Range(0f, 1f)] private float _fatigueSlowdown = 0.4f;
        [Tooltip("Added to the chance of a mistake when fully fatigued.")]
        [SerializeField, Range(0f, 1f)] private float _fatigueMistakes = 0.1f;

        [Header("Morale")]
        [Tooltip("Morale (0..1) someone starts with, and at which it neither helps nor hurts their work.")]
        [SerializeField, Range(0f, 1f)] private float _neutralMorale = 0.6f;
        [Tooltip("How much morale changes their speed: at 0.3, a full point of morale is 30% of their speed.")]
        [SerializeField, Min(0f)] private float _moraleSpeedEffect = 0.3f;
        [Tooltip("Added to the chance of a mistake per point of morale below neutral.")]
        [SerializeField, Min(0f)] private float _moraleMistakeEffect = 0.15f;
        [Tooltip("Morale change for the best or worst possible rating of the day; 3 stars is neutral.")]
        [SerializeField, Min(0f)] private float _moraleFromRating = 0.1f;
        [Tooltip("Morale lost if everyone walked out, in proportion to the share who did.")]
        [SerializeField, Min(0f)] private float _moraleFromWalkOuts = 0.15f;
        [Tooltip("Fatigue at the end of a day above which it starts to hurt morale.")]
        [SerializeField, Range(0f, 1f)] private float _comfortableFatigue = 0.5f;
        [Tooltip("Morale lost for ending the day completely exhausted.")]
        [SerializeField, Min(0f)] private float _moraleFromOverwork = 0.2f;
        [Tooltip("Below this morale at the end of a day, they quit.")]
        [SerializeField, Range(0f, 1f)] private float _quitBelow = 0.1f;

        public float StartingMoney => _startingMoney;
        public float WalkSpeed => _walkSpeed;
        public int Seed => _seed;

        public StaffSettings ToSettings() => new StaffSettings
        {
            WashSecondsPerPlate = _washSecondsPerPlate,
            FatiguePerPlate = _fatiguePerPlate,
            FatigueSlowdown = _fatigueSlowdown,
            FatigueMistakes = _fatigueMistakes,
            CutSeconds = _cutSeconds,
            FatiguePerJob = _fatiguePerJob,
            RefillBelow = _refillBelow,
            BoardPatienceSeconds = _boardPatienceSeconds,
            NeutralMorale = _neutralMorale,
            MoraleSpeedEffect = _moraleSpeedEffect,
            MoraleMistakeEffect = _moraleMistakeEffect,
            MoraleFromRating = _moraleFromRating,
            MoraleFromWalkOuts = _moraleFromWalkOuts,
            ComfortableFatigue = _comfortableFatigue,
            MoraleFromOverwork = _moraleFromOverwork,
            QuitBelow = _quitBelow,
        };

        public System.Collections.Generic.IReadOnlyList<Candidate> CandidatesFor(int day) =>
            StaffMarket.CandidatesFor(day, _seed, _candidatesPerDay, _names, _templates.Select(t => t.ToCandidate()).ToArray());
    }
}
