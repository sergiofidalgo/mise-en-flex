using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// The washing game at the sink, built from real 3D pieces (decisions 2026-11-09: the station
    /// is the interface; then "too flat for the scene"): a ring of chunky toon-shaded segments in
    /// the washer's colour that light up with the plate's progress, a raised target in the centre
    /// whose segments fill green while the soap is held there, and a soap bar the stick balances,
    /// bobbing, leaning as it slides and shaking when it's far off. When the player seems stuck,
    /// an animated thumbstick beside the soap tilts the way to push. The pieces use
    /// <c>Game/ToonOnTop</c>, drawn over whatever is in front of them (the chef, the tap) in a fixed
    /// order, and sit level with the basin's rim. The plate in the basin tips towards the soap.
    /// Everything turns with the camera, never the sink: pushing the stick up moves the soap, and
    /// dips the plate, up on screen however the sink has been turned in the layout editor.
    /// </summary>
    public sealed class SinkWashRingView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const string PlatePrefix = "DirtyPlate";

        // The kit's own units: the rim's middle at 0.84, the edge of the old flat ring at 1.
        private const float RimRadius = 0.84f;
        private const float RimWidth = 0.19f;
        private const int Segments = 15;           // 5 per centring, with 3 a plate
        private const float SegmentGap = 0.09f;      // share of each segment left open
        private const int TargetSegments = 12;

        // Draw order (sortingOrder): the pieces ignore depth, so later ones cover earlier ones.
        private const int RingOrder = 0;
        private const int TargetOrder = 1;
        private const int SoapOrder = 2;
        private const int BubbleOrder = 3;
        private const int StickOrder = 4; // its parts, base upwards, from here

        [SerializeField] private SinkStation _sink;
        [SerializeField] private CoopSettings _settings;
        [Tooltip("The quad over the basin: the kit's anchor and size. It isn't drawn itself.")]
        [SerializeField] private Renderer _ring;
        [Tooltip("Toon material for the kit's pieces; each gets its colour per renderer.")]
        [SerializeField] private Material _toon;
        [Tooltip("Tutorial glyph material for the stick hint: its own fixed light and outline, the same in every scene.")]
        [SerializeField] private Material _icon;
        [Tooltip("Font for the L on the stick: the HUD's display face, a fixed atlas.")]
        [SerializeField] private TMP_FontAsset _stickFont;

        [Header("Look")]
        [Tooltip("Height of the rim's segments and of the soap's float, in kit units (the rim is 0.84 from the centre).")]
        [SerializeField, Min(0f)] private float _segmentHeight = 0.07f;
        [SerializeField, Min(0f)] private float _soapFloat = 0.1f;
        [SerializeField] private Vector3 _soapSize = new Vector3(0.34f, 0.22f, 0.24f);
        [SerializeField] private Color _good = new Color(0.45f, 0.85f, 0.40f);
        [SerializeField] private Color _bad = new Color(0.95f, 0.30f, 0.26f);
        [SerializeField] private Color _targetIdle = new Color(0.97f, 0.97f, 0.95f);
        [Tooltip("How dark an unlit segment is, as a share of the player's colour.")]
        [SerializeField, Range(0f, 1f)] private float _unlit = 0.32f;

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

        [Header("Stick hint")]
        [Tooltip("Seconds off target, not pushing towards the centre, before the stick shows how.")]
        [SerializeField, Min(0f)] private float _hintDelay = 0.6f;
        [Tooltip("How closely the stick must point at the centre to count as pushing the right way (cosine).")]
        [SerializeField, Range(-1f, 1f)] private float _rightWay = 0.5f;
        [SerializeField, Range(0f, 60f)] private float _hintTilt = 28f;
        [SerializeField, Min(0f)] private float _hintPushes = 1.6f;
        [Tooltip("The hint's size, in kit units per its own unit.")]
        [SerializeField, Min(0.1f)] private float _hintSize = 1.3f;

        private MaterialPropertyBlock _block;
        // The plate shown in the basin, found among the sink's children (DirtyPlate…): the kit's
        // builder makes it after the sink's water, so it isn't wired in.
        private Transform _plate;

        // The kit: built on first use, outside the scene's saved objects.
        private Transform _root;
        private readonly List<Renderer> _segments = new List<Renderer>();
        private readonly List<Renderer> _target = new List<Renderer>();
        private Transform _soap;
        private Renderer _soapRenderer;
        private readonly List<Transform> _bubbles = new List<Transform>();
        private Transform _hint;
        private Transform _hintCap;
        private float _hintShown;
        private float _stuckFor;

        private float _fade;
        private float _progress;
        private float _progressVelocity;
        private float _lastRawProgress;
        private float _popLeft;
        private Color _rimColor = new Color(0.91f, 0.53f, 0.23f);
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
            var hold = 0f;
            if (washing)
            {
                _shownPoint = new Vector2(game.Point.X, game.Point.Y);
                _tiltPoint = Vector2.Lerp(_tiltPoint, _shownPoint, 1f - Mathf.Exp(-_tiltFollow * dt));
                if (_settings != null && _sink.Washer.TryGetComponent(out PlayerInput player))
                    _rimColor = _settings.GetPlayerColor(player.playerIndex);
                _progress = Mathf.SmoothDamp(_progress, Mathf.Clamp01(raw), ref _progressVelocity, _progressSmoothing);
                rate = game.Rate;
                hold = game.HoldFraction;
                UpdateStuck(game.Point.Length() > game.GoodRadius, _sink.Washer.MoveInput, dt);
            }
            else
            {
                _stuckFor = 0f;
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
            var pulse = washing ? _pulse * Mathf.Sin(Time.time * _pulseRate * 2f * Mathf.PI) * hold : 0f;
            // Pops in with a little overshoot, and out again when washing stops.
            var appear = Mathf.Max(EaseOutBack(_fade), popT > 0f ? 1f : 0f);
            Draw(appear * (1f + pulse + _pop * popT * popT), rate, hold, popT, _hintShown, dt);
        }

        // The stick hint shows once the soap has been off target a moment with the stick not
        // pushing towards the centre, and goes as soon as it is: help only when it's needed.
        private void UpdateStuck(bool offTarget, Vector2 stick, float dt)
        {
            var toCentre = -_shownPoint.normalized;
            var rightWay = stick.magnitude > 0.3f && Vector2.Dot(stick.normalized, toCentre) > _rightWay;
            _stuckFor = offTarget && !rightWay ? _stuckFor + dt : 0f;
            var want = _stuckFor > _hintDelay ? 1f : 0f;
            _hintShown = Mathf.MoveTowards(_hintShown, want, dt / 0.15f);
        }

        // Places and colours every piece for this frame. Kit units: the rim at 0.84.
        private void Draw(float scale, float rate, float hold, float flash, float hint, float dt)
        {
            FaceCamera();
            _root.position = _ring.transform.position;
            // The quad is the old flat ring, Size across: one kit unit is half of it.
            _root.localScale = Vector3.one * (_ring.transform.lossyScale.x * 0.5f * Mathf.Max(0.001f, scale));

            for (var i = 0; i < _segments.Count; i++)
            {
                var lit = (i + 0.5f) / _segments.Count < _progress;
                var color = lit ? _rimColor : _rimColor * _unlit;
                color = Color.Lerp(color, Color.white, flash);
                Paint(_segments[i], color);
            }
            for (var i = 0; i < _target.Count; i++)
            {
                var filled = hold > 0f && (i + 0.5f) / _target.Count < hold;
                Paint(_target[i], filled ? _good : _targetIdle);
            }

            // The soap: where the game's point is, bobbing; it leans the way it's sliding, and
            // shakes when it's far from the centre. Its colour says how close it is.
            var velocity = dt > 0f ? (_shownPoint - _lastPoint) / dt : Vector2.zero;
            _lastPoint = _shownPoint;
            var point = _shownPoint * RimRadius;
            var bob = Mathf.Sin(Time.time * 4f) * 0.015f;
            _soap.localPosition = new Vector3(point.x, _segmentHeight + _soapFloat + bob, point.y);
            var lean = Vector3.ClampMagnitude(new Vector3(velocity.y, 0f, -velocity.x) * 6f, 20f);
            var shake = rate < 0.2f ? Mathf.Sin(Time.time * 40f) * 6f : 0f;
            _soap.localRotation = Quaternion.Euler(lean.x + shake, 25f, lean.z);
            Paint(_soapRenderer, Color.Lerp(_bad, _good, rate));

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

            // The stick hint: beside the soap on its far side from the centre, so it never hides
            // the soap or the way in, its cap tipping towards the centre, again and again.
            _hint.gameObject.SetActive(hint > 0f);
            if (hint > 0f)
            {
                var outward = _shownPoint.sqrMagnitude > 1e-4f ? _shownPoint.normalized : Vector2.up;
                _hint.localPosition = _soap.localPosition + new Vector3(outward.x * 0.34f, 0.04f, outward.y * 0.34f);
                // Pops in, then breathes a little, like a tutorial prompt.
                _hint.localScale = Vector3.one * (_hintSize * EaseOutBack(hint) * (1f + 0.04f * Mathf.Sin(Time.time * 5f)));
                var toCentre = -_shownPoint;
                var direction = toCentre.sqrMagnitude > 1e-4f ? new Vector3(toCentre.x, 0f, toCentre.y).normalized : Vector3.forward;
                var axis = Vector3.Cross(Vector3.up, direction);
                var push = Mathf.Clamp01(Mathf.Sin(Time.time * _hintPushes * 2f * Mathf.PI) * 1.4f);
                _hintCap.localRotation = Quaternion.AngleAxis(_hintTilt * push, axis);
            }
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

            // The target's centre radius matches the game's (0.25 of the circle).
            var targetRadius = 0.25f * RimRadius;
            var targetStep = Mathf.PI * 2f / TargetSegments;
            for (var i = 0; i < TargetSegments; i++)
                _target.Add(Piece("Target", StationKitMeshes.RingSector(targetRadius - 0.035f, targetRadius + 0.035f, (i + 0.08f) * targetStep, (i + 0.92f) * targetStep, _segmentHeight * 0.6f, 0.012f), _root, TargetOrder));

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

            BuildStick();
            _hint.gameObject.SetActive(false);
        }

        // A thumbstick as on the pad, styled as a tutorial glyph (the user's requests): a domed
        // base with a collar, a short shaft, and a rounded cap with a grippy rim round a lighter
        // dished top marked L. Its own material keeps it the same in every scene (fixed light,
        // its own outline). The shaft and cap pivot at the base, like the real thing. Kit units;
        // drawn base upwards.
        private void BuildStick()
        {
            var baseDark = new Color(0.15f, 0.16f, 0.19f);
            var collarGrey = new Color(0.26f, 0.27f, 0.31f);
            var capGrey = new Color(0.44f, 0.45f, 0.5f);
            var rimGrey = new Color(0.3f, 0.31f, 0.36f);
            var dishGrey = new Color(0.62f, 0.63f, 0.68f);
            var order = StickOrder;

            _hint = new GameObject("StickHint") { hideFlags = HideFlags.DontSave }.transform;
            _hint.SetParent(_root, false);

            var dome = Primitive("StickBase", PrimitiveType.Sphere, _hint, order++, _icon);
            dome.transform.localScale = new Vector3(0.3f, 0.1f, 0.3f);
            Paint(dome, baseDark);
            var collar = Piece("StickCollar", StationKitMeshes.RingSector(0.05f, 0.095f, 0f, Mathf.PI * 2f, 0.035f, 0.012f, 32), _hint, order++, _icon);
            collar.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            Paint(collar, collarGrey);

            _hintCap = new GameObject("StickPivot") { hideFlags = HideFlags.DontSave }.transform;
            _hintCap.SetParent(_hint, false);
            _hintCap.localPosition = new Vector3(0f, 0.04f, 0f);

            var shaft = Primitive("StickShaft", PrimitiveType.Cylinder, _hintCap, order++, _icon);
            shaft.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            shaft.transform.localScale = new Vector3(0.07f, 0.05f, 0.07f); // a cylinder is 2 tall
            Paint(shaft, collarGrey);

            // The cap: a disc with a rounded (bevelled) edge.
            var cap = Piece("StickCap", StationKitMeshes.RingSector(0.0005f, 0.13f, 0f, Mathf.PI * 2f, 0.05f, 0.022f, 40), _hintCap, order++, _icon);
            cap.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            Paint(cap, capGrey);

            // The grippy rim: a ring of small raised blocks round the top.
            for (var i = 0; i < 16; i++)
            {
                var a0 = (i + 0.12f) * Mathf.PI * 2f / 16f;
                var a1 = (i + 0.88f) * Mathf.PI * 2f / 16f;
                var rim = Piece("StickGrip", StationKitMeshes.RingSector(0.092f, 0.122f, a0, a1, 0.018f, 0.006f), _hintCap, order, _icon);
                rim.transform.localPosition = new Vector3(0f, 0.14f, 0f);
                Paint(rim, rimGrey);
            }
            order++;

            // The dish in the middle of the top, lighter, with the L on it.
            var dish = Piece("StickDish", StationKitMeshes.RingSector(0.0005f, 0.085f, 0f, Mathf.PI * 2f, 0.004f, 0.002f, 32), _hintCap, order++, _icon);
            dish.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            Paint(dish, dishGrey);

            var label = new GameObject("L") { hideFlags = HideFlags.DontSave }.AddComponent<TextMeshPro>();
            label.transform.SetParent(_hintCap, false);
            label.transform.localPosition = new Vector3(0f, 0.146f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // flat on the top, upright on screen
            if (_stickFont != null)
                label.font = _stickFont;
            label.text = "L";
            label.fontSize = 1.1f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = baseDark;
            label.rectTransform.sizeDelta = new Vector2(0.16f, 0.16f);
            // Over everything, like the rest of the glyph: TextMeshPro reads its depth test from here.
            label.fontMaterial.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            label.renderer.sortingOrder = order;
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
            _target.Clear();
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
        public void Preview(Vector2 point, float rate, float progress, Color rim, float hold, bool hint)
        {
            _block ??= new MaterialPropertyBlock();
            if (_plate == null)
                _plate = FindPlate();
            EnsureKit();
            _root.gameObject.SetActive(true);
            _shownPoint = point;
            _lastPoint = point;
            _progress = progress;
            _rimColor = rim;
            Draw(1f, rate, hold, 0f, hint ? 1f : 0f, 0f);
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
