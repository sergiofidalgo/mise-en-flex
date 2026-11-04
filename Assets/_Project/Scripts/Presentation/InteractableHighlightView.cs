using Game.Data;
using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows which station a player's Interact will act on by tinting it towards that
    /// player's colour, shakes it briefly when Interact can't do anything there, and turns it red
    /// while it's carried somewhere it can't be put down.
    /// </summary>
    public sealed class InteractableHighlightView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Interactable _interactable;
        [SerializeField] private CoopSettings _settings;
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Transform _shakeTarget;

        [Tooltip("How far the colour moves towards the player's colour (0 = none, 1 = fully).")]
        [SerializeField, Range(0f, 1f)] private float _highlightStrength = 0.45f;

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

        private Color[] _baseColors;
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
        }

        private void OnEnable()
        {
            _interactable.HighlightChanged += ApplyHighlight;
            _interactable.Denied += StartShake;
        }

        private void OnDisable()
        {
            _interactable.HighlightChanged -= ApplyHighlight;
            _interactable.Denied -= StartShake;
        }

        private void Update()
        {
            if (_shakeElapsed >= _denyShakeDuration)
                return;

            _shakeElapsed += Time.deltaTime;
            if (_shakeElapsed >= _denyShakeDuration)
            {
                _shakeTarget.localPosition = _shakeOrigin;
                return;
            }

            // A sine wobble that fades out linearly over the shake.
            var fade = 1f - _shakeElapsed / _denyShakeDuration;
            var offset = Mathf.Sin(_shakeElapsed * _denyShakeFrequency) * _denyShakeDistance * fade;
            _shakeTarget.localPosition = _shakeOrigin + Vector3.right * offset;
        }

        private void ApplyHighlight()
        {
            var targeted = _interactable.IsTargeted;
            var playerColor = targeted ? _settings.GetPlayerColor(_interactable.TargeterPlayerIndex) : Color.clear;

            for (var i = 0; i < _renderers.Length; i++)
            {
                var color = _interactable.CantGoHere
                    ? Color.Lerp(_baseColors[i], _cantGoHereColor, _cantGoHereStrength)
                    : targeted ? Color.Lerp(_baseColors[i], playerColor, _highlightStrength) : _baseColors[i];
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void StartShake()
        {
            _shakeElapsed = 0f;
        }
    }
}
