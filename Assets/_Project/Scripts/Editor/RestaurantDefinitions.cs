using Game.Data;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// The restaurants on the city miniature as data (Phase 6, milestone 1). Each is created once with
    /// its starting values; later tuning in the Inspector is never overwritten. Menu:
    /// Tools/Game/Build Restaurant Definitions (Build Greybox Kitchen runs it too).
    /// </summary>
    public static class RestaurantDefinitions
    {
        public const string Folder = "Assets/_Project/Data/Restaurants";
        public const string GrillPath = Folder + "/RES_Grill.asset";

        [MenuItem("Tools/Game/Build Restaurant Definitions")]
        public static void BuildAll()
        {
            EnsureGrill();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Today's grill counter: the one restaurant every save before Phase 6 played, so its id is the
        /// one the migration gives restaurant 1. Its name, street and style match the Service Config.
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
                price: 0f, // the boss hands it over: it's where the campaign starts
                feeScale: 1f,
                // What the greybox kitchen starts with (GreyboxKitchenBuilder).
                inheritedAppliances: new[] { "hob", "board", "knife rack", "fridge", "shelf", "bar" },
                previousStyle: "Grill",
                oldReviews: null,
                street: "Old Town",
                // The stalls on the plaza beside it (GreyboxStreet).
                vendors: new[] { "Butcher", "Greengrocer" });
            EditorUtility.SetDirty(grill);
            return grill;
        }
    }
}
