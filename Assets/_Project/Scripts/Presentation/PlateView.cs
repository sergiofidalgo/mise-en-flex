using Game.Gameplay.Items;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Makes a used plate look dirty, so it reads from the overhead camera.</summary>
    public sealed class PlateView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private ItemView _itemView;
        [SerializeField] private Renderer _body;
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
            if (dirty)
                _block.SetColor(BaseColorId, _dirty);
            else
                _block.Clear();
            _body.SetPropertyBlock(_block);
        }
    }
}
