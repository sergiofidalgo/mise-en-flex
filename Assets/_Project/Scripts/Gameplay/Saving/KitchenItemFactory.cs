using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Saving;

namespace Game.Gameplay.Saving
{
    /// <summary>Makes items for a save being read back, from this kitchen's catalogue and equipment.</summary>
    public sealed class KitchenItemFactory : IItemFactory
    {
        private readonly KitchenServices _services;

        public KitchenItemFactory(KitchenServices services) => _services = services;

        public Ingredient Ingredient(string typeId) =>
            _services.Catalog.Find(typeId) != null ? _services.CreateItem(typeId) as Ingredient : null;

        public Pan Pan() => _services.Cooking.AddPan(_services.Equipment.Pan);

        public Plate Plate() => new Plate(_services.Equipment.PlateCapacity);

        public Tray Tray() => new Tray(_services.Equipment.TrayCapacity, _services.Equipment.TrayFreshnessFactor);

        public FoodContainer Container() => new FoodContainer(_services.Equipment.ContainerCapacity, _services.Equipment.ContainerFreshnessFactor);

        public Item Plain(string typeId) => new Item(typeId);

        public void Discard(Item item)
        {
            switch (item)
            {
                case Ingredient ingredient:
                    _services.Cooking.Remove(ingredient);
                    break;
                case Pan pan:
                    _services.Cooking.Remove(pan);
                    break;
            }
            _services.ItemViews.Release(item);
        }
    }
}
