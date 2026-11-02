using Game.Gameplay.Items;
using Game.Simulation.Cooking;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a pan's state from anywhere in the kitchen: its colour warms with temperature,
    /// wisps of smoke rise when it's very hot, and thick dark smoke when food in it is close to
    /// burning: the "act now" cue.
    /// </summary>
    public sealed class PanView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private ItemView _itemView;
        [SerializeField] private Renderer _body;
        [SerializeField] private ParticleSystem _smoke;

        [Header("Colour")]
        [SerializeField] private Color _cold = new Color(0.30f, 0.30f, 0.33f);
        [SerializeField] private Color _hot = new Color(0.62f, 0.24f, 0.14f);
        [SerializeField] private float _coldTemperature = 22f;
        [Tooltip("°C shown as fully hot. Searing starts around 140 °C; the pan tops out near 280 °C.")]
        [SerializeField] private float _hotTemperature = 260f;

        [Header("Smoke from a very hot pan")]
        [Tooltip("°C where the first wisps appear (oil starts smoking).")]
        [SerializeField] private float _wispsFrom = 230f;
        [SerializeField] private float _wispsFull = 280f;
        [Tooltip("Particles per second at full wisps.")]
        [SerializeField, Min(0f)] private float _wispRate = 6f;
        [SerializeField] private Color _wispColor = new Color(0.85f, 0.85f, 0.85f, 0.45f);

        [Header("Smoke from burning food")]
        [Tooltip("Fraction of 'burnt' browning where the food starts smoking heavily.")]
        [SerializeField, Range(0f, 1f)] private float _burningFrom = 0.75f;
        [Tooltip("Particles per second when food in the pan is burnt.")]
        [SerializeField, Min(0f)] private float _burningRate = 30f;
        [SerializeField] private Color _burningColor = new Color(0.22f, 0.21f, 0.21f, 0.7f);

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!(_itemView.Item is Pan pan))
                return;

            _body.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.Lerp(_cold, _hot, Mathf.InverseLerp(_coldTemperature, _hotTemperature, pan.Temperature)));
            _body.SetPropertyBlock(_block);

            var wisps = Mathf.InverseLerp(_wispsFrom, _wispsFull, pan.Temperature);
            var burning = 0f;
            foreach (var ingredient in pan.Contents)
            {
                var burntAt = Mathf.Max(ingredient.Properties.BurntBrowning, 0.0001f);
                var closeness = ingredient.Browning(ingredient.DownSide) / burntAt;
                burning = Mathf.Max(burning, Mathf.InverseLerp(_burningFrom, 1f, closeness));
            }

            var emission = _smoke.emission;
            emission.rateOverTime = Mathf.Max(wisps * _wispRate, burning * _burningRate);
            var main = _smoke.main;
            main.startColor = Color.Lerp(_wispColor, _burningColor, burning);
        }
    }
}
