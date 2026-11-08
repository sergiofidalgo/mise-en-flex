using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a hob's heat from the overhead camera: the burner ring glows with the dial, a row of
    /// pips along the back of the hob top shows roughly how high it is, and the gas hob's knob on
    /// the top turns with it.
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
        }

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
