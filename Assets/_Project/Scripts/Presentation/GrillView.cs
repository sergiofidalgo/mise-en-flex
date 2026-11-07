using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The charcoal grill's glow (decision 2026-11-07: always lit, and its light is part of the room).
    /// The coals shimmer, brighter on the hot side, and a warm light flickers gently on what's
    /// around it. Noise rather than a sine, so it never settles into a visible rhythm.
    /// </summary>
    public sealed class GrillView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _hotCoals;
        [SerializeField] private Renderer _coolCoals;
        [SerializeField] private Light _glow;

        [SerializeField] private Color _hotColor = new Color(1f, 0.38f, 0.06f);
        [SerializeField] private Color _coolColor = new Color(0.55f, 0.13f, 0.04f);
        [Tooltip("The light's brightness on average.")]
        [SerializeField, Min(0f)] private float _intensity = 1.6f;
        [Tooltip("How far it wavers either way, as a share of that.")]
        [SerializeField, Range(0f, 1f)] private float _flicker = 0.25f;
        [Tooltip("How quickly it wavers. Low reads as embers; high as flames.")]
        [SerializeField, Min(0f)] private float _flickerSpeed = 1.4f;
        [Tooltip("How much the coals' colour shimmers with it.")]
        [SerializeField, Range(0f, 1f)] private float _shimmer = 0.2f;

        private MaterialPropertyBlock _block;
        private float _seed;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _seed = Random.value * 100f; // grills side by side don't flicker in step
        }

        private void Update()
        {
            var t = Time.time * _flickerSpeed;
            var wave = Mathf.PerlinNoise(t, _seed) * 2f - 1f;

            if (_glow != null)
                _glow.intensity = _intensity * (1f + _flicker * wave);

            Tint(_hotCoals, _hotColor, Mathf.PerlinNoise(t * 1.7f, _seed + 10f));
            Tint(_coolCoals, _coolColor, Mathf.PerlinNoise(t * 1.3f, _seed + 20f));
        }

        private void Tint(Renderer coals, Color color, float noise)
        {
            if (coals == null)
                return;
            var brightness = 1f + _shimmer * (noise * 2f - 1f);
            coals.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color * brightness);
            coals.SetPropertyBlock(_block);
        }
    }
}
