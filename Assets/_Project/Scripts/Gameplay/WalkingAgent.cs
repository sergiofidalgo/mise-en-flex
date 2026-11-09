using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// A person walking around the scene without physics (customers, staff). They take up space
    /// and can be pushed: <see cref="PushResolver"/> keeps them apart from everyone else.
    /// </summary>
    public abstract class WalkingAgent : MonoBehaviour, IPushable
    {
        [Tooltip("Degrees per second they turn to face where they're walking.")]
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

        [Tooltip("Half their width, for bumping into people.")]
        [SerializeField, Min(0f)] private float _bodyRadius = 0.31f;

        public Vector3 Position => transform.position;

        public System.Numerics.Vector2 Intent { get; set; }

        public float BodyRadius => _bodyRadius;

        /// <summary>
        /// How fast this person walks relative to the usual pace: a character trait set by their
        /// look (a retiree ambles, a gym-goer strides). 1 is the configured walking speed.
        /// </summary>
        public float SpeedFactor { get; set; } = 1f;

        /// <summary>Set by their director: where a move on the floor ends up, given the furniture.</summary>
        public System.Func<System.Numerics.Vector2, System.Numerics.Vector2, System.Numerics.Vector2> Slide { get; set; }

        public System.Numerics.Vector2 Shove(System.Numerics.Vector2 shift)
        {
            var from = new System.Numerics.Vector2(transform.position.x, transform.position.z);
            var to = Slide != null ? Slide(from, shift) : from + shift;
            transform.position = new Vector3(to.X, transform.position.y, to.Y);
            return to - from;
        }

        /// <summary>Moves up to <paramref name="step"/> metres towards <paramref name="target"/> on the floor.</summary>
        /// <returns>True once within <paramref name="arriveDistance"/> of it.</returns>
        public bool WalkTowards(Vector3 target, float step, float arriveDistance)
        {
            var position = transform.position;
            target.y = position.y;
            var offset = target - position;
            if (offset.magnitude <= arriveDistance)
                return true;

            transform.position = Vector3.MoveTowards(position, target, step);
            Face(offset);
            return (target - transform.position).magnitude <= arriveDistance;
        }

        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
                return;
            var goal = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, _turnSpeed * Time.deltaTime);
        }
    }
}
