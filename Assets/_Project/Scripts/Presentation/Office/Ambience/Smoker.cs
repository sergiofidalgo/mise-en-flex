using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// Someone smoking on their terrace: every so often they raise the cigarette to their mouth,
    /// hold it, lower it and breathe out a puff. The cigarette smokes a thin wisp all the time.
    /// </summary>
    public sealed class Smoker : MonoBehaviour
    {
        [Tooltip("The shoulder the cigarette arm hangs from.")]
        [SerializeField] private Transform _shoulder;
        [Tooltip("Where the puff comes out: in front of the face.")]
        [SerializeField] private ParticleSystem _exhale;
        [SerializeField, Min(0.5f)] private float _period = 7f;
        [Tooltip("Share of each period the arm is up.")]
        [SerializeField, Range(0.05f, 0.9f)] private float _duty = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _phase;
        [Tooltip("The arm's turn when the cigarette is at the mouth.")]
        [SerializeField] private Vector3 _raised = new Vector3(-135f, 0f, -20f);
        [SerializeField, Min(1)] private int _puff = 4;

        private Quaternion _rest;
        private bool _wasUp;

        public void Set(Transform shoulder, ParticleSystem exhale, float period, float phase) =>
            (_shoulder, _exhale, _period, _phase) = (shoulder, exhale, period, phase);

        private void Awake() => _rest = _shoulder != null ? _shoulder.localRotation : Quaternion.identity;

        private void Update()
        {
            if (_shoulder == null)
                return;
            var up = AmbientMotion.Pulse(Time.time, _period, _duty, _phase);
            // Eased so the arm lingers at the mouth.
            var raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(up * 1.6f));
            _shoulder.localRotation = _rest * Quaternion.Euler(_raised * raise);

            // Breathe out as the arm comes down.
            var isUp = up > 0.5f;
            if (_wasUp && !isUp && _exhale != null)
                _exhale.Emit(_puff);
            _wasUp = isUp;
        }
    }
}
