using UnityEngine;

namespace Game.Presentation.Office.Ambience
{
    /// <summary>
    /// Moves, turns and scales something back and forth about where it was placed: a wing's flap, a
    /// flag, a sheet on a line, a pigeon's peck, a crane's jib, a person shifting their weight. A
    /// wave swings both ways all the time; a pulse is a short bump and then rest.
    /// </summary>
    public sealed class Oscillate : MonoBehaviour
    {
        public enum Shape { Wave, Pulse }

        [SerializeField] private Shape _shape = Shape.Wave;
        [SerializeField] private Vector3 _move;
        [Tooltip("Degrees about each local axis.")]
        [SerializeField] private Vector3 _turn;
        [Tooltip("Added to the scale on each axis.")]
        [SerializeField] private Vector3 _grow;
        [SerializeField, Min(0.01f)] private float _period = 2f;
        [Tooltip("For a pulse: the share of each period it takes.")]
        [SerializeField, Range(0.01f, 1f)] private float _duty = 0.2f;
        [Tooltip("0–1: where in its cycle it starts, so neighbours don't move in step.")]
        [SerializeField, Range(0f, 1f)] private float _phase;

        private Vector3 _position;
        private Quaternion _rotation;
        private Vector3 _scale;

        public void Set(Shape shape, Vector3 move, Vector3 turn, Vector3 grow, float period, float duty, float phase)
        {
            (_shape, _move, _turn, _grow, _period, _duty, _phase) = (shape, move, turn, grow, period, duty, phase);
        }

        private void Awake()
        {
            _position = transform.localPosition;
            _rotation = transform.localRotation;
            _scale = transform.localScale;
        }

        private void Update()
        {
            var t = _shape == Shape.Wave
                ? AmbientMotion.Wave(Time.time, _period, _phase)
                : AmbientMotion.Pulse(Time.time, _period, _duty, _phase);
            transform.localPosition = _position + _move * t;
            transform.localRotation = _rotation * Quaternion.Euler(_turn * t);
            transform.localScale = _scale + _grow * t;
        }
    }
}
