using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Characters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// A tailor's plinth in its player's colour (decision 2026-11-09): that player steps on and
    /// presses A to dress. The chef stops and turns to the camera; the stick browses (up and down
    /// pick what to change, left and right pick the item), and every change to what's worn saves
    /// at once, as everything done in the office does. B steps off. Locked items show on the chef
    /// while browsing but aren't kept.
    /// </summary>
    public sealed class TailorPlinth : MonoBehaviour
    {
        private const string InteractAction = "Interact";
        private const string BackAction = "ButtonB";
        private const string MoveAction = "Move";

        [Tooltip("The player slot this plinth dresses (its colour): 0 is Player 1.")]
        [SerializeField, Min(0)] private int _slot;
        [Tooltip("How close to the plinth's middle a chef must stand (m).")]
        [SerializeField, Min(0.1f)] private float _radius = 0.7f;
        [Tooltip("How far the stick must lean to step, from 0 to 1.")]
        [SerializeField, Range(0.1f, 0.95f)] private float _threshold = 0.55f;
        [Tooltip("Holding the stick steps again after this long (s), then every repeat (s).")]
        [SerializeField, Min(0.05f)] private float _repeatDelay = 0.38f;
        [SerializeField, Min(0.05f)] private float _repeatEvery = 0.16f;
        [Tooltip("The way the chef faces while dressing (degrees): 180 faces the camera.")]
        [SerializeField] private float _faceCamera = 180f;

        private PlayerInput _player;
        private Vector2Int _held;
        private float _heldFor;
        private float _nextRepeat;

        public int Slot => _slot;

        /// <summary>The player of this plinth's slot standing on it, or null.</summary>
        public PlayerInput Standing { get; private set; }

        /// <summary>The dressing under way, or null.</summary>
        public TailorSession Session { get; private set; }

        public PlayerInput Dresser => Session != null ? _player : null;

        private void Update()
        {
            _player = FindPlayer();
            Standing = _player != null && IsOn(_player.transform.position) ? _player : null;

            if (Session == null)
            {
                if (Standing != null && Pressed(Standing, InteractAction))
                    Open();
                return;
            }
            if (_player == null)
            {
                Session = null;
                return;
            }
            if (Pressed(_player, BackAction))
            {
                Close();
                return;
            }
            Browse(_player.actions[MoveAction].ReadValue<Vector2>());
        }

        private void OnDisable()
        {
            if (Session != null)
                Close();
        }

        private void Open()
        {
            Session = new TailorSession(ChefProfileFile.Current.Look(_slot), ChefProfileFile.Current.Unlocked);
            Lock(true);
            _held = Vector2Int.zero;
        }

        private void Close()
        {
            Session.Leave();
            Session = null;
            Lock(false);
        }

        // The chef stays put and faces the camera while dressing.
        private void Lock(bool dressing)
        {
            if (_player == null)
                return;
            if (_player.TryGetComponent<PlayerInteractor>(out var hands))
                hands.IsBusy = dressing;
            if (_player.TryGetComponent<PlayerMovement>(out var movement))
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
                ChefProfileFile.SetLook(_slot, Session.Worn);
        }

        private PlayerInput FindPlayer()
        {
            foreach (var player in PlayerInput.all)
                if (player.playerIndex == _slot)
                    return player;
            return null;
        }

        private bool IsOn(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude <= _radius;
        }

        private static bool Pressed(PlayerInput player, string action) =>
            player.actions[action].WasPressedThisFrame();
    }
}
