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
    /// Follows the people doing the work: keeps every player and staff member in view, zooming out as
    /// they spread apart and in as they gather. To see all of a big restaurant, players have to split up.
    /// As a player walks away from the kitchen it also eases back, so there's more to see out there.
    /// Customers don't count: those out of view get markers at the edge of the screen. Before anyone joins,
    /// and during the Review, it shows the whole restaurant. Its moves build up and settle gently, and it
    /// waits for people to settle before closing in.
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
        [Tooltip("Share of the screen to leave clear at the top, for heads, furniture and the prompts above them.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTop = 0.27f;
        [Tooltip("The same with people as far apart as they get. Everything's smaller out there, so the floor needs the screen more, and a prompt still fits.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTopFarApart = 0.2f;
        [Tooltip("How high it would have to go, at the usual room at the top, to count as far apart.")]
        [SerializeField, Min(1f)] private float _farApartHeight = 12f;
        [Tooltip("The same when showing the whole restaurant: nobody's cooking, so no prompts need the room.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTopForWhole = 0.14f;
        [Tooltip("Share of the screen to leave clear at the bottom.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenBottom = 0.06f;
        [Tooltip("The closest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _minHeight = 6.5f;
        [Tooltip("Metres further back for each metre a player is from the kitchen.")]
        [SerializeField, Min(0f)] private float _backPerMetreAway = 0.45f;
        [Tooltip("The most it backs away for that, in metres.")]
        [SerializeField, Min(0f)] private float _mostBack = 2f;
        [Tooltip("The furthest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _maxHeight = 40f;
        [Tooltip("How far the building's outer walls are from where people can stand, in metres.")]
        [SerializeField, Min(0f)] private float _wallsBeyondRoom = 0.5f;
        [Tooltip("Roughly how many seconds it trails behind someone walking. Quicker than zooming: a chef walks fast, and the longer this is the closer they get to the edge.")]
        [SerializeField, Min(0.01f)] private float _followSeconds = 0.3f;
        [Tooltip("Roughly how many seconds it takes to back away to fit someone in.")]
        [SerializeField, Min(0.01f)] private float _zoomOutSeconds = 1.2f;
        [Tooltip("Roughly how many seconds it takes to close in again.")]
        [SerializeField, Min(0.01f)] private float _zoomInSeconds = 2f;
        [Tooltip("Seconds everyone has to stay put before it starts closing in.")]
        [SerializeField, Min(0f)] private float _holdBeforeClosingIn = 0.75f;
        [Tooltip("The fastest it moves across the floor, in metres per second. Above a chef's walking speed, or they'd outrun it.")]
        [SerializeField, Min(0.1f)] private float _maxSpeed = 6f;
        [Tooltip("The fastest it moves up and down, in metres per second.")]
        [SerializeField, Min(0.1f)] private float _maxZoomSpeed = 2f;

        private readonly List<SimVector2> _points = new List<SimVector2>();
        private Camera _camera;
        private bool _placed;
        private bool _showingWhole;
        private float _heightGoal;
        private float _holdLeft;
        private float _easedHeight;
        private float _easedHeightVelocity;
        private float _heightVelocity;
        private Vector2 _easedAlong;
        private Vector2 _easedAlongVelocity;
        private Vector2 _alongVelocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            transform.rotation = Quaternion.Euler(_tilt, 0f, 0f);
        }

        private void LateUpdate()
        {
            CollectPoints();
            var floor = _root.Services.FloorPlan;
            var walls = new SimVector2(_wallsBeyondRoom, _wallsBeyondRoom);
            var delta = Time.unscaledDeltaTime;
            var closest = ClosestHeight(floor);

            // The further out it has to go, the less room it leaves at the top. Judged by the height
            // needed at the usual room, so the answer doesn't change the question.
            var top = _showingWhole ? _screenTopForWhole : Mathf.Lerp(_screenTop, _screenTopFarApart, Mathf.InverseLerp(_minHeight, _farApartHeight, Fit(floor, walls, closest, _maxHeight, _screenTop).Y));

            var wanted = Fit(floor, walls, closest, _maxHeight, top);

            if (!_placed)
            {
                transform.position = new Vector3(wanted.X, wanted.Y, wanted.Z);
                _easedAlong = new Vector2(wanted.X, wanted.Z);
                _easedHeight = wanted.Y;
                _heightGoal = wanted.Y;
                _placed = true;
                return;
            }

            // Backs away at once, but only closes in once they've settled, so it isn't seen to react
            // to someone coming back, and walking in and out doesn't make it pump.
            if (wanted.Y >= _heightGoal)
            {
                _heightGoal = wanted.Y;
                _holdLeft = _holdBeforeClosingIn;
            }
            else
            {
                _holdLeft -= delta;
                if (_holdLeft <= 0f)
                    _heightGoal = wanted.Y;
            }

            // Everything is eased twice, each half as long: one easing starts moving the moment anything
            // changes, while two build up and settle in a soft S-curve, with no visible start or stop.
            var at = transform.position;
            _easedHeight = Mathf.SmoothDamp(_easedHeight, _heightGoal, ref _easedHeightVelocity, ZoomSeconds(_heightGoal, _easedHeight) / 2f, _maxZoomSpeed, delta);
            var height = Mathf.SmoothDamp(at.y, _easedHeight, ref _heightVelocity, ZoomSeconds(_easedHeight, at.y) / 2f, _maxZoomSpeed, delta);

            // Where to be on the floor at the height it's actually at. The floor position follows
            // quickly and the height slowly, so aiming for the final height's position would slide the
            // view across the floor long before the zoom got there.
            var framed = Fit(floor, walls, height, height, top);
            _easedAlong = Vector2.SmoothDamp(_easedAlong, new Vector2(framed.X, framed.Z), ref _easedAlongVelocity, _followSeconds / 2f, _maxSpeed, delta);
            var along = Vector2.SmoothDamp(new Vector2(at.x, at.z), _easedAlong, ref _alongVelocity, _followSeconds / 2f, _maxSpeed, delta);
            transform.position = new Vector3(along.x, height, along.y);
        }

        private System.Numerics.Vector3 Fit(Game.Data.FloorPlanConfig floor, SimVector2 walls, float lowest, float highest, float top) =>
            CameraFraming.Fit(_points, _tilt, _camera.fieldOfView, _camera.aspect, _margin, lowest, highest, _farMargin - _margin, _nearMargin, top, _screenBottom, floor.RoomMin - walls, floor.RoomMax + walls);

        private float ZoomSeconds(float to, float from) => to > from ? _zoomOutSeconds : _zoomInSeconds;

        // Measured from the kitchen, whichever player is furthest from it.
        private float ClosestHeight(Game.Data.FloorPlanConfig floor)
        {
            var furthest = 0f;
            foreach (var player in PlayerInput.all)
                furthest = Mathf.Max(furthest, CameraFraming.DistanceOutside(Floor(player.transform.position), floor.KitchenAreaMin, floor.KitchenAreaMax));
            return CameraFraming.ClosestHeight(furthest, _minHeight, _backPerMetreAway, _mostBack);
        }

        private void CollectPoints()
        {
            _points.Clear();
            var plan = _root.Services.FloorPlan;

            // Nobody's in yet, or the day's done: the whole restaurant.
            _showingWhole = PlayerInput.all.Count == 0 || _root.Services.Days.Phase == DayPhase.Review;
            if (_showingWhole)
            {
                _points.Add(plan.RoomMin);
                _points.Add(plan.RoomMax);
                return;
            }

            foreach (var player in PlayerInput.all)
                _points.Add(Floor(player.transform.position));
            foreach (var cursor in _editor.Cursors)
                _points.Add(cursor.Held != null ? cursor.Landing : cursor.Position);
            foreach (var agent in _staff.Agents)
                _points.Add(Floor(agent.transform.position));
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
