using System;
using System.Collections.Generic;
using Game.Simulation.Interaction;
using Game.Simulation.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A player's hands: picks the station in front of them each frame and, on Interact,
    /// picks up or puts down; on Use, uses the station. Dependencies arrive through
    /// <see cref="Bind"/> from the scene's composition root when the player joins.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const string InteractActionName = "Interact";
        private const string UseActionName = "Use";

        [Tooltip("Where carried items sit, in front of the chef's chest.")]
        [SerializeField] private Transform _carryAnchor;

        private readonly ItemSlot _hands = new ItemSlot();
        private readonly List<SimVector2> _candidatePositions = new List<SimVector2>();

        private PlayerInput _playerInput;
        private InputAction _interact;
        private InputAction _use;
        private IReadOnlyList<Interactable> _interactables;
        private KitchenServices _services;
        private Interactable _target;

        public int PlayerIndex => _playerInput.playerIndex;

        public ItemSlot Hands => _hands;

        public Interactable Target => _target;

        /// <summary>Raised after every Interact press that had a target.</summary>
        public event Action<InteractionOutcome> Interacted;

        public void Bind(IReadOnlyList<Interactable> interactables, KitchenServices services)
        {
            _interactables = interactables;
            _services = services;
        }

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        // Start, not Awake: PlayerInput has set up this player's own copy of the actions by now.
        private void Start()
        {
            _interact = _playerInput.actions[InteractActionName];
            _use = _playerInput.actions[UseActionName];
            _interact.performed += HandleInteract;
            _use.performed += HandleUse;
        }

        private void OnDestroy()
        {
            if (_interact != null)
                _interact.performed -= HandleInteract;
            if (_use != null)
                _use.performed -= HandleUse;
            SetTarget(null);
        }

        private void Update()
        {
            if (_interactables == null)
                return;

            SetTarget(FindTarget());
        }

        private Interactable FindTarget()
        {
            _candidatePositions.Clear();
            var currentIndex = -1;
            for (var i = 0; i < _interactables.Count; i++)
            {
                var position = _interactables[i].transform.position;
                _candidatePositions.Add(new SimVector2(position.x, position.z));
                if (_interactables[i] == _target)
                    currentIndex = i;
            }

            var origin = new SimVector2(transform.position.x, transform.position.z);
            var best = InteractionTargeting.SelectBest(
                origin, transform.eulerAngles.y, _candidatePositions, _services.Tuning.Targeting, currentIndex);
            return best >= 0 ? _interactables[best] : null;
        }

        private void SetTarget(Interactable next)
        {
            if (next == _target)
                return;

            if (_target != null)
                _target.RemoveTargeter(this);
            _target = next;
            if (_target != null)
                _target.AddTargeter(this);
        }

        private void HandleInteract(InputAction.CallbackContext context)
        {
            if (_target == null || _services == null || _target.Container == null)
                return;

            var held = _hands.Current;
            var outcome = ItemTransfer.Interact(_hands, _target.Container);
            var move = _services.Tuning.ItemMove;
            switch (outcome)
            {
                case InteractionOutcome.PickedUp:
                    // A crate's item doesn't have a view yet; it appears at the crate and flies to the hands.
                    _services.ItemViews.GetOrCreate(_hands.Current, _target.ItemAnchor.position)
                        .MoveTo(_carryAnchor, move);
                    break;
                case InteractionOutcome.Placed:
                    _services.ItemViews.GetOrCreate(held, _carryAnchor.position)
                        .MoveTo(_target.AnchorFor(held), move);
                    break;
                default:
                    _target.NotifyDenied();
                    break;
            }

            Interacted?.Invoke(outcome);
        }

        private void HandleUse(InputAction.CallbackContext context)
        {
            if (_target == null)
                return;

            if (!(_target is IUsable usable) || !usable.TryUse(this))
                _target.NotifyDenied();
        }
    }
}
