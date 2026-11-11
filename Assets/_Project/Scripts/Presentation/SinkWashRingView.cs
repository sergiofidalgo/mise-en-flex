using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// The washing game at the sink, built from real 3D pieces (decisions 2026-11-09: the station
    /// is the interface; then "too flat for the scene"), drawn like the cloth's and mop's ring
    /// (<see cref="CleaningView"/>, decision 2026-11-11: one family): a ring of chunky toon-shaded
    /// segments that turn from grime to water, clockwise from the top, as the plate comes clean,
    /// and a soap bar that follows the stick round, so circling the stick visibly scrubs. The ring
    /// pulses and bubbles rise while the circling washes. At the start, until the stick first
    /// washes, and whenever it stalls, it asks for the spinning stick cue
    /// (<see cref="Hud.StickHintHud"/>, flat HUD art from the control glyph kit). The pieces use
    /// <c>Game/ToonOnTop</c>, drawn over whatever is in front of them (the chef, the tap) in a fixed
    /// order, and sit level with the basin's rim. The plate in the basin tips towards the soap.
    /// Everything turns with the camera, never the sink: pushing the stick up moves the soap, and
    /// dips the plate, up on screen however the sink has been turned in the layout editor.
    /// </summary>
    public sealed class SinkWashRingView : MonoBehaviour, IStickHintSource
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const string PlatePrefix = "DirtyPlate";

        // The kit's own units: the rim's middle at 0.84, the edge of the old flat ring at 1.
        private const float RimRadius = 0.84f;
        private const float RimWidth = 0.19f;
        private const int Segments = 15;           // as the cleaning ring
        private const float SegmentGap = 0.09f;      // share of each segment left open

        // Draw order (sortingOrder): the pieces ignore depth, so later ones cover earlier ones.
        private const int RingOrder = 0;
        private const int SoapOrder = 2;
        private const int BubbleOrder = 3;

        [SerializeField] private SinkStation _sink;
        [Tooltip("The quad over the basin: the kit's anchor and size. It isn't drawn itself.")]
        [SerializeField] private Renderer _ring;
        [Tooltip("Toon material for the kit's pieces; each gets its colour per renderer.")]
        [SerializeField] private Material _toon;

        [Header("Look")]
        [Tooltip("Height of the rim's segments and of the soap's float, in kit units (the rim is 0.84 from the centre).")]
        [SerializeField, Min(0f)] private float _segmentHeight = 0.07f;
        [SerializeField, Min(0f)] private float _soapFloat = 0.1f;
        [SerializeField] private Vector3 _soapSize = new Vector3(0.34f, 0.22f, 0.24f);
        [Tooltip("The soap, idle and while its circling washes.")]
        [SerializeField] private Color _soapIdle = new Color(0.96f, 0.93f, 0.85f);
        [SerializeField] private Color _good = new Color(0.45f, 0.85f, 0.40f);
        [Tooltip("A rim segment still to clean: the plate's grime (the user's call, 2026-11-10).")]
        [SerializeField] private Color _grime = new Color(0.36f, 0.23f, 0.12f);
        [Tooltip("A rim segment cleaned: water. Segments blend from grime to water as the plate cleans.")]
        [SerializeField] private Color _water = new Color(0.33f, 0.66f, 0.92f);

        [Header("Fades")]
        [Tooltip("Seconds to pop in or out when washing starts or stops.")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.18f;
        [Tooltip("Seconds for the rim to catch up with the plate's progress.")]
        [SerializeField, Min(0f)] private float _progressSmoothing = 0.08f;

        [Header("Plate tilt")]
        [Tooltip("Degrees the plate tips with the point at the rim.")]
        [SerializeField, Range(0f, 45f)] private float _maxTilt = 22f;
        [Tooltip("Metres the plate slides towards the point at the rim.")]
        [SerializeField, Min(0f)] private float _maxSlide = 0.05f;
        [Tooltip("How quickly the plate follows the point (per second): high is snappy, low is floaty.")]
        [SerializeField, Min(0.1f)] private float _tiltFollow = 14f;

        [Header("Arcade feedback")]
        [Tooltip("Ring size swing while the soap is held in the centre (share of its size).")]
        [SerializeField, Range(0f, 0.1f)] private float _pulse = 0.025f;
        [SerializeField, Min(0f)] private float _pulseRate = 3f;
        [Tooltip("Extra size at the start of the pop when a plate comes clean.")]
        [SerializeField, Range(0f, 0.5f)] private float _pop = 0.15f;
        [Tooltip("Seconds the plate-done flash and pop last.")]
        [SerializeField, Min(0.01f)] private float _popSeconds = 0.35f;

        [Header("Stick cue (as the cleaning ring's)")]
        [Tooltip("Seconds after washing starts before the stick cue shows; it stays until the stick first washes.")]
        [SerializeField, Min(0f)] private float _openingDelay = 0.15f;
        [Tooltip("Seconds without washing, once started, before the cue shows again.")]
        [SerializeField, Min(0f)] private float _stalledDelay = 0.8f;

        private MaterialPropertyBlock _block;
        // The plate shown in the basin, found among the sink's children (DirtyPlate…): the kit's
        // builder makes it after the sink's water, so it isn't wired in.
        private Transform _plate;

        // The kit: built on first use, outside the scene's saved objects.
        private Transform _root;
        private readonly List<Renderer> _segments = new List<Renderer>();
        private Transform _soap;
        private Renderer _soapRenderer;
        private readonly List<Transform> _bubbles = new List<Transform>();
        private float _hintShown;
        private float _hintAmount;
        private Vector3 _hintWorld;

        /// <summary>0..1: how much the stick hint shows (it fades in when the player seems stuck).</summary>
        public float HintAmount => _root != null && _root.gameObject.activeSelf ? _hintAmount : 0f;

        /// <summary>Where the stick hint goes, in the world: beside the soap.</summary>
        public Vector3 HintWorldPosition => _hintWorld;

        /// <summary>No direction: the cue spins.</summary>
        public Vector3 HintTowardsWorld => Vector3.zero;

        public int HintCount => 1;

        public StickHint HintAt(int index) => new StickHint(HintAmount, _hintWorld, Vector3.zero, StickMotion.Spin);

        // The cue: the game it's for, whether its circling has washed yet, and time without washing.
        private Game.Simulation.Items.WashGame _hintGame;
        private bool _started;
        private float _idle;

        private float _fade;
        private float _progress;
        private float _progressVelocity;
        private float _lastRawProgress;
        private float _popLeft;
        private Vector2 _shownPoint;
        private Vector2 _lastPoint;

        private Vector2 _tiltPoint;
        private float _tiltBlend;
        private bool _drivingPlate;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _writtenPosition;
        private Quaternion _writtenRotation;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _ring.enabled = false;
            if (_plate == null)
                _plate = FindPlate();
        }

        private void OnDestroy()
        {
            if (_root != null)
                DestroyKit();
        }

        private void LateUpdate()
        {
            var game = _sink.Game;
            var washing = game != null && _sink.Washer != null;
            var dt = Time.deltaTime;

            // A plate came clean: progress drops back. The game has usually ended by now, so this
            // runs whether or not anyone is washing.
            var raw = _sink.Sink != null ? _sink.Sink.WashProgress : 0f;
            if (raw < _lastRawProgress - 0.3f)
            {
                _popLeft = _popSeconds;
                _progress = 1f; // the full rim is what flashes
            }
            _lastRawProgress = raw;
            _popLeft = Mathf.Max(0f, _popLeft - dt);
            _fade = Mathf.MoveTowards(_fade, washing ? 1f : 0f, _fadeSeconds > 0f ? dt / _fadeSeconds : 1f);

            var rate = 0f;
            if (washing)
            {
                _shownPoint = new Vector2(game.Point.X, game.Point.Y);
                _tiltPoint = Vector2.Lerp(_tiltPoint, _shownPoint, 1f - Mathf.Exp(-_tiltFollow * dt));
                _progress = Mathf.SmoothDamp(_progress, Mathf.Clamp01(raw), ref _progressVelocity, _progressSmoothing);
                rate = game.OnTrack ? 1f : 0f;
                UpdateCue(game, dt);
            }
            else
            {
                _idle = 0f;
                _hintShown = 0f;
            }
            TiltPlate(washing, dt);

            var visible = Mathf.Max(_fade, _popLeft > 0f ? 1f : 0f);
            if (visible <= 0f)
            {
                if (_root != null)
                    _root.gameObject.SetActive(false);
                return;
            }

            EnsureKit();
            _root.gameObject.SetActive(true);
            var popT = _popLeft / _popSeconds;               // 1 at the start of the pop, 0 at its end
            // Pulses while scrubbing, more the quicker it goes.
            var pulse = washing ? _pulse * Mathf.Sin(Time.time * _pulseRate * 2f * Mathf.PI) * rate : 0f;
            // Pops in with a little overshoot, and out again when washing stops.
            var appear = Mathf.Max(EaseOutBack(_fade), popT > 0f ? 1f : 0f);
            Draw(appear * (1f + pulse + _pop * popT * popT), rate, popT, _hintShown, dt);
        }

        // The stick cue, as on the cleaning ring: from just after washing starts until the circling
        // first washes, then whenever it has stalled a moment.
        private void UpdateCue(Game.Simulation.Items.WashGame game, float dt)
        {
            if (game != _hintGame)
            {
                _hintGame = game;
                _started = false;
                _idle = 0f;
            }
            _idle = game.OnTrack ? 0f : _idle + dt;
            if (game.OnTrack)
                _started = true;
            var want = _started ? _idle > _stalledDelay : _idle > _openingDelay;
            _hintShown = Mathf.MoveTowards(_hintShown, want ? 1f : 0f, dt / 0.15f);
        }

        // Places and colours every piece for this frame. Kit units: the rim at 0.84.
        private void Draw(float scale, float rate, float flash, float hint, float dt)
        {
            FaceCamera();
            _root.position = _ring.transform.position;
            // The quad is the old flat ring, Size across: one kit unit is half of it.
            _root.localScale = Vector3.one * (_ring.transform.lossyScale.x * 0.5f * Mathf.Max(0.001f, scale));

            // The rim fills continuously, clockwise from the top: each segment turns from grime to
            // water as the plate's progress passes through it, like the plate coming clean.
            for (var i = 0; i < _segments.Count; i++)
            {
                var clean = Mathf.Clamp01(_progress * _segments.Count - i);
                var color = Color.Lerp(Color.Lerp(_grime, _water, clean), Color.white, flash);
                Paint(_segments[i], color);
            }
            // The soap follows the stick round the ring, as the cloth does on a table, bobbing and
            // leaning the way it's going; it turns green while its circling washes.
            var velocity = dt > 0f ? (_shownPoint - _lastPoint) / dt : Vector2.zero;
            _lastPoint = _shownPoint;
            var point = _shownPoint * RimRadius;
            var bob = Mathf.Sin(Time.time * 4f) * 0.015f;
            _soap.localPosition = new Vector3(point.x, _segmentHeight + _soapFloat + bob, point.y);
            _soap.localScale = _soapSize;
            var lean = Vector3.ClampMagnitude(new Vector3(velocity.y, 0f, -velocity.x) * 6f, 20f);
            _soap.localRotation = Quaternion.Euler(lean.x, 25f, lean.z);
            Paint(_soapRenderer, Color.Lerp(_soapIdle, _good, rate));

            // Washing well: a few bubbles circle the soap.
            for (var i = 0; i < _bubbles.Count; i++)
            {
                var show = rate > 0.5f;
                _bubbles[i].gameObject.SetActive(show);
                if (!show)
                    continue;
                var angle = Time.time * 3f + i * Mathf.PI * 2f / _bubbles.Count;
                _bubbles[i].localPosition = _soap.localPosition + new Vector3(Mathf.Sin(angle) * 0.3f, 0.05f + 0.04f * Mathf.Sin(Time.time * 5f + i), Mathf.Cos(angle) * 0.3f);
            }

            // The stick cue (StickHintHud, flat HUD art) shows in the centre of the ring, spinning.
            _hintWorld = _root.TransformPoint(new Vector3(0f, _segmentHeight * 0.6f, 0f));
            _hintAmount = hint;
        }

        private void Paint(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.70158f;
            var u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        private void EnsureKit()
        {
            if (_root != null)
                return;
            _block ??= new MaterialPropertyBlock();
            _root = new GameObject($"{name} WashKit") { hideFlags = HideFlags.DontSave }.transform;

            var step = Mathf.PI * 2f / Segments;
            for (var i = 0; i < Segments; i++)
            {
                var from = (i + SegmentGap * 0.5f) * step;
                var to = (i + 1 - SegmentGap * 0.5f) * step;
                _segments.Add(Piece("Segment", StationKitMeshes.RingSector(RimRadius - RimWidth / 2f, RimRadius + RimWidth / 2f, from, to, _segmentHeight, 0.025f), _root, RingOrder));
            }

            _soapRenderer = Primitive("Soap", PrimitiveType.Sphere, _root, SoapOrder);
            _soap = _soapRenderer.transform;
            _soap.localScale = _soapSize;
            for (var i = 0; i < 3; i++)
            {
                var bubble = Primitive("Bubble", PrimitiveType.Sphere, _root, BubbleOrder);
                bubble.transform.localScale = Vector3.one * (0.06f + 0.02f * i);
                Paint(bubble, Color.white);
                _bubbles.Add(bubble.transform);
            }

        }

        private Renderer Piece(string pieceName, Mesh mesh, Transform parent, int order, Material material = null)
        {
            var go = new GameObject(pieceName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            Setup(renderer, order, material);
            return renderer;
        }

        private Renderer Primitive(string pieceName, PrimitiveType type, Transform parent, int order, Material material = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = pieceName;
            go.hideFlags = HideFlags.DontSave;
            if (go.TryGetComponent<Collider>(out var collider))
                DestroyNow(collider);
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<Renderer>();
            Setup(renderer, order, material);
            return renderer;
        }

        private void Setup(Renderer renderer, int order, Material material)
        {
            renderer.sharedMaterial = material != null ? material : _toon;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void DestroyKit()
        {
            foreach (var child in _root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (child.sharedMesh != null && child.sharedMesh.name == "RingSector")
                    DestroyNow(child.sharedMesh);
            }
            DestroyNow(_root.gameObject);
            _root = null;
            _segments.Clear();
            _bubbles.Clear();
        }

        private static void DestroyNow(Object thing)
        {
            if (Application.isPlaying)
                Destroy(thing);
            else
                DestroyImmediate(thing);
        }

        // While washing, the plate levels out and tips towards the point (and slides a little that
        // way), as if the point were where it's being pressed down. Afterwards it eases back to how
        // it landed. If something else moves the plate meanwhile (a new plate landing differently),
        // that becomes the pose to return to.
        private void TiltPlate(bool washing, float dt)
        {
            if (_plate == null || _plate.parent == null)
                return;
            if (!_drivingPlate || _plate.localRotation != _writtenRotation || _plate.localPosition != _writtenPosition)
            {
                _restRotation = _plate.localRotation;
                _restPosition = _plate.localPosition;
            }

            _tiltBlend = Mathf.MoveTowards(_tiltBlend, washing ? 1f : 0f, _fadeSeconds > 0f ? dt / _fadeSeconds : 1f);
            if (_tiltBlend <= 0f)
            {
                _drivingPlate = false;
                return;
            }

            var (rotation, position) = TiltedPose(_tiltPoint);
            _plate.localRotation = Quaternion.Slerp(_restRotation, rotation, _tiltBlend);
            _plate.localPosition = Vector3.Lerp(_restPosition, position, _tiltBlend);
            _writtenRotation = _plate.localRotation;
            _writtenPosition = _plate.localPosition;
            _drivingPlate = true;
        }

        // The plate's pose for a point (1 = rim), in its parent's space: level apart from its
        // landing turn, then tipped so its side towards the point dips.
        private (Quaternion Rotation, Vector3 Position) TiltedPose(Vector2 point)
        {
            var (right, forward) = ScreenAxes();
            var towards = right * point.x + forward * point.y;   // world, on the floor
            var amount = Mathf.Clamp01(point.magnitude);
            var parent = _plate.parent;
            var level = Quaternion.Euler(0f, _restRotation.eulerAngles.y, 0f);
            if (amount < 1e-3f)
                return (level, _restPosition);

            // Turning about (up × towards) carries the plate's up towards the point: that side dips.
            var axis = parent.InverseTransformDirection(Vector3.Cross(Vector3.up, towards.normalized));
            var rotation = Quaternion.AngleAxis(_maxTilt * amount, axis.normalized) * level;
            var slide = parent.InverseTransformVector(towards.normalized * (_maxSlide * amount));
            return (rotation, _restPosition + slide);
        }

        /// <summary>For captures outside Play mode: shows the kit, and tips the plate, for a given state.</summary>
        public void Preview(Vector2 point, bool scrubbing, float progress, bool hint)
        {
            _block ??= new MaterialPropertyBlock();
            if (_plate == null)
                _plate = FindPlate();
            EnsureKit();
            _root.gameObject.SetActive(true);
            _shownPoint = point;
            _lastPoint = point;
            _progress = progress;
            Draw(1f, scrubbing ? 1f : 0f, 0f, hint ? 1f : 0f, 0f);
            if (_plate != null)
            {
                if (!_drivingPlate)
                {
                    _restRotation = _plate.localRotation;
                    _restPosition = _plate.localPosition;
                    _drivingPlate = true;
                }
                (_plate.localRotation, _plate.localPosition) = TiltedPose(point);
            }
        }

        /// <summary>Puts the plate back as it was before <see cref="Preview"/> and removes the kit.</summary>
        public void EndPreview()
        {
            if (_plate != null && _drivingPlate)
            {
                _plate.localRotation = _restRotation;
                _plate.localPosition = _restPosition;
            }
            _drivingPlate = false;
            if (_root != null)
                DestroyKit();
        }

        /// <summary>Turns the kit so its +z, and the stick's up, is screen up. Public for captures.</summary>
        public void FaceCamera()
        {
            if (_root == null)
                return;
            var (_, forward) = ScreenAxes();
            _root.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        // Screen right and screen up, laid on the floor.
        private static (Vector3 Right, Vector3 Forward) ScreenAxes()
        {
            var camera = Camera.main;
            var forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-4f)
                forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.up, Vector3.up) : Vector3.forward;
            forward.Normalize();
            return (Vector3.Cross(Vector3.up, forward), forward);
        }

        private Transform FindPlate()
        {
            foreach (Transform child in _sink != null ? _sink.transform : transform)
            {
                if (child.name.StartsWith(PlatePrefix))
                    return child;
            }
            return null;
        }
    }
}
