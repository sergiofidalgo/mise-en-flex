using Game.Gameplay.Stations;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// While a player faces a hob, a small chip floats above the pan in the colour of the food's
    /// doneness (its middle band: red, pink, tan, grey). The edge bands are thin from the kitchen
    /// camera; the chip makes the same information readable at a glance. No numbers
    /// (decision 2026-11-02).
    /// </summary>
    public sealed class DonenessChipView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private HobStation _hob;
        [SerializeField] private Renderer _chip;

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _chip.enabled = false;
        }

        private void Update()
        {
            var pan = _hob.Hob?.Pan;
            var food = pan != null && pan.Contents.Count > 0 ? pan.Contents[0] : null;

            IngredientView view = null;
            if (food != null && _hob.IsTargeted && _hob.Services.ItemViews.TryGetView(food, out var itemView))
                itemView.TryGetComponent(out view);

            var show = view != null && view.ShowsDoneness;
            _chip.enabled = show;
            if (!show)
                return;

            _chip.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, view.CoreColor);
            _chip.SetPropertyBlock(_block);
        }
    }
}
