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
    /// other and paid for when it goes down, and X sells what's under the cursor. A cutting board or
    /// knife rack on a counter is what's under the cursor there, and goes from one free counter to
    /// another (decision 2026-11-07); the counter can only be moved once it's off.
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

            // Where a board or rack being carried would go: a free counter, or null.
            public CounterStation Onto;
            public Placement Result;
            public float TurnVelocity;

            // Being bought: not furniture yet, and not paid for until it goes down.
            public FurnitureCatalog.Entry Buying;

            public bool ShopOpen;
            public int ShopIndex;
            public bool ShopStickHeld;

            /// <summary>The colliders turned off while it's carried, to turn back on when it lands.</summary>
            public readonly List<Collider> Ghosted = new List<Collider>();
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private StaffDirector _staff;
        [SerializeField] private FurnitureCatalog _catalog;
        [SerializeField] private StreetDoor _streetDoor;

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
        [Tooltip("Metres kept clear around a body when placing furniture, on top of its own radius, so a piece never lands touching a chef's capsule.")]
        [SerializeField, Min(0f)] private float _personalSpace = 0.1f;

        private readonly Dictionary<PlayerInput, Cursor> _cursors = new Dictionary<PlayerInput, Cursor>();
        private readonly Dictionary<object, Interactable> _stations = new Dictionary<object, Interactable>();
        private KitchenPlan _plan;

        public IEnumerable<Cursor> Cursors => _cursors.Values;

        public event Action LayoutChanged;

        public IReadOnlyList<FurnitureCatalog.Entry> Shop => _catalog.Entries;

        public bool CanAfford(FurnitureCatalog.Entry entry) => _root.Services.Days.Balance >= entry.Price;

        /// <summary>The station under a cursor, or null.</summary>
        public Interactable StationUnder(Cursor cursor)
        {
            var piece = _plan.PieceAt(cursor.Position);
            if (piece is CounterStation counter && counter.Fixture != null)
                return counter.Fixture;
            return piece != null ? (Interactable)piece : null;
        }

        public bool IsManaging(PlayerInput player) => _cursors.ContainsKey(player);

        private void Start()
        {
            var floor = _root.Services.FloorPlan;
            _plan = new KitchenPlan(floor.KitchenAreaMin, floor.KitchenAreaMax, _snapRange, floor.StationHalfSize, floor.WalkerRadius);

            // The door's own rule keeps it on the walls; the plan only keeps furniture off it.
            _plan.Add(_streetDoor, Floor(_streetDoor.transform.position), _streetDoor.transform.eulerAngles.y, _streetDoor.HalfSize);
            _plan.GiveArea(_streetDoor, floor.RoomMin - SimVector2.One, floor.RoomMax + SimVector2.One);
            foreach (var station in _root.Services.Stations.All)
            {
                if (station is CounterTopStation)
                    continue; // it has no floor of its own
                _plan.Add(station, Floor(station.transform.position), station.transform.eulerAngles.y, station.HalfSize);
                if (station.BelongsInDiningRoom)
                    _plan.GiveArea(station, floor.DiningAreaMin, floor.DiningAreaMax);
            }
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

                // RB and LB turn what's carried; over a table, they add and remove its chairs.
                if (player.actions[RotateRightAction].WasPressedThisFrame())
                {
                    if (cursor.Held != null)
                        Rotate(cursor, _rotationStep);
                    else if (StationUnder(cursor) is TableStation table && !AddChair(cursor))
                        table.NotifyDenied();
                }
                else if (player.actions[RotateLeftAction].WasPressedThisFrame())
                {
                    if (cursor.Held != null)
                        Rotate(cursor, -_rotationStep);
                    else if (StationUnder(cursor) is TableStation table && !RemoveChair(cursor))
                        table.NotifyDenied();
                }

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
                    cursor.Result = Placement.Ok;
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
            if (cursor.Held == null || cursor.Held == _streetDoor || cursor.Held is CounterTopStation)
                return;
            cursor.Degrees = Mathf.Repeat(cursor.Degrees + degrees, 360f);
            UpdateHeld(cursor);
        }

        // Works out where what's being carried would land, and shows it there, on the floor: lifted,
        // the angled camera draws it away from the spot it would actually take.
        private void UpdateHeld(Cursor cursor)
        {
            if (cursor.Held == _streetDoor)
            {
                UpdateHeldDoor(cursor);
                return;
            }
            if (cursor.Held is CounterTopStation fixture)
            {
                UpdateHeldFixture(cursor, fixture);
                return;
            }

            var home = cursor.Buying == null && Mathf.Approximately(Mathf.DeltaAngle(cursor.Degrees, cursor.HeldFromDegrees), 0f)
                ? Floor(cursor.HeldFrom)
                : (SimVector2?)null;
            cursor.Landing = _plan.Snap(cursor.Held, cursor.Position, cursor.Degrees, home);
            cursor.Result = Check(cursor);
            cursor.Held.CantGoHere = cursor.Result != Placement.Ok;
            cursor.Held.transform.position = new Vector3(cursor.Landing.X, cursor.HeldFrom.y, cursor.Landing.Y);
        }

        // The door faces in from whichever wall it's on, so where it goes also decides how it's turned.
        private void UpdateHeldDoor(Cursor cursor)
        {
            var floor = _root.Services.FloorPlan;
            var fit = DoorPlacement.Fit(cursor.Position, floor.DoorLineMin, floor.DoorLineMax, floor.DoorSnapRange, floor.DoorEndMargin);
            cursor.Landing = fit.Position;
            if (fit.OnAWall)
                cursor.Degrees = fit.Degrees;
            cursor.Result = Check(cursor);
            cursor.Held.CantGoHere = cursor.Result != Placement.Ok;
            cursor.Held.transform.position = new Vector3(cursor.Landing.X, cursor.HeldFrom.y, cursor.Landing.Y);
        }

        // A board or rack lands on the counter under the cursor, turned the way it is, if nothing's on
        // it. Its own counter counts as free, so it can always go back.
        private void UpdateHeldFixture(Cursor cursor, CounterTopStation fixture)
        {
            var counter = _plan.PieceAt(cursor.Position) as CounterStation;
            var free = counter != null && (counter == fixture.OnCounter || CounterTopStation.CanGoOn(counter));
            cursor.Onto = free ? counter : null;
            if (counter != null)
            {
                cursor.Landing = _plan.PositionOf(counter);
                cursor.Degrees = _plan.DegreesOf(counter);
            }
            else
            {
                cursor.Landing = cursor.Position;
            }
            cursor.Result = Check(cursor);
            fixture.CantGoHere = cursor.Result != Placement.Ok;
            fixture.transform.position = new Vector3(cursor.Landing.X, counter != null ? counter.transform.position.y : 0f, cursor.Landing.Y);
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
            if (station == null || _cursors.Values.Any(other => other.Held == station))
                return false;

            cursor.Held = station;
            cursor.HeldFrom = station.transform.position;
            cursor.HeldFromDegrees = station is CounterTopStation ? station.transform.eulerAngles.y : _plan.DegreesOf(station);
            cursor.Degrees = cursor.HeldFromDegrees;
            cursor.TurnVelocity = 0f;
            cursor.Landing = station is CounterTopStation fixture ? Floor(fixture.transform.position) : _plan.PositionOf(station);
            cursor.Onto = (station as CounterTopStation)?.OnCounter;
            cursor.Result = Placement.Ok;
            Ghost(cursor);
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

            if (station is CounterTopStation fixture)
            {
                if (buying != null)
                {
                    fixture.Bind(_root.Services);
                    _root.Services.Stations.Add(fixture);
                    cursor.Buying = null;
                }
                fixture.PutOn(cursor.Onto);
                Land(cursor);
                cursor.Held = null;
                cursor.Onto = null;
                LayoutChanged?.Invoke();
                return true;
            }

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
                if (station is TableStation moved)
                    moved.ArrangeChairs();
            }
            Land(cursor);
            cursor.Held = null;
            Physics.SyncTransforms();
            ClearBodiesFrom(station);

            LayoutChanged?.Invoke();
            return true;
        }

        public void Cancel(Cursor cursor)
        {
            if (cursor.Held == null)
                return;
            cursor.Held.CantGoHere = false;
            cursor.Onto = null;
            if (cursor.Buying != null)
            {
                _plan.Remove(cursor.Held);
                Destroy(cursor.Held.gameObject);
                cursor.Buying = null;
                cursor.Ghosted.Clear();
            }
            else if (cursor.Held is CounterTopStation fixture)
            {
                fixture.PutOn(fixture.OnCounter);
                Land(cursor);
            }
            else
            {
                // Its old spot was open while it was away, so someone may be standing there now.
                cursor.Held.transform.SetPositionAndRotation(cursor.HeldFrom, Quaternion.Euler(0f, cursor.HeldFromDegrees, 0f));
                Land(cursor);
                Physics.SyncTransforms();
                ClearBodiesFrom(cursor.Held);
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
                cursor.Result = Placement.Ok;
            }

            if (actions[PickAction].WasPressedThisFrame())
            {
                var entry = _catalog.Entries[cursor.ShopIndex];
                if (entry.IsItem)
                    cursor.Result = BuyItem(entry);
                else if (!Buy(cursor, entry))
                    cursor.Result = Placement.CantAfford;
            }
        }

        /// <summary>
        /// Kitchenware: paid for at once and put on the first free counter (decision 2026-11-07).
        /// A knife hangs on a knife rack with room first (decision 2026-11-08). With nowhere to go it
        /// isn't bought, rather than paid for with nowhere to go.
        /// </summary>
        public Placement BuyItem(FurnitureCatalog.Entry entry)
        {
            var services = _root.Services;
            var isKnife = entry.ItemTypeId == Game.Simulation.Items.Knife.KnifeTypeId;
            var rack = isKnife
                ? services.Stations.All.OfType<KnifeRackStation>().FirstOrDefault(r => r.KnifeCount < r.Capacity)
                : null;
            var counter = services.Stations.All.OfType<CounterStation>().FirstOrDefault(c => c.IsClear);
            if (rack == null && counter == null)
                return Placement.NoFreeCounter;
            if (!services.Days.TrySpend(entry.Price))
                return Placement.CantAfford;

            if (rack != null)
            {
                rack.TryHang(new Game.Simulation.Items.Knife(), rack.ItemAnchor.position);
                return Placement.Ok;
            }

            Game.Simulation.Items.Item item = isKnife
                ? new Game.Simulation.Items.Knife()
                : services.Cooking.AddPan(services.Equipment.Pan);
            counter.Slot.Put(item);
            services.ItemViews.PlaceImmediately(item, counter.ItemAnchor);
            return Placement.Ok;
        }

        /// <summary>
        /// A bought knife rack goes straight onto the free counter nearest a cutting board (user's call,
        /// 2026-11-08): the two are used together on every cut. It takes that whole counter, as a board
        /// does. With no free counter, it's carried like anything else.
        /// </summary>
        private bool TryPutRackByABoard(Cursor cursor, FurnitureCatalog.Entry entry)
        {
            if (entry.Prefab.GetComponent<KnifeRackStation>() == null)
                return false;
            var services = _root.Services;
            var boards = services.Stations.All.OfType<PrepStation>().Where(b => b.OnCounter != null).ToList();
            var counter = services.Stations.All.OfType<CounterStation>()
                .Where(c => c.IsClear && boards.Count > 0)
                .OrderBy(c => boards.Min(b => SimVector2.DistanceSquared(Floor(c.transform.position), Floor(b.transform.position))))
                .FirstOrDefault();
            if (counter == null || !services.Days.TrySpend(entry.Price))
                return false;

            var rack = Instantiate(entry.Prefab).GetComponent<KnifeRackStation>();
            rack.name = entry.DisplayName;
            rack.Bind(services);
            services.Stations.Add(rack);
            rack.PutOn(counter);
            cursor.ShopOpen = false;
            // The cursor goes to it, so it's lit up where it landed and nobody wonders where it went.
            cursor.Position = Floor(counter.transform.position);
            LayoutChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Puts a new piece on the cursor, to be carried like any other. Nothing's paid yet. A knife
        /// rack is the exception: paid for and put on the free counter nearest a board at once.
        /// </summary>
        /// <returns>False if the money isn't there.</returns>
        public bool Buy(Cursor cursor, FurnitureCatalog.Entry entry)
        {
            if (cursor.Held != null || entry.IsItem || !CanAfford(entry))
                return false;
            if (TryPutRackByABoard(cursor, entry))
                return true;

            var position = new Vector3(cursor.Position.X, 0f, cursor.Position.Y);
            var instance = Instantiate(entry.Prefab, position, Quaternion.identity);
            instance.name = entry.DisplayName;

            cursor.ShopOpen = false;
            cursor.Buying = entry;
            cursor.Held = instance.GetComponent<Interactable>();
            if (!(cursor.Held is CounterTopStation))
                _plan.GiveSize(cursor.Held, cursor.Held.HalfSize);
            if (cursor.Held.BelongsInDiningRoom)
                _plan.GiveArea(cursor.Held, _root.Services.FloorPlan.DiningAreaMin, _root.Services.FloorPlan.DiningAreaMax);
            cursor.HeldFrom = position;
            cursor.HeldFromDegrees = 0f;
            cursor.Degrees = 0f;
            cursor.TurnVelocity = 0f;
            Ghost(cursor);
            UpdateHeld(cursor);
            return true;
        }

        /// <summary>What's stopping this being sold right now, if anything.</summary>
        public SaleProblem SaleProblemOf(Cursor cursor, Interactable station)
        {
            var entry = _catalog.Find(station.FurnitureId);
            var carried = _cursors.Values.Any(other => other.Held == station);
            // Any kind of hob keeps the kitchen cooking, so the last of them is what can't go.
            var howMany = station is HobStation
                ? _root.Services.Stations.All.Count(other => other is HobStation)
                : _root.Services.Stations.All.Count(other => other.FurnitureId == station.FurnitureId);
            return FurnitureRules.CheckSale(entry != null, station.IsClear, carried || _staff.IsHeadedTo(station), entry != null && entry.KeepOne, howMany);
        }

        /// <summary>What selling this would pay, chairs included.</summary>
        public float SellPriceOf(Interactable station)
        {
            var entry = _catalog.Find(station.FurnitureId);
            if (entry == null)
                return 0f;
            var chairs = station is TableStation table && table.HasChoiceOfChairs ? table.ChairCount - 1 : 0;
            return FurnitureRules.SellPrice(entry.Price + chairs * _catalog.ChairPrice, _catalog.SellFraction);
        }

        public float ChairPrice => _catalog.ChairPrice;

        public float ChairRefund => FurnitureRules.SellPrice(_catalog.ChairPrice, _catalog.SellFraction);

        /// <summary>Adds a chair to the table under the cursor, if there's room and the money.</summary>
        public bool AddChair(Cursor cursor)
        {
            if (!(StationUnder(cursor) is TableStation table) || !table.CanAddChair || !_root.Services.Days.TrySpend(ChairPrice))
                return false;
            table.AddChair();
            LayoutChanged?.Invoke();
            return true;
        }

        /// <summary>Takes a chair from the table under the cursor, which has to be clear.</summary>
        public bool RemoveChair(Cursor cursor)
        {
            if (!(StationUnder(cursor) is TableStation table) || !table.HasChoiceOfChairs || table.ChairCount <= 1 || !table.IsClear)
                return false;
            table.RemoveChair();
            _root.Services.Days.Receive(ChairRefund);
            LayoutChanged?.Invoke();
            return true;
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
            if (cursor.Held is CounterTopStation)
            {
                if (cursor.Onto == null)
                    return Placement.NotOnAFreeCounter;
                return cursor.Buying != null && !CanAfford(cursor.Buying) ? Placement.CantAfford : Placement.Ok;
            }

            var result = _plan.Check(cursor.Held, cursor.Landing, cursor.Degrees, People());
            if (result == Placement.Ok && cursor.Held is TableStation table)
            {
                var at = new Game.Simulation.Movement.Footprint(cursor.Landing, cursor.Degrees, table.HalfSize);
                if (table.RoomForChairs(at, _root.Services.Building) < table.ChairCount)
                    return Placement.NoRoomForChairs;
            }
            if (result == Placement.Ok && cursor.Held == _streetDoor)
            {
                var floor = _root.Services.FloorPlan;
                var onAWall = DoorPlacement.Fit(cursor.Position, floor.DoorLineMin, floor.DoorLineMax, floor.DoorSnapRange, floor.DoorEndMargin).OnAWall;
                return onAWall ? Placement.Ok : Placement.NotOnAWall;
            }
            return result == Placement.Ok && cursor.Buying != null && !CanAfford(cursor.Buying) ? Placement.CantAfford : result;
        }

        // Everyone, with how far their body reaches plus a little room, so a piece can only land
        // clear of them: one dropped onto a chef's capsule shoves them out through the floor.
        private IEnumerable<(SimVector2, float)> People()
        {
            foreach (var player in PlayerInput.all)
            {
                var radius = player.TryGetComponent(out PlayerMovement movement) && movement.IsReady
                    ? movement.BodyRadius
                    : _root.Services.FloorPlan.WalkerRadius;
                yield return (Floor(player.transform.position), radius + _personalSpace);
            }
            foreach (var agent in _staff.Agents)
                yield return (Floor(agent.transform.position), agent.BodyRadius + _personalSpace);
        }

        // While carried, a piece is only a preview: its colliders would otherwise sweep through
        // anyone it's carried over and push them about (or out of the kitchen altogether).
        private static void Ghost(Cursor cursor)
        {
            cursor.Ghosted.Clear();
            foreach (var collider in cursor.Held.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled)
                    continue;
                collider.enabled = false;
                cursor.Ghosted.Add(collider);
            }
        }

        private static void Land(Cursor cursor)
        {
            foreach (var collider in cursor.Ghosted)
            {
                if (collider != null)
                    collider.enabled = true;
            }
            cursor.Ghosted.Clear();
        }

        // Anyone standing where a piece has just landed steps out to the nearest free spot. Placing
        // is refused when someone's in the way, but a piece sent back home on Cancel can't be.
        private void ClearBodiesFrom(Interactable station)
        {
            var halfSize = _root.Services.FloorPlan.StationHalfSize;
            var footprint = station.Footprint;
            StationLayout layout = null;
            foreach (var player in PlayerInput.all)
            {
                if (!player.TryGetComponent(out PlayerMovement movement) || !movement.IsReady)
                    continue;
                if (footprint.Contains(Floor(player.transform.position), halfSize, movement.BodyRadius))
                    movement.StepClear();
            }
            foreach (var agent in _staff.Agents)
            {
                if (!footprint.Contains(Floor(agent.transform.position), halfSize, agent.BodyRadius))
                    continue;
                layout ??= _root.Services.NewLayout();
                var spot = layout.NearestFree(Floor(agent.transform.position));
                if (spot.HasValue)
                    agent.transform.position = new Vector3(spot.Value.X, agent.transform.position.y, spot.Value.Y);
            }
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
