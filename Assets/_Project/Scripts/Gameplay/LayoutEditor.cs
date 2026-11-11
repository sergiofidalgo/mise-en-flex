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
    /// Management mode (decisions 2026-11-04; Prep only since 2026-11-10): a player presses Select and their
    /// chef waits while a cursor of theirs rearranges the kitchen. A picks up and places, RB and LB
    /// rotate, B puts what's held back where it was, and Select returns to the chef. Furniture goes exactly where they put it, snapping
    /// to what's around it when close. What's on a station moves with it. Each player has their own
    /// cursor, and two can't hold the same thing. D-pad down opens the shop, whose furniture is carried like any
    /// other and paid for when it goes down (LB and RB turn its pages), and X sells what's under the cursor. The cutting board and
    /// the knife rack are each built into a counter of their own (decision 2026-11-08), moved like any piece.
    /// </summary>
    public sealed class LayoutEditor : MonoBehaviour
    {
        private const string ManageAction = "Manage";
        private const string MoveAction = "Move";
        private const string NavigateAction = "Navigate"; // lists step with the D-pad or arrows (2026-11-10)
        private const string PickAction = "Interact";
        private const string CancelAction = "ButtonB";
        private const string RotateLeftAction = "RotateLeft";
        private const string RotateRightAction = "RotateRight";
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
            /// <summary>Where it would land is outside its usual room: allowed, but it makes mess (decisions 2026-11-11).</summary>
            public bool OutOfItsRoom;
            public float TurnVelocity;

            // Being bought: not furniture yet, and not paid for until it goes down.
            public FurnitureCatalog.Entry Buying;

            public bool ShopOpen;
            /// <summary>The row chosen on the shop's page showing.</summary>
            public int ShopIndex;
            public bool ShopStickHeld;
            /// <summary>The shop's page showing, turned with LB and RB (2026-11-10).</summary>
            public ShopSection ShopSection;
            /// <summary>A presses in the shop so far, so its card can answer each one, even a repeated refusal.</summary>
            public int Presses;

            /// <summary>The colliders turned off while it's carried, to turn back on when it lands.</summary>
            public readonly List<Collider> Ghosted = new List<Collider>();
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private StaffDirector _staff;
        [SerializeField] private FurnitureCatalog _catalog;
        [SerializeField] private StreetDoor _streetDoor;
        [Tooltip("The street's wheelie bin, rolled round the outside walls (decisions 2026-11-11). Optional.")]
        [SerializeField] private WheelieBin _bin;

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
        [Tooltip("How far, in metres, the cursor can go out past the building's inside walls: far enough to reach the wheelie bin.")]
        [SerializeField, Min(0f)] private float _outsideReach = 1.8f;

        private readonly Dictionary<PlayerInput, Cursor> _cursors = new Dictionary<PlayerInput, Cursor>();
        private readonly Dictionary<object, Interactable> _stations = new Dictionary<object, Interactable>();
        private KitchenPlan _plan;

        public IEnumerable<Cursor> Cursors => _cursors.Values;

        public event Action LayoutChanged;

        public IReadOnlyList<FurnitureCatalog.Entry> Shop => _catalog.Entries;

        /// <summary>The shop's pages that have something on them, in order.</summary>
        public IReadOnlyList<ShopSection> ShopSections =>
            ((ShopSection[])Enum.GetValues(typeof(ShopSection))).Where(s => _catalog.Entries.Any(e => e.Section == s)).ToList();

        /// <summary>What's on the page a cursor's shop shows.</summary>
        public IReadOnlyList<FurnitureCatalog.Entry> ShopPage(Cursor cursor) =>
            _catalog.Entries.Where(e => e.Section == cursor.ShopSection).ToList();

        public bool CanAfford(FurnitureCatalog.Entry entry) => _root.Services.Days.Balance >= entry.Price;

        /// <summary>The station under a cursor, or null.</summary>
        public Interactable StationUnder(Cursor cursor)
        {
            var piece = _plan.PieceAt(cursor.Position);
            if (piece != null)
                return (Interactable)piece;
            return _bin != null && _bin.Footprint.Contains(cursor.Position, 0f) ? _bin : null;
        }

        public bool IsManaging(PlayerInput player) => _cursors.ContainsKey(player);

        /// <summary>
        /// The furniture standing outside its usual room (an open kitchen, decisions 2026-11-11):
        /// appliances in the dining room and tables in the kitchen. It makes mess, and guests react
        /// to it. Pieces still being carried count where they stood. Empty before Start.
        /// </summary>
        public IReadOnlyList<Interactable> OutOfTheirRooms() =>
            _plan == null ? Array.Empty<Interactable>() : _plan.OutOfTheirRooms().OfType<Interactable>().ToList();

        /// <summary>Whether this placed piece stands outside its usual room.</summary>
        public bool IsOutOfItsRoom(Interactable station) =>
            _plan != null && _plan.Pieces.Contains(station) && _plan.IsOutOfItsRoom(station, _plan.PositionOf(station));

        private void Start()
        {
            var floor = _root.Services.FloorPlan;
            _plan = new KitchenPlan(floor.KitchenAreaMin, floor.KitchenAreaMax, _snapRange, floor.StationHalfSize, floor.WalkerRadius);
            // An open kitchen (decisions 2026-11-11): anything may stand in either room.
            _plan.AddRoom(Room.DiningRoom, floor.DiningAreaMin, floor.DiningAreaMax);

            // The door's own rule keeps it on the walls; the plan only keeps furniture off it.
            _plan.Add(_streetDoor, Floor(_streetDoor.transform.position), _streetDoor.transform.eulerAngles.y, _streetDoor.HalfSize);
            _plan.GiveArea(_streetDoor, floor.RoomMin - SimVector2.One, floor.RoomMax + SimVector2.One);
            foreach (var station in _root.Services.Stations.All)
            {
                // The street's stalls aren't the restaurant's to move or sell.
                if (station is StallStation)
                    continue;
                _plan.Add(station, Floor(station.transform.position), station.transform.eulerAngles.y, station.HalfSize);
                if (station.BelongsInDiningRoom)
                    _plan.SetUsualRoom(station, Room.DiningRoom);
            }
        }

        private void Update()
        {
            var phase = _root.Services.Days.Phase;
            // Nobody rearranges the room while the boss's men are carrying it out.
            // Rearranging is for Prep: Plan is forkcast's, which covers the screen (2026-11-10).
            var open = phase == DayPhase.Prep && !Closed;

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
                    PressView(player);
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
                else if (cursor.Held == null && DownPressed(cursor))
                {
                    // D-pad down drops the shop open under the player's card (the user's call,
                    // 2026-11-11: down, as the card's tab points, not Y).
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

        // View toggles rearranging, one press.
        private void PressView(PlayerInput player)
        {
            if (IsManaging(player))
                Leave(player);
            else
                Enter(player);
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
            // Out past the walls only as far as the bin, and not behind the building, where it can't go.
            var reach = _bin != null ? _outsideReach : 0f;
            cursor.Position = new SimVector2(
                Mathf.Clamp(cursor.Position.X + by.X, floor.RoomMin.X - reach, floor.RoomMax.X + reach),
                Mathf.Clamp(cursor.Position.Y + by.Y, floor.RoomMin.Y - reach, floor.RoomMax.Y));

            if (cursor.Held == null)
                return;

            UpdateHeld(cursor);
        }

        public void Rotate(Cursor cursor, float degrees)
        {
            if (cursor.Held == null || cursor.Held == _streetDoor || cursor.Held == _bin)
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
            if (cursor.Held != null && cursor.Held == _bin)
            {
                UpdateHeldBin(cursor);
                return;
            }

            var home = cursor.Buying == null && Mathf.Approximately(Mathf.DeltaAngle(cursor.Degrees, cursor.HeldFromDegrees), 0f)
                ? Floor(cursor.HeldFrom)
                : (SimVector2?)null;
            cursor.Landing = _plan.Snap(cursor.Held, cursor.Position, cursor.Degrees, home);
            cursor.Result = Check(cursor);
            cursor.OutOfItsRoom = _plan.IsOutOfItsRoom(cursor.Held, cursor.Landing);
            cursor.Held.CantGoHere = cursor.Result != Placement.Ok;
            if (cursor.Held is TableStation table)
                table.PreviewChairs(new Game.Simulation.Movement.Footprint(cursor.Landing, cursor.Degrees, table.HalfSize), _root.Services.Building);
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

        // The bin goes to the nearest spot against a wall and rolls there after the cursor, round
        // the corners rather than through the building; the spot counts for placing straight away.
        private void UpdateHeldBin(Cursor cursor)
        {
            var spot = _bin.Track.Snap(cursor.Position);
            _bin.RollTo(spot);
            var (position, degrees) = _bin.Track.At(spot);
            cursor.Landing = position;
            cursor.Degrees = degrees;
            cursor.Result = Check(cursor);
            cursor.Held.CantGoHere = cursor.Result != Placement.Ok;
        }

        // Only the look eases round: the new angle already counts for snapping and placing, so a
        // press is never waiting on the animation, and pressing again mid-turn just carries on.
        private void TurnHeld(Cursor cursor)
        {
            if (cursor.Held == null || cursor.Held == _bin) // the bin turns itself, as it rolls round a corner
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
            cursor.TurnVelocity = 0f;
            cursor.Result = Placement.Ok;
            if (station == _bin)
            {
                // Picked up mid-roll, its home is where it was headed.
                var (home, homeDegrees) = _bin.Track.At(_bin.Target);
                cursor.HeldFrom = new Vector3(home.X, station.transform.position.y, home.Y);
                cursor.HeldFromDegrees = homeDegrees;
                cursor.Degrees = homeDegrees;
                cursor.Landing = home;
                cursor.OutOfItsRoom = false;
                Ghost(cursor);
                return true;
            }
            cursor.HeldFrom = station.transform.position;
            cursor.HeldFromDegrees = _plan.DegreesOf(station);
            cursor.Degrees = cursor.HeldFromDegrees;
            cursor.Landing = _plan.PositionOf(station);
            cursor.OutOfItsRoom = _plan.IsOutOfItsRoom(station, cursor.Landing);
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
            if (station == _bin)
            {
                // It carries on rolling to the spot; as far as everything else goes, it's there.
                station.CantGoHere = false;
                Land(cursor);
                cursor.Held = null;
                LayoutChanged?.Invoke();
                return true;
            }
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
                if (station is TableStation moved)
                    moved.ArrangeChairs();
            }
            Land(cursor);
            cursor.Held = null;
            cursor.OutOfItsRoom = false;
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
            if (cursor.Held == _bin)
            {
                _bin.RollTo(_bin.Track.Snap(Floor(cursor.HeldFrom))); // back where it was, on its wheels
                Land(cursor);
            }
            else if (cursor.Buying != null)
            {
                _plan.Remove(cursor.Held);
                Destroy(cursor.Held.gameObject);
                cursor.Buying = null;
                cursor.Ghosted.Clear();
            }
            else
            {
                // Its old spot was open while it was away, so someone may be standing there now.
                cursor.Held.transform.SetPositionAndRotation(cursor.HeldFrom, Quaternion.Euler(0f, cursor.HeldFromDegrees, 0f));
                if (cursor.Held is TableStation table)
                    table.ArrangeChairs(); // its chairs as they were, after the preview hid some
                Land(cursor);
                Physics.SyncTransforms();
                ClearBodiesFrom(cursor.Held);
            }
            cursor.Held = null;
            cursor.Result = Placement.Ok;
            cursor.OutOfItsRoom = false;
        }

        // A fresh press of D-pad down (or the down arrow), with the shop closed. Only a fresh press:
        // the D-pad is let go before it counts again.
        private bool DownPressed(Cursor cursor)
        {
            var move = cursor.Player.actions[NavigateAction].ReadValue<Vector2>();
            if (move.magnitude < _shopStickThreshold)
            {
                cursor.ShopStickHeld = false;
                return false;
            }
            if (cursor.ShopStickHeld)
                return false;
            cursor.ShopStickHeld = true;
            return move.y < 0f && Mathf.Abs(move.y) >= Mathf.Abs(move.x);
        }

        // The D-pad moves through the grid, one press at a time; A buys, and B closes it.
        private void UpdateShop(Cursor cursor)
        {
            var actions = cursor.Player.actions;
            if (actions[CancelAction].WasPressedThisFrame())
            {
                cursor.ShopOpen = false;
                return;
            }

            // LB and RB turn the pages, as on the tailor's dressing card; the list starts at its top.
            var sections = ShopSections.ToList();
            if (sections.Count == 0)
                return;
            if (!sections.Contains(cursor.ShopSection))
                cursor.ShopSection = sections[0];
            var turn = actions[RotateRightAction].WasPressedThisFrame() ? 1 : actions[RotateLeftAction].WasPressedThisFrame() ? -1 : 0;
            if (turn != 0)
            {
                var at = sections.IndexOf(cursor.ShopSection);
                cursor.ShopSection = sections[(at + turn + sections.Count) % sections.Count];
                cursor.ShopIndex = 0;
                cursor.Result = Placement.Ok;
                return;
            }
            var page = ShopPage(cursor);
            cursor.ShopIndex = Mathf.Clamp(cursor.ShopIndex, 0, page.Count - 1);

            // The page is a grid of pictures (ShopGrid, 2026-11-11): left and right step one piece,
            // up and down a row. Navigate's y is up-positive, the grid's rows run down.
            var move = actions[NavigateAction].ReadValue<Vector2>();
            if (move.magnitude < _shopStickThreshold)
            {
                cursor.ShopStickHeld = false;
            }
            else if (!cursor.ShopStickHeld)
            {
                cursor.ShopStickHeld = true;
                var across = Mathf.Abs(move.x) > Mathf.Abs(move.y);
                cursor.ShopIndex = Game.Simulation.Furniture.ShopGrid.Step(cursor.ShopIndex, page.Count,
                    across ? (move.x > 0f ? 1 : -1) : 0, across ? 0 : (move.y > 0f ? -1 : 1));
                cursor.Result = Placement.Ok;
            }

            if (actions[PickAction].WasPressedThisFrame())
            {
                cursor.Presses++;
                var entry = page[cursor.ShopIndex];
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

            // A plate joins the clean pile, if it has room.
            if (entry.ItemTypeId == Game.Simulation.Items.Plate.PlateTypeId)
            {
                var stack = services.Stations.All.OfType<PlateStackStation>().FirstOrDefault(s => s.Stack.Count < s.Stack.Capacity);
                if (stack == null)
                    return Placement.NoFreeCounter;
                if (!services.Days.TrySpend(entry.Price))
                    return Placement.CantAfford;
                stack.Stack.AddNew(1, services.Equipment.PlateCapacity);
                return Placement.Ok;
            }

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

        /// <summary>Puts a new piece on the cursor, to be carried like any other. Nothing's paid yet.</summary>
        /// <returns>False if the money isn't there.</returns>
        public bool Buy(Cursor cursor, FurnitureCatalog.Entry entry)
        {
            if (cursor.Held != null || entry.IsItem || !CanAfford(entry))
                return false;

            var position = new Vector3(cursor.Position.X, 0f, cursor.Position.Y);
            var instance = Instantiate(entry.Prefab, position, Quaternion.identity);
            instance.name = entry.DisplayName;

            cursor.ShopOpen = false;
            cursor.Buying = entry;
            cursor.Held = instance.GetComponent<Interactable>();
            _plan.GiveSize(cursor.Held, cursor.Held.HalfSize);
            if (cursor.Held.BelongsInDiningRoom)
                _plan.SetUsualRoom(cursor.Held, Room.DiningRoom);
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

        /// <summary>Set while the boss is in (Debt.BossVisitDirector): management mode can't open.</summary>
        public bool Closed { get; set; }

        /// <summary>
        /// The bodyguards take a piece (§15.3): it leaves the kitchen as a sale would, but its body
        /// stays in the scene for them to carry out, and nothing is paid here (the director credits
        /// it against the debt).
        /// </summary>
        public void Repossess(Interactable station)
        {
            var services = _root.Services;
            station.Retire(services);
            services.Messes?.Sold(station);
            services.Stations.Remove(station);
            _plan.Remove(station);
            Physics.SyncTransforms();
            LayoutChanged?.Invoke();
        }

        /// <summary>Sells what's under the cursor, if it can be sold.</summary>
        public bool Sell(Cursor cursor)
        {
            var station = StationUnder(cursor);
            if (station == null || cursor.Held != null || SaleProblemOf(cursor, station) != SaleProblem.None)
                return false;

            var services = _root.Services;
            services.Days.Receive(SellPriceOf(station));

            // The first morning's starter stock needs somewhere to go; there's always another, as the last can't be sold.
            if (station is StorageStation storage)
                storage.PassStarterStockTo(services.Stations.All.OfType<StorageStation>().First(other => other != storage && other.FurnitureId == storage.FurnitureId));

            station.Retire(services);
            services.Messes?.Sold(station);
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
            if (cursor.Held != null && cursor.Held == _bin)
                return CheckBin(new Game.Simulation.Movement.Footprint(cursor.Landing, cursor.Degrees, _bin.Track.HalfSize));
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
                if (!onAWall)
                    return Placement.NotOnAWall;
                return DoorBlockedByBin(cursor) ? Placement.BlocksTheDoor : Placement.Ok;
            }
            return result == Placement.Ok && cursor.Buying != null && !CanAfford(cursor.Buying) ? Placement.CantAfford : result;
        }

        // The bin can't stand in the doorway, on the guests' way in from the pavement, or on anyone.
        private Placement CheckBin(Game.Simulation.Movement.Footprint at)
        {
            foreach (var clear in _bin.KeepClear(_streetDoor.Footprint))
            {
                if (at.Overlaps(clear, 0f))
                    return Placement.BlocksTheDoor;
            }
            foreach (var (position, radius) in People())
            {
                if (at.Contains(position, 0f, radius))
                    return Placement.SomeoneInTheWay;
            }
            return Placement.Ok;
        }

        // The street door can't go where the bin would then stand in its way.
        private bool DoorBlockedByBin(Cursor cursor)
        {
            if (_bin == null)
                return false;
            var bin = _bin.Track.FootprintAt(_bin.Target);
            var door = new Game.Simulation.Movement.Footprint(cursor.Landing, cursor.Degrees, _streetDoor.HalfSize);
            return _bin.KeepClear(door).Any(clear => bin.Overlaps(clear, 0f));
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
