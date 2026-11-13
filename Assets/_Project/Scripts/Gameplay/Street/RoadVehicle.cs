using UnityEngine;

namespace Game.Gameplay.Street
{
    /// <summary>
    /// A car driving the road outside: what <see cref="HitAndRun"/> checks chefs against. It only
    /// says how big the car is and how fast it's going now; whatever drives it along its lane is
    /// none of its business.
    /// </summary>
    public sealed class RoadVehicle : MonoBehaviour
    {
        [Tooltip("Half its width and half its length, in metres. Its length runs along its forward (local +Z).")]
        [SerializeField] private Vector2 _halfSize = new Vector2(1.05f, 2.15f);

        private Vector3 _last;
        private bool _measured;

        public Vector2 HalfSize => _halfSize;

        /// <summary>Metres a second, measured from how far it moved last frame.</summary>
        public float Speed { get; private set; }

        private void LateUpdate()
        {
            var here = transform.position;
            var deltaTime = Time.deltaTime;
            if (_measured && deltaTime > 0f)
            {
                var moved = (here - _last).magnitude;
                // A car that wraps round from one end of the street to the other didn't drive there.
                Speed = moved > 10f ? Speed : moved / deltaTime;
            }
            _last = here;
            _measured = true;
        }
    }
}
