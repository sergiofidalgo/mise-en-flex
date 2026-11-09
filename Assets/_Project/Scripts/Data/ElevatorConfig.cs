using Game.Simulation.Office;
using UnityEngine;

namespace Game.Data
{
    /// <summary>The office elevator's timings (decision 2026-11-09, "The boss's floor").</summary>
    [CreateAssetMenu(menuName = "Game/Elevator Config", fileName = "ElevatorConfig")]
    public sealed class ElevatorConfig : ScriptableObject
    {
        [Tooltip("Seconds from the bell to the doors starting to part. Short, so a join answers at once; long enough that the ding leads.")]
        [SerializeField, Min(0f)] private float _ringToOpen = 0.35f;
        [Tooltip("Seconds for the doors to open fully.")]
        [SerializeField, Min(0.01f)] private float _opening = 0.6f;
        [Tooltip("Seconds the doors stay open once the car and doorway are clear.")]
        [SerializeField, Min(0f)] private float _holdOpen = 1.5f;
        [Tooltip("Seconds for the doors to close.")]
        [SerializeField, Min(0.01f)] private float _closing = 0.8f;
        [Tooltip("Metres each door panel slides when fully open.")]
        [SerializeField, Min(0f)] private float _doorTravel = 1.3f;

        public float DoorTravel => _doorTravel;

        public ElevatorSettings ToSettings() => new ElevatorSettings(_ringToOpen, _opening, _holdOpen, _closing);
    }
}
