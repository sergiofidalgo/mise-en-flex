using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Gives each player their colour and a short "pop" when they join, so it's
    /// obvious who just came in and which body is yours.
    /// </summary>
    public sealed class PlayerVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CoopSettings _settings;
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private Renderer[] _tintedRenderers;
        [SerializeField] private Transform _popTarget;

        [Tooltip("Seconds for the join pop to settle.")]
        [SerializeField, Min(0.01f)] private float _joinPopDuration = 0.3f;

        [Tooltip("How far the pop overshoots before settling (ease-out-back strength).")]
        [SerializeField, Min(0f)] private float _joinPopOvershoot = 2.5f;

        private float _popElapsed;
        private Vector3 _popBaseScale;

        private void Start()
        {
            ApplyColor(_settings.GetPlayerColor(_playerInput.playerIndex));

            _popBaseScale = _popTarget.localScale;
            _popTarget.localScale = Vector3.zero;
            _popElapsed = 0f;
        }

        private void Update()
        {
            if (_popElapsed >= _joinPopDuration)
                return;

            _popElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_popElapsed / _joinPopDuration);
            _popTarget.localScale = _popBaseScale * EaseOutBack(t, _joinPopOvershoot);
        }

        private void ApplyColor(Color color)
        {
            // A property block tints each player without creating material copies.
            var block = new MaterialPropertyBlock();
            foreach (var target in _tintedRenderers)
            {
                target.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                target.SetPropertyBlock(block);
            }
        }

        // Standard ease-out-back: overshoots past 1 then settles, which reads as a
        // springy "pop". At t=1 it returns exactly 1.
        private static float EaseOutBack(float t, float overshoot)
        {
            var c3 = overshoot + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }
    }
}
