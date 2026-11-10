using Game.Data;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// The restaurants on the city miniature as data (Phase 6, milestones 1 and 2). Each is created
    /// once with its starting values; later tuning in the Inspector is never overwritten. Menu:
    /// Tools/Game/Build Restaurant Definitions (Build Greybox Kitchen and Build Office run it too).
    /// <para>
    /// Six lots, cheapest first (prices: the user's call, 2026-11-10, the steep ladder). Only the
    /// grill and Casa Lucía, the mid-size dining room, get kitchens in milestone 3; the rest are
    /// data until 6b or Phase 7.
    /// </para>
    /// </summary>
    public static class RestaurantDefinitions
    {
        public const string Folder = "Assets/_Project/Data/Restaurants";
        public const string GrillPath = Folder + "/RES_Grill.asset";
        public const string MiniaturePath = Folder + "/MiniatureConfig.asset";

        /// <summary>The mid-size dining room, the second size playable in 6a.</summary>
        public const string DiningRoomId = "casa-lucia";

        /// <summary>The grill's kitchen: the scene every restaurant opened before milestone 3.</summary>
        public const string GrillScene = "Kitchen_Greybox";

        /// <summary>Casa Lucía's kitchen (milestone 3).</summary>
        public const string DiningRoomScene = "Kitchen_CasaLucia";

        public const string SceneFolder = "Assets/_Project/Scenes";

        /// <summary>Where a kitchen scene of this name is saved.</summary>
        public static string ScenePath(string scene) => $"{SceneFolder}/{scene}.unity";

        [MenuItem("Tools/Game/Build Restaurant Definitions")]
        public static void BuildAll()
        {
            EnsureMiniature();
            EnsureKitchens();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The lots that have a kitchen scene, the grill first (milestone 3). The two playable in 6a
        /// get theirs set once if they have none (the user's calls, 2026-11-10): the grill as it always
        /// was, and Casa Lucía 12 m wide with a 5 m kitchen and a 6 m dining room, its guests 2.4 times
        /// as frequent as the grill's (its fee scale) and up to 6 waiting at the door.
        /// </summary>
        public static RestaurantDefinition[] EnsureKitchens()
        {
            var lots = EnsureLots();
            foreach (var lot in lots)
            {
                if (lot == null || !string.IsNullOrEmpty(lot.KitchenScene))
                    continue;
                if (lot.Id == Version1Restaurant.GrillId)
                    lot.SetKitchen(GrillScene, RestaurantSettings.DefaultRoomWidth, RestaurantSettings.DefaultKitchenDepth, RestaurantSettings.DefaultDiningDepth, guestRate: 1f, maxQueue: 0);
                else if (lot.Id == DiningRoomId)
                    lot.SetKitchen(DiningRoomScene, roomWidth: 12f, kitchenDepth: 5f, diningDepth: 6f, guestRate: 2.4f, maxQueue: 6);
                else
                    continue;
                EditorUtility.SetDirty(lot);
            }
            return System.Array.FindAll(lots, lot => lot != null && !string.IsNullOrEmpty(lot.KitchenScene));
        }

        /// <summary>The miniature's config with every lot on it, created once.</summary>
        public static MiniatureConfig EnsureMiniature()
        {
            var lots = EnsureLots();
            var existing = AssetDatabase.LoadAssetAtPath<MiniatureConfig>(MiniaturePath);
            if (existing != null && existing.Lots.Count > 0)
                return existing;
            var config = GreyboxAssets.EnsureAsset<MiniatureConfig>(MiniaturePath);
            config.Set(lots, startingLoan: 500f, suggestedNames: new[]
            {
                "Golden Fork", "Second Helping", "Salt & Ember", "Little Copper", "Night Kitchen", "The Hungry Owl",
                "Butter & Bloom", "Side Street", "Chef's Table", "Open Flame", "Plate Club", "The Good Pan",
                "Sunday Roast", "Crumb", "Last Orders", "Well Done", "Corner Plate", "Lucky Spoon",
            });
            EditorUtility.SetDirty(config);
            return config;
        }

        /// <summary>Every lot on the miniature, cheapest first.</summary>
        public static RestaurantDefinition[] EnsureLots() => new[]
        {
            EnsureGrill(),
            Ensure("RES_Forno", "forno-rosso", "Forno Rosso", RestaurantSize.Small, seats: 8, price: 2500f, feeScale: 1.6f,
                new[] { "counter", "hob", "board", "fridge", "shelf", "table" }, "Pizzeria",
                new[] { Old(4f, "Proper crust, and the owner remembered my name.", "Giulia R."), Old(2.5f, "Waited forty minutes for a margherita.", "Tom B.") },
                "Market Street", new[] { "Greengrocer", "Dairy" }),
            Ensure("RES_CasaLucia", DiningRoomId, "Casa Lucía", RestaurantSize.DiningRoom, seats: 16, price: 6000f, feeScale: 2.4f,
                new[] { "hob", "hob", "board", "knife rack", "fridge", "shelf", "table", "table", "table", "table" }, "Spanish home cooking",
                new[] { Old(4.5f, "Tortilla like my grandmother's.", "Carmen L."), Old(3f, "Lovely room, slow kitchen.", "Pete H."), Old(2f, "The croquetas were cold in the middle.", "Ana M.") },
                "Riverside", new[] { "Butcher", "Greengrocer", "Fishmonger" }),
            Ensure("RES_PetitZinc", "petit-zinc", "Le Petit Zinc", RestaurantSize.DiningRoom, seats: 24, price: 12000f, feeScale: 3.5f,
                new[] { "induction hob", "induction hob", "board", "knife rack", "fridge", "fridge", "shelf", "table", "table", "table", "table", "table", "table" }, "French bistro",
                new[] { Old(4f, "Steak frites worth the trip.", "Claire D."), Old(3.5f, "Charming, if a little tired.", "Marcus W.") },
                "Theatre Row", new[] { "Butcher", "Greengrocer", "Dairy" }),
            Ensure("RES_HarbourLights", "harbour-lights", "Harbour Lights", RestaurantSize.Large, seats: 32, price: 25000f, feeScale: 5f,
                new[] { "induction hob", "induction hob", "board", "board", "knife rack", "fridge", "fridge", "shelf", "shelf", "bar", "bar", "table", "table", "table", "table", "table", "table" }, "Sushi",
                new[] { Old(4.5f, "The view, the tuna, the sake. Perfect.", "Kenji T."), Old(1.5f, "Paid a fortune for warm fish.", "Lisa G."), Old(3.5f, "Busy on weekends, book ahead.", "Omar F.") },
                "The Harbour", new[] { "Fishmonger", "Greengrocer" }),
            Ensure("RES_GrandVesper", "grand-vesper", "The Grand Vesper", RestaurantSize.Large, seats: 48, price: 50000f, feeScale: 8f,
                new[] { "grill", "grill", "induction hob", "induction hob", "board", "board", "knife rack", "fridge", "fridge", "fridge", "shelf", "shelf", "table", "table", "table", "table", "table", "table", "table", "table" }, "Steakhouse",
                new[] { Old(5f, "Best ribeye in the city, no contest.", "Harold P."), Old(3f, "Grand room, grander prices.", "Nina K.") },
                "Uptown", new[] { "Butcher", "Greengrocer", "Dairy", "Fishmonger" }),
        };

        /// <summary>
        /// Today's grill counter: the one restaurant every save before Phase 6 played, so its id is the
        /// one the migration gives restaurant 1. Its name, street and style match the Service Config.
        /// The first restaurant of a campaign: its style is fixed, so players choose only its name.
        /// </summary>
        public static RestaurantDefinition EnsureGrill()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RestaurantDefinition>(GrillPath);
            if (existing != null)
                return existing;

            var grill = GreyboxAssets.EnsureAsset<RestaurantDefinition>(GrillPath);
            grill.Set(
                id: Version1Restaurant.GrillId,
                displayName: "The Copper Pan",
                size: RestaurantSize.Counter,
                seats: 3, // the three bar tables along the dining side of the wall, one chair each
                price: 400f, // the boss lends 500: this, and 100 for the first morning (decision 2026-11-10)
                feeScale: 1f,
                // What the greybox kitchen starts with (GreyboxKitchenBuilder).
                inheritedAppliances: new[] { "hob", "board", "knife rack", "fridge", "shelf", "bar" },
                previousStyle: "Grill",
                oldReviews: new[] { Old(3f, "Decent burger, sticky counter.", "Dave K."), Old(2f, "The cook looked like he'd given up.", "Sam P.") },
                street: "Old Town",
                // The stalls on the plaza beside it (GreyboxStreet).
                vendors: new[] { "Butcher", "Greengrocer" },
                fixedStyle: true);
            EditorUtility.SetDirty(grill);
            return grill;
        }

        private static RestaurantDefinition Ensure(string asset, string id, string name, RestaurantSize size, int seats, float price, float feeScale,
            string[] appliances, string previousStyle, RestaurantDefinition.OldReviewEntry[] oldReviews, string street, string[] vendors)
        {
            var path = $"{Folder}/{asset}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RestaurantDefinition>(path);
            if (existing != null)
                return existing;
            var definition = GreyboxAssets.EnsureAsset<RestaurantDefinition>(path);
            definition.Set(id, name, size, seats, price, feeScale, appliances, previousStyle, oldReviews, street, vendors);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static RestaurantDefinition.OldReviewEntry Old(float stars, string line, string guest) =>
            new RestaurantDefinition.OldReviewEntry(stars, line, guest);
    }
}
