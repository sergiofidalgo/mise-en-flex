using Game.Simulation.Staff;
using UnityEngine;

namespace Game.Gameplay.Staff
{
    /// <summary>A staff member's body in the scene. Moved by the <see cref="StaffDirector"/>.</summary>
    public sealed class StaffAgent : WalkingAgent, IPushable
    {
        [Tooltip("Where a carried plate sits.")]
        [SerializeField] private Transform _carryAnchor;

        [Tooltip("Half their width, for bumping into people.")]
        [SerializeField, Min(0f)] private float _bodyRadius = 0.31f;

        public Transform CarryAnchor => _carryAnchor;

        public StaffMember Member { get; private set; }

        public IStaffJob Job { get; private set; }

        public Vector3 Position => transform.position;

        public System.Numerics.Vector2 Intent { get; set; }

        public float BodyRadius => _bodyRadius;

        /// <summary>Set by the director: whether a floor position is clear of furniture.</summary>
        public System.Func<System.Numerics.Vector2, bool> IsFree { get; set; }

        public System.Numerics.Vector2 Shove(System.Numerics.Vector2 shift)
        {
            var from = new System.Numerics.Vector2(transform.position.x, transform.position.z);
            var to = from + shift;
            if (IsFree != null && !IsFree(to))
                return System.Numerics.Vector2.Zero;
            transform.position = new Vector3(to.X, transform.position.y, to.Y);
            return shift;
        }

        public void Initialize(StaffMember member, IStaffJob job)
        {
            Member = member;
            Job = job;
        }
    }
}
