using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// Moves along a straight lane, facing the way it goes: cars and a delivery rider driving
    /// through and coming round again (wrap), or someone pacing up and down a pavement, pausing at
    /// each end before turning back (ping-pong). A walker in the character kit swings their legs
    /// by itself (WalkCycleView follows the movement).
    /// </summary>
    public sealed class LaneMover : MonoBehaviour
    {
        [SerializeField] private Vector3 _from;
        [SerializeField] private Vector3 _to;
        [SerializeField, Min(0.1f)] private float _speed = 1.3f;
        [Tooltip("Off: drive to the end and start again from the beginning. On: walk back and forth.")]
        [SerializeField] private bool _pingPong;
        [Tooltip("Seconds standing at each end before turning back (ping-pong only).")]
        [SerializeField, Min(0f)] private float _pause = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _phase;

        public void Set(Vector3 from, Vector3 to, float speed, bool pingPong, float pause, float phase)
        {
            (_from, _to, _speed, _pingPong, _pause, _phase) = (from, to, speed, pingPong, pause, phase);
            Place(0f);
        }

        private void Update() => Place(Time.time);

        private void Place(float time)
        {
            var length = Vector3.Distance(_from, _to);
            var (along, forward) = AmbientMotion.Lane(time, length, _speed, _pingPong, _pause, _phase);
            var direction = (_to - _from).normalized;
            transform.localPosition = _from + direction * along;
            if (direction.sqrMagnitude > 0f)
                transform.localRotation = Quaternion.LookRotation(forward ? direction : -direction);
        }
    }
}
