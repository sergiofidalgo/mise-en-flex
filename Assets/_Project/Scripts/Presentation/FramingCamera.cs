using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Staff;
using Game.Simulation.Days;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Presentation
{
    /// <summary>
    /// Holds one still shot of the kitchen, where the cooking is, with the customers served across its
    /// wall. Walking around inside it never moves the camera. When someone leaves it, the shot grows to
    /// take them in, always keeping the kitchen, and settles back into the same shot when they return.
    /// Once nearly all of the restaurant would show anyway, it shows all of it, centred and still. Only
    /// past a height where the kitchen can't sensibly stay in does it follow the people alone. Customers
    /// don't count: those out of view get markers at the edge of the screen. Before anyone joins, and
    /// during the Review, it shows the whole restaurant. Once anyone leaves the building it holds one
    /// still shot of the street in front of it, and one more, further out, while anyone's on the road:
    /// it never follows them outside (user's call, 2026-11-14).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FramingCamera : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private StaffDirector _staff;
        [SerializeField] private LayoutEditor _editor;

        [Tooltip("How far the camera looks down from the horizon, in degrees.")]
        [SerializeField, Range(10f, 89f)] private float _tilt = 55f;
        [Tooltip("Metres of floor to keep at the sides of everyone.")]
        [SerializeField, Min(0f)] private float _margin = 2f;
        [Tooltip("Metres of floor to keep beyond the farthest. Little: the share of the screen below gives the room.")]
        [SerializeField, Min(0f)] private float _farMargin = 1.5f;
        [Tooltip("Metres of floor to keep before the nearest. Little: nothing stands below them on screen.")]
        [SerializeField, Min(0f)] private float _nearMargin = 0.6f;
        [Tooltip("Metres of floor in front of the kitchen in its shot: the customers served across the wall, and a little behind them.")]
        [SerializeField, Min(0f)] private float _frontOfKitchen = 2f;
        [Tooltip("Metres past the kitchen's sides in its shot, for the walls and the units on them. Anyone within this counts as in the kitchen.")]
        [SerializeField, Min(0f)] private float _kitchenSides = 0.5f;
        [Tooltip("Metres past the kitchen's back in its shot, for the back wall. The room left at the top of the screen shows what stands on it.")]
        [SerializeField, Min(0f)] private float _kitchenBehind = 0.5f;
        [Tooltip("The highest it goes keeping the kitchen in the shot. Past it, in a big restaurant, it frames the people alone.")]
        [SerializeField, Min(1f)] private float _mostKeepingKitchen = 18f;
        [Tooltip("Shows the whole restaurant as soon as anyone leaves the kitchen, if that's no more than this many times the kitchen's height. Otherwise the kitchen's shot grows to take them in.")]
        [SerializeField, Min(1f)] private float _wholeWithin = 1.4f;
        [Tooltip("Share of the screen to leave clear at the top, for heads, furniture and the prompts above them.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTop = 0.27f;
        [Tooltip("The same with people as far apart as they get. Everything's smaller out there, so the floor needs the screen more, and a prompt still fits.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTopFarApart = 0.2f;
        [Tooltip("How high it would have to go, at the usual room at the top, to count as far apart.")]
        [SerializeField, Min(1f)] private float _farApartHeight = 12f;
        [Tooltip("The same when showing the whole restaurant: nobody's cooking, so no prompts need the room.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTopForWhole = 0.14f;
        [Tooltip("Metres of floor to keep beyond the back of the room when showing all of it: less than with people far apart, as the room at the top already shows the back wall and what stands on it.")]
        [SerializeField, Min(0f)] private float _wholeFarMargin = 0.9f;
        [Tooltip("Share of the screen to leave clear at the bottom.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenBottom = 0.06f;
        [Tooltip("The closest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _minHeight = 6.5f;
        [Tooltip("The furthest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _maxHeight = 40f;
        [Tooltip("How far the building's outer walls are from where people can stand, in metres.")]
        [SerializeField, Min(0f)] private float _wallsBeyondRoom = 0.5f;
        [Tooltip("How quickly it changes shot when backing away: it's most of the way there in twice this many seconds. It moves off at once, quickest at the start. Height and floor position move together, in one smooth arc.")]
        [SerializeField, Min(0.01f)] private float _zoomOutSeconds = 0.45f;
        [Tooltip("The same when closing in.")]
        [SerializeField, Min(0.01f)] private float _zoomInSeconds = 0.7f;
        [Tooltip("The same when the first player joins, or a new day starts after the Review: quicker, as everyone's waiting to start.")]
        [SerializeField, Min(0.01f)] private float _joinSeconds = 0.32f;
        [Tooltip("Seconds it waits before closing in, not moving at all meanwhile. None by default (user's call): coming back into the kitchen or joining, it starts at once.")]
        [SerializeField, Min(0f)] private float _holdBeforeClosingIn = 0f;
        [Tooltip("The fastest it moves, in metres per second, in any direction.")]
        [SerializeField, Min(0.1f)] private float _maxSpeed = 40f;
        [Tooltip("Metres of floor kept around everyone even while the camera is still catching up: past this, it's pushed along at once instead of easing.")]
        [SerializeField, Min(0f)] private float _edgeMargin = 0.6f;
        [Tooltip("Share of the screen at the top it never lets anyone into, so heads stay in view while it catches up.")]
        [SerializeField, Range(0f, 0.45f)] private float _edgeScreenTop = 0.1f;
        [Tooltip("The same at the bottom.")]
        [SerializeField, Range(0f, 0.45f)] private float _edgeScreenBottom = 0.02f;

        [Tooltip("The street in front of the building and the plaza beside it, up to the kerb, with the building (x, z corners): one still shot of it while anyone's out of the building but not on the road. Set by the street's builder.")]
        [SerializeField] private Vector2 _streetMin = new Vector2(-14f, -10.3f);
        [SerializeField] private Vector2 _streetMax = new Vector2(4.1f, 3f);
        [Tooltip("Everywhere anyone can walk outside, the road and the far pavement too (x, z corners): one still shot of it while anyone's on the road. Set by the street's builder.")]
        [SerializeField] private Vector2 _groundsMin = new Vector2(-14f, -19.4f);
        [SerializeField] private Vector2 _groundsMax = new Vector2(10f, 3f);
        [Tooltip("Where the road starts, along the floor (z). Set by the street's builder.")]
        [SerializeField] private float _kerb = -10.3f;
        [Tooltip("Metres of floor the outside's still shots keep round their stretch of street, so anyone at its edge is clear of the screen's edge.")]
        [SerializeField, Min(0f)] private float _outsideMargin = 0.6f;
        [Tooltip("Metres past the building's outer walls at which someone counts as out of it: clear of the doorway. Back in counts from half this, so standing in the doorway doesn't flick the shot. The road counts from the kerb itself, and back on the pavement from half this up from it.")]
        [SerializeField, Min(0f)] private float _outBy = 0.3f;

        private readonly List<SimVector2> _points = new List<SimVector2>();
        private CameraFraming.Outside _outside;
        private readonly List<SimVector2> _people = new List<SimVector2>();
        private Camera _camera;
        private bool _placed;
        private bool _showingWhole;
        private bool _wentWhole;
        private bool _joining;
        private bool _hasArea;
        private SimVector2 _areaMin;
        private SimVector2 _areaMax;
        private Vector3 _goal;
        private float _holdLeft;
        private Vector3 _velocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            transform.rotation = Quaternion.Euler(_tilt, 0f, 0f);
        }

        private void LateUpdate()
        {
            var floor = _root.Services.FloorPlan;
            var walls = new SimVector2(_wallsBeyondRoom, _wallsBeyondRoom);
            var delta = Time.unscaledDeltaTime;
            CollectPeople();
            // The road's line is moved up by the out margin so stepping off the kerb is the road at once:
            // the street's shot then never has to show past the kerb.
            _outside = CameraFraming.WhereOutside(_people, _outside, floor.RoomMin - walls, floor.RoomMax + walls, _kerb + _outBy, _outBy, _outBy / 2f);

            // Nobody's in yet, or the day's done: the whole restaurant.
            var wasShowingWhole = _showingWhole;
            _showingWhole = PlayerInput.all.Count == 0 || _root.Services.Days.Phase == DayPhase.Review;
            if (wasShowingWhole && !_showingWhole)
                _joining = true;
            float top;
            System.Numerics.Vector3 wanted;
            if (_showingWhole)
            {
                UseWhole(floor);
                top = _screenTopForWhole;
                wanted = Fit(floor, walls, _minHeight, _maxHeight, top, _wholeFarMargin);
            }
            else if (_outside != CameraFraming.Outside.No)
            {
                // Out of the building: one still shot of where they've gone, whatever they do there,
                // and one step further out for the road. It used to grow the kitchen's shot after them,
                // creeping out with every step, then frame them alone a metre onto the road, which with
                // one chef dropped it from 15 m to its closest (the bug, 2026-11-14). Coming back in, the
                // dining room's whole-restaurant shot takes over with its own doorway margins.
                wanted = _outside == CameraFraming.Outside.Road
                    ? StillShot(_groundsMin, _groundsMax)
                    : StillShot(_streetMin, _streetMax);
                _wentWhole = true;
            }
            else
            {
                var someoneOut = UseKitchenAndPeople(floor, walls);
                top = TopFor(floor, walls);
                wanted = Fit(floor, walls, _minHeight, _maxHeight, top);

                // Someone's left the kitchen and all of it isn't much more: all of it, centred and
                // still, and the move there starts at once, rather than creeping after them.
                if (someoneOut)
                {
                    var kitchen = KitchenHeight(floor, walls);
                    UseWhole(floor);
                    var whole = Fit(floor, walls, _minHeight, _maxHeight, _screenTopForWhole, _wholeFarMargin);
                    _wentWhole = CameraFraming.ShowWhole(kitchen, whole.Y, _maxHeight, _wholeWithin);
                    if (_wentWhole)
                    {
                        top = _screenTopForWhole;
                        wanted = whole;
                    }
                    else
                    {
                        UseKitchenAndPeople(floor, walls);
                    }
                }
                else
                {
                    _wentWhole = false;
                }
            }

            var wantedAt = new Vector3(wanted.X, wanted.Y, wanted.Z);
            if (!_placed)
            {
                transform.position = wantedAt;
                _goal = wantedAt;
                _placed = true;
                return;
            }

            // Backs away at once, and closes in at once too unless told to wait first. The doorway's
            // in-and-out margins keep walking in and out from making it pump. While it waits, it
            // doesn't move at all: sliding across first and dropping later is what made it hook.
            if (wantedAt.y >= _goal.y)
            {
                _goal = wantedAt;
                _holdLeft = _holdBeforeClosingIn;
            }
            else
            {
                _holdLeft -= delta;
                if (_holdLeft <= 0f)
                    _goal = wantedAt;
            }

            // Height and floor position are eased as one point in space, so they start and arrive
            // together and a change of shot is one arc rather than a slide followed by a zoom. Eased
            // once (user's call): it moves off the moment the shot changes, quickest at the start, and
            // settles into the new shot without passing it. A two-stage S-curve felt late every time.
            var at = transform.position;
            // The quick move in after joining lasts until it arrives, or until it has to back away.
            if (_goal.y > at.y || (_goal - at).sqrMagnitude < 0.0025f)
                _joining = false;
            var eased = Vector3.SmoothDamp(at, _goal, ref _velocity, ZoomSeconds(_goal.y, at.y), _maxSpeed, delta);

            // Someone walking out of the shot can still outrun the easing. Then it hurries along the arc
            // it's on, which leads to a shot that shows them, and goes on easing from there towards the
            // same shot, never past it. Under that sits a hard limit, for anyone that arc doesn't cover:
            // it's pushed at once so nobody leaves the screen, and held there while it's needed.
            var hurried = CameraFraming.HurryAlong(_people, ToSim(eased), ToSim(_goal), _tilt, _camera.fieldOfView, _camera.aspect, _edgeMargin, _maxHeight, _edgeScreenTop, _edgeScreenBottom);
            var kept = CameraFraming.KeepInView(_people, hurried, _tilt, _camera.fieldOfView, _camera.aspect, _edgeMargin, _maxHeight, _edgeScreenTop, _edgeScreenBottom);
            if (kept != hurried)
                _velocity = Vector3.zero;
            transform.position = new Vector3(kept.X, kept.Y, kept.Z);
        }

        private static System.Numerics.Vector3 ToSim(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);

        private System.Numerics.Vector3 Fit(Game.Data.FloorPlanConfig floor, SimVector2 walls, float lowest, float highest, float top, float? farMargin = null) =>
            CameraFraming.Fit(_points, _tilt, _camera.fieldOfView, _camera.aspect, _margin, lowest, highest, (farMargin ?? _farMargin) - _margin, _nearMargin, top, _screenBottom,
                floor.RoomMin - walls, floor.RoomMax + walls,
                _hasArea ? _areaMin : (SimVector2?)null, _hasArea ? _areaMax : (SimVector2?)null);

        private System.Numerics.Vector3 StillShot(Vector2 min, Vector2 max) =>
            CameraFraming.StillShot(new SimVector2(min.x, min.y), new SimVector2(max.x, max.y), _outsideMargin, _tilt, _camera.fieldOfView, _camera.aspect,
                _minHeight, _maxHeight, _screenTopForWhole, _screenBottom);

        private float ZoomSeconds(float to, float from) => to > from ? _zoomOutSeconds : _joining ? _joinSeconds : _zoomInSeconds;

        // The further out it has to go, the less room it leaves at the top. Judged by the height
        // needed at the usual room, so the answer doesn't change the question.
        private float TopFor(Game.Data.FloorPlanConfig floor, SimVector2 walls) =>
            Mathf.Lerp(_screenTop, _screenTopFarApart, Mathf.InverseLerp(_minHeight, _farApartHeight, Fit(floor, walls, _minHeight, _maxHeight, _screenTop).Y));

        private void UseWhole(Game.Data.FloorPlanConfig floor)
        {
            _hasArea = false;
            _points.Clear();
            _points.Add(floor.RoomMin);
            _points.Add(floor.RoomMax);
        }

        private (SimVector2 Min, SimVector2 Max) KitchenShot(Game.Data.FloorPlanConfig floor, SimVector2 walls) =>
            CameraFraming.KitchenShot(floor.KitchenAreaMin, floor.KitchenAreaMax, _frontOfKitchen, _kitchenSides, _kitchenBehind, floor.RoomMin - walls, floor.RoomMax + walls);

        // The kitchen's shot alone, however far anyone is, with the room at the top it has alone. Not
        // the room it would leave with someone far off: that shrinks as they walk away, which flipped
        // the choice between the whole restaurant and following them as they crossed the dining room.
        private float KitchenHeight(Game.Data.FloorPlanConfig floor, SimVector2 walls)
        {
            var shot = KitchenShot(floor, walls);
            _points.Clear();
            (_hasArea, _areaMin, _areaMax) = (true, shot.Min, shot.Max);
            return Fit(floor, walls, _minHeight, _maxHeight, TopFor(floor, walls)).Y;
        }

        // The kitchen's shot and anyone outside it, or everyone alone if keeping the kitchen in would
        // take it too high, as in a big restaurant with someone far off. Anyone inside the shot shows
        // already, so they don't count: moving about in there never moves the camera. Says whether
        // anyone has left the kitchen.
        private bool UseKitchenAndPeople(Game.Data.FloorPlanConfig floor, SimVector2 walls)
        {
            var shot = KitchenShot(floor, walls);
            var someoneOut = false;
            _points.Clear();
            (_hasArea, _areaMin, _areaMax) = (true, shot.Min, shot.Max);
            foreach (var person in _people)
            {
                // Back in only a little further in than out, so standing in the doorway doesn't flick it.
                if (!CameraFraming.Inside(person, floor.KitchenAreaMin, floor.KitchenAreaMax, _wentWhole ? _kitchenSides / 2f : _kitchenSides))
                    someoneOut = true;
                if (!CameraFraming.Inside(person, shot.Min, shot.Max))
                    _points.Add(person);
            }
            if (Fit(floor, walls, _minHeight, _maxHeight, _screenTop).Y <= _mostKeepingKitchen)
                return someoneOut;

            _hasArea = false;
            _points.Clear();
            _points.AddRange(_people);
            return someoneOut;
        }

        private void CollectPeople()
        {
            _people.Clear();
            // A player who's managing is wherever their cursor is, not where they left their chef:
            // rearranging the kitchen from the dining room frames the kitchen. The chef may go off
            // screen meanwhile, but it stands still, so nothing's missed.
            foreach (var player in PlayerInput.all)
                if (!_editor.IsManaging(player))
                    _people.Add(Floor(player.transform.position));
            foreach (var cursor in _editor.Cursors)
                _people.Add(cursor.Held != null ? cursor.Landing : cursor.Position);
            foreach (var agent in _staff.Agents)
                _people.Add(Floor(agent.transform.position));
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
