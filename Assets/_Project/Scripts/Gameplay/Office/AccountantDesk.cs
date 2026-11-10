using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Debt;
using Game.Gameplay.Saving;
using Game.Gameplay.Stations;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// The accountant's desk, where the campaign borrows (the user's call, 2026-11-10: the
    /// accountant, the room by the elevator, rather than the boss's desk). A player on the visitors'
    /// side presses A and sits down to the books: left and right pick an amount (steps of 250), A
    /// borrows it and saves at once, B gets up. It isn't refused for being unwise (players learn by
    /// playing); only the terms cap it, at 2,000 borrowed.
    /// </summary>
    public sealed class AccountantDesk : MonoBehaviour
    {
        private const string InteractAction = "Interact";
        private const string BackAction = "ButtonB";
        private const string MoveAction = "Move";

        [Tooltip("The office's one copy of the campaign, shared with the miniature, which saves it.")]
        [SerializeField] private OfficeCampaign _office;
        [Tooltip("Where the borrower stands, in front of the desk (local), and how near counts (m).")]
        [SerializeField] private Vector3 _visitorSide = new Vector3(0f, 0f, 1.5f);
        [SerializeField, Min(0.2f)] private float _radius = 1.3f;
        [Tooltip("How far the stick must lean to step, from 0 to 1.")]
        [SerializeField, Range(0.1f, 0.95f)] private float _threshold = 0.4f;
        [Tooltip("Seconds before a held stick steps again.")]
        [SerializeField, Min(0.05f)] private float _repeatDelay = 0.3f;

        private readonly LoanSettings _terms = new LoanSettings();
        private PlayerInput _borrower;
        private int _heldDirection;
        private float _nextRepeat;

        /// <summary>A player close enough to sit down, or null.</summary>
        public PlayerInput Standing { get; private set; }

        /// <summary>The player at the books, or null.</summary>
        public PlayerInput Borrower => _borrower;

        /// <summary>The campaign as it stands while the books are open; null when closed.</summary>
        public CampaignSave Campaign { get; private set; }

        /// <summary>What can be borrowed now, smallest first.</summary>
        public IReadOnlyList<float> Amounts { get; private set; } = new float[0];

        /// <summary>The amount picked, as an index into <see cref="Amounts"/>.</summary>
        public int Selected { get; private set; }

        public LoanSettings Terms => _terms;

        /// <summary>Raised after a loan, with the amount: for feedback.</summary>
        public event System.Action<float> Borrowed;

        /// <summary>Raised when the pick moves, or a press does nothing (+1, -1, 0).</summary>
        public event System.Action<int> Stepped;

        public Vector3 VisitorPoint => transform.TransformPoint(_visitorSide);

        private void Update()
        {
            if (_borrower == null)
            {
                Standing = CampaignLaunch.Decided ? FindStanding() : null;
                if (Standing != null && Pressed(Standing, InteractAction))
                    Open(Standing);
                return;
            }
            Standing = null;
            if (Pressed(_borrower, BackAction))
            {
                Close();
                return;
            }
            if (Pressed(_borrower, InteractAction))
            {
                Borrow();
                return;
            }
            Browse(_borrower.actions[MoveAction].ReadValue<Vector2>().x + DPad(_borrower));
        }

        private void OnDisable()
        {
            if (_borrower != null)
                Close();
        }

        private void Open(PlayerInput player)
        {
            Campaign = _office != null ? _office.Campaign : null;
            if (Campaign == null)
                return; // no office campaign, or a file this game can't read: left alone
            _borrower = player;
            Lock(true);
            Refresh();
            // Start on the smallest: a big loan should be a choice.
            Selected = 0;
        }

        private void Close()
        {
            Lock(false);
            _borrower = null;
            Campaign = null;
        }

        private void Borrow()
        {
            if (Amounts.Count == 0 || !_office.Lend(Amounts[Selected], _terms))
            {
                Stepped?.Invoke(0);
                return;
            }
            var amount = Amounts[Selected];
            Refresh();
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, Amounts.Count - 1));
            Borrowed?.Invoke(amount);
        }

        private void Refresh() => Amounts = Campaign.ToLoan().AmountsOnOffer(_terms);

        private void Browse(float x)
        {
            var direction = Mathf.Abs(x) >= _threshold ? (int)Mathf.Sign(x) : 0;
            if (direction == 0)
            {
                _heldDirection = 0;
                return;
            }
            if (direction == _heldDirection && Time.time < _nextRepeat)
                return;
            _heldDirection = direction;
            _nextRepeat = Time.time + _repeatDelay;
            var next = Mathf.Clamp(Selected + direction, 0, Mathf.Max(0, Amounts.Count - 1));
            Stepped?.Invoke(next != Selected ? direction : 0);
            Selected = next;
        }

        // The borrower stays put and faces the desk.
        private void Lock(bool sitting)
        {
            if (_borrower == null)
                return;
            if (_borrower.TryGetComponent<PlayerInteractor>(out var hands))
                hands.IsBusy = sitting;
            if (_borrower.TryGetComponent<PlayerMovement>(out var movement))
            {
                var towardDesk = transform.position - _borrower.transform.position;
                movement.FacingOverride = sitting ? Mathf.Atan2(towardDesk.x, towardDesk.z) * Mathf.Rad2Deg : (float?)null;
            }
        }

        private PlayerInput FindStanding()
        {
            PlayerInput nearest = null;
            var best = _radius;
            var spot = VisitorPoint;
            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent<PlayerInteractor>(out var hands) && hands.IsBusy)
                    continue;
                var offset = player.transform.position - spot;
                offset.y = 0f;
                if (offset.magnitude <= best)
                {
                    best = offset.magnitude;
                    nearest = player;
                }
            }
            return nearest;
        }

        private static float DPad(PlayerInput player)
        {
            foreach (var device in player.devices)
                if (device is Gamepad pad)
                    return pad.dpad.x.ReadValue();
            return 0f;
        }

        private static bool Pressed(PlayerInput player, string action) =>
            player.actions[action].WasPressedThisFrame();
    }
}
