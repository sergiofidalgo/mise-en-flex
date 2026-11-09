using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// A propeller cap's blades (the user's reference, 2026-11-09): idling round slowly standing
    /// still, whirring as the chef walks faster, from the walk's own speed.
    /// </summary>
    public sealed class PropellerSpin : MonoBehaviour
    {
        [Tooltip("Degrees a second standing still.")]
        [SerializeField, Min(0f)] private float _idle = 120f;
        [Tooltip("Degrees a second at a full walk.")]
        [SerializeField, Min(0f)] private float _walking = 1440f;

        private WalkCycleView _gait;
        private float _speed;

        private void Start() => _gait = GetComponentInParent<WalkCycleView>();

        private void Update()
        {
            var target = Mathf.Lerp(_idle, _walking, _gait != null ? _gait.Walking : 0f);
            // Spins up quickly and winds down slowly, like a real one.
            var rate = target > _speed ? 6f : 1.5f;
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
            transform.Rotate(Vector3.up, _speed * Time.deltaTime, Space.Self);
        }
    }
}
