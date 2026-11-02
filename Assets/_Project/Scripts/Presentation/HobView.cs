using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Shows a hob's heat from the overhead camera: the burner ring glows with the dial and a
    /// row of pips along the back of the hob top shows roughly how high it is. Placeholder until
    /// the art phase.
    /// </summary>
    public sealed class HobView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private HobStation _hob;
        [SerializeField] private Renderer _burner;
        [Tooltip("Pips lowest first; together they span the whole dial.")]
        [SerializeField] private Renderer[] _pips = System.Array.Empty<Renderer>();

        [SerializeField] private Color _burnerOff = new Color(0.08f, 0.08f, 0.09f);
        [SerializeField] private Color _burnerFull = new Color(1f, 0.35f, 0.05f);
        [SerializeField] private Color _pipOff = new Color(0.12f, 0.12f, 0.13f);
        [SerializeField] private Color _pipOn = new Color(1f, 0.55f, 0.1f);

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
            // The dial is continuous; each pip covers an equal share of it and lights once the
            // dial is into its share, so any heat at all lights the first pip.
            var lit = Mathf.CeilToInt(_hob.Hob.Dial * _pips.Length - 0.001f);
            for (var i = 0; i < _pips.Length; i++)
                SetColor(_pips[i], i < lit ? _pipOn : _pipOff);
        }

        private void SetColor(Renderer target, Color color)
        {
            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }
    }
}
