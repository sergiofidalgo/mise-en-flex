using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The glass lift on the front of the boss's tower (user's note, 2026-11-09: "as the camera goes
    /// up the building you could see the elevator with a chef going up through it"). It waits at the
    /// street; on the climb it rides up just ahead of the camera, so the chef inside stays in frame,
    /// and stops under the floor. Once the camera lands, the chef has gone in (they come out of the
    /// floor's own elevator as players join) and the empty cab sinks slowly back to the street.
    /// </summary>
    public sealed class OpeningElevator : MonoBehaviour
    {
        [SerializeField] private Transform _cab;
        [Tooltip("Whoever rides it up: hidden once the camera has landed.")]
        [SerializeField] private GameObject _rider;
        [Tooltip("The cab floor's lowest and highest heights (world y).")]
        [SerializeField] private float _bottom;
        [SerializeField] private float _top;
        [Tooltip("How far above the camera's height the cab floor rides, so the chef sits in the middle of the frame (m).")]
        [SerializeField] private float _lead = 1.5f;
        [Tooltip("How quickly the cab catches up with where it should be (s).")]
        [SerializeField, Min(0.05f)] private float _ease = 0.5f;
        [Tooltip("The empty cab's speed on its way back down (m/s).")]
        [SerializeField, Min(0.5f)] private float _returnSpeed = 6f;

        private float _target;
        private float _velocity;
        private bool _returning;

        private void Awake()
        {
            _target = _bottom;
            Place(_bottom);
        }

        /// <summary>Rides along with a camera at <paramref name="cameraHeight"/>.</summary>
        public void Track(float cameraHeight)
        {
            _returning = false;
            _target = Mathf.Clamp(cameraHeight + _lead, _bottom, _top);
        }

        /// <summary>The camera has landed: the chef is in, the empty cab goes back down.</summary>
        public void Release()
        {
            if (_rider != null)
                _rider.SetActive(false);
            _returning = true;
            _target = _bottom;
        }

        private void Update()
        {
            if (_cab == null)
                return;
            var y = _cab.position.y;
            y = _returning
                ? Mathf.MoveTowards(y, _target, _returnSpeed * Time.deltaTime)
                : Mathf.SmoothDamp(y, _target, ref _velocity, _ease);
            Place(y);
        }

        private void Place(float y)
        {
            if (_cab == null)
                return;
            var at = _cab.position;
            _cab.position = new Vector3(at.x, y, at.z);
        }
    }
}
