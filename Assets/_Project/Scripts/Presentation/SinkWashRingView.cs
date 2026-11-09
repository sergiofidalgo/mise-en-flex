using Game.Data;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// The washing game drawn on the sink's water instead of above the chef (decision 2026-11-09:
    /// the station is the interface). A flat quad just over the water carries the whole game in
    /// <c>Game/FX/WashRing</c>: the suds patch to aim for, the point the stick balances, and a
    /// chunky rim in the washer's colour that lights up segment by segment with the plate's progress.
    /// The plate in the basin tips towards the point, so the balancing reads as the plate itself
    /// wobbling (the user's idea); the ring pulses while washing well and pops when a plate's done.
    /// Everything turns with the camera, never the sink: pushing the stick up moves the point, and
    /// dips the plate, up on screen however the sink has been turned in the layout editor.
    /// </summary>
    public sealed class SinkWashRingView : MonoBehaviour
    {
        private static readonly int PointId = Shader.PropertyToID("_Point");
        private static readonly int RateId = Shader.PropertyToID("_Rate");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int GoodRadiusId = Shader.PropertyToID("_GoodRadius");
        private static readonly int OkRadiusId = Shader.PropertyToID("_OkRadius");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");

        private const string PlatePrefix = "DirtyPlate";

        [SerializeField] private SinkStation _sink;
        [SerializeField] private CoopSettings _settings;
        [Tooltip("The flat quad over the water; turned each frame to face the camera's way.")]
        [SerializeField] private Renderer _ring;

        [Header("Fades")]
        [Tooltip("Seconds to fade in or out when washing starts or stops.")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.15f;
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
        [Tooltip("Ring size swing while washing well (share of its size).")]
        [SerializeField, Range(0f, 0.1f)] private float _pulse = 0.025f;
        [Tooltip("Pulses per second while washing well.")]
        [SerializeField, Min(0f)] private float _pulseRate = 3f;
        [Tooltip("Extra size at the start of the pop when a plate comes clean.")]
        [SerializeField, Range(0f, 0.5f)] private float _pop = 0.15f;
        [Tooltip("Seconds the plate-done flash and pop last.")]
        [SerializeField, Min(0.01f)] private float _popSeconds = 0.35f;

        private MaterialPropertyBlock _block;
        // The plate shown in the basin, found among the sink's children (DirtyPlate…): the kitchen
        // builder makes it after the sink's water, so it isn't wired in.
        private Transform _plate;
        private Vector3 _ringScale;
        private float _fade;
        private float _progress;
        private float _progressVelocity;
        private float _lastRawProgress;
        private float _popLeft;
        private Color _rimColor = Color.white;

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
            _ringScale = _ring.transform.localScale;
            _ring.enabled = false;
            if (_plate == null)
                _plate = FindPlate();
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
            // Hold on while the pop plays, even though washing has stopped.
            var visible = Mathf.Max(_fade, _popLeft > 0f ? 1f : 0f);
            _ring.enabled = visible > 0f;

            if (washing)
            {
                _tiltPoint = Vector2.Lerp(_tiltPoint, new Vector2(game.Point.X, game.Point.Y), 1f - Mathf.Exp(-_tiltFollow * dt));
                if (_settings != null && _sink.Washer.TryGetComponent(out PlayerInput player))
                    _rimColor = _settings.GetPlayerColor(player.playerIndex);
                _progress = Mathf.SmoothDamp(_progress, Mathf.Clamp01(raw), ref _progressVelocity, _progressSmoothing);
                _block.SetVector(PointId, new Vector4(game.Point.X, game.Point.Y, 0f, 0f));
                _block.SetFloat(RateId, game.Rate);
                _block.SetFloat(GoodRadiusId, game.GoodRadius);
                _block.SetFloat(OkRadiusId, game.OkRadius);
            }
            TiltPlate(washing, dt);

            if (!_ring.enabled)
                return;
            FaceCamera();
            var popT = _popLeft / _popSeconds;               // 1 at the start of the pop, 0 at its end
            var pulse = washing ? _pulse * Mathf.Sin(Time.time * _pulseRate * 2f * Mathf.PI) * (game.Rate) : 0f;
            _ring.transform.localScale = _ringScale * (1f + pulse + _pop * popT * popT);
            _block.SetFloat(ProgressId, _progress);
            _block.SetFloat(FlashId, popT);
            _block.SetColor(RimColorId, _rimColor);
            _block.SetFloat(FadeId, visible);
            _ring.SetPropertyBlock(_block);
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

        /// <summary>For captures outside Play mode: shows the ring, and tips the plate, for a given state.</summary>
        public void Preview(Vector2 point, float rate, float progress, Color rim, float goodRadius, float okRadius)
        {
            _block ??= new MaterialPropertyBlock();
            if (_plate == null)
                _plate = FindPlate();
            FaceCamera();
            _block.SetVector(PointId, point);
            _block.SetFloat(RateId, rate);
            _block.SetFloat(ProgressId, progress);
            _block.SetFloat(GoodRadiusId, goodRadius);
            _block.SetFloat(OkRadiusId, okRadius);
            _block.SetColor(RimColorId, rim);
            _block.SetFloat(FadeId, 1f);
            _block.SetFloat(FlashId, 0f);
            _ring.SetPropertyBlock(_block);
            _ring.enabled = true;
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

        /// <summary>Puts the plate back as it was before <see cref="Preview"/>.</summary>
        public void EndPreview()
        {
            if (_plate != null && _drivingPlate)
            {
                _plate.localRotation = _restRotation;
                _plate.localPosition = _restPosition;
            }
            _drivingPlate = false;
            _ring.enabled = false;
        }

        /// <summary>
        /// Lies flat (a Quad faces up when pitched 90°) and points its UV up along the camera's
        /// forward direction on the floor, which is screen up. Public for captures.
        /// </summary>
        public void FaceCamera()
        {
            var (_, forward) = ScreenAxes();
            _ring.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
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
