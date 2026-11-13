using Game.Presentation.Office.Ambience;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The helicopter's pass in the opening (the user's note, 2026-11-11: it used to show flying
    /// round the tower as the camera reached the top, and no longer did). On its ordinary lap of the
    /// skyline (FlyLoop) it is in the camera's view at that moment only by luck, so the opening calls
    /// it over: as the camera nears the cornice it leaves its lap for one pass along a curve across
    /// the top of the tower, timed to cross the frame while the camera crests and tips onto the
    /// floor, and then goes back to its lap.
    /// </summary>
    public sealed class OpeningFlyby : MonoBehaviour
    {
        [Tooltip("Its ordinary lap, switched off for the pass.")]
        [SerializeField] private FlyLoop _loop;
        [Tooltip("The pass: from, by way of, to (world space). A curve pulled towards the middle point.")]
        [SerializeField] private Vector3 _from;
        [SerializeField] private Vector3 _via;
        [SerializeField] private Vector3 _to;
        [SerializeField, Min(0.5f)] private float _seconds = 8f;
        [Tooltip("Degrees it leans into the curve.")]
        [SerializeField] private float _bank = 14f;

        private float _clock = -1f;

        public float Seconds => _seconds;

        /// <summary>Starts the pass now.</summary>
        public void Begin()
        {
            if (_clock >= 0f)
                return;
            _clock = 0f;
            if (_loop != null)
                _loop.enabled = false;
            Place(0f);
        }

        /// <summary>Back to its lap at once (the opening was skipped).</summary>
        public void Cancel()
        {
            _clock = -1f;
            if (_loop != null)
                _loop.enabled = true;
        }

        private void Update()
        {
            if (_clock < 0f)
                return;
            _clock += Time.deltaTime;
            if (_clock >= _seconds)
            {
                Cancel();
                return;
            }
            Place(_clock);
        }

        /// <summary>Where it is <paramref name="seconds"/> into the pass: for stills too.</summary>
        public void Place(float seconds)
        {
            var u = Mathf.Clamp01(seconds / _seconds);
            var at = Curve(u);
            var heading = Curve(Mathf.Min(1f, u + 0.01f)) - Curve(Mathf.Max(0f, u - 0.01f));
            // Which way the curve bends decides which way it leans.
            var bend = Vector3.Cross(_via - _from, _to - _via).y;
            var rotation = heading.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(heading.normalized) : transform.rotation;
            transform.SetPositionAndRotation(at, rotation * Quaternion.Euler(0f, 0f, bend > 0f ? -_bank : _bank));
        }

        // A quadratic Bézier: leaves `from` heading for `via`, arrives at `to` coming from it.
        private Vector3 Curve(float u) => (1f - u) * (1f - u) * _from + 2f * u * (1f - u) * _via + u * u * _to;
    }
}
