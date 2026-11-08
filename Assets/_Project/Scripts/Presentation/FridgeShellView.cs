using System;
using Game.Gameplay;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Makes each of a fridge's four sides glass or solid from what's around it (decision
    /// 2026-11-08). A side right up against a wall or furniture is solid, since there's nothing to see
    /// through it. One facing open floor is see-through: glass on the left and right, a sliding door
    /// at the front and back. It looks again every frame, so it follows the layout as pieces move,
    /// even while the fridge itself is being carried. People and loose items don't count.
    /// </summary>
    public sealed class FridgeShellView : MonoBehaviour
    {
        /// <summary>One side of the fridge: what shows when it's open, and when it's against something.</summary>
        [Serializable]
        public sealed class Side
        {
            [Tooltip("Turned to face this side; the fridge's front is its unturned forward (−Z).")]
            public Transform Root;
            [Tooltip("Shown when the side faces open floor: glass, or a door's panes.")]
            public GameObject[] Open = Array.Empty<GameObject>();
            [Tooltip("Shown when the side is up against something.")]
            public GameObject Solid;
            [Tooltip("Which way this side faces, in its root's space before turning.")]
            public Vector3 Outward = Vector3.back;

            public bool IsOpen { get; internal set; } = true;
        }

        [SerializeField] private Side _front = new Side();
        [SerializeField] private Side _back = new Side();
        [SerializeField] private Side _left = new Side();
        [SerializeField] private Side _right = new Side();

        [Header("What counts as up against something")]
        [Tooltip("Metres beyond the fridge's footprint that are checked on each side.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _reach = 0.2f;
        [Tooltip("Metres between the footprint and the start of the check, so a neighbour's face just touching doesn't count, but one flush against it does.")]
        [SerializeField, Range(0f, 0.1f)] private float _gap = 0.02f;
        [Tooltip("Half the fridge's footprint, in metres.")]
        [SerializeField, Min(0.1f)] private float _halfSize = 0.5f;

        private static readonly Collider[] Hits = new Collider[16];
        private bool _shown;

        public enum Facing { Front, Back, Left, Right }

        /// <summary>True if that side faces open floor (glass or a door); false if it's solid.</summary>
        public bool IsOpen(Facing facing) => SideOf(facing).IsOpen;

        private Side SideOf(Facing facing) => facing switch
        {
            Facing.Front => _front,
            Facing.Back => _back,
            Facing.Left => _left,
            _ => _right,
        };

        private void Start() => Refresh();

        private void Update() => Refresh();

        /// <summary>Looks around and makes each side glass or solid. Public so it can run outside Play mode.</summary>
        public void Refresh()
        {
            Apply(_front, force: !_shown);
            Apply(_back, force: !_shown);
            Apply(_left, force: !_shown);
            Apply(_right, force: !_shown);
            _shown = true;
        }

        private void Apply(Side side, bool force)
        {
            if (side.Root == null)
                return;
            var open = !Against(side.Root.localRotation * side.Outward);
            if (!force && open == side.IsOpen)
                return;
            side.IsOpen = open;
            foreach (var part in side.Open)
                if (part != null)
                    part.SetActive(open);
            if (side.Solid != null)
                side.Solid.SetActive(!open);
        }

        // Is there a wall or furniture in a slab just beyond this side? The slab is narrower than the
        // side and kept off the floor, so a diagonal neighbour's corner or the floor never counts.
        private bool Against(Vector3 outward)
        {
            var across = Vector3.Cross(Vector3.up, outward).normalized;
            var centre = outward * (_halfSize + _gap + _reach / 2f) + Vector3.up * 0.9f;
            var size = across * (_halfSize * 1.6f) + Vector3.up * 1.2f + outward * _reach;
            var halfExtents = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) / 2f;

            var world = transform.TransformPoint(centre);
            var scale = transform.lossyScale;
            halfExtents = Vector3.Scale(halfExtents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var count = Physics.OverlapBoxNonAlloc(world, halfExtents, Hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.transform.IsChildOf(transform))
                    continue;
                if (hit is CharacterController || hit.GetComponentInParent<IPushable>() != null)
                    continue; // people move: they don't make a wall
                if (hit.attachedRigidbody != null && !hit.attachedRigidbody.isKinematic)
                    continue; // nor do loose items
                return true;
            }
            return false;
        }
    }
}
