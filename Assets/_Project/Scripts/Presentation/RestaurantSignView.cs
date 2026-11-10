using Game.Data;
using Game.Gameplay;
using TMPro;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The street sign's lettering (Phase 6, milestone 3): the restaurant's own name, as bought or
    /// renamed, and its style beneath it, so each place reads as itself from the street. Set once
    /// the kitchen has loaded which restaurant it plays.
    /// </summary>
    public sealed class RestaurantSignView : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private TextMeshPro _name;
        [SerializeField] private TextMeshPro _style;

        private void Start()
        {
            var services = _root != null ? _root.Services : null;
            if (services == null)
                return;
            if (_name != null)
                _name.text = services.RestaurantName;
            if (_style != null)
                _style.text = string.IsNullOrEmpty(services.StyleName) ? "" : Tr.Get(services.StyleName);
        }
    }
}
