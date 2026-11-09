using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// A person's walk (the user's note after playtesting, 2026-11-09: walking should show): the
    /// legs swing from the hips, the body bobs once a step and waddles side to side, and, for
    /// people whose arms nothing else moves, the arms swing against the legs. The gait advances
    /// with distance walked, not time, so feet don't skate at any speed, and fades out when
    /// standing. Works for anyone who moves (players, customers, staff), as it watches its own
    /// position. Chefs' arms are posed by <see cref="ChefArmsView"/>, which follows this gait.
    /// </summary>
    public sealed class WalkCycleView : MonoBehaviour
    {
        [SerializeField] private Transform _leftHip;
        [SerializeField] private Transform _rightHip;
        [Tooltip("Bobs up and down with each step (the upper body).")]
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _leftShoulder;
        [SerializeField] private Transform _rightShoulder;
        [Tooltip("Swing the arms against the legs. Off for chefs, whose ChefArmsView poses the arms in step with this gait.")]
        [SerializeField] private bool _swingArms = true;

        [Tooltip("Metres walked per full cycle (two steps). Short legs take short, quick steps.")]
        [SerializeField, Min(0.1f)] private float _strideLength = 1f;
        [Tooltip("Degrees each leg swings either way at full speed.")]
        [SerializeField, Range(0f, 60f)] private float _legSwing = 30f;
        [Tooltip("Degrees each free arm swings either way at full speed.")]
        [SerializeField, Range(0f, 60f)] private float _armSwing = 22f;
        [Tooltip("How far the body rises at mid-step, in metres.")]
        [SerializeField, Min(0f)] private float _bobHeight = 0.035f;
        [Tooltip("Degrees the body rolls onto each foot: the waddle.")]
        [SerializeField, Range(0f, 15f)] private float _waddle = 4f;
        [Tooltip("The walking speed (m/s) that gives the full swing.")]
        [SerializeField, Min(0.1f)] private float _fullSpeed = 2.5f;

        private Vector3 _lastPosition;
        private float _speed;
        private float _bodyHeight;
        private Quaternion _restRotation;
        private bool _rested;

        /// <summary>Where the walk is in its cycle, in radians: the left leg is forward at π/2.</summary>
        public float Phase { get; private set; }

        /// <summary>0 standing to 1 walking at full speed.</summary>
        public float Walking { get; private set; }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            Rest();
        }

        // The standing pose everything swings about, taken once, before anything has moved
        // (also for a still picture outside Play mode, where OnEnable doesn't run).
        private void Rest()
        {
            if (_rested)
                return;
            _rested = true;
            if (_body != null)
                _bodyHeight = _body.localPosition.y;
            _restRotation = transform.localRotation;
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            var moved = transform.position - _lastPosition;
            moved.y = 0f;
            _lastPosition = transform.position;

            _speed = Mathf.Lerp(_speed, moved.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
            Walking = Mathf.Clamp01(_speed / _fullSpeed);
            Phase = Mathf.Repeat(Phase + moved.magnitude / _strideLength * 2f * Mathf.PI, 2f * Mathf.PI);
            Show(Phase, Walking);
        }

        /// <summary>Poses the walk at <paramref name="phase"/> and strength; public for still previews.</summary>
        public void Show(float phase, float walking)
        {
            Rest();
            var swing = Mathf.Sin(phase) * walking;
            // Legs: the left forward as the right goes back. Positive X turns a hanging leg forward.
            if (_leftHip != null)
                _leftHip.localRotation = Quaternion.Euler(-swing * _legSwing, 0f, 0f);
            if (_rightHip != null)
                _rightHip.localRotation = Quaternion.Euler(swing * _legSwing, 0f, 0f);
            // Arms swing against the legs on the same side.
            if (_swingArms && _leftShoulder != null)
                _leftShoulder.localRotation = Quaternion.Euler(swing * _armSwing, 0f, 0f);
            if (_swingArms && _rightShoulder != null)
                _rightShoulder.localRotation = Quaternion.Euler(-swing * _armSwing, 0f, 0f);

            // The body is highest as a leg passes under it (twice a cycle) and leans onto the foot
            // that's down: a waddle, which reads at the camera's distance where legs barely do.
            if (_body != null)
            {
                var position = _body.localPosition;
                position.y = _bodyHeight + Mathf.Abs(Mathf.Cos(phase)) * _bobHeight * walking;
                _body.localPosition = position;
            }
            transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, Mathf.Cos(phase) * _waddle * walking);
        }
    }
}
