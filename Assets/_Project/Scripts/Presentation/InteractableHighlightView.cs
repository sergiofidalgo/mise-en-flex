using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows which station a player's Interact will act on by lighting it up in its own colour,
    /// shakes it briefly when Interact can't do anything there, and turns it red while it's
    /// carried somewhere it can't be put down. Storage gives a gentle hop, unlike that shake, when
    /// stock bought at a stall arrives in it (decision 2026-11-10).
    /// </summary>
    public sealed class InteractableHighlightView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Interactable _interactable;
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Transform _shakeTarget;

        [Tooltip("How much brighter a lit station gets, added to each of its colour channels.")]
        [SerializeField, Range(0f, 1f)] private float _lightUp = 0.2f;

        [Tooltip("The colour furniture turns while it's carried somewhere it can't go.")]
        [SerializeField] private Color _cantGoHereColor = new Color(0.95f, 0.15f, 0.12f);

        [Tooltip("How far it moves towards that colour (0 = none, 1 = fully).")]
        [SerializeField, Range(0f, 1f)] private float _cantGoHereStrength = 0.85f;

        [Tooltip("Seconds the 'can't do that' shake lasts.")]
        [SerializeField, Min(0.01f)] private float _denyShakeDuration = 0.15f;

        [Tooltip("Metres of side-to-side movement at the start of the shake.")]
        [SerializeField, Min(0f)] private float _denyShakeDistance = 0.04f;

        [Tooltip("Shake speed in radians per second.")]
        [SerializeField, Min(0f)] private float _denyShakeFrequency = 70f;

        [Tooltip("Seconds the gentle hop lasts when bought stock arrives (storage only).")]
        [SerializeField, Min(0.01f)] private float _stockedDuration = 0.45f;

        [Tooltip("Metres it lifts at the top of that hop.")]
        [SerializeField, Min(0f)] private float _stockedLift = 0.05f;

        [Tooltip("Hops in that time: the second smaller, like something settling.")]
        [SerializeField, Min(1)] private int _stockedHops = 2;

        private Color[] _baseColors;
        private float _stockedElapsed = float.MaxValue;
        private StorageStation _storage;
        private MaterialPropertyBlock _block;
        private Vector3 _shakeOrigin;
        private float _shakeElapsed = float.MaxValue;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseColors = new Color[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
                _baseColors[i] = _renderers[i].sharedMaterial.GetColor(BaseColorId);
            _shakeOrigin = _shakeTarget.localPosition;
            _storage = _interactable as StorageStation;
        }

        private void OnEnable()
        {
            _interactable.HighlightChanged += ApplyHighlight;
            _interactable.Denied += StartShake;
            if (_storage != null)
                _storage.Stocked += StartHop;
        }

        private void OnDisable()
        {
            _interactable.HighlightChanged -= ApplyHighlight;
            _interactable.Denied -= StartShake;
            if (_storage != null)
                _storage.Stocked -= StartHop;
        }

        private void Update()
        {
            var shaking = _shakeElapsed < _denyShakeDuration;
            var hopping = _stockedElapsed < _stockedDuration;
            if (!shaking && !hopping)
                return;

            var offset = Vector3.zero;
            if (shaking)
            {
                _shakeElapsed += Time.deltaTime;
                // A sine wobble that fades out linearly over the shake.
                var fade = Mathf.Max(0f, 1f - _shakeElapsed / _denyShakeDuration);
                offset += Vector3.right * (Mathf.Sin(_shakeElapsed * _denyShakeFrequency) * _denyShakeDistance * fade);
            }
            if (hopping)
            {
                _stockedElapsed += Time.deltaTime;
                // Bouncing hops, each smaller than the last: |sin| makes the bounces, the fade
                // shrinks them, so it lands and settles like a delivery put down on it.
                var t = Mathf.Clamp01(_stockedElapsed / _stockedDuration);
                offset += Vector3.up * (Mathf.Abs(Mathf.Sin(t * Mathf.PI * _stockedHops)) * _stockedLift * (1f - t));
            }
            _shakeTarget.localPosition = _shakeOrigin + offset;
        }

        private void ApplyHighlight()
        {
            var targeted = _interactable.IsTargeted;
            var light = new Color(_lightUp, _lightUp, _lightUp, 0f);

            for (var i = 0; i < _renderers.Length; i++)
            {
                var color = _interactable.CantGoHere
                    ? Color.Lerp(_baseColors[i], _cantGoHereColor, _cantGoHereStrength)
                    // Adding rather than blending towards white: a dark hob brightens as much as
                    // a pale counter, and stays recognisably itself.
                    : targeted ? _baseColors[i] + light : _baseColors[i];
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void StartShake()
        {
            _shakeElapsed = 0f;
        }

        private void StartHop(int portions)
        {
            _stockedElapsed = 0f;
        }
    }
}
