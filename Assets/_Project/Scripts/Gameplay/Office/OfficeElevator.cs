using Game.Data;
using Game.Simulation.Office;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// The office's one big elevator (decision 2026-11-09): players join into its car, and each
    /// join rings the bell and opens the doors (once the player has named themselves, 2026-11-13) (<see cref="ElevatorDoors"/>). Where a joining chef
    /// stands is <see cref="PlayerJoinController"/>'s job, with spawn points inside the car; this
    /// slides the door panels, whose colliders keep a chef in until they part.
    /// </summary>
    public sealed class OfficeElevator : MonoBehaviour
    {
        [SerializeField] private PlayerInputManager _players;
        [SerializeField] private ElevatorConfig _config;
        [Tooltip("The door panels; each slides along its own local X, away from the other.")]
        [SerializeField] private Transform _leftDoor;
        [SerializeField] private Transform _rightDoor;
        [Tooltip("The car and its doorway: a chef inside holds the doors open.")]
        [SerializeField] private BoxCollider _car;
        [Tooltip("New players name themselves in the closed car; the bell rings and the doors open when they're done (2026-11-13).")]
        [SerializeField] private JoinNaming _naming;

        private Vector3 _leftClosed;
        private Vector3 _rightClosed;

        public ElevatorDoors Doors { get; private set; }

        private void Awake()
        {
            Doors = new ElevatorDoors(_config.ToSettings());
            _leftClosed = _leftDoor.localPosition;
            _rightClosed = _rightDoor.localPosition;
        }

        // With naming, the car arrives when a player is ready: at once for one carried back from a
        // kitchen, on Done for a new one, who waits in the closed car meanwhile.
        private void OnEnable()
        {
            if (_naming != null)
                _naming.Arrived += HandleJoined;
            else
                _players.onPlayerJoined += HandleJoined;
        }

        private void OnDisable()
        {
            if (_naming != null)
                _naming.Arrived -= HandleJoined;
            else
                _players.onPlayerJoined -= HandleJoined;
        }

        private void HandleJoined(PlayerInput player) => Doors.Arrive();

        private void Update()
        {
            Doors.Occupied = AnyoneInCar();
            Doors.Tick(Time.deltaTime);

            // Eased both ways, so the panels start and stop softly like a real lift's.
            var slide = Mathf.SmoothStep(0f, 1f, Doors.Openness) * _config.DoorTravel;
            _leftDoor.localPosition = _leftClosed + Vector3.left * slide;
            _rightDoor.localPosition = _rightClosed + Vector3.right * slide;
        }

        private bool AnyoneInCar()
        {
            var bounds = _car.bounds;
            foreach (var player in PlayerInput.all)
            {
                var feet = player.transform.position;
                // Feet are on the floor; test half a metre up, inside the box's height.
                if (bounds.Contains(feet + Vector3.up * 0.5f))
                    return true;
            }
            return false;
        }
    }
}
