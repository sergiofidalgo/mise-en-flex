using Game.Data;
using System;
using System.Collections.Generic;
using Game.Simulation.Interaction;
using Game.Simulation.Days;
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
        private const string HeatDownActionName = "HeatDown";
        private const string HeatUpActionName = "HeatUp";
        private const string ButtonBActionName = "ButtonB";
        private const string ButtonYActionName = "ButtonY";
        private const string MoveActionName = "Move";

        [Tooltip("Where carried items sit, in front of the chef's chest.")]
        [SerializeField] private Transform _carryAnchor;

        [Tooltip("Where a held tool (the knife) sits, at the chef's side.")]
        [SerializeField] private Transform _toolAnchor;

        private readonly ItemSlot _hands = new ItemSlot();
        private readonly ItemSlot _tool = new ItemSlot();
        private readonly List<SimVector2> _candidatePositions = new List<SimVector2>();

        private PlayerInput _playerInput;
        private InputAction _interact;
        private InputAction _use;
        private InputAction _heatDown;
        private InputAction _heatUp;
        private InputAction _buttonB;
        private InputAction _buttonY;
        private IReadOnlyList<Interactable> _interactables;
        private KitchenServices _services;
        private Interactable _target;
        private Game.Gameplay.Cleaning.ChefCleaning _cleaning;

        public int PlayerIndex => _playerInput.playerIndex;

        public ItemSlot Hands => _hands;

        /// <summary>The tool held alongside the hands (the knife), or null.</summary>
        public Item Tool => _tool.Current;

        // How far RT must be pulled to open a slot menu.
        private const float MenuTriggerThreshold = 0.5f;

        /// <summary>True while RT (keyboard E) is held facing a station with a slot menu.</summary>
        public bool IsSlotMenuOpen =>
            _target is ISlotMenuStation && _heatUp != null && _heatUp.ReadValue<float>() >= MenuTriggerThreshold;

        /// <summary>
        /// True while RT (keyboard E) is held with a tray in hand: the face buttons choose what comes
        /// off it. Not at storage (RT there is its own menu) or a hob (RT turns up the heat).
        /// </summary>
        public bool IsTrayMenuOpen =>
            _hands.Current is Tray && CanOpenTrayMenu && _heatUp != null && _heatUp.ReadValue<float>() >= MenuTriggerThreshold;

        /// <summary>True when holding a tray here could open its menu, for the prompt.</summary>
        public bool CanOpenTrayMenu =>
            _hands.Current is Tray tray && !tray.IsEmpty && _target != null && !(_target is ISlotMenuStation) && !(_target is IHeatControl);

        public Interactable Target => _target;

        /// <summary>Cleaning and putting out fires with X (Phase 5). Null until bound.</summary>
        public Game.Gameplay.Cleaning.ChefCleaning Cleaning => _cleaning;

        /// <summary>Chefs work in Prep and Service; Plan and Review happen between shifts.</summary>
        /// <summary>
        /// Set by a station that takes over the chef, like washing by hand: they stay put, the stick
        /// goes to the station, and only Use works (to stop), or B (to back out).
        /// </summary>
        public bool IsBusy { get; set; }

        /// <summary>
        /// Set with <see cref="IsBusy"/> by a station B should back out of (washing): B, the button
        /// that backs out of everything, then stops it, as Use does (decision 2026-11-09).
        /// </summary>
        public System.Action BackOut { get; set; }

        /// <summary>In management mode: the chef waits while the player's cursor rearranges the kitchen.</summary>
        public bool IsManaging { get; set; }

        /// <summary>True while X (Use) is held down.</summary>
        public bool IsUseHeld => _use != null && _use.IsPressed();

        /// <summary>The kitchen's services, or null outside a kitchen (the office).</summary>
        public KitchenServices Services => _services;

        // Set when an X press goes to cleaning (washing, wiping, mopping, a fire) rather than a
        // station's own action; cleared when X is let go or B backs out.
        private bool _holdingToClean;

        /// <summary>
        /// Holding X to clean (decision 2026-11-11): the chef stays planted. While a job runs the
        /// stick works it; between jobs it turns the chef on the spot, and whatever cleaning comes
        /// into reach starts by itself. Letting go of X frees them to walk.
        /// </summary>
        public bool IsPlanted => (_holdingToClean && IsUseHeld && CanAct && (IsBusy || _hands.IsEmpty))
            // Outside a kitchen (the office), X has nothing to clean: holding it still plants the chef
            // and the stick turns them on the spot, the same control everywhere (the user's call).
            || (_services == null && IsUseHeld && !IsBusy && !IsManaging);

        /// <summary>The stick right now, for a station that has taken over the chef.</summary>
        public Vector2 MoveInput => _playerInput.actions[MoveActionName].ReadValue<Vector2>();

        /// <summary>True while the forkcast dashboard is up (Review, and Plan until it's put away): the chef waits.</summary>
        public bool IsReviewing => _services != null && _services.Forkcast.IsOpen;

        public bool CanAct => _services != null && !IsManaging && !_services.Paused &&
            (_services.Days.Phase == DayPhase.Prep || _services.Days.Phase == DayPhase.Service);

        /// <summary>
        /// Raised when a station took a press and did something (a chop at the board, a flip at the
        /// hob), with that station: what the chef's arms act out.
        /// </summary>
        public event Action<Interactable> Acted;

        /// <summary>Raised after every Interact press that had a target.</summary>
        public event Action<InteractionOutcome> Interacted;

        public void Bind(IReadOnlyList<Interactable> interactables, KitchenServices services)
        {
            _interactables = interactables;
            _services = services;
            if (!TryGetComponent(out _cleaning))
                _cleaning = gameObject.AddComponent<Game.Gameplay.Cleaning.ChefCleaning>();
            _cleaning.Bind(this, services);
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
            _heatDown = _playerInput.actions[HeatDownActionName];
            _heatUp = _playerInput.actions[HeatUpActionName];
            _buttonB = _playerInput.actions[ButtonBActionName];
            _buttonY = _playerInput.actions[ButtonYActionName];
            _interact.performed += HandleInteract;
            _use.performed += HandleUse;
            _buttonB.performed += HandleButtonB;
            _buttonY.performed += HandleButtonY;
        }

        private void OnDestroy()
        {
            if (_interact != null)
                _interact.performed -= HandleInteract;
            if (_use != null)
                _use.performed -= HandleUse;
            if (_buttonB != null)
                _buttonB.performed -= HandleButtonB;
            if (_buttonY != null)
                _buttonY.performed -= HandleButtonY;
            SetTarget(null);
        }

        private void Update()
        {
            if (_holdingToClean && !IsUseHeld)
                _holdingToClean = false;
            if (_interactables == null || IsBusy || IsManaging || _services.Paused)
                return;

            SetTarget(FindTarget());
            if (IsPlanted)
                TryStartCleaning();

            // The triggers turn the heat of whatever hob the chef is facing.
            if (_target is IHeatControl heat && _heatDown != null)
                heat.AdjustHeat(_heatDown.ReadValue<float>(), _heatUp.ReadValue<float>(), Time.unscaledDeltaTime);
        }

        /// <summary>In management mode: lights up what the cursor is over instead of what the chef faces.</summary>
        public void Hover(Interactable station) => SetTarget(station);

        private Interactable FindTarget()
        {
            _candidatePositions.Clear();
            var origin = new SimVector2(transform.position.x, transform.position.z);
            var currentIndex = -1;
            for (var i = 0; i < _interactables.Count; i++)
            {
                // Walls stop reaching, unless the station's against them: out of range is out of reach.
                // Outer walls always do: indoors and the street outside don't reach each other.
                var position = _interactables[i].transform.position;
                var building = _services.Building;
                var sameSide = building.IsIndoors(origin) == building.IsIndoors(new SimVector2(position.x, position.z));
                _candidatePositions.Add(sameSide && building.CanReach(origin, _interactables[i].Footprint)
                    ? new SimVector2(position.x, position.z)
                    : origin + new SimVector2(float.MaxValue / 2f, 0f));
                if (_interactables[i] == _target)
                    currentIndex = i;
            }

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

        /// <summary>
        /// Puts a station-supplied item (e.g. from storage) into the empty hands; its view appears
        /// at <paramref name="spawnPosition"/> and flies to the hands.
        /// </summary>
        public void ReceiveItem(Item item, Vector3 spawnPosition)
        {
            _hands.Put(item);
            _services.ItemViews.Place(item, _carryAnchor, _services.Tuning.ItemMove, spawnPosition);
        }

        /// <summary>Takes a tool into the tool slot; its view flies there from <paramref name="spawnPosition"/>.</summary>
        public void ReceiveTool(Item tool, Vector3 spawnPosition)
        {
            _tool.Put(tool);
            _services.ItemViews.Place(tool, _toolAnchor, _services.Tuning.ItemMove, spawnPosition);
        }

        /// <summary>Gives up the held tool (the station places its view).</summary>
        public Item ReleaseTool() => _tool.Take();

        /// <summary>The held tray's contents as a menu, one per face button.</summary>
        public void DescribeTrayMenu(List<PromptLine> lines)
        {
            if (!(_hands.Current is Tray tray))
                return;
            for (var i = 0; i < tray.Capacity; i++)
            {
                var control = (PromptControl)i;
                lines.Add(i < tray.Items.Count
                    ? new PromptLine(control, ItemLabel(tray.Items[i]))
                    : new PromptLine(control, "—", available: false));
            }
        }

        private static string ItemLabel(Item item)
        {
            if (!(item is Plate plate))
                return Tr.Get(item.TypeId);
            if (plate.IsDirty)
                return Tr.Get("dirty plate");
            return plate.Items.Count == 0 ? Tr.Get("empty plate") : Tr.Format("plate: {what}", ("what", Tr.Get(plate.Items[0].TypeId)));
        }

        // Puts one chosen thing off the tray, by the same rules as putting it down from the hands.
        // If it can't go, it stays where it was on the tray.
        private bool PlaceFromTray(int index)
        {
            if (!(_hands.Current is Tray tray) || index >= tray.Items.Count || _target.Container == null)
                return false;

            var item = tray.TakeAt(index);
            var single = new ItemSlot();
            single.Put(item);
            var outcome = ItemTransfer.Interact(single, _target.Container);
            if (single.Current != null)
                tray.InsertAt(index, single.Take());
            if (outcome == InteractionOutcome.Nothing)
                return false;

            RefreshHeld(_target.ItemAnchor.position);
            Interacted?.Invoke(outcome);
            return true;
        }

        /// <summary>Puts the views of what's held, and what's on the station faced, where they belong.</summary>
        public void RefreshHeld(Vector3 spawnPosition)
        {
            var views = _services.ItemViews;
            var move = _services.Tuning.ItemMove;
            views.Place(_hands.Current, _carryAnchor, move, spawnPosition);
            var onStation = _target != null ? _target.HeldItem : null;
            if (onStation != null)
                views.Place(onStation, _target.AnchorFor(onStation), move, _carryAnchor.position);
        }

        // Stations that answer to face buttons (storage) get them first while the hands are empty,
        // or with a tray when their menu is open. An open tray menu comes before everything.
        private bool TryFaceButton(FaceButton button)
        {
            (_target as IPlayerFocused)?.FocusOn(this);
            if (IsTrayMenuOpen)
            {
                if (!PlaceFromTray((int)button))
                    _target.NotifyDenied();
                return true;
            }

            if (_target is IFaceButtonTarget target && (_hands.IsEmpty || (_hands.Current is Tray && IsSlotMenuOpen)))
            {
                if (!target.TryPress(button, this))
                    _target.NotifyDenied();
                else
                    Acted?.Invoke(_target);
                return true;
            }
            return false;
        }

        private void HandleButtonB(InputAction.CallbackContext context)
        {
            if (IsBusy)
            {
                // Backing out ends the hold too, or the next frame would start it again.
                _holdingToClean = false;
                BackOut?.Invoke();
                return;
            }
            if (CanAct && _target != null && !TryFaceButton(FaceButton.East))
                _target.NotifyDenied();
        }

        private void HandleButtonY(InputAction.CallbackContext context)
        {
            if (CanAct && !IsBusy && _target != null && !TryFaceButton(FaceButton.North))
                _target.NotifyDenied();
        }

        private void HandleInteract(InputAction.CallbackContext context)
        {
            if (_target == null || !CanAct || IsBusy)
                return;
            (_target as IPlayerFocused)?.FocusOn(this);
            if (TryFaceButton(FaceButton.South))
                return;

            // Hands first ("A prioritises putting the ingredient away"), then the tool.
            var outcome = _target.Container != null
                ? ItemTransfer.Interact(_hands, _target.Container)
                : InteractionOutcome.Nothing;
            if (outcome == InteractionOutcome.Nothing)
            {
                if (!(_target is IToolStation tools) || !tools.TryExchangeTool(this))
                    _target.NotifyDenied();
            }
            else
            {
                // Re-place both sides; only views whose place changed move. Brand-new items
                // (from a rack or the plate stack) appear at the station and fly to the hands.
                RefreshHeld(_target.ItemAnchor.position);
            }

            Interacted?.Invoke(outcome);
        }

        private void HandleUse(InputAction.CallbackContext context)
        {
            // Cleaning and washing run while X is held; a press mid-way changes nothing.
            if (!CanAct || IsBusy)
                return;
            // Facing nothing, X still mops the floor at the chef's feet, or holds to turn and look.
            if (_target == null)
            {
                BeginHold();
                return;
            }
            if (TryFaceButton(FaceButton.West))
                return;

            // The station's own action first (flip, chop, ring the bell); cleaning is what X does
            // when it has none: what the chef holds or faces decides which.
            if (!(_target is SinkStation) && _target is IUsable usable && usable.TryUse(this))
                Acted?.Invoke(_target);
            else if (!BeginHold())
                _target.NotifyDenied();
        }

        // X went to cleaning: plant the chef and start whatever's in reach. With nothing to clean
        // the chef stays planted all the same, turning on the spot, until X is let go.
        private bool BeginHold()
        {
            if (!_hands.IsEmpty)
                return false;
            _holdingToClean = true;
            return TryStartCleaning();
        }

        // The sink washes, anywhere else the cloth, mop or extinguisher comes out.
        private bool TryStartCleaning()
        {
            if (IsBusy || !_hands.IsEmpty)
                return false;
            if (_target is SinkStation sink && sink.TryUse(this))
            {
                Acted?.Invoke(_target);
                return true;
            }
            return _cleaning != null && _cleaning.TryStart(_target);
        }
    }
}
