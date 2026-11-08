using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a hob's heat from the overhead camera: the burner glows with the dial, a row of pips
    /// along the back of the hob top shows roughly how high it is, the gas hob's knob on the top
    /// turns with it, and its flames grow, flicker and go from blue to orange (the user's note,
    /// 2026-11-08), licking past the pan's edge at high heat.
    /// </summary>
    public sealed class HobView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The name of the hob's heat knob, which the kitchen builder gives it.</summary>
        public const string KnobName = "Knob";

        [SerializeField] private HobStation _hob;
        [SerializeField] private Renderer _burner;
        [Tooltip("Pips lowest first; together they span the whole dial.")]
        [SerializeField] private Renderer[] _pips = System.Array.Empty<Renderer>();
        [Tooltip("The gas hob's flame tongues round its burner. Empty on the induction hob.")]
        [SerializeField] private Renderer[] _flames = System.Array.Empty<Renderer>();
        [Tooltip("A flame's height scale at the lowest heat and at full heat.")]
        [SerializeField] private Vector2 _flameHeight = new Vector2(0.5f, 2.6f);
        [Tooltip("A flame's width scale at the lowest heat and at full heat.")]
        [SerializeField] private Vector2 _flameWidth = new Vector2(0.8f, 1.8f);
        [Tooltip("How much the flames flicker in height, as a share of it, and how fast.")]
        [SerializeField, Range(0f, 0.5f)] private float _flicker = 0.18f;
        [SerializeField, Min(0f)] private float _flickerSpeed = 13f;
        [SerializeField] private Color _flameLow = new Color(0.35f, 0.6f, 1f);
        [SerializeField] private Color _flameHigh = new Color(1f, 0.42f, 0.04f);
        [Tooltip("Degrees the knob turns, clockwise from above, from off to full heat.")]
        [SerializeField, Range(0f, 360f)] private float _knobTurn = 270f;

        [SerializeField] private Color _burnerOff = new Color(0.08f, 0.08f, 0.09f);
        [SerializeField] private Color _burnerFull = new Color(1f, 0.35f, 0.05f);
        [SerializeField] private Color _pipOff = new Color(0.12f, 0.12f, 0.13f);
        [SerializeField] private Color _pipOn = new Color(1f, 0.55f, 0.1f);

        private MaterialPropertyBlock _block;
        // The gas hob's heat knob, a child called "Knob", turned with the dial; the induction hob has none.
        private Transform _knob;
        private Quaternion _knobOff;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _knob = transform.Find(KnobName);
            if (_knob != null)
                _knobOff = _knob.localRotation;
        }

        private void Update()
        {
            if (_hob.Hob == null)
                return;

            SetColor(_burner, Color.Lerp(_burnerOff, _burnerFull, _hob.Hob.Dial));
            // The dial is continuous; each pip covers an equal share of it and lights once the
            // dial is into its share, so any heat at all lights the first pip.
            var lit = Mathf.CeilToInt(_hob.Hob.Dial * _pips.Length - 0.001f);
            for (var i = 0; i < _pips.Length; i++)
                SetColor(_pips[i], i < lit ? _pipOn : _pipOff);

            // A positive turn about up is clockwise seen from above, like turning a real knob up.
            if (_knob != null)
                _knob.localRotation = _knobOff * Quaternion.Euler(0f, _hob.Hob.Dial * _knobTurn, 0f);

            ShowFlames(_hob.Hob.Dial);
        }

        // Off, the flames are gone; any heat lights them, and higher heat makes them taller, wider
        // and more orange. Each flickers on its own beat so the ring never pulses as one.
        private void ShowFlames(float dial)
        {
            var on = dial > 0.001f;
            var color = Color.Lerp(_flameLow, _flameHigh, FlameWarmth(dial));
            var height = Mathf.Lerp(_flameHeight.x, _flameHeight.y, dial);
            var width = Mathf.Lerp(_flameWidth.x, _flameWidth.y, dial);
            for (var i = 0; i < _flames.Length; i++)
            {
                var flame = _flames[i];
                if (flame.enabled != on)
                    flame.enabled = on;
                if (!on)
                    continue;
                var flicker = 1f + _flicker * Mathf.Sin(Time.time * _flickerSpeed + i * 2.3f) * Mathf.Sin(Time.time * _flickerSpeed * 0.61f + i);
                flame.transform.localScale = new Vector3(width, height * flicker, width);
                SetColor(flame, color);
            }
        }

        /// <summary>
        /// How far a flame has gone from blue to orange: blue only at the very bottom of the dial,
        /// so middling heat reads as fire rather than a pink blend of the two.
        /// </summary>
        public static float FlameWarmth(float dial) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dial / 0.3f));

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
