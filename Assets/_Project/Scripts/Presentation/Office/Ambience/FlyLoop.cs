using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// Flies round a loop for ever, facing where it's going and leaning into the turn: birds
    /// wheeling over the roofs, a helicopter circling the skyline, someone drifting round a pool.
    /// </summary>
    public sealed class FlyLoop : MonoBehaviour
    {
        [SerializeField] private Vector3 _centre;
        [SerializeField] private Vector2 _radii = new Vector2(10f, 8f);
        [Tooltip("Metres it rises and dips, twice a lap.")]
        [SerializeField] private float _lift = 1f;
        [SerializeField, Min(0.1f)] private float _period = 20f;
        [SerializeField, Range(0f, 1f)] private float _phase;
        [SerializeField] private bool _clockwise;
        [Tooltip("Degrees it leans into the turn.")]
        [SerializeField] private float _bank = 15f;

        public void Set(Vector3 centre, Vector2 radii, float lift, float period, float phase, bool clockwise, float bank)
        {
            (_centre, _radii, _lift, _period, _phase, _clockwise, _bank) = (centre, radii, lift, period, phase, clockwise, bank);
            // On its loop straight away, so it's in place in the editor too, not at the origin.
            Place(0f);
        }

        private void Update() => Place(Time.time);

        private void Place(float time)
        {
            var now = AmbientMotion.Loop(_centre, _radii, _lift, time, _period, _phase, _clockwise);
            // A moment ahead, to face along the path.
            var ahead = AmbientMotion.Loop(_centre, _radii, _lift, time + 0.1f, _period, _phase, _clockwise);
            transform.localPosition = now;
            var heading = ahead - now;
            if (heading.sqrMagnitude > 1e-6f)
                transform.localRotation = Quaternion.LookRotation(heading.normalized) * Quaternion.Euler(0f, 0f, _clockwise ? _bank : -_bank);
        }
    }
}
