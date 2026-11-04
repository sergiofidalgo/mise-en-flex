using System.Collections.Generic;
using Game.Gameplay.Staff;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Gameplay
{
    /// <summary>Someone who takes up space and can be pushed: a chef or a staff member.</summary>
    public interface IPushable
    {
        Vector3 Position { get; }

        /// <summary>How far they walked, or tried to, this frame.</summary>
        SimVector2 Intent { get; }

        float BodyRadius { get; }

        /// <summary>Moves them, stopping at furniture.</summary>
        /// <returns>How far they actually went.</returns>
        SimVector2 Shove(SimVector2 shift);
    }

    /// <summary>
    /// After everyone has walked, moves apart anyone who overlaps, by the rule in
    /// <see cref="PushSolver"/>. Someone pinned against furniture can't give way, so the pusher
    /// takes the rest and stops.
    /// </summary>
    public sealed class PushResolver : MonoBehaviour
    {
        // Enough to settle chains of people without shaking.
        private const int Passes = 2;

        [SerializeField] private StaffDirector _director;

        [Tooltip("How much a body resists being pushed: 0 goes along at the pusher's full pace, 0.5 at half.")]
        [SerializeField, Range(0f, 0.5f)] private float _resistance = 0.4f;

        private readonly List<IPushable> _bodies = new List<IPushable>();

        private void LateUpdate()
        {
            _bodies.Clear();
            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent(out PlayerMovement movement) && movement.IsReady)
                    _bodies.Add(movement);
            }
            _bodies.AddRange(_director.Agents);

            for (var pass = 0; pass < Passes; pass++)
            {
                for (var i = 0; i < _bodies.Count; i++)
                {
                    for (var j = i + 1; j < _bodies.Count; j++)
                        Resolve(_bodies[i], _bodies[j]);
                }
            }
        }

        private void Resolve(IPushable a, IPushable b)
        {
            var (shiftA, shiftB) = PushSolver.Separate(
                Floor(a.Position), a.Intent, Floor(b.Position), b.Intent, a.BodyRadius + b.BodyRadius, _resistance);
            if (shiftA == SimVector2.Zero && shiftB == SimVector2.Zero)
                return;

            // Whatever one couldn't give way, because of furniture, the other has to.
            var movedA = a.Shove(shiftA);
            var total = shiftA + -shiftB; // the same direction: how far A and B need to end up apart
            var movedB = b.Shove(-(total - movedA));
            var left = total - movedA + movedB;
            if (left.LengthSquared() > 1e-8f)
                a.Shove(left);
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
