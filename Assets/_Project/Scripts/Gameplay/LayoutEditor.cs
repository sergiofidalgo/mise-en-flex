using System;
using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Customers;
using Game.Gameplay.Staff;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
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
    /// cursor, and two can't hold the same thing.
    /// </summary>
    public sealed class LayoutEditor : MonoBehaviour
    {
        private const string ManageAction = "Manage";
        private const string MoveAction = "Move";
        private const string PickAction = "Interact";
        private const string CancelAction = "ButtonB";
        private const string RotateLeftAction = "RotateLeft";
        private const string RotateRightAction = "RotateRight";

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
            public readonly List<object> Blocked = new List<object>();
        }

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private StaffDirector _staff;
        [SerializeField] private CustomerDirector _customers;

        [Tooltip("Real metres per second the cursor moves at full stick.")]
        [SerializeField, Min(0.1f)] private float _cursorSpeed = 6f;
        [Tooltip("How close, in metres, furniture has to get to something before it snaps into line.")]
        [SerializeField, Min(0f)] private float _snapRange = 0.15f;
        [Tooltip("Degrees each press of RB or LB turns what's being carried.")]
        [SerializeField, Min(1f)] private float _rotationStep = 45f;
        [Tooltip("Half the room's width and depth, for walking.")]
        [SerializeField] private Vector2 _roomHalfSize = new Vector2(4.6f, 3.6f);
        [Tooltip("Where the centres of stations can go: lowest corner.")]
        [SerializeField] private Vector2 _areaMin = new Vector2(-4.4f, -2.4f);
        [Tooltip("Where the centres of stations can go: highest corner.")]
        [SerializeField] private Vector2 _areaMax = new Vector2(4.4f, 3.4f);
        [SerializeField, Min(0f)] private float _stationHalfSize = 0.5f;
        [SerializeField, Min(0f)] private float _walkerRadius = 0.3f;
        [SerializeField, Min(0f)] private float _standDistance = 0.85f;

        private readonly Dictionary<PlayerInput, Cursor> _cursors = new Dictionary<PlayerInput, Cursor>();
        private readonly Dictionary<object, Interactable> _stations = new Dictionary<object, Interactable>();
        private KitchenPlan _plan;

        public IEnumerable<Cursor> Cursors => _cursors.Values;

        public event Action LayoutChanged;

        /// <summary>The station under a cursor, or null.</summary>
        public Interactable StationUnder(Cursor cursor)
        {
            var piece = _plan.PieceAt(cursor.Position);
            return piece != null ? (Interactable)piece : null;
        }

        public bool IsManaging(PlayerInput player) => _cursors.ContainsKey(player);

        private void Start()
        {
            _plan = new KitchenPlan(
                new SimVector2(_roomHalfSize.x, _roomHalfSize.y),
                new SimVector2(_areaMin.x, _areaMin.y),
                new SimVector2(_areaMax.x, _areaMax.y),
                _snapRange, _stationHalfSize, _walkerRadius, _standDistance);
            foreach (var station in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                _plan.Add(station, Floor(station.transform.position), station.transform.eulerAngles.y, rowLocked: station is PassStation);

            // What a chef works facing one side of needs that side clear; a counter or pile doesn't.
            foreach (var station in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                if (station is HobStation || station is PrepStation || station is SinkStation || station is StorageStation)
                    _plan.GiveFront(station);
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

                var stick = player.actions[MoveAction].ReadValue<Vector2>();
                MoveCursor(cursor, new SimVector2(stick.x, stick.y) * (_cursorSpeed * Time.unscaledDeltaTime));

                if (player.actions[RotateRightAction].WasPressedThisFrame())
                    Rotate(cursor, _rotationStep);
                else if (player.actions[RotateLeftAction].WasPressedThisFrame())
                    Rotate(cursor, -_rotationStep);

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
            cursor.Position = new SimVector2(
                Mathf.Clamp(cursor.Position.X + by.X, -_roomHalfSize.x, _roomHalfSize.x),
                Mathf.Clamp(cursor.Position.Y + by.Y, -_roomHalfSize.y, _roomHalfSize.y));

            if (cursor.Held == null)
                return;

            UpdateHeld(cursor);
        }

        public void Rotate(Cursor cursor, float degrees)
        {
            if (cursor.Held == null || !_plan.CanRotate(cursor.Held))
                return;
            cursor.Degrees = Mathf.Repeat(cursor.Degrees + degrees, 360f);
            UpdateHeld(cursor);
        }

        // Works out where what's being carried would land, and shows it there, on the floor: lifted,
        // the angled camera draws it away from the spot it would actually take.
        private void UpdateHeld(Cursor cursor)
        {
            var home = Mathf.Approximately(Mathf.DeltaAngle(cursor.Degrees, cursor.HeldFromDegrees), 0f)
                ? Floor(cursor.HeldFrom)
                : (SimVector2?)null;
            cursor.Landing = _plan.Snap(cursor.Held, cursor.Position, cursor.Degrees, home);
            cursor.Result = Check(cursor);
            cursor.Held.MarkedRed = cursor.Result != Placement.Ok;
            cursor.Held.transform.SetPositionAndRotation(
                new Vector3(cursor.Landing.X, cursor.HeldFrom.y, cursor.Landing.Y),
                Quaternion.Euler(0f, cursor.Degrees, 0f));
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
            cursor.HeldFromDegrees = _plan.DegreesOf(station);
            cursor.Degrees = cursor.HeldFromDegrees;
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
            station.MarkedRed = false;
            station.transform.SetPositionAndRotation(
                new Vector3(cursor.Landing.X, cursor.HeldFrom.y, cursor.Landing.Y),
                Quaternion.Euler(0f, cursor.Degrees, 0f));
            _plan.Move(station, cursor.Landing, cursor.Degrees);
            cursor.Held = null;
            Physics.SyncTransforms();

            // A stool stays in front of its pass counter.
            if (station is PassStation pass && _customers != null)
                _customers.MoveSeat(pass.SeatIndex, cursor.Landing.X);

            LayoutChanged?.Invoke();
            return true;
        }

        public void Cancel(Cursor cursor)
        {
            if (cursor.Held == null)
                return;
            ClearBlocked(cursor);
            cursor.Held.MarkedRed = false;
            cursor.Held.transform.SetPositionAndRotation(cursor.HeldFrom, Quaternion.Euler(0f, cursor.HeldFromDegrees, 0f));
            cursor.Held = null;
            cursor.Result = Placement.Ok;
        }

        // Whatever a placement would cut off turns red too, so it's clear what it would block.
        private Placement Check(Cursor cursor)
        {
            ClearBlocked(cursor);
            var result = _plan.Check(cursor.Held, cursor.Landing, cursor.Degrees, People(), cursor.Blocked);
            foreach (var piece in cursor.Blocked)
                ((Interactable)piece).MarkedRed = true;
            return result;
        }

        private static void ClearBlocked(Cursor cursor)
        {
            foreach (var piece in cursor.Blocked)
                ((Interactable)piece).MarkedRed = false;
            cursor.Blocked.Clear();
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
