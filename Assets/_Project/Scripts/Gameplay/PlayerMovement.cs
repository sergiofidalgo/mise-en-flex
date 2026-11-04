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
    public sealed class PlayerMovement : MonoBehaviour, IPushable
    {
        private const string MoveActionName = "Move";

        // A small constant downward speed while grounded keeps isGrounded stable on flat floors.
        private const float GroundedDownwardSpeed = -1f;

        [SerializeField] private TuningConfig _tuning;

        private CharacterController _controller;
        private InputAction _move;
        private Stations.PlayerInteractor _interactor;
        private PlanarMover _mover;
        private float _verticalSpeed;

        public PlanarMover Mover => _mover;

        public bool IsReady => _controller != null;

        public Vector3 Position => transform.position;

        public System.Numerics.Vector2 Intent { get; private set; }

        public float BodyRadius => _controller.radius;

        public System.Numerics.Vector2 Shove(System.Numerics.Vector2 shift)
        {
            var before = transform.position;
            _controller.Move(new Vector3(shift.X, 0f, shift.Y));
            var moved = transform.position - before;
            return new System.Numerics.Vector2(moved.x, moved.z);
        }

        // Start, not Awake: by now PlayerInput has set up this player's own copy of the
        // actions, and the join controller has placed us on our spawn point.
        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _move = GetComponent<PlayerInput>().actions[MoveActionName];
            _interactor = GetComponent<Stations.PlayerInteractor>();
            _mover = new PlanarMover(_tuning.Movement, transform.eulerAngles.y);

            // Chefs don't block each other through physics: PushResolver lets them push instead.
            foreach (var other in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (other != this && other._controller != null)
                    Physics.IgnoreCollision(_controller, other._controller);
            }
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            // A station can take over the chef, and the stick with it (washing by hand).
            var stick = _interactor != null && _interactor.IsBusy ? Vector2.zero : _move.ReadValue<Vector2>();

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
            Intent = new System.Numerics.Vector2(velocity.X, velocity.Y) * deltaTime;
            _controller.Move(new Vector3(velocity.X, _verticalSpeed, velocity.Y) * deltaTime);
            transform.rotation = Quaternion.Euler(0f, _mover.FacingDegrees, 0f);
        }
    }
}
