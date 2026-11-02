using Game.Data;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay
{
    /// <summary>
    /// Drives a player's <see cref="CharacterController"/> from the Move action using
    /// the simulation's <see cref="PlanarMover"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const string MoveActionName = "Move";

        // A small constant downward speed while grounded keeps isGrounded stable on flat floors.
        private const float GroundedDownwardSpeed = -1f;

        [SerializeField] private TuningConfig _tuning;

        private CharacterController _controller;
        private InputAction _move;
        private PlanarMover _mover;
        private float _verticalSpeed;

        public PlanarMover Mover => _mover;

        // Start, not Awake: by now PlayerInput has set up this player's own copy of the
        // actions, and the join controller has placed us on our spawn point.
        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _move = GetComponent<PlayerInput>().actions[MoveActionName];
            _mover = new PlanarMover(_tuning.Movement, transform.eulerAngles.y);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            var stick = _move.ReadValue<Vector2>();

            // Read tuning every frame so Inspector edits apply live while playing.
            _mover.Settings = _tuning.Movement;

            // The kitchen camera is fixed and faces world +Z (yaw 0), so stick up maps
            // straight to forward. If the camera ever rotates, rotate the stick by its yaw here.
            _mover.Tick(deltaTime, new System.Numerics.Vector2(stick.x, stick.y));

            // Simple gravity keeps the controller grounded; there's no jumping or falling gameplay.
            _verticalSpeed = _controller.isGrounded
                ? GroundedDownwardSpeed
                : _verticalSpeed + Physics.gravity.y * deltaTime;

            var velocity = _mover.Velocity;
            _controller.Move(new Vector3(velocity.X, _verticalSpeed, velocity.Y) * deltaTime);
            transform.rotation = Quaternion.Euler(0f, _mover.FacingDegrees, 0f);
        }
    }
}
