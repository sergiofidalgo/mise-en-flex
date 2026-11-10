using System.Linq;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// The scenes in a build, in order (decision 2026-11-09): the game boots into the office, whose
    /// opening holds the street, the menu and the floor, and the miniature table loads a kitchen.
    /// Every restaurant with a kitchen of its own has its scene listed after the office, the grill's
    /// first (Phase 6, milestone 3). The old MainMenu scene stays in the project for the menu's UI
    /// captures but isn't in builds. Every scene builder calls this, so whichever runs last leaves
    /// the same order.
    /// </summary>
    public static class BootOrder
    {
        public static void Apply()
        {
            EditorBuildSettings.scenes = Scenes()
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
        }

        /// <summary>The office, then every kitchen scene the restaurant definitions name, the grill's first.</summary>
        public static string[] Scenes() =>
            new[] { OfficeBuilder.ScenePath, RestaurantDefinitions.ScenePath(RestaurantDefinitions.GrillScene) }
                .Concat(RestaurantDefinitions.EnsureKitchens().Select(r => RestaurantDefinitions.ScenePath(r.KitchenScene)))
                .Distinct()
                .ToArray();
    }
}
