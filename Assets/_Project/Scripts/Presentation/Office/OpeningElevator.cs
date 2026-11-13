using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The glass lift on the front of the boss's tower (user's note, 2026-11-09: "as the camera goes
    /// up the building you could see the elevator with a chef going up through it"). It waits at the
    /// street; on the climb it rides locked to the camera's height, so the chef inside stays in
    /// frame (the user's note, 2026-11-11: it fell out of view), then covers the last few floors on
    /// its own while the camera goes over the cornice, coming up into the boss's floor's elevator
    /// exactly as the camera lands, and once the camera has landed and the chef
    /// has stepped out it sinks back down the shaft and switches itself off.
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
        [Tooltip("Metres over which the cab eases off the camera's pace where it stops riding beside it.")]
        [SerializeField, Min(0f)] private float _settle = 4f;
        [Tooltip("The last metres under the top stop, which the cab covers on its own so as to arrive exactly as the camera lands.")]
        [SerializeField, Min(0f)] private float _approach = 14f;
        [Tooltip("How far through the climb's time (0–1) that last stretch starts.")]
        [SerializeField, Range(0f, 0.95f)] private float _approachFrom = 0.5f;
        [Tooltip("Seconds the cab takes to pick up the camera's pace as the climb starts.")]
        [SerializeField, Min(0.01f)] private float _catchUp = 0.35f;
        [Tooltip("The empty cab's speed on its way back down (m/s).")]
        [SerializeField, Min(0.5f)] private float _returnSpeed = 6f;
        [Tooltip("How far down the shaft the empty cab goes before it switches off (m): out of the floor's view.")]
        [SerializeField, Min(1f)] private float _hideBelow = 12f;

        private bool _returning;
        private bool _tracking;
        private float _offset;
        private float _offsetVelocity;

        public float Lead => _lead;
        public float Bottom => _bottom;
        public float Top => _top;
        public float Settle => _settle;

        /// <summary>The cab floor's height for a camera at <paramref name="cameraHeight"/>, <paramref name="progress"/> (0–1) through the climb's time.</summary>
        public float HeightAt(float cameraHeight, float progress) =>
            OpeningPath.LiftClimb(cameraHeight, progress, _lead, _bottom, _top, _settle, _approach, _approachFrom);
        public Transform Cab => _cab;

        private void Awake() => Place(_bottom);

        /// <summary>Rides along with a camera at <paramref name="cameraHeight"/>, <paramref name="progress"/> (0–1) through the climb's time; call every frame of the climb.</summary>
        public void Track(float cameraHeight, float progress)
        {
            if (_cab == null)
                return;
            _returning = false;
            var wanted = HeightAt(cameraHeight, progress);
            if (!_tracking)
            {
                // Wherever the cab stands as the climb starts, it closes on its place beside the
                // camera in a moment and is then locked to it: the offset is eased, not the height,
                // so there's no lag however fast the camera goes.
                _tracking = true;
                _offset = _cab.position.y - wanted;
                _offsetVelocity = 0f;
            }
            _offset = Mathf.SmoothDamp(_offset, 0f, ref _offsetVelocity, _catchUp);
            Place(Mathf.Clamp(wanted + _offset, _bottom, _top));
        }

        /// <summary>The camera has landed: the chef is in, the empty cab goes back down.</summary>
        public void Release()
        {
            if (_rider != null)
                _rider.SetActive(false);
            _tracking = false;
            _returning = true;
        }

        private void Update()
        {
            if (_cab == null || !_returning)
                return;
            var y = Mathf.MoveTowards(_cab.position.y, _bottom, _returnSpeed * Time.deltaTime);
            Place(y);
            // Nobody sees the shaft from the floor: once it's well down, it's done.
            if (y <= Mathf.Max(_bottom, _top - _hideBelow))
                gameObject.SetActive(false);
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
