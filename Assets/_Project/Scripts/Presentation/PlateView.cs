using Game.Gameplay.Items;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Makes a used plate look dirty, so it reads from the overhead camera: with the plate shader,
    /// smeared sauce, grease and crumbs, different each time it gets dirty (decision 2026-11-08);
    /// without it, a plain dirty tint.
    /// </summary>
    public sealed class PlateView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private ItemView _itemView;
        [SerializeField] private Renderer _body;
        [Tooltip("Tint for a dirty plate when its material has no grime (greybox).")]
        [SerializeField] private Color _dirty = new Color(0.45f, 0.36f, 0.25f);

        private MaterialPropertyBlock _block;
        private bool _shownDirty;

        private void Awake() => _block = new MaterialPropertyBlock();

        private void Update()
        {
            var dirty = _itemView.Item is Plate plate && plate.IsDirty;
            if (dirty == _shownDirty)
                return;

            _shownDirty = dirty;
            _body.GetPropertyBlock(_block);
            if (PlateGrime.HasGrime(_body))
            {
                // A new mess every time it's used.
                PlateGrime.Set(_block, dirty ? 1f : 0f, Random.value);
            }
            else if (dirty)
            {
                _block.SetColor(BaseColorId, _dirty);
            }
            else
            {
                _block.Clear();
            }
            _body.SetPropertyBlock(_block);
        }
    }
}
