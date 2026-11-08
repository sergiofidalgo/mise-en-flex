using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The realism dials. Read by the simulation, never by scattered checks in gameplay code.
    /// More dials (minigame complexity, hygiene) arrive with their systems;
    /// the settings menu comes in Phase 5.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Realism Settings", fileName = "RealismSettings")]
    public sealed class RealismSettings : ScriptableObject
    {
        [Tooltip("How narrow doneness windows are when scoring (0 = generous, 1 = strict). Used from milestone 6.")]
        [SerializeField, Range(0f, 1f)] private float _cookingPrecision = 0.5f;

        [Tooltip("Multiplies how fast food loses freshness. 0 = never spoils.")]
        [SerializeField, Min(0f)] private float _spoilageMultiplier = 1f;

        [Tooltip("Multiplies how long customers wait before walking out. 2 = twice as patient, 0 = never walk out.")]
        [SerializeField, Min(0f)] private float _customerPatience = 1f;

        // The players' own settings, set at runtime from the settings file. Not serialized, so the
        // asset keeps the designer's defaults.
        [System.NonSerialized] private bool _overridden;
        [System.NonSerialized] private float _playerPrecision;
        [System.NonSerialized] private float _playerPatience;
        [System.NonSerialized] private float _playerSpoilage;

        public float CookingPrecision => _overridden ? _playerPrecision : _cookingPrecision;
        public float CustomerPatience => _overridden ? _playerPatience : _customerPatience;
        public float SpoilageMultiplier => _overridden ? _playerSpoilage : _spoilageMultiplier;

        public float DefaultCookingPrecision => _cookingPrecision;
        public float DefaultCustomerPatience => _customerPatience;
        public float DefaultSpoilageMultiplier => _spoilageMultiplier;

        /// <summary>Uses the players' chosen values from now on (the Settings page).</summary>
        public void UsePlayerValues(float precision, float spoilage, float patience)
        {
            _overridden = true;
            _playerPrecision = precision;
            _playerSpoilage = spoilage;
            _playerPatience = patience;
        }
    }
}
