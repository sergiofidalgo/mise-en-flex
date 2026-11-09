using System.Linq;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// The scenes in a build, in order (decision 2026-11-09): the game boots into the office, whose
    /// opening holds the street, the menu and the floor, and the miniature table loads the kitchen.
    /// The old MainMenu scene stays in the project for the menu's UI captures but isn't in builds.
    /// Every scene builder calls this, so whichever runs last leaves the same order.
    /// </summary>
    public static class BootOrder
    {
        private const string KitchenScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";

        public static void Apply()
        {
            EditorBuildSettings.scenes = new[] { OfficeBuilder.ScenePath, KitchenScenePath }
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
        }
    }
}
