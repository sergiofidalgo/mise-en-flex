using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Moves a chef's arms with what the chef is doing (the user's note, 2026-11-08), so a held pan
    /// or knife is held, not floating, and the chef acts out the work (the user's references): arms
    /// swing as the chef walks; both hands grip whatever is carried, a pan side by side on its
    /// handle; a knife rides in the belt and only comes out at the board, where each cut is a chop
    /// and the other hand steadies the food; a flip at a hob tosses the pan one-handed by its handle, the other hand
    /// keeping hold of anything carried; washing holds the plate
    /// in one hand and scrubs with the other. Each arm turns about its shoulder and hangs straight
    /// down at rest; poses blend smoothly. Hands only reach to points just in front of the chef at
    /// counter height, over the station they face, so arms never reach into walls or furniture.
    /// </summary>
    public sealed class ChefArmsView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private Transform _leftShoulder;
        [SerializeField] private Transform _rightShoulder;
        [Tooltip("Optional: the legs' walk. When set, the arms swing in step with it, against the legs.")]
        [SerializeField] private WalkCycleView _gait;
        [Tooltip("Where carried things are held; both hands grip either side of it.")]
        [SerializeField] private Transform _carryAnchor;
        [Tooltip("The knife's place; it rides in the belt or comes out at the board from here.")]
        [SerializeField] private Transform _toolAnchor;

        [Header("Holding a pan by its handle")]
        [Tooltip("Degrees the pan turns from straight ahead; its handle points back towards the chef.")]
        [SerializeField] private float _panTurn = -15f;
        [Tooltip("Where the pan sits from the carry point, in its frame (m, before the held scale).")]
        [SerializeField] private Vector3 _panOffset = new Vector3(-0.04f, -0.08f, 0.2f);
        [Tooltip("Where the hands grip the handle, side by side, in the pan's own frame.")]
        [SerializeField] private Vector3 _panGripNear = new Vector3(0f, 0.1f, -0.29f);
        [SerializeField] private Vector3 _panGripFar = new Vector3(0f, 0.11f, -0.4f);

        [Header("The knife")]
        [Tooltip("The sheath on the hip: shown only while the chef has a knife, so you can tell at a glance (the user's note, 2026-11-08).")]
        [SerializeField] private Renderer[] _sheath = System.Array.Empty<Renderer>();
        [Tooltip("Where a picked-up knife rides, in the sheath on the right hip, from the tool point (its frame, m before the held scale): its bolster at the sheath's mouth (CHEF_SHEATH in the Blender script). It only comes out at the board (the user's notes, 2026-11-08).")]
        [SerializeField] private Vector3 _beltOffset = new Vector3(-0.076f, -0.069f, -0.177f);
        [Tooltip("The knife's turn in the sheath: blade straight down, flat against the hip.")]
        [SerializeField] private Vector3 _beltTurn = new Vector3(90f, 90f, 0f);
        [Tooltip("Where the knife is held over the board, from the tool point: pointing away from the chef, edge down (the user's reference).")]
        [SerializeField] private Vector3 _boardOffset = new Vector3(-0.2f, 0.06f, 0.26f);
        [SerializeField] private Vector3 _boardTurn = new Vector3(0f, -8f, -90f);
        [Tooltip("Where the other hand steadies the food on the board, from the chef (m).")]
        [SerializeField] private Vector3 _steadyHand = new Vector3(-0.08f, 0.98f, 0.62f);
        [Tooltip("Where the knife hand grips, in the knife's own frame.")]
        [SerializeField] private Vector3 _knifeGrip = new Vector3(0f, 0f, -0.08f);
        [Tooltip("How high the knife lifts between cuts, and how long one chop takes (s).")]
        [SerializeField, Min(0f)] private float _chopHeight = 0.09f;
        [SerializeField, Min(0.05f)] private float _chopTime = 0.22f;

        [Header("Flipping")]
        // Exaggerated so a flip reads from the overhead camera in a busy kitchen (the user's
        // playtest, 2026-11-09: the first, realistic toss was very hard to notice).
        [Tooltip("How long a flip takes (s): a toss of the pan at a hob, a flick of the hand at a grill.")]
        [SerializeField, Min(0.05f)] private float _flipTime = 0.6f;
        [Tooltip("How high the pan lifts off the hob in a toss (m), and how far it tips (degrees).")]
        [SerializeField, Min(0f)] private float _tossHeight = 0.25f;
        [SerializeField, Range(0f, 60f)] private float _tossTilt = 35f;
        [Tooltip("How high the food jumps out of the pan or off the grill (m); it turns a full somersault on the way.")]
        [SerializeField, Min(0f)] private float _foodJump = 0.4f;
        [Tooltip("Where the hand flicks a flip at a grill, from the chef (m), and how high.")]
        [SerializeField] private Vector3 _flipHand = new Vector3(0.1f, 1.02f, 0.5f);
        [SerializeField, Min(0f)] private float _flipHeight = 0.32f;

        [Header("Washing")]
        [Tooltip("Where the left hand holds the plate and the right hand scrubs it, from the chef (m) (the user's reference).")]
        [SerializeField] private Vector3 _washHold = new Vector3(-0.2f, 0.85f, 0.5f);
        [SerializeField] private Vector3 _washScrub = new Vector3(0.06f, 0.87f, 0.55f);
        [Tooltip("How fast and how wide the scrubbing hand circles.")]
        [SerializeField, Min(0f)] private float _scrubRate = 9f;
        [SerializeField, Min(0f)] private float _scrubSize = 0.07f;

        [Header("Walking and other carried things")]
        [Tooltip("How far each hand grips to the side of the carry point (m), for a pan's toss.")]
        [SerializeField, Min(0f)] private float _gripWidth = 0.17f;
        [Tooltip("From the shoulder joint to the middle of the hand, in the shoulder's units: the arm is rigid, so the hand always ends up this far out.")]
        [SerializeField, Min(0.01f)] private float _handReach = 0.48f;
        [Tooltip("The hand's radius, in the shoulder's units: hands sit this far outside what they hold.")]
        [SerializeField, Min(0f)] private float _handRadius = 0.08f;
        [Tooltip("How far in front of the shoulders' line the chef's front is, in the shoulder's units: what's carried stays clear of it.")]
        [SerializeField, Min(0f)] private float _bodyClearance = 0.3f;
        [Tooltip("Degrees each arm swings either way while walking at full speed.")]
        [SerializeField, Range(0f, 80f)] private float _swingAngle = 20f;
        [Tooltip("Swings a second, in radians of the swing's cycle, at full speed.")]
        [SerializeField, Min(0f)] private float _swingRate = 9f;
        [Tooltip("The walking speed (m/s) that gives a full swing.")]
        [SerializeField, Min(0.1f)] private float _fullSpeed = 3f;
        [Tooltip("How quickly the arms move into a new pose; higher is snappier.")]
        [SerializeField, Min(0.1f)] private float _follow = 14f;

        private enum Stroke { None, Chop, Flip }

        private Vector3 _lastPosition;
        private float _speed;
        private float _phase;
        private float _walking;
        private Stroke _stroke;
        private float _strokeAge;
        private ItemView _tossedPan;
        // The food in the air during a flip, with where it rests, to put it back.
        private readonly System.Collections.Generic.List<(ItemView View, Vector3 Position, Quaternion Rotation)> _flying =
            new System.Collections.Generic.List<(ItemView, Vector3, Quaternion)>();

        private void OnEnable()
        {
            _lastPosition = transform.position;
            if (_interactor != null)
                _interactor.Acted += HandleActed;
        }

        private void OnDisable()
        {
            EndToss();
            if (_interactor != null)
                _interactor.Acted -= HandleActed;
        }

        // A chop at the board, or a flip at a hob or grill.
        private void HandleActed(Interactable station)
        {
            EndToss();
            if (station is PrepStation)
                _stroke = Stroke.Chop;
            else if (station is HobStation || station is GrillStation)
                _stroke = Stroke.Flip;
            else
                return;
            _strokeAge = 0f;
            // At a hob the chef tosses the pan on it by its handle (the user's references); the first
            // item view under the hob is its pan, the food under that.
            _tossedPan = station is HobStation ? station.GetComponentInChildren<ItemView>() : null;
            if (_stroke == Stroke.Flip)
            {
                // The food on the hob (in its pan) or on the grill goes up with the flip.
                foreach (var food in station.GetComponentsInChildren<ItemView>())
                    if (food != _tossedPan)
                        _flying.Add((food, food.transform.localPosition, food.transform.localRotation));
            }
        }

        // Puts a tossed pan back down flat on its hob, and the food back where it lay.
        private void EndToss()
        {
            if (_tossedPan != null)
                _tossedPan.SetHeldPose(Vector3.zero, Quaternion.identity, immediate: true);
            _tossedPan = null;
            foreach (var (view, position, rotation) in _flying)
                if (view != null)
                    view.SetHeldPose(position, rotation, immediate: true);
            _flying.Clear();
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // How fast the chef is walking, from how far they moved, smoothed so it doesn't flicker.
            var moved = transform.position - _lastPosition;
            moved.y = 0f;
            _lastPosition = transform.position;
            _speed = Mathf.Lerp(_speed, moved.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
            _walking = Mathf.Clamp01(_speed / _fullSpeed);
            _phase += dt * _swingRate * _walking;
            if (_gait != null)
            {
                // In step with the legs: each arm swings back as the leg on its side goes forward.
                _phase = _gait.Phase;
                _walking = _gait.Walking;
            }
            _strokeAge += dt;
            if (_tossedPan != null || _flying.Count > 0)
            {
                if (_strokeAge >= _flipTime)
                    EndToss();
                else
                {
                    var t = _strokeAge / _flipTime;
                    // The pan: up and back down, tipping its far side up then down to throw the food.
                    if (_tossedPan != null)
                        _tossedPan.SetHeldPose(Vector3.up * (Mathf.Sin(Mathf.PI * t) * _tossHeight),
                            Quaternion.Euler(-Mathf.Sin(2f * Mathf.PI * t) * _tossTilt, 0f, 0f), immediate: true);
                    // The food leaves a moment after the pan starts up, rises well above it turning a
                    // full somersault, and lands as the pan settles: the flip you can see.
                    var air = Mathf.Clamp01((t - 0.12f) / 0.8f);
                    var lift = Mathf.Sin(Mathf.PI * air) * _foodJump;
                    var turn = Quaternion.Euler(-360f * Mathf.SmoothStep(0f, 1f, air), 0f, 0f);
                    foreach (var (view, position, rotation) in _flying)
                        if (view != null)
                            view.SetHeldPose(position + Vector3.up * lift, rotation * turn, immediate: true);
                }
            }

            var carried = _interactor.Hands.Current != null ? _carryAnchor.GetComponentInChildren<ItemView>() : null;
            var isPan = _interactor.Hands.Current is Game.Simulation.Cooking.Pan;
            if (carried != null && isPan)
                carried.SetHeldPose(_panOffset, Quaternion.Euler(0f, _panTurn, 0f));
            else if (carried != null)
                carried.SetHeldPose(Grip(carried).Offset, Quaternion.identity);

            // The knife stays in the belt except facing the board with empty hands.
            var atBoard = _interactor.Target is PrepStation && _interactor.Hands.Current == null;
            var knife = _interactor.Tool != null ? _toolAnchor.GetComponentInChildren<ItemView>() : null;
            ShowSheath(_interactor.Tool != null);
            if (knife != null)
            {
                if (atBoard)
                    knife.SetHeldPose(_boardOffset + Vector3.up * (ChopLift() / _toolAnchor.lossyScale.y),
                        Quaternion.Euler(_boardTurn), immediate: _stroke == Stroke.Chop);
                else
                    knife.SetHeldPose(_beltOffset, Quaternion.Euler(_beltTurn));
            }
            Pose(carried, isPan, atBoard ? knife : null, _interactor.IsBusy, 1f - Mathf.Exp(-_follow * dt));
        }

        /// <summary>
        /// Poses the chef at once for a still picture in the Editor (the capture tool): carrying
        /// whatever is under the carry point (a pan if <paramref name="pan"/>) with any knife tucked
        /// in the belt; or, if <paramref name="cutting"/>, at the board with the knife raised.
        /// </summary>
        public void PreviewHolding(bool pan, bool cutting = false)
        {
            _stroke = Stroke.None;
            var carried = _carryAnchor.GetComponentInChildren<ItemView>();
            if (carried != null && pan)
            {
                carried.transform.localPosition = _panOffset;
                carried.transform.localRotation = Quaternion.Euler(0f, _panTurn, 0f);
            }
            else if (carried != null)
            {
                carried.transform.localPosition = Grip(carried).Offset;
                carried.transform.localRotation = Quaternion.identity;
            }
            var knife = _toolAnchor.GetComponentInChildren<ItemView>();
            ShowSheath(knife != null);
            if (knife != null)
            {
                knife.transform.localPosition = cutting
                    ? _boardOffset + Vector3.up * (_chopHeight / _toolAnchor.lossyScale.y)
                    : _beltOffset;
                knife.transform.localRotation = Quaternion.Euler(cutting ? _boardTurn : _beltTurn);
            }
            Pose(carried, pan, cutting ? knife : null, false, 1f);
        }

        private void ShowSheath(bool shown)
        {
            foreach (var part in _sheath)
                if (part != null && part.enabled != shown)
                    part.enabled = shown;
        }

        // How far the knife is above the board: raised between cuts, down to it and back in a chop.
        private float ChopLift()
        {
            if (_stroke != Stroke.Chop || _strokeAge >= _chopTime)
                return _chopHeight;
            var t = _strokeAge / _chopTime;
            // Down fast, up slower: the chop lands a third of the way in.
            var down = t < 0.33f ? t / 0.33f : 1f - (t - 0.33f) / 0.67f;
            return _chopHeight * (1f - down);
        }

        private void Pose(ItemView carried, bool isPan, ItemView knifeAtBoard, bool busy, float blend)
        {
            var right = transform.right;
            var up = transform.up;
            var swing = Mathf.Sin(_phase) * _swingAngle * _walking;
            var leftAim = Swing(_leftShoulder, swing, right, up);
            var rightAim = Swing(_rightShoulder, -swing, right, up);

            if (carried != null && isPan)
            {
                // Both hands side by side on the pan's handle, the pan out in front (the user's
                // reference, 2026-11-08), not flat like a tray.
                leftAim = carried.transform.TransformPoint(_panGripNear);
                rightAim = carried.transform.TransformPoint(_panGripFar);
            }
            else if (carried != null)
            {
                var grip = Grip(carried);
                leftAim = grip.Left;
                rightAim = grip.Right;
            }
            else if (knifeAtBoard != null)
            {
                // Cutting (the user's reference): the right hand on the knife, the left steadying the food.
                rightAim = knifeAtBoard.transform.TransformPoint(_knifeGrip);
                leftAim = transform.TransformPoint(_steadyHand);
            }
            else if (busy)
            {
                // Washing (the user's reference): the left hand holds the plate, the right scrubs it in circles.
                var t = Time.time * _scrubRate;
                leftAim = transform.TransformPoint(_washHold);
                rightAim = transform.TransformPoint(_washScrub)
                    + (right * Mathf.Cos(t) + transform.forward * Mathf.Sin(t)) * _scrubSize;
            }

            if (_tossedPan != null)
            {
                // Tossing the pan on the hob one-handed, the right hand on its handle moving with it
                // (the user's reference); the left keeps hold of anything carried, a plate say.
                rightAim = _tossedPan.transform.TransformPoint(_panGripFar);
                if (carried != null)
                    leftAim = _carryAnchor.position - right * (_gripWidth * 0.5f);
                blend = 1f;
            }
            else if (_stroke == Stroke.Flip && _strokeAge < _flipTime && carried == null)
            {
                // A flip at the grill: the right hand flicks up and back down over it.
                var lift = Mathf.Sin(Mathf.PI * _strokeAge / _flipTime) * _flipHeight;
                rightAim = transform.TransformPoint(_flipHand) + up * lift;
                blend = 1f;
            }
            else if (_stroke == Stroke.Chop && knifeAtBoard != null)
            {
                blend = 1f; // the hand stays on the knife through a fast chop
            }

            Aim(_leftShoulder, leftAim, up, blend);
            Aim(_rightShoulder, rightAim, up, blend);
        }

        /// <summary>
        /// Where the hands hold <paramref name="carried"/>, and where it sits for them (the user's
        /// note, 2026-11-10: hands went through a tray). The arms are rigid and only turn, so a hand
        /// always ends a reach from its shoulder: the grip is worked out from that, and the item
        /// moves to meet the hands rather than the hands to meet the item. By its sides, the hands
        /// sit just outside its left and right, halfway up; under it (trays, plates), the palms are
        /// just below its base. Its base stays at the carry point's height, so it shows as before,
        /// and it's held nearer its back if holding it by the middle would put its back into the chef.
        /// Returns the item's offset from the carry point (in the carry point's space) and the two
        /// hands' world positions.
        /// </summary>
        public (Vector3 Offset, Vector3 Left, Vector3 Right) Grip(ItemView carried)
        {
            // Metres, in the chef's frame about the carry point: x right, y up, z forward.
            var right = transform.right;
            var up = transform.up;
            var forward = transform.forward;
            var origin = _carryAnchor.position;
            Vector3 Local(Vector3 world)
            {
                var d = world - origin;
                return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, forward));
            }
            Vector3 World(Vector3 local) => origin + right * local.x + up * local.y + forward * local.z;

            var scale = carried.transform.lossyScale;
            var shape = carried.Shape;
            var min = Vector3.Scale(shape.min, scale);
            var max = Vector3.Scale(shape.max, scale);
            var centre = (min + max) * 0.5f;
            var chef = _leftShoulder.lossyScale.y;
            var reach = _handReach * chef;
            var palm = _handRadius * chef;
            var leftShoulder = Local(_leftShoulder.position);
            var rightShoulder = Local(_rightShoulder.position);
            var shoulderX = Mathf.Abs(rightShoulder.x - leftShoulder.x) * 0.5f;
            var midX = (rightShoulder.x + leftShoulder.x) * 0.5f;
            var shoulderY = (rightShoulder.y + leftShoulder.y) * 0.5f;
            var shoulderZ = (rightShoulder.z + leftShoulder.z) * 0.5f;

            float handX;
            float gripY; // the grip's height above the item's origin
            if (carried.Grip == ItemView.CarryGrip.Under)
            {
                var half = (max.x - min.x) * 0.5f;
                handX = Mathf.Clamp(shoulderX * 0.8f, palm * 0.5f, Mathf.Max(half - palm, palm * 0.5f));
                gripY = min.y - palm;
            }
            else
            {
                handX = (max.x - min.x) * 0.5f + palm;
                gripY = centre.y;
            }

            // The hand is a reach from the shoulder: dx² + dy² + dz² = reach². It stays at least a
            // third of a reach in front; if the height asks for more, the hands come up or down
            // towards the shoulders instead, and the item with them.
            var handY = gripY;
            var dx = handX - shoulderX;
            var dy = handY - shoulderY;
            var ahead = reach / 3f;
            var across = Mathf.Max(reach * reach - dx * dx, ahead * ahead);
            if (dy * dy > across - ahead * ahead)
            {
                dy = Mathf.Sign(dy) * Mathf.Sqrt(across - ahead * ahead);
                handY = shoulderY + dy;
            }
            var handZ = shoulderZ + Mathf.Sqrt(Mathf.Max(across - dy * dy, 0f));

            // Held at its middle, unless that puts its back into the chef's front; never off its back.
            var front = shoulderZ + _bodyClearance * chef;
            var gripZ = Mathf.Min(centre.z, handZ + min.z - front);
            gripZ = Mathf.Max(gripZ, Mathf.Min(min.z + palm, centre.z));

            var offset = new Vector3(midX - centre.x, handY - gripY, handZ - gripZ);
            return (_carryAnchor.InverseTransformPoint(World(offset)),
                World(new Vector3(midX - handX, handY, handZ)),
                World(new Vector3(midX + handX, handY, handZ)));
        }

        // A point straight below the shoulder, swung forward or back about the chef's right.
        private static Vector3 Swing(Transform shoulder, float degrees, Vector3 right, Vector3 up) =>
            shoulder.position + Quaternion.AngleAxis(degrees, right) * -up;

        // Turns the arm, which hangs down the chef's -up at rest, to point at the target.
        private void Aim(Transform shoulder, Vector3 target, Vector3 up, float blend)
        {
            var towards = target - shoulder.position;
            if (towards.sqrMagnitude < 1e-6f)
                return;
            var pose = Quaternion.FromToRotation(-up, towards.normalized) * transform.rotation;
            shoulder.rotation = Quaternion.Slerp(shoulder.rotation, pose, blend);
        }
    }
}
