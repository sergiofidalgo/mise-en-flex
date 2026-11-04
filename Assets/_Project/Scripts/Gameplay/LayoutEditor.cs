using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay.Staff;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Furniture;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay
{
    /// <summary>
    /// Management mode (decisions 2026-11-04): during Plan and Prep, a player presses Select and their
    /// chef waits while a cursor of theirs rearranges the kitchen. A picks up and places, RB and LB
    /// rotate, B puts what's held back where it was, and Select returns to the chef. Furniture goes exactly where they put it, snapping
    /// to what's around it when close. What's on a station moves with it. Each player has their own
    /// cursor, and two can't hold the same thing. Y opens the shop, whose furniture is carried like any
    /// other and paid for when it goes down, and X sells what's under the cursor.
    /// </summary>
    public sealed class LayoutEditor : MonoBehaviour
    {
        private const string ManageAction = "Manage";
        private const string MoveAction = "Move";
        private const string PickAction = "Interact";
        private const string CancelAction = "ButtonB";
        private const string RotateLeftAction = "RotateLeft";
        private const string RotateRightAction = "RotateRight";
        private const string ShopAction = "ButtonY";
        private const string SellAction = "Use";

        public sealed class Cursor
        {
            public PlayerInput Player;
            public SimVector2 Position;
            public Interactable Held;
            public Vector3 HeldFrom;
            public float HeldFromDegrees;
            public SimVector2 Landing;
            public float Degrees;
            public Placement Result;
            public float TurnVelocity;

            // Being bought: not furniture yet, and not paid for until it goes down.
            public FurnitureCatalog.Entry Buying;

            public bool ShopOpen;
            public int ShopIndex;
            public bool ShopStickHeld;
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private StaffDirector _staff;
        [SerializeField] private FurnitureCatalog _catalog;

        [Tooltip("Real metres per second the cursor moves at full stick.")]
        [SerializeField, Min(0.1f)] private float _cursorSpeed = 6f;
        [Tooltip("How close, in metres, furniture has to get to something before it snaps into line.")]
        [SerializeField, Min(0f)] private float _snapRange = 0.15f;
        [Tooltip("Degrees each press of RB or LB turns what's being carried.")]
        [SerializeField, Min(1f)] private float _rotationStep = 45f;
        [Tooltip("How quickly what's being carried eases round to its new angle: it takes about twice this, in seconds.")]
        [SerializeField, Min(0.001f)] private float _turnSmoothing = 0.05f;
        [Tooltip("How far the stick has to go to move down the shop's list.")]
        [SerializeField, Range(0.1f, 1f)] private float _shopStickThreshold = 0.5f;

        private readonly Dictionary<PlayerInput, Cursor> _cursors = new Dictionary<PlayerInput, Cursor>();
        private readonly Dictionary<object, Interactable> _stations = new Dictionary<object, Interactable>();
        private KitchenPlan _plan;

        public IEnumerable<Cursor> Cursors => _cursors.Values;

        public event Action LayoutChanged;

        public IReadOnlyList<FurnitureCatalog.Entry> Shop => _catalog.Entries;

        /// <summary>The pass is built into the wall between the rooms; anything else can move.</summary>
        public bool CanMove(Interactable station) => !(station is PassStation);

        public bool CanAfford(FurnitureCatalog.Entry entry) => _root.Services.Days.Balance >= entry.Price;

        /// <summary>The station under a cursor, or null.</summary>
        public Interactable StationUnder(Cursor cursor)
        {
            var piece = _plan.PieceAt(cursor.Position);
            return piece != null ? (Interactable)piece : null;
        }

        public bool IsManaging(PlayerInput player) => _cursors.ContainsKey(player);

        private void Start()
        {
            var floor = _root.Services.FloorPlan;
            _plan = new KitchenPlan(floor.KitchenAreaMin, floor.KitchenAreaMax, _snapRange, floor.StationHalfSize, floor.WalkerRadius);
            foreach (var station in _root.Services.Stations.All)
                _plan.Add(station, Floor(station.transform.position), station.transform.eulerAngles.y, station.HalfSize);
        }

        private void Update()
        {
            var phase = _root.Services.Days.Phase;
            var open = phase == DayPhase.Plan || phase == DayPhase.Prep;

            foreach (var player in PlayerInput.all)
            {
                if (!open)
                {
                    Leave(player);
                    continue;
                }
                if (!_dayLoop.WasHereLastFrame(player))
                    continue;

                if (player.actions[ManageAction].WasPressedThisFrame())
                {
                    if (IsManaging(player))
                        Leave(player);
                    else
                        Enter(player);
                    continue;
                }

                if (!_cursors.TryGetValue(player, out var cursor))
                    continue;

                if (cursor.ShopOpen)
                {
                    UpdateShop(cursor);
                    continue;
                }

                var stick = player.actions[MoveAction].ReadValue<Vector2>();
                MoveCursor(cursor, new SimVector2(stick.x, stick.y) * (_cursorSpeed * Time.unscaledDeltaTime));

                if (player.actions[RotateRightAction].WasPressedThisFrame())
                    Rotate(cursor, _rotationStep);
                else if (player.actions[RotateLeftAction].WasPressedThisFrame())
                    Rotate(cursor, -_rotationStep);

                TurnHeld(cursor);
                Light(cursor);

                if (player.actions[PickAction].WasPressedThisFrame())
                {
                    if (cursor.Held == null)
                        PickUp(cursor);
                    else
                        Place(cursor);
                }
                else if (player.actions[CancelAction].WasPressedThisFrame())
                {
                    Cancel(cursor);
                }
                else if (cursor.Held == null && player.actions[ShopAction].WasPressedThisFrame())
                {
                    cursor.ShopOpen = true;
                    cursor.ShopStickHeld = true; // until it's let go, so opening doesn't also move the list
                }
                else if (cursor.Held == null && player.actions[SellAction].WasPressedThisFrame())
                {
                    if (!Sell(cursor))
                        StationUnder(cursor)?.NotifyDenied();
                }
            }
        }

        public Cursor Enter(PlayerInput player)
        {
            if (_cursors.TryGetValue(player, out var existing))
                return existing;
            var cursor = new Cursor { Player = player, Position = Floor(player.transform.position) };
            _cursors[player] = cursor;
            if (player.TryGetComponent(out PlayerInteractor interactor))
                interactor.IsManaging = true;
            return cursor;
        }

        public void Leave(PlayerInput player)
        {
            if (!_cursors.TryGetValue(player, out var cursor))
                return;
            if (cursor.Held != null)
                Cancel(cursor);
            _cursors.Remove(player);
            if (player.TryGetComponent(out PlayerInteractor interactor))
            {
                interactor.Hover(null);
                interactor.IsManaging = false;
            }
        }

        public void MoveCursor(Cursor cursor, SimVector2 by)
        {
            var floor = _root.Services.FloorPlan;
            cursor.Position = new SimVector2(
                Mathf.Clamp(cursor.Position.X + by.X, floor.RoomMin.X, floor.RoomMax.X),
                Mathf.Clamp(cursor.Position.Y + by.Y, floor.RoomMin.Y, floor.RoomMax.Y));

            if (cursor.Held == null)
                return;

            UpdateHeld(cursor);
        }

        public void Rotate(Cursor cursor, float degrees)
        {
            if (cursor.Held == null)
                return;
            cursor.Degrees = Mathf.Repeat(cursor.Degrees + degrees, 360f);
            UpdateHeld(cursor);
        }

        // Works out where what's being carried would land, and shows it there, on the floor: lifted,
        // the angled camera draws it away from the spot it would actually take.
        private void UpdateHeld(Cursor cursor)
        {
            var home = cursor.Buying == null && Mathf.Approximately(Mathf.DeltaAngle(cursor.Degrees, cursor.HeldFromDegrees), 0f)
                ? Floor(cursor.HeldFrom)
                : (SimVector2?)null;
            cursor.Landing = _plan.Snap(cursor.Held, cursor.Position, cursor.Degrees, home);
            cursor.Result = Check(cursor);
            cursor.Held.CantGoHere = cursor.Result != Placement.Ok;
            cursor.Held.transform.position = new Vector3(cursor.Landing.X, cursor.HeldFrom.y, cursor.Landing.Y);
        }

        // Only the look eases round: the new angle already counts for snapping and placing, so a
        // press is never waiting on the animation, and pressing again mid-turn just carries on.
        private void TurnHeld(Cursor cursor)
        {
            if (cursor.Held == null)
                return;
            var shown = cursor.Held.transform.eulerAngles.y;
            shown = Mathf.Abs(Mathf.DeltaAngle(shown, cursor.Degrees)) < 0.1f
                ? cursor.Degrees
                : Mathf.SmoothDampAngle(shown, cursor.Degrees, ref cursor.TurnVelocity, _turnSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            cursor.Held.transform.rotation = Quaternion.Euler(0f, shown, 0f);
        }

        // Furniture under a cursor lights up, just as when a chef walks up to it.
        private void Light(Cursor cursor)
        {
            if (cursor.Player.TryGetComponent(out PlayerInteractor interactor))
                interactor.Hover(cursor.Held != null ? cursor.Held : StationUnder(cursor));
        }

        public bool PickUp(Cursor cursor)
        {
            var station = StationUnder(cursor);
            if (station == null || !CanMove(station) || _cursors.Values.Any(other => other.Held == station))
                return false;

            cursor.Held = station;
            cursor.HeldFrom = station.transform.position;
            cursor.HeldFromDegrees = _plan.DegreesOf(station);
            cursor.Degrees = cursor.HeldFromDegrees;
            cursor.TurnVelocity = 0f;
            cursor.Landing = _plan.PositionOf(station);
            cursor.Result = Placement.Ok;
            return true;
        }

        public bool Place(Cursor cursor)
        {
            if (cursor.Held == null)
                return false;

            cursor.Result = Check(cursor);
            if (cursor.Result != Placement.Ok)
                return false;

            var station = cursor.Held;
            var buying = cursor.Buying;
            if (buying != null && !_root.Services.Days.TrySpend(buying.Price))
                return false;
            station.CantGoHere = false;
            station.transform.SetPositionAndRotation(
                new Vector3(cursor.Landing.X, cursor.HeldFrom.y, cursor.Landing.Y),
                Quaternion.Euler(0f, cursor.Degrees, 0f));
            if (buying != null)
            {
                // Now it's furniture: it works, and everyone knows about it.
                station.Bind(_root.Services);
                _plan.Add(station, cursor.Landing, cursor.Degrees, station.HalfSize);
                _root.Services.Stations.Add(station);
                cursor.Buying = null;
            }
            else
            {
                _plan.Move(station, cursor.Landing, cursor.Degrees);
            }
            cursor.Held = null;
            Physics.SyncTransforms();

            LayoutChanged?.Invoke();
            return true;
        }

        public void Cancel(Cursor cursor)
        {
            if (cursor.Held == null)
                return;
            cursor.Held.CantGoHere = false;
            if (cursor.Buying != null)
            {
                _plan.Remove(cursor.Held);
                Destroy(cursor.Held.gameObject);
                cursor.Buying = null;
            }
            else
            {
                cursor.Held.transform.SetPositionAndRotation(cursor.HeldFrom, Quaternion.Euler(0f, cursor.HeldFromDegrees, 0f));
            }
            cursor.Held = null;
            cursor.Result = Placement.Ok;
        }

        // The stick moves down the list, one press at a time; A buys, and B or Y closes it.
        private void UpdateShop(Cursor cursor)
        {
            var actions = cursor.Player.actions;
            if (actions[CancelAction].WasPressedThisFrame() || actions[ShopAction].WasPressedThisFrame())
            {
                cursor.ShopOpen = false;
                return;
            }

            var stick = actions[MoveAction].ReadValue<Vector2>().y;
            if (Mathf.Abs(stick) < _shopStickThreshold)
            {
                cursor.ShopStickHeld = false;
            }
            else if (!cursor.ShopStickHeld)
            {
                cursor.ShopStickHeld = true;
                var count = _catalog.Entries.Length;
                cursor.ShopIndex = (cursor.ShopIndex + (stick > 0f ? count - 1 : 1)) % count;
            }

            if (actions[PickAction].WasPressedThisFrame() && !Buy(cursor, _catalog.Entries[cursor.ShopIndex]))
                cursor.Result = Placement.CantAfford;
        }

        /// <summary>Puts a new piece on the cursor, to be carried like any other. Nothing's paid yet.</summary>
        /// <returns>False if the money isn't there.</returns>
        public bool Buy(Cursor cursor, FurnitureCatalog.Entry entry)
        {
            if (cursor.Held != null || !CanAfford(entry))
                return false;

            var position = new Vector3(cursor.Position.X, 0f, cursor.Position.Y);
            var instance = Instantiate(entry.Prefab, position, Quaternion.identity);
            instance.name = entry.DisplayName;

            cursor.ShopOpen = false;
            cursor.Buying = entry;
            cursor.Held = instance.GetComponent<Interactable>();
            _plan.GiveSize(cursor.Held, cursor.Held.HalfSize);
            if (cursor.Held.BelongsInDiningRoom)
                _plan.GiveArea(cursor.Held, _root.Services.FloorPlan.DiningAreaMin, _root.Services.FloorPlan.DiningAreaMax);
            cursor.HeldFrom = position;
            cursor.HeldFromDegrees = 0f;
            cursor.Degrees = 0f;
            cursor.TurnVelocity = 0f;
            UpdateHeld(cursor);
            return true;
        }

        /// <summary>What's stopping this being sold right now, if anything.</summary>
        public SaleProblem SaleProblemOf(Cursor cursor, Interactable station)
        {
            var entry = _catalog.Find(station.FurnitureId);
            var carried = _cursors.Values.Any(other => other.Held == station);
            var howMany = _root.Services.Stations.All.Count(other => other.FurnitureId == station.FurnitureId);
            return FurnitureRules.CheckSale(entry != null, station.IsClear, carried || _staff.IsHeadedTo(station), entry != null && entry.KeepOne, howMany);
        }

        /// <summary>What selling this would pay.</summary>
        public float SellPriceOf(Interactable station)
        {
            var entry = _catalog.Find(station.FurnitureId);
            return entry != null ? FurnitureRules.SellPrice(entry.Price, _catalog.SellFraction) : 0f;
        }

        /// <summary>Sells what's under the cursor, if it can be sold.</summary>
        public bool Sell(Cursor cursor)
        {
            var station = StationUnder(cursor);
            if (station == null || cursor.Held != null || SaleProblemOf(cursor, station) != SaleProblem.None)
                return false;

            var services = _root.Services;
            services.Days.Receive(SellPriceOf(station));

            // Morning deliveries need somewhere to go; there's always another, as the last can't be sold.
            if (station is StorageStation storage)
                storage.PassDeliveriesTo(services.Stations.All.OfType<StorageStation>().First(other => other != storage && other.FurnitureId == storage.FurnitureId));

            station.Retire(services);
            services.Stations.Remove(station);
            _plan.Remove(station);
            station.gameObject.SetActive(false); // gone this frame, for anything looking before it's destroyed
            Destroy(station.gameObject);
            Physics.SyncTransforms();
            LayoutChanged?.Invoke();
            return true;
        }

        private Placement Check(Cursor cursor)
        {
            var result = _plan.Check(cursor.Held, cursor.Landing, cursor.Degrees, People());
            return result == Placement.Ok && cursor.Buying != null && !CanAfford(cursor.Buying) ? Placement.CantAfford : result;
        }

        private IEnumerable<SimVector2> People()
        {
            foreach (var player in PlayerInput.all)
                yield return Floor(player.transform.position);
            foreach (var agent in _staff.Agents)
                yield return Floor(agent.transform.position);
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
