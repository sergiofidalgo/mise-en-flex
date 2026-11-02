using Game.Simulation.Movement;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Game-feel values designers tune. Gameplay reads these every frame, so edits
    /// in the Inspector apply immediately during Play mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Tuning Config", fileName = "TuningConfig")]
    public sealed class TuningConfig : ScriptableObject
    {
        [Header("Player movement")]
        [Tooltip("Metres per second at full stick. The greybox kitchen is 10 m wide.")]
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;

        [Tooltip("Seconds from standstill to full speed. Decision 2026-11-02: about 0.08 (snappy).")]
        [SerializeField, Min(0f)] private float _accelerationTime = 0.08f;

        [Tooltip("Seconds from full speed to a stop after letting go of the stick.")]
        [SerializeField, Min(0f)] private float _decelerationTime = 0.06f;

        [Tooltip("Degrees per second the chef turns to face the stick. 1080 = a half turn in ~0.17 s.")]
        [SerializeField, Min(0f)] private float _turnSpeedDegrees = 1080f;

        public MovementSettings Movement =>
            new MovementSettings(_moveSpeed, _accelerationTime, _decelerationTime, _turnSpeedDegrees);
    }
}
