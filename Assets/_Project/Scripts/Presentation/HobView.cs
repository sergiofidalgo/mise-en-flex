using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a hob's state from the overhead camera: the burner glows with the dial, and the
    /// pan's colour warms with its temperature. Placeholder until the art phase.
    /// </summary>
    public sealed class HobView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private HobStation _hob;
        [SerializeField] private Renderer _burner;
        [SerializeField] private Renderer _pan;

        [SerializeField] private Color _burnerOff = new Color(0.08f, 0.08f, 0.09f);
        [SerializeField] private Color _burnerFull = new Color(1f, 0.35f, 0.05f);

        [SerializeField] private Color _panCold = new Color(0.30f, 0.30f, 0.33f);
        [SerializeField] private Color _panHot = new Color(0.70f, 0.22f, 0.12f);
        [Tooltip("°C shown as fully cold.")]
        [SerializeField] private float _panColdTemperature = 22f;
        [Tooltip("°C shown as fully hot. Searing starts around 140 °C; the pan tops out near 280 °C.")]
        [SerializeField] private float _panHotTemperature = 260f;

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (_hob.Hob == null)
                return;

            SetColor(_burner, Color.Lerp(_burnerOff, _burnerFull, _hob.Hob.Dial));
            var heat = Mathf.InverseLerp(_panColdTemperature, _panHotTemperature, _hob.Pan.Temperature);
            SetColor(_pan, Color.Lerp(_panCold, _panHot, heat));
        }

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
