using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// A person walking around the scene without physics (customers, staff), so they never get
    /// stuck on chefs or each other.
    /// </summary>
    public abstract class WalkingAgent : MonoBehaviour
    {
        [Tooltip("Degrees per second they turn to face where they're walking.")]
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

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
