using Game.Data;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>
    /// The scene object for one <see cref="Item"/>. It parents itself to whatever holds
    /// the item (hands, counter, a pan's slot) and flies there in a short arc so moves are
    /// easy to follow. Containers (pans, plates) expose slots where their contents sit.
    /// </summary>
    public sealed class ItemView : MonoBehaviour
    {
        /// <summary>How a chef holds the item (ChefArmsView). Pans are held by the handle instead.</summary>
        public enum CarryGrip
        {
            /// <summary>Hands on its left and right sides, halfway up: food, pots, containers.</summary>
            Sides,
            /// <summary>Palms under its base, the item resting on them: trays and plates.</summary>
            Under,
        }

        [Tooltip("For containers: where each content item sits, in content order.")]
        [SerializeField] private Transform[] _slotAnchors = System.Array.Empty<Transform>();
        [Tooltip("How a chef holds it: by its sides, or resting on the palms (trays, plates).")]
        [SerializeField] private CarryGrip _grip = CarryGrip.Sides;

        private Vector3 _startLocalPosition;
        private Vector3 _startLocalScale;
        private Vector3 _baseScale;
        private ItemMoveSettings _move;
        private float _elapsed;
        private bool _moving;
        private bool _posed;
        private Vector3 _poseOffset;
        private Quaternion _poseRotation = Quaternion.identity;
        private float _poseScale = 1f;
        private Bounds? _shape;

        public Item Item { get; private set; }

        public CarryGrip Grip => _grip;

        /// <summary>
        /// The item's own shape in its local space (its visible meshes, not what's on it or in it),
        /// for the hands to grip its outside rather than pass through it. Measured once.
        /// </summary>
        public Bounds Shape => _shape ??= MeasureShape();

        private Bounds MeasureShape()
        {
            var found = false;
            var shape = new Bounds();
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled
                    || filter.GetComponentInParent<ItemView>() != this)
                    continue;
                var mesh = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z);
                    var point = transform.InverseTransformPoint(filter.transform.TransformPoint(local));
                    if (found)
                        shape.Encapsulate(point);
                    else
                    {
                        shape = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                }
            }
            return shape;
        }

        /// <summary>The anchor this view was last sent to.</summary>
        public Transform Anchor { get; private set; }

        public void Initialize(Item item)
        {
            Item = item;
            _baseScale = transform.localScale;
        }

        /// <summary>Where content number <paramref name="index"/> sits; the container itself if it has no slot for it.</summary>
        public Transform SlotAnchor(int index) =>
            index >= 0 && index < _slotAnchors.Length ? _slotAnchors[index] : transform;

        /// <summary>
        /// Holds the item at an offset and turn from its anchor, for how it looks in a chef's hands
        /// (a pan held by its handle, decision 2026-11-08), or how it lies (a knife shown bigger on a
        /// counter), optionally scaled. Eased into; cleared by the next move. Only presentation:
        /// where the item is held for play doesn't change.
        /// </summary>
        public void SetHeldPose(Vector3 localOffset, Quaternion localRotation, bool immediate = false, float scale = 1f)
        {
            _posed = true;
            _poseOffset = localOffset;
            _poseRotation = localRotation;
            _poseScale = scale;
            if (immediate && !_moving)
            {
                transform.localPosition = localOffset;
                transform.localRotation = localRotation;
                transform.localScale = _baseScale * scale;
            }
        }

        public void MoveTo(Transform anchor, ItemMoveSettings move)
        {
            _posed = false;
            _poseOffset = Vector3.zero;
            _poseRotation = Quaternion.identity;
            _poseScale = 1f;
            Anchor = anchor;
            transform.SetParent(anchor, worldPositionStays: true);
            transform.localRotation = Quaternion.identity;
            _startLocalPosition = transform.localPosition;
            // Anchors can show items at different sizes (bigger on a counter than in the fridge):
            // reparenting keeps the size it had, and the flight eases it to the new one.
            _startLocalScale = transform.localScale;
            _move = move;
            _elapsed = 0f;
            _moving = true;

            if (move.Duration <= 0f)
                Finish();
        }

        private void Update()
        {
            if (!_moving)
            {
                if (_posed)
                {
                    var ease = 1f - Mathf.Exp(-14f * Time.deltaTime);
                    transform.localPosition = Vector3.Lerp(transform.localPosition, _poseOffset, ease);
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, _poseRotation, ease);
                    transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * _poseScale, ease);
                }
                return;
            }

            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / _move.Duration);
            if (t >= 1f)
            {
                Finish();
                return;
            }

            // Ease-out so the item leaves fast and settles gently. The arc and pop use
            // sin(pi * t): zero at both ends, peaking mid-flight.
            var eased = 1f - (1f - t) * (1f - t);
            var bump = Mathf.Sin(Mathf.PI * t);
            transform.localPosition = Vector3.Lerp(_startLocalPosition, Vector3.zero, eased)
                                      + Vector3.up * (_move.ArcHeight * bump);
            transform.localScale = Vector3.Lerp(_startLocalScale, _baseScale, eased) * (1f + _move.Pop * bump);
        }

        private void Finish()
        {
            transform.localPosition = Vector3.zero;
            transform.localScale = _baseScale;
            _moving = false;
        }
    }
}
