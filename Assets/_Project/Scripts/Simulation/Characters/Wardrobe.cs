using System;
using System.Collections.Generic;

namespace Game.Simulation.Characters
{
    /// <summary>The parts of a chef's look a player chooses at the tailor (decision 2026-11-09).</summary>
    public enum WardrobeModule
    {
        Hat,
        Face,
        Skin,
        Accessory,
        Outfit,
    }

    /// <summary>One thing on the rail: an id that saves, an English name the game translates, and
    /// whether it's a crazy item that has to be earned before it can be worn.</summary>
    public sealed class WardrobeItem
    {
        public WardrobeItem(string id, string name, bool crazy = false)
        {
            Id = id;
            Name = name;
            Crazy = crazy;
        }

        public string Id { get; }
        public string Name { get; }
        public bool Crazy { get; }
    }

    /// <summary>
    /// Everything the tailor offers, in the order it's browsed (decision 2026-11-09). Ids are what
    /// saves, so they never change once released; names can. The first item of each module is
    /// the default chef. The presentation layer builds a part for every id.
    /// </summary>
    public static class Wardrobe
    {
        public static readonly IReadOnlyList<WardrobeModule> Modules = new[]
        {
            WardrobeModule.Hat, WardrobeModule.Face, WardrobeModule.Skin, WardrobeModule.Accessory, WardrobeModule.Outfit,
        };

        private static readonly Dictionary<WardrobeModule, WardrobeItem[]> Items = new Dictionary<WardrobeModule, WardrobeItem[]>
        {
            [WardrobeModule.Hat] = new[]
            {
                new WardrobeItem("toque", "Toque"),
                new WardrobeItem("cap", "Cap"),
                new WardrobeItem("bandana", "Bandana"),
                new WardrobeItem("headband", "Headband"),
                new WardrobeItem("horns", "Devil horns", crazy: true),
                new WardrobeItem("crown", "Crown", crazy: true),
                new WardrobeItem("partyhat", "Party hat", crazy: true),
                new WardrobeItem("captain", "Captain's hat", crazy: true),
                new WardrobeItem("propeller", "Propeller cap", crazy: true),
                new WardrobeItem("headphones", "Headphones", crazy: true),
                new WardrobeItem("straw", "Straw hat", crazy: true),
                new WardrobeItem("cowboy", "Cowboy hat", crazy: true),
                new WardrobeItem("plumed", "Plumed hat", crazy: true),
                new WardrobeItem("headdress", "Feather headdress", crazy: true),
            },
            // A face comes with its hair (the user's call): style and colour, nose, facial hair.
            [WardrobeModule.Face] = new[]
            {
                new WardrobeItem("crop", "Short crop"),
                new WardrobeItem("moustache", "Moustache"),
                new WardrobeItem("bun", "Blond bun"),
                new WardrobeItem("afro", "Afro"),
                new WardrobeItem("bald", "Bald"),
                new WardrobeItem("fringe", "Silver fringe"),
                new WardrobeItem("ginger", "Ginger crop"),
                new WardrobeItem("darkbun", "Dark bun"),
            },
            [WardrobeModule.Skin] = SkinTones(),
            [WardrobeModule.Accessory] = new[]
            {
                new WardrobeItem("none", "None"),
                new WardrobeItem("glasses", "Glasses"),
                new WardrobeItem("sunglasses", "Sunglasses"),
                new WardrobeItem("aviators", "Aviators"),
            },
            [WardrobeModule.Outfit] = new[]
            {
                new WardrobeItem("whites", "Chef whites"),
                new WardrobeItem("linecook", "Line cook"),
                new WardrobeItem("wok", "Wok cook"),
                new WardrobeItem("pastry", "Pastry chef"),
                new WardrobeItem("suit", "Suit", crazy: true),
                new WardrobeItem("tourist", "Tourist", crazy: true),
            },
        };

        /// <summary>The number of skin tones (the presentation layer has one colour for each).</summary>
        public const int SkinToneCount = 8;

        private static WardrobeItem[] SkinTones()
        {
            var tones = new WardrobeItem[SkinToneCount];
            for (var i = 0; i < tones.Length; i++)
                tones[i] = new WardrobeItem("skin" + i, "Tone {n}");
            // Tone 2 is the default chef's.
            (tones[0], tones[1]) = (tones[1], tones[0]);
            return tones;
        }

        public static IReadOnlyList<WardrobeItem> Options(WardrobeModule module) => Items[module];

        /// <summary>The item with this id, or null if it isn't on the rail.</summary>
        public static WardrobeItem Find(WardrobeModule module, string id)
        {
            foreach (var item in Items[module])
                if (item.Id == id)
                    return item;
            return null;
        }

        public static WardrobeItem Default(WardrobeModule module) => Items[module][0];

        /// <summary>Where this id is on the rail, or 0 (the default) if it isn't there.</summary>
        public static int IndexOf(WardrobeModule module, string id)
        {
            var items = Items[module];
            for (var i = 0; i < items.Length; i++)
                if (items[i].Id == id)
                    return i;
            return 0;
        }

        /// <summary>Crazy items are worn only once earned; everything else is always open.</summary>
        public static bool IsLocked(WardrobeItem item, ICollection<string> unlocked) =>
            item.Crazy && (unlocked == null || !unlocked.Contains(item.Id));
    }

    /// <summary>A chef's look: one item id per module. Plain data, so it saves as JSON.</summary>
    [Serializable]
    public sealed class ChefLook
    {
        public string Hat = "toque";
        public string Face = "crop";
        public string Skin = "skin1";
        public string Accessory = "none";
        public string Outfit = "whites";

        public string Get(WardrobeModule module) => module switch
        {
            WardrobeModule.Hat => Hat,
            WardrobeModule.Face => Face,
            WardrobeModule.Skin => Skin,
            WardrobeModule.Accessory => Accessory,
            _ => Outfit,
        };

        public void Set(WardrobeModule module, string id)
        {
            switch (module)
            {
                case WardrobeModule.Hat: Hat = id; break;
                case WardrobeModule.Face: Face = id; break;
                case WardrobeModule.Skin: Skin = id; break;
                case WardrobeModule.Accessory: Accessory = id; break;
                default: Outfit = id; break;
            }
        }

        public ChefLook Copy() => (ChefLook)MemberwiseClone();

        /// <summary>
        /// Puts anything that isn't on the rail (an item removed since it was saved, a damaged file)
        /// back to the default chef's, and anything locked back to the default too.
        /// </summary>
        public void Repair(ICollection<string> unlocked)
        {
            foreach (var module in Wardrobe.Modules)
            {
                var item = Wardrobe.Find(module, Get(module));
                if (item == null || Wardrobe.IsLocked(item, unlocked))
                    Set(module, Wardrobe.Default(module).Id);
            }
        }

        public bool SameAs(ChefLook other)
        {
            foreach (var module in Wardrobe.Modules)
                if (Get(module) != other.Get(module))
                    return false;
            return true;
        }
    }
}
