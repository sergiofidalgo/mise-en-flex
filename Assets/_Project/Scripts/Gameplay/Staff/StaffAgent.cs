using Game.Simulation.Staff;
using UnityEngine;

namespace Game.Gameplay.Staff
{
    /// <summary>A staff member's body in the scene. Moved by the <see cref="StaffDirector"/>.</summary>
    public sealed class StaffAgent : WalkingAgent
    {
        [Tooltip("Where a carried plate sits.")]
        [SerializeField] private Transform _carryAnchor;

        public Transform CarryAnchor => _carryAnchor;

        public StaffMember Member { get; private set; }

        public IStaffJob Job { get; private set; }

        public void Initialize(StaffMember member, IStaffJob job)
        {
            Member = member;
            Job = job;
        }
    }
}
