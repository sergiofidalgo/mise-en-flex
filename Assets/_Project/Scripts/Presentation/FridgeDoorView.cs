using System;
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

        private static readonly Collider[] Hits = new Collider[16];

        private float _amount;
        private int _keptOpenOnFrame = int.MinValue;

        // The door's pose: which side it hinges on and how far it may go this time.
        private Vector3 _leftHinge;
        private bool _hingedRight;
        private float _limit = -1f;
        private Bounds _panel; // in the door's own space, as modelled
        private bool _measured;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen { get; private set; }

        /// <summary>True if, for lack of room on the left, the door last hinged on the right.</summary>
        public bool HingedRight => _hingedRight;

        /// <summary>Degrees the door may open to, from the last look around; the full angle until then.</summary>
        public float OpenLimit => _limit < 0f ? _openAngle : _limit;

        /// <summary>Looks around for room to swing, as it does each time it opens from shut.</summary>
        public void FindRoomToOpen() => LookForRoom(canChangeSide: true);

        /// <summary>Call every frame something should hold the door open.</summary>
        public void KeepOpen() => _keptOpenOnFrame = Time.frameCount;

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
                    Closed?.Invoke();
                }
            }

            _amount = Swing.Step(_amount, open, _openSeconds, _closeSeconds, Time.deltaTime);
            Show(_amount);
        }

        /// <summary>Poses the door, 0 shut to 1 open. Public so it can be previewed outside Play mode.</summary>
        public void Show(float amount)
        {
            Measure();
            var limit = _limit < 0f ? _openAngle : _limit;
            // Blocked, it eases to a stop short of the obstacle; the overshoot that makes a free
            // swing feel thrown open would carry it into whatever is there, so it's cut at the limit.
            var target = limit >= _openAngle ? _openAngle : Mathf.Max(0f, limit - _stopShort);
            var angle = Mathf.Min(Swing.EaseOutBack(amount) * target, limit);
            ApplyHinge();
            _door.localRotation = Quaternion.Euler(0f, _hingedRight ? -angle : angle, 0f);
        }

        // Finds how far the door can swing, and on which side. The side only changes while it's
        // shut, so a door caught mid-close and opened again never jumps across.
        private void LookForRoom(bool canChangeSide)
        {
            Measure();
            if (!canChangeSide)
            {
                _limit = Swing.FreeAngle(_openAngle, _sweepStep, angle => Blocked(_hingedRight, angle));
                return;
            }

            var left = Swing.FreeAngle(_openAngle, _sweepStep, angle => Blocked(hingedRight: false, angle));
            var right = left >= _openAngle ? left : Swing.FreeAngle(_openAngle, _sweepStep, angle => Blocked(hingedRight: true, angle));
            _hingedRight = right > left + _preferLeftBy;
            _limit = _hingedRight ? right : left;
        }

        // Is the door, at this angle on this side, inside anything that isn't this fridge or a person?
        private bool Blocked(bool hingedRight, float angle)
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
                // People step aside: the door doesn't refuse to open because a chef is near.
                if (hit is CharacterController || hit.attachedRigidbody != null)
                    continue;
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
