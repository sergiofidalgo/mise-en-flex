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
    /// looks slightly ahead of where the players are going, as a person operating it would.
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
        [Tooltip("Roughly how many seconds it takes to close in again. Slower, so it doesn't pump.")]
        [SerializeField, Min(0.01f)] private float _zoomInSeconds = 2f;
        [Tooltip("The fastest it ever moves, in metres per second.")]
        [SerializeField, Min(0.1f)] private float _maxSpeed = 14f;
        [Tooltip("Seconds ahead of the players it looks while they walk.")]
        [SerializeField, Min(0f)] private float _leadSeconds = 0.2f;
        [Tooltip("The furthest ahead it looks, in metres.")]
        [SerializeField, Min(0f)] private float _mostLead = 0.8f;
        [Tooltip("How quickly that follows a change of direction, per second.")]
        [SerializeField, Min(0.01f)] private float _leadRate = 3f;

        private readonly List<SimVector2> _points = new List<SimVector2>();
        private Camera _camera;
        private bool _placed;
        private bool _showingWhole;
        private Vector2 _panVelocity;
        private float _zoomVelocity;
        private Vector3 _eased;
        private Vector2 _aimVelocity;
        private float _aimZoomVelocity;
        private Vector3 _lead;
        private Vector3 _lastMiddle;
        private int _lastCount;

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
            var fit = CameraFraming.Fit(_points, _tilt, _camera.fieldOfView, _camera.aspect, _margin, ClosestHeight(floor), _maxHeight, _farMargin - _margin, _nearMargin, _showingWhole ? _screenTopForWhole : _screenTop, _screenBottom, floor.RoomMin - walls, floor.RoomMax + walls);
            var target = new Vector3(fit.X, fit.Y, fit.Z);

            // A little ahead of where they're heading, as someone operating a camera would.
            var delta = Time.unscaledDeltaTime;
            target += Lead(delta);

            if (!_placed)
            {
                transform.position = target;
                _eased = target;
                _placed = true;
                return;
            }

            // Eased twice, each half as long: one easing starts moving the moment anything changes,
            // while two build up and settle in a soft S-curve, with no visible start or stop. Across the
            // floor it follows closely; up and down it takes its time, as that's what's noticed.
            var at = transform.position;
            var aiming = Vector2.SmoothDamp(new Vector2(_eased.x, _eased.z), new Vector2(target.x, target.z), ref _aimVelocity, _followSeconds / 2f, _maxSpeed, delta);
            var aimingHeight = Mathf.SmoothDamp(_eased.y, target.y, ref _aimZoomVelocity, ZoomSeconds(target.y, _eased.y) / 2f, _maxSpeed, delta);
            _eased = new Vector3(aiming.x, aimingHeight, aiming.y);

            var along = Vector2.SmoothDamp(new Vector2(at.x, at.z), aiming, ref _panVelocity, _followSeconds / 2f, _maxSpeed, delta);
            var height = Mathf.SmoothDamp(at.y, aimingHeight, ref _zoomVelocity, ZoomSeconds(aimingHeight, at.y) / 2f, _maxSpeed, delta);
            transform.position = new Vector3(along.x, height, along.y);
        }

        private float ZoomSeconds(float to, float from) => to > from ? _zoomOutSeconds : _zoomInSeconds;

        // Where the players are heading, eased so it doesn't jerk when they change direction, and small
        // enough not to change the framing. Nothing while the whole restaurant's showing.
        private Vector3 Lead(float delta)
        {
            var count = PlayerInput.all.Count;
            if (count == 0 || delta <= 0f)
                return Vector3.zero;

            var sum = Vector3.zero;
            foreach (var player in PlayerInput.all)
                sum += player.transform.position;
            var middle = sum / count;
            var velocity = count == _lastCount ? (middle - _lastMiddle) / delta : Vector3.zero;
            _lastMiddle = middle;
            _lastCount = count;

            velocity.y = 0f;
            var wanted = _showingWhole ? Vector3.zero : Vector3.ClampMagnitude(velocity * _leadSeconds, _mostLead);
            _lead = Vector3.Lerp(_lead, wanted, 1f - Mathf.Exp(-_leadRate * delta));
            return _lead;
        }

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
