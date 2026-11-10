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
    /// does. LB and RB change what's being changed (hat, face, skin, accessory, outfit); the stick
    /// or the D-pad, left and right, picks the item, at once on a press and quickly repeating while
    /// held (the user found the first version sluggish). B hops back down. Locked items show while browsing but aren't kept. The tailor walks
    /// over to whoever has just climbed up (see TailorAttendant).
    /// </summary>
    public sealed class TailorPlinth : MonoBehaviour
    {
        /// <summary>Raised after every step: +1/-1 for the item, 0 when the section changed. For feedback.</summary>
        public event System.Action<int> Stepped;

        private const string InteractAction = "Interact";
        private const string BackAction = "ButtonB";
        private const string NavigateAction = "Navigate"; // the D-pad or arrows, not the stick (2026-11-10)
        // LB and RB (keyboard Z and C) change section, as tabs do on the Review dashboard.
        private const string PreviousSectionAction = "RotateLeft";
        private const string NextSectionAction = "RotateRight";

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
        [SerializeField, Range(0.1f, 0.95f)] private float _threshold = 0.4f;
        [Tooltip("Holding steps again after this long (s), then every repeat (s), speeding up to the fastest after a second.")]
        [SerializeField, Min(0.02f)] private float _repeatDelay = 0.24f;
        [SerializeField, Min(0.02f)] private float _repeatEvery = 0.1f;
        [SerializeField, Min(0.02f)] private float _repeatFastest = 0.05f;
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
            if (Pressed(_dresser, PreviousSectionAction) || Pressed(_dresser, NextSectionAction))
            {
                Session.StepModule(Pressed(_dresser, NextSectionAction) ? 1 : -1);
                _held = Vector2Int.zero;
                Stepped?.Invoke(0);
                return;
            }
            Browse(_dresser.actions[NavigateAction].ReadValue<Vector2>().x);
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

        // One step per push, then repeating while held, faster the longer it's held, like a menu.
        private void Browse(float x)
        {
            var direction = new Vector2Int(Mathf.Abs(x) >= _threshold ? (int)Mathf.Sign(x) : 0, 0);
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
                Step(direction.x);
                return;
            }
            _heldFor += Time.deltaTime;
            if (_heldFor >= _nextRepeat)
            {
                var speedUp = Mathf.Clamp01((_heldFor - _repeatDelay) / 1f);
                _nextRepeat += Mathf.Lerp(_repeatEvery, _repeatFastest, speedUp);
                Step(direction.x);
            }
        }

        private void Step(int direction)
        {
            if (Session.StepItem(direction))
                ChefProfileFile.SetLook(_dresser.playerIndex, Session.Worn);
            Stepped?.Invoke(direction);
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
