using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Presentation
{
    /// <summary>
    /// Follows the action: keeps every player and customer in view, zooming out as they spread apart
    /// and in as they gather. It backs away faster than it closes in, so nobody leaves the screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FramingCamera : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private CustomerDirector _customers;
        [SerializeField] private LayoutEditor _editor;

        [Tooltip("How far the camera looks down from the horizon, in degrees.")]
        [SerializeField, Range(10f, 89f)] private float _tilt = 55f;
        [Tooltip("Metres of floor to keep around everyone.")]
        [SerializeField, Min(0f)] private float _margin = 2f;
        [Tooltip("The closest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _minHeight = 8f;
        [Tooltip("The furthest it gets, in metres above the floor.")]
        [SerializeField, Min(1f)] private float _maxHeight = 40f;
        [Tooltip("How quickly it backs away to fit someone in, per second.")]
        [SerializeField, Min(0.01f)] private float _zoomOutRate = 3f;
        [Tooltip("How quickly it closes in again, per second. Slower, so it doesn't pump.")]
        [SerializeField, Min(0.01f)] private float _zoomInRate = 1f;

        private readonly List<SimVector2> _points = new List<SimVector2>();
        private Camera _camera;
        private bool _placed;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            transform.rotation = Quaternion.Euler(_tilt, 0f, 0f);
        }

        private void LateUpdate()
        {
            CollectPoints();
            var fit = CameraFraming.Fit(_points, _tilt, _camera.fieldOfView, _camera.aspect, _margin, _minHeight, _maxHeight);
            var target = new Vector3(fit.X, fit.Y, fit.Z);

            if (!_placed)
            {
                transform.position = target;
                _placed = true;
                return;
            }

            // Eases the same whatever the frame rate.
            var rate = target.y > transform.position.y ? _zoomOutRate : _zoomInRate;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime));
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
