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
        [Tooltip("For containers: where each content item sits, in content order.")]
        [SerializeField] private Transform[] _slotAnchors = System.Array.Empty<Transform>();

        private Vector3 _startLocalPosition;
        private Vector3 _baseScale;
        private ItemMoveSettings _move;
        private float _elapsed;
        private bool _moving;
        private bool _posed;
        private Vector3 _poseOffset;
        private Quaternion _poseRotation = Quaternion.identity;

        public Item Item { get; private set; }

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
        /// (a pan held by its handle, decision 2026-11-08). Eased into; cleared by the next move.
        /// Only presentation: where the item is held for play doesn't change.
        /// </summary>
        public void SetHeldPose(Vector3 localOffset, Quaternion localRotation, bool immediate = false)
        {
            _posed = true;
            _poseOffset = localOffset;
            _poseRotation = localRotation;
            if (immediate && !_moving)
            {
                transform.localPosition = localOffset;
                transform.localRotation = localRotation;
            }
        }

        public void MoveTo(Transform anchor, ItemMoveSettings move)
        {
            _posed = false;
            _poseOffset = Vector3.zero;
            _poseRotation = Quaternion.identity;
            Anchor = anchor;
            transform.SetParent(anchor, worldPositionStays: true);
            transform.localRotation = Quaternion.identity;
            _startLocalPosition = transform.localPosition;
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
            transform.localScale = _baseScale * (1f + _move.Pop * bump);
        }

        private void Finish()
        {
            transform.localPosition = Vector3.zero;
            transform.localScale = _baseScale;
            _moving = false;
        }
    }
}
