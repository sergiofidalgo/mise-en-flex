using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// A courier waiting outside the restaurant gets their order and rides off (the user's note,
    /// 2026-11-11: "pick their order and leave with their scooter soon after the camera stops").
    /// On the scooter. Once begun, after a delay: whoever is on foot walks to it and gets on (the
    /// standing figure is swapped for a seated one), the phone goes away, hands go to the bars, and
    /// after a breath the scooter pulls out along its route, gathering speed, and is gone.
    /// </summary>
    public sealed class CourierDeparture : MonoBehaviour
    {
        [Tooltip("Seconds after the camera stops before anything happens.")]
        [SerializeField, Min(0f)] private float _delay = 2f;
        [Tooltip("The courier on foot who walks to the scooter; none if they're already sitting on it.")]
        [SerializeField] private Transform _walker;
        [Tooltip("Where they walk to, in their parent's space.")]
        [SerializeField] private Vector3 _walkTo;
        [SerializeField, Min(0.1f)] private float _walkSpeed = 1.7f;
        [Tooltip("The same courier sitting on the scooter: shown as the walker arrives.")]
        [SerializeField] private GameObject _seated;
        [Tooltip("Hidden as they set off: the phone they were scrolling.")]
        [SerializeField] private GameObject[] _putAway = System.Array.Empty<GameObject>();
        [Tooltip("The rider's shoulders, turned to reach the bars.")]
        [SerializeField] private Transform[] _arms = System.Array.Empty<Transform>();
        [SerializeField] private Vector3[] _grip = System.Array.Empty<Vector3>();
        [Tooltip("Seconds between hands on the bars and pulling out.")]
        [SerializeField, Min(0f)] private float _breath = 0.6f;
        [Tooltip("The way out, in the scooter's parent's space, starting where it stands.")]
        [SerializeField] private Vector3[] _route = System.Array.Empty<Vector3>();
        [SerializeField, Min(0.5f)] private float _speed = 7f;
        [SerializeField, Min(0.5f)] private float _acceleration = 3.5f;
        [SerializeField, Min(1f)] private float _turnSpeed = 150f;

        private enum Step { Parked, Waiting, Walking, Mounted, Riding }

        private Step _step = Step.Parked;
        private float _clock;
        private float _pace;
        private float _along;
        private float _length;

        public void Set(float delay, Transform walker, Vector3 walkTo, GameObject seated, GameObject[] putAway, Transform[] arms, Vector3[] grip, Vector3[] route)
        {
            (_delay, _walker, _walkTo, _seated, _putAway, _arms, _grip, _route) = (delay, walker, walkTo, seated, putAway, arms, grip, route);
        }

        /// <summary>The camera has stopped at the entrance: the order's on its way out.</summary>
        public void Begin()
        {
            if (_step != Step.Parked)
                return;
            _step = Step.Waiting;
            _clock = 0f;
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            switch (_step)
            {
                case Step.Waiting:
                    if (_clock < _delay)
                        break;
                    if (_walker != null)
                        _step = Step.Walking;
                    else
                        Mount();
                    break;
                case Step.Walking:
                    var to = _walkTo - _walker.localPosition;
                    to.y = 0f;
                    if (to.magnitude < 0.05f)
                    {
                        Mount();
                        break;
                    }
                    _walker.localRotation = Quaternion.RotateTowards(_walker.localRotation, Quaternion.LookRotation(to.normalized), 540f * Time.deltaTime);
                    _walker.localPosition = Vector3.MoveTowards(_walker.localPosition, new Vector3(_walkTo.x, _walker.localPosition.y, _walkTo.z), _walkSpeed * Time.deltaTime);
                    break;
                case Step.Mounted:
                    if (_clock < _breath || _route.Length < 2)
                        break;
                    _step = Step.Riding;
                    _length = OpeningPath.Length(_route);
                    break;
                case Step.Riding:
                    _pace = Mathf.Min(_speed, _pace + _acceleration * Time.deltaTime);
                    _along += _pace * Time.deltaTime;
                    transform.localPosition = OpeningPath.PointAlong(_route, _along, out var heading);
                    transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(heading), _turnSpeed * Time.deltaTime);
                    if (_along >= _length)
                        gameObject.SetActive(false);
                    break;
            }
        }

        private void Mount()
        {
            if (_walker != null)
                _walker.gameObject.SetActive(false);
            if (_seated != null)
                _seated.SetActive(true);
            foreach (var thing in _putAway)
                if (thing != null)
                    thing.SetActive(false);
            for (var i = 0; i < _arms.Length && i < _grip.Length; i++)
            {
                if (_arms[i] == null)
                    continue;
                // Whatever idle was moving the arm (scrolling) stops.
                foreach (var idle in _arms[i].GetComponents<Oscillate>())
                    idle.enabled = false;
                _arms[i].localRotation = Quaternion.Euler(_grip[i]);
            }
            _step = Step.Mounted;
            _clock = 0f;
        }
    }
}
