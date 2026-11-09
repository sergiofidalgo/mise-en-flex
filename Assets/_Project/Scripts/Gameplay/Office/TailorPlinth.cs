using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// One of the tailor's step-ladders (the user's notes, 2026-11-09): any player at its foot
    /// presses A and hops up onto the top step, where they dress (their own slot's chef). The chef
    /// stops and faces the camera; the stick browses (up and down pick what to change, left and
    /// right the item); every change to what's worn saves at once, as everything done in the office
    /// does. B hops back down. Locked items show while browsing but aren't kept. The tailor walks
    /// over to whoever has just climbed up (see TailorAttendant).
    /// </summary>
    public sealed class TailorPlinth : MonoBehaviour
    {
        private const string InteractAction = "Interact";
        private const string BackAction = "ButtonB";
        private const string MoveAction = "Move";

        [Tooltip("Where a chef stands to climb up: in front of the ladder, on the floor (local).")]
        [SerializeField] private Vector3 _foot = new Vector3(0f, 0f, -0.75f);
        [Tooltip("Where a chef stands on the top step (local).")]
        [SerializeField] private Vector3 _top = new Vector3(0f, 0.45f, 0.05f);
        [Tooltip("How close to the foot a chef must be to climb (m).")]
        [SerializeField, Min(0.1f)] private float _radius = 0.6f;
        [Tooltip("How long the hop up or down takes (s), and how high it arcs (m).")]
        [SerializeField, Min(0.05f)] private float _hopTime = 0.3f;
        [SerializeField, Min(0f)] private float _hopHeight = 0.25f;
        [Tooltip("How far the stick must lean to step, from 0 to 1.")]
        [SerializeField, Range(0.1f, 0.95f)] private float _threshold = 0.55f;
        [Tooltip("Holding the stick steps again after this long (s), then every repeat (s).")]
        [SerializeField, Min(0.05f)] private float _repeatDelay = 0.38f;
        [SerializeField, Min(0.05f)] private float _repeatEvery = 0.16f;
        [Tooltip("The way the chef faces while dressing (degrees): 180 faces the camera.")]
        [SerializeField] private float _faceCamera = 180f;

        private PlayerInput _dresser;
        private Vector2Int _held;
        private float _heldFor;
        private float _nextRepeat;
        private Vector3 _hopFrom;
        private Vector3 _hopTo;
        private float _hopAge = -1f;

        /// <summary>A player at the foot of the ladder who could climb it, or null.</summary>
        public PlayerInput Standing { get; private set; }

        /// <summary>The dressing under way, or null.</summary>
        public TailorSession Session { get; private set; }

        /// <summary>The player dressing up here, or null.</summary>
        public PlayerInput Dresser => Session != null ? _dresser : null;

        /// <summary>The player slot being dressed (their chef and colour), or -1.</summary>
        public int DresserSlot => Dresser != null ? Dresser.playerIndex : -1;

        /// <summary>When the current dresser climbed up, so the tailor goes to the newest.</summary>
        public float ClimbedAt { get; private set; }

        /// <summary>The top step in the world, where the dresser stands.</summary>
        public Vector3 TopPoint => transform.TransformPoint(_top);

        public Vector3 FootPoint => transform.TransformPoint(_foot);

        private void Update()
        {
            if (Session == null)
            {
                if (_hopAge >= 0f)
                {
                    Standing = null; // someone is still hopping down
                    return;
                }
                Standing = FindStanding();
                if (Standing != null && Pressed(Standing, InteractAction))
                    Open(Standing);
                return;
            }
            Standing = null;
            if (_dresser == null)
            {
                Session = null;
                return;
            }
            if (_hopAge >= 0f)
                return; // let the hop up finish before browsing
            if (Pressed(_dresser, BackAction))
            {
                Close();
                return;
            }
            Browse(_dresser.actions[MoveAction].ReadValue<Vector2>());
        }

        // After the chef's own movement: carry them through the hop.
        private void LateUpdate()
        {
            if (_hopAge < 0f || _dresser == null)
                return;
            _hopAge += Time.deltaTime;
            var t = Mathf.Clamp01(_hopAge / _hopTime);
            var eased = t * t * (3f - 2f * t);
            _dresser.transform.position = Vector3.Lerp(_hopFrom, _hopTo, eased) + Vector3.up * (Mathf.Sin(Mathf.PI * t) * _hopHeight);
            Physics.SyncTransforms();
            if (t >= 1f)
            {
                _hopAge = -1f;
                if (Session == null)
                    Release();
            }
        }

        private void OnDisable()
        {
            if (Session != null)
                Close();
        }

        private void Open(PlayerInput player)
        {
            _dresser = player;
            var slot = player.playerIndex;
            Session = new TailorSession(ChefProfileFile.Current.Look(slot), ChefProfileFile.Current.Unlocked);
            ClimbedAt = Time.time;
            Lock(true);
            _held = Vector2Int.zero;
            Hop(TopPoint);
        }

        private void Close()
        {
            Session.Leave();
            Session = null;
            if (_dresser != null)
                Hop(FootPoint); // released once down
        }

        private void Hop(Vector3 to)
        {
            _hopFrom = _dresser.transform.position;
            _hopTo = to;
            _hopAge = 0f;
        }

        private void Release()
        {
            Lock(false);
            _dresser = null;
        }

        // The chef stays put and faces the camera while dressing.
        private void Lock(bool dressing)
        {
            if (_dresser == null)
                return;
            if (_dresser.TryGetComponent<PlayerInteractor>(out var hands))
                hands.IsBusy = dressing;
            if (_dresser.TryGetComponent<PlayerMovement>(out var movement))
                movement.FacingOverride = dressing ? _faceCamera : (float?)null;
        }

        // One step per push, then repeating while held, like a menu.
        private void Browse(Vector2 stick)
        {
            var direction = Mathf.Abs(stick.y) >= Mathf.Abs(stick.x)
                ? new Vector2Int(0, Mathf.Abs(stick.y) >= _threshold ? (int)Mathf.Sign(stick.y) : 0)
                : new Vector2Int(Mathf.Abs(stick.x) >= _threshold ? (int)Mathf.Sign(stick.x) : 0, 0);
            if (direction == Vector2Int.zero)
            {
                _held = Vector2Int.zero;
                return;
            }
            if (direction != _held)
            {
                _held = direction;
                _heldFor = 0f;
                _nextRepeat = _repeatDelay;
                Step(direction);
                return;
            }
            _heldFor += Time.deltaTime;
            if (_heldFor >= _nextRepeat)
            {
                _nextRepeat += _repeatEvery;
                Step(direction);
            }
        }

        private void Step(Vector2Int direction)
        {
            if (direction.y != 0)
                Session.StepModule(-direction.y); // up is the one above on the card
            else if (Session.StepItem(direction.x))
                ChefProfileFile.SetLook(_dresser.playerIndex, Session.Worn);
        }

        // The nearest player at the ladder's foot who isn't already dressing on another ladder.
        private PlayerInput FindStanding()
        {
            PlayerInput nearest = null;
            var best = _radius;
            var foot = FootPoint;
            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent<PlayerInteractor>(out var hands) && hands.IsBusy)
                    continue;
                var offset = player.transform.position - foot;
                offset.y = 0f;
                if (offset.magnitude <= best)
                {
                    best = offset.magnitude;
                    nearest = player;
                }
            }
            return nearest;
        }

        private static bool Pressed(PlayerInput player, string action) =>
            player.actions[action].WasPressedThisFrame();
    }
}
