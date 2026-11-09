using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// A loose part of a hat that answers the chef's movement (the user's note, 2026-11-09): a
    /// feather, a party hat on its elastic, a bandana's tails. It hangs from its pivot on a spring:
    /// it trails behind as the chef walks, swings on past when they stop or turn, settles, and
    /// drifts a little in a breeze while they stand. Watches its own position, so it works on
    /// anyone; only presentation.
    /// </summary>
    public sealed class HatSway : MonoBehaviour
    {
        [Tooltip("How hard it springs back upright; higher is stiffer and quicker.")]
        [SerializeField, Min(1f)] private float _stiffness = 70f;
        [Tooltip("How quickly a swing dies away.")]
        [SerializeField, Min(0f)] private float _damping = 7f;
        [Tooltip("Degrees it trails per m/s of walking.")]
        [SerializeField, Min(0f)] private float _drag = 7f;
        [Tooltip("Degrees it swings per m/s² of starting, stopping or turning.")]
        [SerializeField, Min(0f)] private float _inertia = 1.2f;
        [Tooltip("Degrees the breeze moves it, and how fast the breeze changes.")]
        [SerializeField, Min(0f)] private float _breeze = 5f;
        [SerializeField, Min(0f)] private float _breezeSpeed = 0.6f;
        [Tooltip("It never leans further than this (degrees).")]
        [SerializeField, Range(1f, 80f)] private float _maxAngle = 35f;

        private Quaternion _rest;
        private Vector3 _lastPosition;
        private Vector3 _lastVelocity;
        private Vector2 _angle;    // degrees about world X (forward and back) and Z (side to side)
        private Vector2 _angleSpeed;
        private float _seed;
        private bool _started;

        public void Set(float stiffness, float damping, float drag, float inertia, float breeze, float maxAngle) =>
            (_stiffness, _damping, _drag, _inertia, _breeze, _maxAngle) = (stiffness, damping, drag, inertia, breeze, maxAngle);

        private void OnEnable()
        {
            if (!_started)
            {
                _rest = transform.localRotation;
                _seed = Random.value * 100f;
                _started = true;
            }
            _lastPosition = transform.position;
            _lastVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            var velocity = (transform.position - _lastPosition) / dt;
            velocity.y = 0f;
            var acceleration = Vector3.ClampMagnitude((velocity - _lastVelocity) / dt, 30f);
            _lastPosition = transform.position;
            _lastVelocity = velocity;

            // Where it wants to lean: away from where it's going, and on past a sudden stop. A
            // turn about world X by +θ tips the part's top towards +Z, so trailing behind a chef
            // walking +Z is a negative θ; about Z, +φ tips it towards -X.
            var push = -velocity * _drag - acceleration * _inertia;
            var time = Time.time * _breezeSpeed;
            var breeze = new Vector2(Mathf.PerlinNoise(_seed, time) - 0.5f, Mathf.PerlinNoise(time, _seed) - 0.5f) * (2f * _breeze);
            var target = new Vector2(push.z, -push.x) + breeze;
            target = Vector2.ClampMagnitude(target, _maxAngle);

            // A damped spring towards it, so it overshoots and settles like something loose.
            _angleSpeed += ((target - _angle) * _stiffness - _angleSpeed * _damping) * dt;
            _angle += _angleSpeed * dt;
            _angle = Vector2.ClampMagnitude(_angle, _maxAngle);

            var parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            transform.rotation = Quaternion.Euler(_angle.x, 0f, _angle.y) * parent * _rest;
        }
    }
}
