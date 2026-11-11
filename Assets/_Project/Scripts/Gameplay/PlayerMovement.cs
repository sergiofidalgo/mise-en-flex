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
        private Vector3 _spawnPosition;
        private Quaternion _writtenRotation;

        public PlanarMover Mover => _mover;

        /// <summary>
        /// A way to face regardless of the stick (degrees), turned to smoothly: the tailor turns a
        /// dressing chef to the camera. Null: face where they walk.
        /// </summary>
        public float? FacingOverride { get; set; }

        // How fast a chef turns to an override (degrees a second).
        private const float OverrideTurnSpeed = 540f;

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
            _writtenRotation = transform.rotation;

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
            // A station can take over the chef, and the stick with it (washing, cleaning).
            var taken = _interactor != null && (_interactor.IsBusy || _interactor.IsManaging || _interactor.IsReviewing);
            var stick = taken ? Vector2.zero : _move.ReadValue<Vector2>();
            // Holding X with nothing to clean yet (decision 2026-11-11): planted, the stick turns the
            // chef on the spot, so they can swing round to the dirt or the sink without stepping off.
            var planted = _interactor != null && _interactor.IsPlanted;

            // Read tuning every frame so Inspector edits apply live while playing.
            _mover.Settings = _tuning.Movement;

            // Something else turned the chef since last frame (a station facing them to their work,
            // a test standing them somewhere): keep that facing rather than snapping back.
            if (!FacingOverride.HasValue && Quaternion.Angle(transform.rotation, _writtenRotation) > 0.5f)
                _mover.Face(transform.eulerAngles.y);

            // The kitchen camera is fixed and faces world +Z (yaw 0), so stick up maps
            // straight to forward. If the camera ever rotates, rotate the stick by its yaw here.
            _mover.Tick(deltaTime, new System.Numerics.Vector2(stick.x, stick.y), planted);

            // Stopped near a station roughly in front: settle round to face it (decision 2026-11-11).
            if (!taken && _interactor != null && _interactor.Target != null)
            {
                var here = transform.position;
                var station = _interactor.Target.transform.position;
                _mover.Assist(deltaTime, new System.Numerics.Vector2(here.x, here.z), new System.Numerics.Vector2(station.x, station.z));
            }

            // Simple gravity keeps the controller grounded; there's no jumping or falling gameplay.
            _verticalSpeed = _controller.isGrounded
                ? GroundedDownwardSpeed
                : _verticalSpeed + Physics.gravity.y * deltaTime;

            var velocity = _mover.Velocity;
            Intent = new System.Numerics.Vector2(velocity.X, velocity.Y) * deltaTime;
            _controller.Move(new Vector3(velocity.X, _verticalSpeed, velocity.Y) * deltaTime);
            transform.rotation = FacingOverride.HasValue
                ? Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, FacingOverride.Value, 0f), OverrideTurnSpeed * deltaTime)
                : Quaternion.Euler(0f, _mover.FacingDegrees, 0f);
            _writtenRotation = transform.rotation;
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
