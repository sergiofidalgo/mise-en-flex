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
        private static readonly int TipColorId = Shader.PropertyToID("_TipColor");

        /// <summary>The name of the hob's heat knob, which the kitchen builder gives it.</summary>
        public const string KnobName = "Knob";

        [SerializeField] private HobStation _hob;
        [SerializeField] private Renderer _burner;
        [Tooltip("Pips lowest first; together they span the whole dial.")]
        [SerializeField] private Renderer[] _pips = System.Array.Empty<Renderer>();
        [Tooltip("The gas hob's flame tongues round its burner. Empty on the induction hob.")]
        [SerializeField] private Renderer[] _flames = System.Array.Empty<Renderer>();
        [Tooltip("Shorter, brighter flames between the tongues: the fire's hot heart.")]
        [SerializeField] private Renderer[] _flameCores = System.Array.Empty<Renderer>();
        [Tooltip("Soft flame puffs rising round the pan; their rate follows the heat.")]
        [SerializeField] private ParticleSystem[] _puffs = System.Array.Empty<ParticleSystem>();
        [Tooltip("Embers drifting off the fire; only at higher heat, more as it rises.")]
        [SerializeField] private ParticleSystem[] _embers = System.Array.Empty<ParticleSystem>();
        [SerializeField, Min(0f)] private float _puffRate = 70f;
        [SerializeField, Min(0f)] private float _emberRate = 40f;
        [Tooltip("A core's size relative to the tongues round it.")]
        [SerializeField, Range(0f, 1f)] private float _coreScale = 0.5f;
        [SerializeField] private Color _coreLow = new Color(0.1f, 0.3f, 0.9f);
        [SerializeField] private Color _coreHigh = new Color(0.15f, 0.35f, 1f);
        [Tooltip("The fire's light on the cooking surface, sized and coloured with the heat.")]
        [SerializeField] private Renderer[] _glow = System.Array.Empty<Renderer>();
        [Tooltip("The glow's width in metres at the lowest heat and at full heat.")]
        [SerializeField] private Vector2 _glowSize = new Vector2(0.5f, 1f);
        [SerializeField] private Color _glowLow = new Color(0.25f, 0.4f, 1f, 0.25f);
        [SerializeField] private Color _glowHigh = new Color(1f, 0.5f, 0.15f, 0.8f);
        [Tooltip("A flame's height scale at the lowest heat and at full heat.")]
        [SerializeField] private Vector2 _flameHeight = new Vector2(0.7f, 2.4f);
        [Tooltip("A flame's width scale at the lowest heat and at full heat.")]
        [SerializeField] private Vector2 _flameWidth = new Vector2(0.5f, 0.95f);
        [Tooltip("How much the flames flicker in height, as a share of it, and how fast.")]
        [SerializeField, Range(0f, 0.5f)] private float _flicker = 0.18f;
        [SerializeField, Min(0f)] private float _flickerSpeed = 13f;
        [Tooltip("A tongue's base colour at low heat and at high heat: gas burns blue.")]
        [SerializeField] private Color _flameLow = new Color(0.1f, 0.3f, 1f);
        [SerializeField] private Color _flameHigh = new Color(1f, 0.78f, 0.28f);
        [Tooltip("A tongue's tip colour at low heat and at high heat, when it licks orange round the pan.")]
        [SerializeField] private Color _tipLow = new Color(0.25f, 0.3f, 1f);
        [SerializeField] private Color _tipHigh = new Color(1f, 0.42f, 0.06f);
        [Tooltip("Degrees the knob turns, clockwise from above, from off (pointing to the top of the hob) to full heat.")]
        [SerializeField, Range(0f, 360f)] private float _knobTurn = 270f;

        [Header("Pan fire (Phase 5)")]
        [Tooltip("Tall flames standing out of a pan on fire, copied from the tongues.")]
        [SerializeField, Min(0)] private int _fireTongues = 9;
        [Tooltip("Their height and width scales, and how far round the pan's middle they stand (metres).")]
        [SerializeField] private Vector3 _fireSize = new Vector3(6.5f, 2.8f, 0.09f);
        [SerializeField] private Color _fireColor = new Color(1f, 0.62f, 0.12f);
        [SerializeField] private Color _fireTip = new Color(0.95f, 0.22f, 0.04f);
        [Tooltip("A smouldering pan's tongues lean in and grow as it gets close to catching.")]
        [SerializeField, Range(0f, 1f)] private float _smoulderShow = 0.45f;

        [SerializeField] private Color _burnerOff = new Color(0.08f, 0.08f, 0.09f);
        [SerializeField] private Color _burnerFull = new Color(1f, 0.35f, 0.05f);
        [SerializeField] private Color _pipOff = new Color(0.12f, 0.12f, 0.13f);
        [Tooltip("Each pip's colour when lit, lowest first: green, orange, red.")]
        [SerializeField] private Color[] _pipOn =
        {
            new Color(0.3f, 0.85f, 0.3f), new Color(1f, 0.6f, 0.1f), new Color(0.95f, 0.2f, 0.15f),
        };

        private MaterialPropertyBlock _block;
        private Renderer[] _fire;
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
                SetColor(_pips[i], i < lit ? PipOn(i) : _pipOff);

            // A positive turn about up is clockwise seen from above, like turning a real knob up.
            if (_knob != null)
                _knob.localRotation = _knobOff * Quaternion.Euler(0f, _hob.Hob.Dial * _knobTurn, 0f);

            // On fire, the gas roars at full whatever the knob says, and the pan burns.
            var burning = _hob.IsOnFire;
            ShowFlames(burning ? 1f : _hob.Hob.Dial);
            ShowPanFire(burning ? 1f : SmoulderShown());
        }

        private float SmoulderShown()
        {
            var services = _hob.Services;
            var smoulder = services != null ? services.Fires.Smouldering(_hob.Hob) : 0f;
            return smoulder > 0f ? _smoulderShow * smoulder : 0f;
        }

        // Tall flames out of the pan itself, copied from the burner's tongues so they share its look.
        private void ShowPanFire(float amount)
        {
            if (amount <= 0f && _fire == null)
                return;
            if (_fire == null)
            {
                if (_flames.Length == 0 || _hob.ItemAnchor == null)
                    return;
                _fire = new Renderer[_fireTongues];
                for (var i = 0; i < _fireTongues; i++)
                {
                    var tongue = Instantiate(_flames[0], _hob.ItemAnchor);
                    tongue.name = "PanFire";
                    var angle = i * Mathf.PI * 2f / _fireTongues;
                    var ring = i % 3 == 0 ? 0f : _fireSize.z * (i % 2 == 0 ? 1f : 0.6f);
                    tongue.transform.localPosition = new Vector3(Mathf.Cos(angle) * ring, 0.04f, Mathf.Sin(angle) * ring);
                    tongue.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                    _fire[i] = tongue;
                }
            }

            var on = amount > 0f;
            var t = Time.time * _flickerSpeed;
            for (var i = 0; i < _fire.Length; i++)
            {
                var tongue = _fire[i];
                if (tongue.enabled != on)
                    tongue.enabled = on;
                if (!on)
                    continue;
                // Each licks on its own beat, more wildly than a burner's flame.
                var flicker = 1f + 2f * _flicker * Mathf.Sin(t * 0.8f + i * 1.7f) * Mathf.Sin(t * 0.47f + i * 0.9f);
                var middle = i % 3 == 0 ? 1.2f : 1f;
                tongue.transform.localScale = new Vector3(_fireSize.y, _fireSize.x * middle * flicker, _fireSize.y) * amount;
                tongue.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, _fireColor);
                _block.SetColor(TipColorId, _fireTip);
                tongue.SetPropertyBlock(_block);
            }
        }

        // Off, the flames are gone; any heat lights them, and higher heat makes them taller, wider
        // and more orange. Each flickers on its own beat so the ring never pulses as one.
        private void ShowFlames(float dial)
        {
            var on = dial > 0.001f;
            var warmth = FlameWarmth(dial);
            var height = Mathf.Lerp(_flameHeight.x, _flameHeight.y, dial);
            var width = Mathf.Lerp(_flameWidth.x, _flameWidth.y, dial);
            ShowRing(_flames, on, 1f, height, width, Color.Lerp(_flameLow, _flameHigh, warmth), Color.Lerp(_tipLow, _tipHigh, warmth), 0f);
            ShowRing(_flameCores, on, _coreScale, height, width, Color.Lerp(_coreLow, _coreHigh, warmth), _coreLow, 1.1f);

            var glowColor = Color.Lerp(_glowLow, _glowHigh, warmth);
            var glowSize = Mathf.Lerp(_glowSize.x, _glowSize.y, dial);
            foreach (var glow in _glow)
            {
                if (glow.enabled != on)
                    glow.enabled = on;
                if (!on)
                    continue;
                glow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
                SetColor(glow, glowColor);
            }

            // The orange glow and embers come with the orange tips, at high heat only.
            SetRate(_puffs, on ? _puffRate * dial * warmth : 0f);
            SetRate(_embers, on ? _emberRate * dial * warmth : 0f);
        }

        private void ShowRing(Renderer[] ring, bool on, float scale, float height, float width, Color color, Color tip, float beat)
        {
            for (var i = 0; i < ring.Length; i++)
            {
                var flame = ring[i];
                if (flame.enabled != on)
                    flame.enabled = on;
                if (!on)
                    continue;
                var t = Time.time * _flickerSpeed;
                var flicker = 1f + _flicker * Mathf.Sin(t + i * 2.3f + beat) * Mathf.Sin(t * 0.61f + i + beat);
                flame.transform.localScale = new Vector3(width, height * flicker, width) * scale;
                flame.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(TipColorId, tip);
                flame.SetPropertyBlock(_block);
            }
        }

        private static void SetRate(ParticleSystem[] systems, float rate)
        {
            foreach (var particles in systems)
            {
                var emission = particles.emission;
                emission.rateOverTime = rate;
            }
        }

        /// <summary>
        /// How far a gas flame's tips have gone from blue to orange: blue at low and middling heat,
        /// licking orange only as it gets high, as a real gas flame does round a pan.
        /// </summary>
        public static float FlameWarmth(float dial) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dial - 0.35f) / 0.55f));

        private Color PipOn(int pip) => _pipOn.Length == 0 ? Color.white : _pipOn[Mathf.Min(pip, _pipOn.Length - 1)];

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
