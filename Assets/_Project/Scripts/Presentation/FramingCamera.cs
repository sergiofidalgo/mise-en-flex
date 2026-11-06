using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Presentation
{
    /// <summary>
    /// Follows the action: keeps every player and customer in view, zooming out as they spread apart
    /// and in as they gather. During service the street door counts too, so someone walking in is
    /// already on screen and doesn't make it jump. Its moves ease in and out, and it ignores small
    /// changes, so it doesn't chase every step someone takes. It backs away sooner than it closes in,
    /// so nobody leaves the screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FramingCamera : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private CustomerDirector _customers;
        [SerializeField] private LayoutEditor _editor;
        [SerializeField] private StreetDoor _door;

        [Tooltip("How far the camera looks down from the horizon, in degrees.")]
        [SerializeField, Range(10f, 89f)] private float _tilt = 55f;
        [Tooltip("Metres of floor to keep at the sides of everyone.")]
        [SerializeField, Min(0f)] private float _margin = 2f;
        [Tooltip("Metres of floor to keep beyond the farthest. Little: the share of the screen below gives the room.")]
        [SerializeField, Min(0f)] private float _farMargin = 0.7f;
        [Tooltip("Metres of floor to keep before the nearest. Little: nothing stands below them on screen.")]
        [SerializeField, Min(0f)] private float _nearMargin = 0.55f;
        [Tooltip("Share of the screen to leave clear at the top, for heads, furniture and the prompts above them.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTop = 0.2f;
        [Tooltip("Share of the screen to leave clear at the bottom.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenBottom = 0.015f;
        [Tooltip("The closest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _minHeight = 10f;
        [Tooltip("The furthest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _maxHeight = 40f;
        [Tooltip("How far the building's outer walls are from where people can stand, in metres.")]
        [SerializeField, Min(0f)] private float _wallsBeyondRoom = 0.5f;
        [Tooltip("Roughly how many seconds it trails behind someone walking. Quicker than zooming: a chef walks fast, and the longer this is the closer they get to the edge.")]
        [SerializeField, Min(0.01f)] private float _followSeconds = 0.7f;
        [Tooltip("Roughly how many seconds it takes to back away to fit someone in.")]
        [SerializeField, Min(0.01f)] private float _zoomOutSeconds = 1f;
        [Tooltip("Roughly how many seconds it takes to close in again. Slower, so it doesn't pump.")]
        [SerializeField, Min(0.01f)] private float _zoomInSeconds = 2f;
        [Tooltip("The fastest it ever moves, in metres per second.")]
        [SerializeField, Min(0.1f)] private float _maxSpeed = 8f;
        [Tooltip("How far where it should be has to drift, in metres, before it bothers moving.")]
        [SerializeField, Min(0f)] private float _settleDistance = 1f;

        private readonly List<SimVector2> _points = new List<SimVector2>();
        private Camera _camera;
        private bool _placed;
        private Vector3 _goal;
        private Vector2 _panVelocity;
        private float _zoomVelocity;

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
            var fit = CameraFraming.Fit(_points, _tilt, _camera.fieldOfView, _camera.aspect, _margin, _minHeight, _maxHeight, _farMargin - _margin, _nearMargin, _screenTop, _screenBottom, floor.RoomMin - walls, floor.RoomMax + walls);
            var target = new Vector3(fit.X, fit.Y, fit.Z);

            if (!_placed)
            {
                transform.position = target;
                _goal = target;
                _placed = true;
                return;
            }

            // Small changes don't count, or it would follow a chef's every step. Needing to fit
            // someone in always counts, though, so nobody drifts off the edge waiting.
            if ((target - _goal).magnitude > _settleDistance || target.y > _goal.y)
                _goal = target;

            // SmoothDamp starts and stops gently, where easing towards a target starts at full speed.
            // Across the floor it follows closely; up and down it takes its time, as that's what's noticed.
            var at = transform.position;
            var delta = Time.unscaledDeltaTime;
            var along = Vector2.SmoothDamp(new Vector2(at.x, at.z), new Vector2(_goal.x, _goal.z), ref _panVelocity, _followSeconds, _maxSpeed, delta);
            var height = Mathf.SmoothDamp(at.y, _goal.y, ref _zoomVelocity, _goal.y > at.y ? _zoomOutSeconds : _zoomInSeconds, _maxSpeed, delta);
            transform.position = new Vector3(along.x, height, along.y);
        }

        private void CollectPoints()
        {
            _points.Clear();
            foreach (var player in PlayerInput.all)
                _points.Add(Floor(player.transform.position));
            foreach (var cursor in _editor.Cursors)
                _points.Add(cursor.Held != null ? cursor.Landing : cursor.Position);
            foreach (var agent in _customers.Agents.Values)
                _points.Add(Floor(agent.transform.position));
            if (_points.Count > 0 && _root.Services.Days.Phase == DayPhase.Service)
                _points.Add(Floor(_door.transform.position));

            // Nobody in yet: the kitchen they're about to join.
            if (_points.Count == 0)
            {
                var plan = _root.Services.FloorPlan;
                _points.Add(plan.KitchenAreaMin);
                _points.Add(plan.KitchenAreaMax);
            }
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
