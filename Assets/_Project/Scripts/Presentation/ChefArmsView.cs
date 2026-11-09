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
        [Tooltip("How long a flip takes (s): a toss of the pan at a hob, a flick of the hand at a grill.")]
        [SerializeField, Min(0.05f)] private float _flipTime = 0.4f;
        [Tooltip("How high the pan lifts off the hob in a toss (m), and how far it tips (degrees).")]
        [SerializeField, Min(0f)] private float _tossHeight = 0.12f;
        [SerializeField, Range(0f, 45f)] private float _tossTilt = 18f;
        [Tooltip("Where the hand flicks a flip at a grill, from the chef (m), and how high.")]
        [SerializeField] private Vector3 _flipHand = new Vector3(0.1f, 1.02f, 0.5f);
        [SerializeField, Min(0f)] private float _flipHeight = 0.16f;

        [Header("Washing")]
        [Tooltip("Where the left hand holds the plate and the right hand scrubs it, from the chef (m) (the user's reference).")]
        [SerializeField] private Vector3 _washHold = new Vector3(-0.2f, 0.85f, 0.5f);
        [SerializeField] private Vector3 _washScrub = new Vector3(0.06f, 0.87f, 0.55f);
        [Tooltip("How fast and how wide the scrubbing hand circles.")]
        [SerializeField, Min(0f)] private float _scrubRate = 9f;
        [SerializeField, Min(0f)] private float _scrubSize = 0.07f;

        [Header("Walking and other carried things")]
        [Tooltip("How far each hand grips to the side of the carry point (m).")]
        [SerializeField, Min(0f)] private float _gripWidth = 0.17f;
        [Tooltip("Degrees each arm swings either way while walking at full speed.")]
        [SerializeField, Range(0f, 80f)] private float _swingAngle = 32f;
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
        }

        // Puts a tossed pan back down flat on its hob.
        private void EndToss()
        {
            if (_tossedPan != null)
                _tossedPan.SetHeldPose(Vector3.zero, Quaternion.identity, immediate: true);
            _tossedPan = null;
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
            if (_tossedPan != null)
            {
                if (_strokeAge >= _flipTime)
                    EndToss();
                else
                {
                    // Up and back down, tipping its far side up then down so the food goes over.
                    var t = _strokeAge / _flipTime;
                    _tossedPan.SetHeldPose(Vector3.up * (Mathf.Sin(Mathf.PI * t) * _tossHeight),
                        Quaternion.Euler(-Mathf.Sin(2f * Mathf.PI * t) * _tossTilt, 0f, 0f), immediate: true);
                }
            }

            var carried = _interactor.Hands.Current != null ? _carryAnchor.GetComponentInChildren<ItemView>() : null;
            var isPan = _interactor.Hands.Current is Game.Simulation.Cooking.Pan;
            if (carried != null && isPan)
                carried.SetHeldPose(_panOffset, Quaternion.Euler(0f, _panTurn, 0f));

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
                leftAim = _carryAnchor.position - right * _gripWidth;
                rightAim = _carryAnchor.position + right * _gripWidth;
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
