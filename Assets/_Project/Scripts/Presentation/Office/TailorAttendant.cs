using Game.Gameplay.Office;
using UnityEngine;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The tailor (the user's notes, 2026-11-09). With nobody dressing, they potter about near the
    /// middle of the room, pausing now and then. When a chef climbs a ladder they walk over, stand
    /// in front and take one measure after another with a tape stretched between their hands (chest,
    /// waist, an arm, a leg, the head) while the player browses. If another chef climbs up, they go
    /// to the newest one. Only presentation: dressing doesn't wait for them.
    /// </summary>
    public sealed class TailorAttendant : MonoBehaviour
    {
        private enum Measure { Chest, Waist, Arm, Leg, Head }

        [Tooltip("The ladders the tailor serves.")]
        [SerializeField] private TailorPlinth[] _ladders = System.Array.Empty<TailorPlinth>();
        [Tooltip("The tailor's figure: their walk, waist (to stoop) and shoulders (to reach).")]
        [SerializeField] private WalkCycleView _gait;
        [SerializeField] private Transform _waist;
        [SerializeField] private Transform _leftShoulder;
        [SerializeField] private Transform _rightShoulder;
        [Tooltip("The tape: a thin strip stretched between the hands while measuring.")]
        [SerializeField] private Transform _tape;
        [Tooltip("Where they potter about with nobody to serve, and how far from it (m).")]
        [SerializeField] private Vector3 _home;
        [SerializeField, Min(0f)] private float _roam = 1.4f;

        [SerializeField, Min(0.1f)] private float _walkSpeed = 1.4f;
        [SerializeField, Min(0.1f)] private float _strollSpeed = 0.55f;
        [SerializeField, Min(1f)] private float _turnSpeed = 360f;
        [Tooltip("Shoulder to hand (m), as the kit builds them.")]
        [SerializeField, Min(0.1f)] private float _armLength = 0.48f;
        [Tooltip("How long one measure takes (s), from and to.")]
        [SerializeField] private Vector2 _measureTime = new Vector2(1.3f, 2.3f);

        private TailorPlinth _serving;
        private Vector3 _goal;
        private float _pauseUntil;
        private Measure _measure;
        private float _measureUntil;
        private float _reach;   // 0 arms hanging to 1 measuring, eased
        private float _stoop;

        private void Start()
        {
            _goal = transform.position;
            if (_tape != null)
                _tape.gameObject.SetActive(false);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var customer = Newest();
            if (customer != _serving)
            {
                _serving = customer;
                _measureUntil = 0f;
            }

            bool arrived;
            if (_serving != null)
            {
                // Beside the ladder's front corner on the room's side, clear of where chefs hop up
                // and down, facing the chef.
                var top = _serving.TopPoint;
                var ladder = _serving.transform;
                var side = Vector3.Dot(_home - ladder.position, ladder.right) >= 0f ? ladder.right : -ladder.right;
                _goal = new Vector3(ladder.position.x, transform.position.y, ladder.position.z) - ladder.forward * 0.5f + side * 0.62f;
                arrived = Walk(_goal, _walkSpeed, dt);
                if (arrived)
                    Face(new Vector3(top.x, transform.position.y, top.z), dt);
            }
            else
            {
                arrived = Walk(_goal, _strollSpeed, dt);
                if (arrived && Time.time >= _pauseUntil)
                {
                    // A new spot near the middle of the room, after a pause.
                    var spot = Random.insideUnitCircle * _roam;
                    _goal = _home + new Vector3(spot.x, 0f, spot.y);
                    _pauseUntil = Time.time + Random.Range(2f, 5f);
                }
            }

            var measuring = _serving != null && arrived;
            if (measuring && Time.time >= _measureUntil)
            {
                _measure = (Measure)Random.Range(0, 5);
                _measureUntil = Time.time + Random.Range(_measureTime.x, _measureTime.y);
            }
            _reach = Mathf.MoveTowards(_reach, measuring ? 1f : 0f, dt * 3f);
            _stoop = Mathf.MoveTowards(_stoop, measuring && _measure == Measure.Leg ? 1f : 0f, dt * 2.5f);
            if (_gait != null)
                _gait.SwingArms = _reach <= 0f;
        }

        // After the walk has posed the arms: the measuring reach, the stoop and the tape.
        private void LateUpdate()
        {
            if (_waist != null)
                _waist.localRotation = Quaternion.Euler(_stoop * 35f, 0f, 0f);
            if (_serving == null && _reach <= 0f)
            {
                if (_tape != null)
                    _tape.gameObject.SetActive(false);
                return;
            }
            if (_serving == null)
                return;

            var (left, right) = Points(_serving.TopPoint);
            var l = Aim(_leftShoulder, left);
            var r = Aim(_rightShoulder, right);
            if (_tape != null)
            {
                var show = _reach > 0.95f;
                _tape.gameObject.SetActive(show);
                if (show)
                {
                    var span = r - l;
                    _tape.SetPositionAndRotation((l + r) / 2f, Quaternion.LookRotation(span.sqrMagnitude > 1e-6f ? span : Vector3.forward, Vector3.up));
                    _tape.localScale = new Vector3(0.035f, 0.006f, Mathf.Max(0.02f, span.magnitude));
                }
            }
        }

        // Where each hand goes on the chef standing on the top step, for the measure being taken.
        private (Vector3 Left, Vector3 Right) Points(Vector3 feet)
        {
            var toChef = Vector3.ProjectOnPlane(feet - transform.position, Vector3.up).normalized;
            var across = Vector3.Cross(Vector3.up, toChef);
            var near = feet - toChef * 0.3f; // the chef's front
            switch (_measure)
            {
                case Measure.Chest: return (near + Vector3.up * 1.0f - across * 0.27f, near + Vector3.up * 1.0f + across * 0.27f);
                case Measure.Waist: return (near + Vector3.up * 0.72f - across * 0.29f, near + Vector3.up * 0.72f + across * 0.29f);
                case Measure.Arm: return (near + Vector3.up * 1.05f + across * 0.28f, near + Vector3.up * 0.55f + across * 0.38f);
                case Measure.Leg: return (near + Vector3.up * 0.5f + across * 0.1f, near + Vector3.up * 0.08f + across * 0.1f);
                default: return (near + Vector3.up * 1.42f - across * 0.25f, near + Vector3.up * 1.42f + across * 0.25f);
            }
        }

        // Turns an arm (which hangs down its shoulder's -up at rest) towards a point, blended by the
        // reach; returns where the hand ends up, which the arm can't stretch past.
        private Vector3 Aim(Transform shoulder, Vector3 target)
        {
            if (shoulder == null)
                return target;
            var towards = target - shoulder.position;
            if (towards.sqrMagnitude < 1e-6f)
                return target;
            var hanging = shoulder.rotation;
            var reaching = Quaternion.FromToRotation(-transform.up, towards.normalized) * transform.rotation;
            shoulder.rotation = Quaternion.Slerp(hanging, reaching, _reach);
            return shoulder.position + towards.normalized * Mathf.Min(towards.magnitude, _armLength);
        }

        // Walks towards a point on the floor; true once there.
        private bool Walk(Vector3 goal, float speed, float dt)
        {
            var offset = Vector3.ProjectOnPlane(goal - transform.position, Vector3.up);
            if (offset.magnitude < 0.05f)
                return true;
            Face(goal, dt);
            transform.position += offset.normalized * Mathf.Min(offset.magnitude, speed * dt);
            return false;
        }

        private void Face(Vector3 point, float dt)
        {
            var look = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (look.sqrMagnitude < 1e-4f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), _turnSpeed * dt);
        }

        // The ladder whose chef climbed up most recently, or null.
        private TailorPlinth Newest()
        {
            TailorPlinth newest = null;
            foreach (var ladder in _ladders)
                if (ladder != null && ladder.Dresser != null && (newest == null || ladder.ClimbedAt > newest.ClimbedAt))
                    newest = ladder;
            return newest;
        }
    }
}
