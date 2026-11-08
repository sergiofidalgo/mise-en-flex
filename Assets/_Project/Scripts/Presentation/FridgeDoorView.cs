using System;
using System.Collections.Generic;
using Game.Gameplay;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Opens a fridge's door while anyone has its menu open (RT held), with a puff of cold air as
    /// it opens. A chef's <see cref="StorageUseView"/> keeps it open each frame, so two players at
    /// one fridge keep it open until both let go. Opened and Closed are for sound.
    ///
    /// The door knows its surroundings: each time it opens from shut, it checks how far it can
    /// swing on each side before touching a wall or furniture, hinges on the side with more room,
    /// and stops short of whatever is there instead of passing through it. Players can put a
    /// fridge anywhere, so no position is assumed.
    /// </summary>
    public sealed class FridgeDoorView : MonoBehaviour
    {
        [Tooltip("Turns about its hinge; closed at no rotation. Modelled hinged on the fridge's left.")]
        [SerializeField] private Transform _door;
        [SerializeField] private ParticleSystem _mist;

        [Tooltip("Degrees the door swings open, outwards towards the camera, when nothing is in the way.")]
        [SerializeField, Range(0f, 150f)] private float _openAngle = 105f;
        [SerializeField, Min(0.01f)] private float _openSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float _closeSeconds = 0.3f;
        [Tooltip("Puffs of cold air when the door opens.")]
        [SerializeField, Min(0)] private int _mistBurst = 14;

        [Header("Room to swing")]
        [Tooltip("Degrees between the angles checked for room to swing.")]
        [SerializeField, Range(1f, 15f)] private float _sweepStep = 5f;
        [Tooltip("Degrees it stops short of something in the way, so it never looks pressed into it.")]
        [SerializeField, Range(0f, 20f)] private float _stopShort = 4f;
        [Tooltip("Metres the door is taken as slimmer when checking, so touching a neighbour's face isn't a hit.")]
        [SerializeField, Range(0f, 0.05f)] private float _clearance = 0.02f;
        [Tooltip("Degrees more room the right-hand side needs before the door hinges there instead.")]
        [SerializeField, Range(0f, 45f)] private float _preferLeftBy = 10f;

        [Header("Making way")]
        [Tooltip("Metres per second a chef who opens the door shuffles aside, out of its swing.")]
        [SerializeField, Min(0.1f)] private float _stepAsideSpeed = 3f;
        [Tooltip("Metres a chef keeps from the door's swing once they've stepped aside.")]
        [SerializeField, Range(0f, 0.3f)] private float _stepClearance = 0.08f;
        [Tooltip("Degrees per second the door swings on once whoever held it back is clear. It gives way to anyone at once.")]
        [SerializeField, Min(10f)] private float _followSpeed = 500f;

        private static readonly Collider[] Hits = new Collider[16];

        private float _amount;
        private int _keptOpenOnFrame = int.MinValue;

        // The door's pose: which side it hinges on and how far walls and furniture let it go.
        private Vector3 _leftHinge;
        private bool _hingedRight;
        private float _limit = -1f;
        private Bounds _panel; // in the door's own space, as modelled
        private bool _measured;

        // How far it may go this frame, people included, eased so it never jumps.
        private float _shownRoom = float.MaxValue;

        // Past any overshoot: as good as no limit.
        private float Unlimited => _openAngle * 1.5f;

        // Chefs holding it open, by the frame they last did; and those already sent aside this opening.
        private readonly Dictionary<PlayerMovement, int> _openers = new Dictionary<PlayerMovement, int>();
        private readonly HashSet<PlayerMovement> _madeWay = new HashSet<PlayerMovement>();
        private readonly List<PlayerMovement> _gone = new List<PlayerMovement>();

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen { get; private set; }

        /// <summary>True if, for lack of room on the left, the door last hinged on the right.</summary>
        public bool HingedRight => _hingedRight;

        /// <summary>Degrees walls and furniture let the door open to, from the last look around; the full angle until then.</summary>
        public float OpenLimit => _limit < 0f ? _openAngle : _limit;

        /// <summary>Looks around for room to swing, as it does each time it opens from shut.</summary>
        public void FindRoomToOpen() => LookForRoom(canChangeSide: true);

        /// <summary>
        /// Call every frame something should hold the door open. A chef who opens it steps aside,
        /// out of its swing, if they're standing in it.
        /// </summary>
        public void KeepOpen(PlayerMovement chef = null)
        {
            _keptOpenOnFrame = Time.frameCount;
            if (chef != null)
                _openers[chef] = Time.frameCount;
        }

        private void Update()
        {
            // One frame's grace, so the order scripts run in doesn't make it flicker shut.
            var open = Time.frameCount - _keptOpenOnFrame <= 1;
            if (open != IsOpen)
            {
                IsOpen = open;
                if (open)
                {
                    LookForRoom(canChangeSide: _amount <= 0f);
                    _mist.Emit(_mistBurst);
                    Opened?.Invoke();
                }
                else
                {
                    _madeWay.Clear();
                    Closed?.Invoke();
                }
            }

            if (open)
                MakeWay();

            _amount = Swing.Step(_amount, open, _openSeconds, _closeSeconds, Time.deltaTime);
            if (_amount > 0f)
            {
                // Walls and furniture, and now people too: anyone in the swing holds the door back
                // (the opener until they've shuffled clear), and it gives way to anyone who walks in.
                Measure();
                var room = Swing.FreeAngle(_openAngle, _sweepStep, angle => Blocked(_hingedRight, angle, people: true));
                var allowed = room >= _openAngle ? Unlimited : Mathf.Max(0f, room - _stopShort);
                // It never passes through anyone, so it gives way at once; it follows them out at its own pace.
                var shown = Mathf.Min(_shownRoom, Unlimited);
                _shownRoom = allowed < shown ? allowed : Mathf.MoveTowards(shown, allowed, _followSpeed * Time.deltaTime);
            }
            else
            {
                _shownRoom = Unlimited;
            }
            Show(_amount);
        }

        /// <summary>Poses the door, 0 shut to 1 open. Public so it can be previewed outside Play mode.</summary>
        public void Show(float amount)
        {
            Measure();
            // Free, it's thrown open and overshoots a little; held back by something, it stops short of it.
            var angle = Mathf.Min(Swing.EaseOutBack(amount) * _openAngle, _shownRoom);
            ApplyHinge();
            _door.localRotation = Quaternion.Euler(0f, _hingedRight ? -angle : angle, 0f);
        }

        // Sends each chef who opened the door, once per opening, to the side away from the hinge if
        // they're standing in its swing. Like anyone opening a fridge, they make room for the door.
        private void MakeWay()
        {
            _gone.Clear();
            foreach (var pair in _openers)
            {
                var chef = pair.Key;
                if (chef == null || Time.frameCount - pair.Value > 1)
                {
                    _gone.Add(chef);
                    continue;
                }
                if (!_madeWay.Add(chef) || !chef.IsReady)
                    continue;

                Measure();
                var local = transform.InverseTransformPoint(chef.transform.position);
                var at = new Vector2(local.x, local.z);
                var hinge = new Vector2(_hingedRight ? -_leftHinge.x : _leftHinge.x, _leftHinge.z);
                var radius = chef.BodyRadius + _stepClearance + _panel.extents.z;
                var length = _panel.max.x;
                var swing = OpenLimit;
                if (!Swing.SweepTouches(hinge, length, _hingedRight, swing, _sweepStep, at, radius))
                    continue;

                var spot = Swing.StepAsideSpot(hinge, _hingedRight, at, radius);
                chef.StepAside(transform.TransformPoint(new Vector3(spot.x, local.y, spot.y)), transform.position, _stepAsideSpeed);
            }
            foreach (var chef in _gone)
                _openers.Remove(chef);
        }

        // Finds how far walls and furniture let the door swing, and on which side. The side only
        // changes while it's shut, so a door caught mid-close and opened again never jumps across.
        // People don't choose the side: they step aside instead.
        private void LookForRoom(bool canChangeSide)
        {
            Measure();
            if (!canChangeSide)
            {
                _limit = Swing.FreeAngle(_openAngle, _sweepStep, angle => Blocked(_hingedRight, angle, people: false));
                return;
            }

            var left = Swing.FreeAngle(_openAngle, _sweepStep, angle => Blocked(hingedRight: false, angle, people: false));
            var right = left >= _openAngle ? left : Swing.FreeAngle(_openAngle, _sweepStep, angle => Blocked(hingedRight: true, angle, people: false));
            _hingedRight = right > left + _preferLeftBy;
            _limit = _hingedRight ? right : left;
        }

        // Is the door, at this angle on this side, inside anything but this fridge and loose items?
        private bool Blocked(bool hingedRight, float angle, bool people)
        {
            var side = hingedRight ? -1f : 1f;
            var hinge = new Vector3(_leftHinge.x * side, _leftHinge.y, _leftHinge.z);
            var turn = Quaternion.Euler(0f, angle * side, 0f);
            // Hinged on the right, the door is mirrored: its panel reaches to the fridge's left.
            var centre = Vector3.Scale(_panel.center, new Vector3(side, 1f, 1f));
            var world = transform.TransformPoint(hinge + turn * centre);
            var scale = transform.lossyScale;
            var extents = Vector3.Scale(_panel.extents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            extents = Vector3.Max(extents - Vector3.one * _clearance, Vector3.one * 0.001f);

            var count = Physics.OverlapBoxNonAlloc(world, extents, Hits, transform.rotation * turn, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.transform.IsChildOf(transform))
                    continue;
                var isPerson = hit is CharacterController || hit.GetComponentInParent<IPushable>() != null;
                if (isPerson)
                {
                    if (people)
                        return true;
                    continue;
                }
                if (hit.attachedRigidbody != null)
                    continue; // loose items don't stop a door
                return true;
            }
            return false;
        }

        private void ApplyHinge()
        {
            _door.localPosition = new Vector3(_hingedRight ? -_leftHinge.x : _leftHinge.x, _leftHinge.y, _leftHinge.z);
            var scale = _door.localScale;
            scale.x = _hingedRight ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            _door.localScale = scale;
        }

        // The door as built: where its left hinge is and the box its panel fills in its own space,
        // measured once from its meshes so any door model works.
        private void Measure()
        {
            if (_measured)
                return;
            _measured = true;
            _leftHinge = _door.localPosition;

            var any = false;
            foreach (var filter in _door.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                var bounds = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var inDoor = _door.InverseTransformPoint(filter.transform.TransformPoint(local));
                    if (any)
                    {
                        _panel.Encapsulate(inDoor);
                    }
                    else
                    {
                        _panel = new Bounds(inDoor, Vector3.zero);
                        any = true;
                    }
                }
            }
        }
    }
}
