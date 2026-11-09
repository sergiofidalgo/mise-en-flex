using System.Collections.Generic;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The office's camera (spec: docs/office-scene.md §2). Before anyone joins it holds a wide shot
    /// of the whole floor; once someone has, it follows the players at the kitchen's tilt, zooming
    /// out as they spread apart, and never shows past the floor's edges more than it must.
    /// A simpler cousin of the kitchen's FramingCamera, which is tied to the kitchen's systems.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class OfficeCamera : MonoBehaviour
    {
        [Tooltip("Degrees the camera looks down. The kitchen uses 55.")]
        [SerializeField, Range(10f, 89f)] private float _tilt = 55f;
        [Tooltip("The floor's corners on the ground (X, Z), for the wide shot and for keeping the view on the building.")]
        [SerializeField] private Vector2 _floorMin = new Vector2(-12f, -9f);
        [SerializeField] private Vector2 _floorMax = new Vector2(12f, 14f);
        [Tooltip("Metres of floor kept around the players.")]
        [SerializeField, Min(0f)] private float _margin = 3f;
        [Tooltip("Closest the camera comes, in metres up: keeps a chef from filling the screen.")]
        [SerializeField, Min(1f)] private float _minHeight = 10f;
        [Tooltip("Farthest it goes while following. At the kitchen's 100 px chef rule, about 22 m.")]
        [SerializeField, Min(1f)] private float _maxHeight = 22f;
        [Tooltip("Farthest it goes for the wide shot of the whole floor.")]
        [SerializeField, Min(1f)] private float _wideMaxHeight = 60f;
        [Tooltip("Metres kept around the whole floor in the wide shot, so the terrace and helipad aren't cropped.")]
        [SerializeField, Min(0f)] private float _wideMargin = 2f;
        [Tooltip("Share of the screen kept clear at the top for heads and hints.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTop = 0.15f;
        [Tooltip("Seconds to most of the way to a new framing.")]
        [SerializeField, Min(0.01f)] private float _ease = 0.5f;

        private readonly List<SimVector2> _points = new List<SimVector2>();
        private Camera _camera;
        private Vector3 _velocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            transform.rotation = Quaternion.Euler(_tilt, 0f, 0f);
            transform.position = Wanted();
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(_tilt, 0f, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, Wanted(), ref _velocity, _ease);
        }

        /// <summary>Where the camera should be now: on the players, or on the whole floor before anyone joins.</summary>
        public Vector3 Wanted()
        {
            _points.Clear();
            foreach (var player in PlayerInput.all)
                _points.Add(new SimVector2(player.transform.position.x, player.transform.position.z));

            var min = new SimVector2(_floorMin.x, _floorMin.y);
            var max = new SimVector2(_floorMax.x, _floorMax.y);
            var aspect = _camera != null ? _camera.aspect : 16f / 9f;
            var fov = _camera != null ? _camera.fieldOfView : 45f;
            System.Numerics.Vector3 at;
            if (_points.Count == 0)
            {
                _points.Add(min);
                _points.Add(max);
                at = CameraFraming.Fit(_points, _tilt, fov, aspect, _wideMargin, _minHeight, _wideMaxHeight, screenTop: 0.04f);
            }
            else
            {
                at = CameraFraming.Fit(_points, _tilt, fov, aspect, _margin, _minHeight, _maxHeight,
                    screenTop: _screenTop, buildingMin: min, buildingMax: max);
            }
            return new Vector3(at.X, at.Y, at.Z);
        }
    }
}
