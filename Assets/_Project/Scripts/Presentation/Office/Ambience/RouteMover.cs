using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// Rides a route of straight legs, turning to face along it: the opening's delivery couriers
    /// (the user's note, 2026-11-11: "delivery motorbikes approaching or leaving"), who come in from
    /// one end of the street, pull up at the kerb, wait while the order changes hands, and ride off
    /// at the other end, to come round again a while later. Hidden while away.
    /// </summary>
    public sealed class RouteMover : MonoBehaviour
    {
        [Tooltip("The route's corners, in the parent's space.")]
        [SerializeField] private Vector3[] _points = System.Array.Empty<Vector3>();
        [SerializeField, Min(0.1f)] private float _speed = 5f;
        [Tooltip("Which corner they stop at; −1 to ride straight through.")]
        [SerializeField] private int _stopAt = -1;
        [SerializeField, Min(0f)] private float _stopSeconds = 4f;
        [Tooltip("Seconds away between leaving at one end and coming in again at the other.")]
        [SerializeField, Min(0f)] private float _awaySeconds = 6f;
        [SerializeField, Range(0f, 1f)] private float _phase;
        [Tooltip("How quickly they turn onto a new leg (degrees a second).")]
        [SerializeField, Min(1f)] private float _turnSpeed = 220f;
        [Tooltip("What to hide while they're away (their own children, usually).")]
        [SerializeField] private GameObject _body;

        private float _length;
        private float _stopDistance;
        private bool _away;

        public void Set(Vector3[] points, float speed, int stopAt, float stopSeconds, float awaySeconds, float phase, GameObject body)
        {
            (_points, _speed, _stopAt, _stopSeconds, _awaySeconds, _phase, _body) = (points, speed, stopAt, stopSeconds, awaySeconds, phase, body);
            Measure();
            Place(0f, snap: true);
        }

        private void Awake()
        {
            Measure();
            Place(Time.time, snap: true);
        }

        private void Update() => Place(Time.time, snap: false);

        /// <summary>Where they'd be at <paramref name="time"/>: for stills.</summary>
        public void Freeze(float time)
        {
            Measure();
            Place(time, snap: true);
        }

        private void Measure()
        {
            _length = OpeningPath.Length(_points);
            _stopDistance = -1f;
            if (_stopAt > 0 && _stopAt < _points.Length - 1)
            {
                _stopDistance = 0f;
                for (var i = 1; i <= _stopAt; i++)
                    _stopDistance += Vector3.Distance(_points[i - 1], _points[i]);
            }
        }

        private void Place(float time, bool snap)
        {
            if (_points.Length < 2)
                return;
            var along = AmbientMotion.Route(time, _length, _speed, _stopDistance, _stopSeconds, _awaySeconds, _phase);
            var away = along < 0f;
            if (_body != null && _body.activeSelf == away)
                _body.SetActive(!away);
            // Coming back in, they face the first leg at once instead of swinging round from the last.
            snap |= _away && !away;
            _away = away;
            if (away)
                return;
            transform.localPosition = OpeningPath.PointAlong(_points, along, out var heading);
            var facing = Quaternion.LookRotation(heading);
            transform.localRotation = snap ? facing : Quaternion.RotateTowards(transform.localRotation, facing, _turnSpeed * Time.deltaTime);
        }
    }
}
