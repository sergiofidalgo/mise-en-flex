using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;

namespace Game.Simulation.Saving
{
    /// <summary>Makes new items while a save is read back. The gameplay layer knows the catalogue and the equipment.</summary>
    public interface IItemFactory
    {
        /// <summary>A new ingredient of this type, registered with the cooking simulation; null if the type is unknown.</summary>
        Ingredient Ingredient(string typeId);

        Pan Pan();

        Plate Plate();

        Tray Tray();

        FoodContainer Container();

        /// <summary>Anything else, by type id.</summary>
        Item Plain(string typeId);

        /// <summary>An item that was made but had nowhere to go: undo whatever making it registered.</summary>
        void Discard(Item item);
    }

    /// <summary>
    /// Turns items into flat <see cref="ItemRecord"/>s and back. Flat because the contents of a pan or
    /// a plate point back at it by index, which keeps the file simple for any JSON writer.
    /// </summary>
    public static class ItemRecords
    {
        /// <summary>Adds <paramref name="item"/>, and everything inside it, to <paramref name="into"/>.</summary>
        /// <param name="station">Index of the station it's on, or -1 if it's loose.</param>
        /// <param name="slot">Which place on that station.</param>
        public static void Write(Item item, int station, int slot, List<ItemRecord> into) => Write(item, station, slot, -1, into);

        private static void Write(Item item, int station, int slot, int parent, List<ItemRecord> into)
        {
            var record = new ItemRecord { Station = station, Slot = slot, Parent = parent, TypeId = item.TypeId, Kind = KindOf(item) };
            switch (item)
            {
                case Ingredient ingredient:
                    record.Ingredient = ingredient.Capture();
                    break;
                case Pan pan:
                    record.Temperature = pan.Temperature;
                    break;
                case Plate plate:
                    record.Dirty = plate.IsDirty;
                    break;
            }

            into.Add(record);
            var index = into.Count - 1;
            if (item is IItemCollection collection)
            {
                foreach (var inside in collection.Items)
                    Write(inside, -1, 0, index, into);
            }
        }

        /// <summary>
        /// Rebuilds the items. Only top-level items are returned, each with its station and slot;
        /// contents are already back inside their containers.
        /// </summary>
        public static List<(ItemRecord Record, Item Item)> Read(IReadOnlyList<ItemRecord> records, IItemFactory factory)
        {
            var built = new Item[records.Count];
            var topLevel = new List<(ItemRecord, Item)>();
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var item = Create(record, factory);
                built[i] = item;
                if (item == null)
                    continue;

                if (record.Parent < 0)
                {
                    topLevel.Add((record, item));
                }
                else if (record.Parent < i && built[record.Parent] is IItemCollection parent && parent.CanAccept(item))
                {
                    parent.Put(item);
                }
                else
                {
                    // Its container is gone or full (a changed capacity): it's lost rather than misplaced.
                    factory.Discard(item);
                    built[i] = null;
                    continue;
                }

                // After it's in place, since putting it somewhere can change its state (a plate stops its doneness).
                if (item is Ingredient ingredient)
                    ingredient.Restore(record.Ingredient);
            }

            // Plates go dirty last: marking one dirty empties it.
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].Dirty && built[i] is Plate plate)
                    plate.MarkDirty();
            }
            return topLevel;
        }

        private static Item Create(ItemRecord record, IItemFactory factory)
        {
            switch (record.Kind)
            {
                case ItemKind.Ingredient:
                    return factory.Ingredient(record.TypeId);
                case ItemKind.Pan:
                    var pan = factory.Pan();
                    if (pan.Properties.FixedTemperature == null)
                        pan.Temperature = record.Temperature;
                    return pan;
                case ItemKind.Plate:
                    return factory.Plate();
                case ItemKind.Knife:
                    return new Knife();
                case ItemKind.Tray:
                    return factory.Tray();
                case ItemKind.Container:
                    return factory.Container();
                default:
                    return factory.Plain(record.TypeId);
            }
        }

        private static ItemKind KindOf(Item item)
        {
            switch (item)
            {
                case Ingredient _:
                    return ItemKind.Ingredient;
                case Pan _:
                    return ItemKind.Pan;
                case Plate _:
                    return ItemKind.Plate;
                case Knife _:
                    return ItemKind.Knife;
                case Tray _:
                    return ItemKind.Tray;
                case FoodContainer _:
                    return ItemKind.Container;
                default:
                    return ItemKind.Plain;
            }
        }
    }
}
