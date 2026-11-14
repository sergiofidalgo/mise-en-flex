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
    /// While anyone's on the terrace it pulls back to a still shot of it and the rooftops past the rail,
    /// and closes in again when they're all back inside, with the kitchen's timings.
    /// With anyone next to a side window, inside or on the terrace, it leans a few metres that way.
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
        [Tooltip("Farthest it goes while following: far enough that two players at opposite corners of the floor both " +
                 "stay in view (user's note, 2026-11-09). The office has no leash, unlike the kitchen, so the kitchen's " +
                 "100 px chef rule (about 22 m) gives way here when players split up.")]
        [SerializeField, Min(1f)] private float _maxHeight = 55f;
        [Tooltip("Farthest it goes for the wide shot of the whole floor.")]
        [SerializeField, Min(1f)] private float _wideMaxHeight = 60f;
        [Tooltip("Metres kept around the whole floor in the wide shot, so the terrace and helipad aren't cropped.")]
        [SerializeField, Min(0f)] private float _wideMargin = 2f;
        [Tooltip("Share of the screen kept clear at the top for heads and hints.")]
        [SerializeField, Range(0f, 0.45f)] private float _screenTop = 0.15f;
        [Tooltip("Seconds to most of the way to a new framing.")]
        [SerializeField, Min(0.01f)] private float _ease = 0.5f;

        [Header("Terrace shot")]
        [Tooltip("The terrace's corners on the ground (X, Z). While anyone's on it, the camera pulls back over the rooftops (user's request, 2026-11-10).")]
        [SerializeField] private Vector2 _terraceMin = new Vector2(-17.5f, 13.5f);
        [SerializeField] private Vector2 _terraceMax = new Vector2(17.5f, 19.5f);
        [Tooltip("Metres of ground the terrace shot shows past the rail, over the near rooftops.")]
        [SerializeField, Min(0f)] private float _beyondRail = 20f;
        [Tooltip("Metres either side of the terrace's edge that keep the shot as it was, so standing in the doorway doesn't flip it.")]
        [SerializeField, Min(0f)] private float _doorwaySlack = 0.5f;
        [Tooltip("Smooth time pulling back to the terrace shot: mostly there in about twice this. The kitchen's 0.45 (decision 2026-11-08).")]
        [SerializeField, Min(0.01f)] private float _zoomOutSeconds = 0.45f;
        [Tooltip("Smooth time closing in again when everyone's back inside. The kitchen's 0.7.")]
        [SerializeField, Min(0.01f)] private float _zoomInSeconds = 0.7f;
        [Tooltip("Metres from its goal at which closing in counts as done, and the usual follow ease takes over.")]
        [SerializeField, Min(0.01f)] private float _settledWithin = 0.3f;

        [Header("Leaning to a side window")]
        [Tooltip("Metres from the west or east glass (or the terrace's side rail) that count as next to it (user's request, 2026-11-14).")]
        [SerializeField, Min(0f)] private float _windowZone = 2f;
        [Tooltip("Metres either side of that line that keep the lean as it was, so standing on it doesn't flicker.")]
        [SerializeField, Min(0f)] private float _windowSlack = 0.5f;
        [Tooltip("Metres the shot slides towards the window inside, past the wall, without zooming.")]
        [SerializeField, Min(0f)] private float _insideLean = 4f;
        [Tooltip("The same on the terrace, whose shot is wider.")]
        [SerializeField, Min(0f)] private float _terraceLean = 5f;
        [Tooltip("Metres of floor a lean always keeps round every player: it leans less rather than lose anyone.")]
        [SerializeField, Min(0f)] private float _leanKeepMargin = 1f;

        private readonly List<SimVector2> _points = new List<SimVector2>();
        private Camera _camera;
        private Vector3 _velocity;
        private bool _onTerrace;
        private bool _closingIn;
        private bool _leaningOut;
        private int _lean;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            transform.rotation = Quaternion.Euler(_tilt, 0f, 0f);
            transform.position = Wanted();
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(_tilt, 0f, 0f);
            var wasOnTerrace = _onTerrace;
            var wasLeaning = _lean;
            var wanted = Wanted();
            if (wasOnTerrace && !_onTerrace)
                _closingIn = true;
            // Leaning towards a window is a move out, with the zoom-out's timing; leaning back is a
            // move in, with the closing-in timing. Going straight from one side to the other is out.
            if (_lean != wasLeaning)
            {
                _leaningOut = _lean != 0;
                _closingIn |= _lean == 0;
            }
            var settled = Vector3.Distance(transform.position, wanted) < _settledWithin;
            if (settled)
                _closingIn = _leaningOut = false;
            // One eased arc, height and ground position together, starting at once (as the kitchen's).
            // Switching smooth time keeps the velocity, so there's no jolt where one ease hands over.
            var ease = _onTerrace || _leaningOut ? _zoomOutSeconds : _closingIn ? _zoomInSeconds : _ease;
            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref _velocity, ease);
        }

        /// <summary>Where the camera should be now: on the players, on the terrace and the roofs past it while anyone's out there, or on the whole floor before anyone joins; leaning towards a side window anyone's next to.</summary>
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
            var terraceMin = new SimVector2(_terraceMin.x, _terraceMin.y);
            var terraceMax = new SimVector2(_terraceMax.x, _terraceMax.y);
            _onTerrace = _points.Count > 0 && TerraceFraming.AnyoneOnTerrace(_points, terraceMin, terraceMax, _onTerrace, _doorwaySlack);
            if (_onTerrace)
            {
                at = TerraceFraming.Fit(_points, terraceMin, terraceMax, _beyondRail, _doorwaySlack, _tilt, fov, aspect, _margin,
                    _minHeight, _maxHeight, _screenTop);
            }
            else if (_points.Count == 0)
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

            // Someone by a side window: slide that way, past the wall, to show more of the city.
            _lean = _points.Count > 0 && PlayerInput.all.Count > 0
                ? WindowLean.Side(_points, _floorMin.x, _floorMax.x, _windowZone, _windowSlack, _lean)
                : 0;
            at = WindowLean.Apply(at, _lean, _onTerrace ? _terraceLean : _insideLean, _points, _tilt, fov, aspect, _leanKeepMargin, _screenTop);
            return new Vector3(at.X, at.Y, at.Z);
        }
    }
}
