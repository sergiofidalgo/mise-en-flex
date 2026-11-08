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

        // There's no falling gameplay: anyone this far below the floor they started on has been
        // knocked through it (e.g. by furniture placed on them) and is put back on their feet.
        private const float FellThroughDepth = 1f;

        [SerializeField] private TuningConfig _tuning;

        private CharacterController _controller;
        private InputAction _move;
        private Stations.PlayerInteractor _interactor;
        private PlanarMover _mover;
        private float _verticalSpeed;
        private float _floorHeight;

        // A short shuffle the game makes for the chef, e.g. out of a fridge door's swing.
        private Vector3? _stepTo;
        private Vector3 _stepLookAt;
        private float _stepSpeed;
        private float _stepTimeLeft;

        // A shuffle gives up after this long, e.g. if something it didn't expect is in the way.
        private const float StepTimeout = 0.6f;
        // Stick input beyond this takes over from a shuffle at once: the player is always in charge.
        private const float StepCancelStick = 0.2f;
        // Close enough to the spot to stop.
        private const float StepArrived = 0.01f;
        private Vector3 _spawnPosition;

        public PlanarMover Mover => _mover;

        /// <summary>
        /// Set by the composition root: the free spot on the floor nearest a point, given the
        /// furniture as it stands now, or null if there's none.
        /// </summary>
        public System.Func<System.Numerics.Vector2, System.Numerics.Vector2?> FindFreeSpot { get; set; }

        /// <summary>
        /// Puts the chef straight onto a floor point: a CharacterController can't be moved by
        /// setting its position while enabled, and gravity from before mustn't carry over.
        /// </summary>
        public void Teleport(System.Numerics.Vector2 floorPoint)
        {
            var at = new Vector3(floorPoint.X, _floorHeight, floorPoint.Y);
            if (_controller == null)
            {
                transform.position = at;
                return;
            }
            _controller.enabled = false;
            transform.position = at;
            _controller.enabled = true;
            _verticalSpeed = GroundedDownwardSpeed;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Shuffles the chef to <paramref name="to"/> at <paramref name="speed"/> m/s, turning to keep
        /// facing <paramref name="lookAt"/>, unless that spot isn't free floor. Any stick input
        /// cancels it.
        /// </summary>
        /// <returns>False if there's no free floor there, so the chef stays put.</returns>
        public bool StepAside(Vector3 to, Vector3 lookAt, float speed)
        {
            var spot = new System.Numerics.Vector2(to.x, to.z);
            var free = FindFreeSpot?.Invoke(spot);
            if (free.HasValue && System.Numerics.Vector2.Distance(free.Value, spot) > 0.05f)
                return false;
            _stepTo = new Vector3(to.x, transform.position.y, to.z);
            _stepLookAt = lookAt;
            _stepSpeed = Mathf.Max(speed, 0.1f);
            _stepTimeLeft = StepTimeout;
            return true;
        }

        /// <summary>Moves the chef out from under anything just put where they stand, to the nearest free spot.</summary>
        public void StepClear()
        {
            var here = new System.Numerics.Vector2(transform.position.x, transform.position.z);
            var spot = FindFreeSpot?.Invoke(here);
            if (spot.HasValue && spot.Value != here)
                Teleport(spot.Value);
        }

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
            _floorHeight = transform.position.y;
            _spawnPosition = transform.position;

            // Chefs don't block each other through physics: PushResolver lets them push instead.
            foreach (var other in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            {
                if (other != this && other._controller != null)
                    Physics.IgnoreCollision(_controller, other._controller);
            }
        }

        private void Update()
        {
            if (transform.position.y < _floorHeight - FellThroughDepth)
                Rescue();

            var deltaTime = Time.deltaTime;
            // A station can take over the chef, and the stick with it (washing by hand).
            var stick = _interactor != null && (_interactor.IsBusy || _interactor.IsManaging) ? Vector2.zero : _move.ReadValue<Vector2>();

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
            var step = Step(stick, deltaTime);
            Intent = new System.Numerics.Vector2(velocity.X * deltaTime + step.x, velocity.Y * deltaTime + step.z);
            _controller.Move(new Vector3(velocity.X, _verticalSpeed, velocity.Y) * deltaTime + step);
            transform.rotation = Quaternion.Euler(0f, _mover.FacingDegrees, 0f);
        }

        // This frame's part of a shuffle, if one's going, turning the chef to keep looking where it says.
        private Vector3 Step(Vector2 stick, float deltaTime)
        {
            if (!_stepTo.HasValue)
                return Vector3.zero;
            _stepTimeLeft -= deltaTime;
            var left = _stepTo.Value - transform.position;
            left.y = 0f;
            if (stick.magnitude > StepCancelStick || _stepTimeLeft <= 0f || left.magnitude <= StepArrived)
            {
                _stepTo = null;
                return Vector3.zero;
            }

            var look = _stepLookAt - transform.position;
            if (look.sqrMagnitude > 1e-4f)
                _mover.TurnTowards(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, deltaTime);
            return Vector3.ClampMagnitude(left, _stepSpeed * deltaTime);
        }

        // Back to the nearest free floor above where they fell, or to where they joined.
        private void Rescue()
        {
            var here = new System.Numerics.Vector2(transform.position.x, transform.position.z);
            var spot = FindFreeSpot?.Invoke(here) ?? new System.Numerics.Vector2(_spawnPosition.x, _spawnPosition.z);
            Teleport(spot);
            Debug.LogWarning($"{name} fell through the floor and was put back at {spot}.", this);
        }
    }
}
