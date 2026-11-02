using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>An ingredient crate: hands out a new item of one type every time.</summary>
    public sealed class CrateStation : Interactable
    {
        [Tooltip("Item type id handed out, matching an entry in the item catalog.")]
        [SerializeField] private string _itemTypeId;

        private ItemDispenser _dispenser;

        public override IItemContainer Container => _dispenser;

        public override void Bind(KitchenServices services)
        {
            _dispenser = new ItemDispenser(_itemTypeId, () => services.CreateItem(_itemTypeId));
        }
    }
}
