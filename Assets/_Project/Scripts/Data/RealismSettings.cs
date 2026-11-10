using System;
using Game.Simulation.Realism;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The realism dials (game design §12). Read by the simulation, never by scattered checks in
    /// gameplay code. The asset holds the designer's defaults and the three presets; the players'
    /// own values come from the settings file (<c>PlayerSettings</c>) and the pause menu.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Realism Settings", fileName = "RealismSettings")]
    public sealed class RealismSettings : ScriptableObject
    {
        /// <summary>One preset's dials, as the Inspector shows them.</summary>
        [Serializable]
        public sealed class DialValues
        {
            [Range(0f, 1f)] public float CookingPrecision = 0.5f;
            [Min(0f)] public float Spoilage = 1f;
            [Min(0f)] public float Patience = 1f;
            [Range(0f, 1f)] public float MinigameComplexity = 0.5f;
            [Min(0f)] public float Hygiene = 1f;

            public DialValues() { }

            public DialValues(float precision, float spoilage, float patience, float complexity, float hygiene)
            {
                CookingPrecision = precision;
                Spoilage = spoilage;
                Patience = patience;
                MinigameComplexity = complexity;
                Hygiene = hygiene;
            }

            public RealismDials ToDials() => new RealismDials
            {
                CookingPrecision = CookingPrecision,
                Spoilage = Spoilage,
                Patience = Patience,
                MinigameComplexity = MinigameComplexity,
                Hygiene = Hygiene,
            };
        }

        [Tooltip("How narrow doneness windows are when scoring (0 = generous, 1 = strict).")]
        [SerializeField, Range(0f, 1f)] private float _cookingPrecision = 0.5f;

        [Tooltip("Multiplies how fast food loses freshness. 0 = never spoils.")]
        [SerializeField, Min(0f)] private float _spoilageMultiplier = 1f;

        [Tooltip("Multiplies how long customers wait before walking out. 2 = twice as patient, 0 = never walk out.")]
        [SerializeField, Min(0f)] private float _customerPatience = 1f;

        [Tooltip("How demanding the stick games are (sink, cleaning, extinguisher): 0 = gentle, 0.5 = as tuned, 1 = hard.")]
        [SerializeField, Range(0f, 1f)] private float _minigameComplexity = 0.5f;

        [Tooltip("Multiplies how much dirt a service leaves. 0 = none, 1 = as tuned.")]
        [SerializeField, Min(0f)] private float _hygiene = 1f;

        [Header("Presets (the user's values, 2026-11-10)")]
        [SerializeField] private DialValues _relaxed = new DialValues(0.2f, 0.5f, 1.5f, 0.25f, 0.5f);
        [SerializeField] private DialValues _standard = new DialValues(0.5f, 1f, 1f, 0.5f, 1f);
        [SerializeField] private DialValues _chef = new DialValues(0.8f, 1.5f, 0.75f, 0.8f, 1.5f);

        [Header("Minigame complexity")]
        [Tooltip("Doublings of forgiveness from the dial's middle to its end: targets grow and run-away forces shrink by 2^(Strength × (0.5 − complexity)).")]
        [SerializeField, Min(0f)] private float _complexityStrength = 1f;
        [Tooltip("Extra pushes per plate at the sink over the whole dial: 4 means complexity 0 has two fewer, 1 has two more.")]
        [SerializeField, Min(0f)] private float _complexityCorrectionsSpan = 4f;

        // The players' own settings, set at runtime from the settings file. Not serialized, so the
        // asset keeps the designer's defaults.
        [NonSerialized] private RealismDials _player;

        public float CookingPrecision => _player?.CookingPrecision ?? _cookingPrecision;
        public float CustomerPatience => _player?.Patience ?? _customerPatience;
        public float SpoilageMultiplier => _player?.Spoilage ?? _spoilageMultiplier;
        public float MinigameComplexity => _player?.MinigameComplexity ?? _minigameComplexity;
        public float Hygiene => _player?.Hygiene ?? _hygiene;

        /// <summary>The designer's defaults, for a settings file that has never set a dial.</summary>
        public RealismDials Defaults => new RealismDials
        {
            CookingPrecision = _cookingPrecision,
            Spoilage = _spoilageMultiplier,
            Patience = _customerPatience,
            MinigameComplexity = _minigameComplexity,
            Hygiene = _hygiene,
        };

        public RealismPresets Presets => new RealismPresets(_relaxed.ToDials(), _standard.ToDials(), _chef.ToDials());

        public MinigameComplexitySettings Complexity => new MinigameComplexitySettings
        {
            Strength = _complexityStrength,
            CorrectionsSpan = _complexityCorrectionsSpan,
        };

        /// <summary>Uses the players' chosen values from now on (the settings in the pause menu).</summary>
        public void UsePlayerValues(RealismDials dials) => _player = dials.Copy();
    }
}
