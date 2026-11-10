using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The office scene isn't kept in git (decision 2026-11-10): the builder makes ~23,000 objects for the
    /// tower and city, about 55 MB of scene, which GitHub warns about on every push. Instead it's built
    /// from <see cref="OfficeBuilder"/> whenever it's missing: in a fresh clone or worktree, the first time
    /// the Editor opens. Batch runs build it explicitly (Tools/Game/Build Office, or
    /// <c>-executeMethod Game.Editor.OfficeBuilder.Build</c>), so this waits for an interactive Editor.
    /// Kitchens other than the grill's are built the same way when missing (Phase 6, milestone 3).
    /// </summary>
    [InitializeOnLoad]
    public static class GeneratedScenes
    {
        static GeneratedScenes()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            // Kitchens other than the grill's aren't in git either (Phase 6, milestone 3): each is
            // generated from its restaurant's definition.
            var kitchens = RestaurantDefinitions.EnsureKitchens()
                .Where(r => !File.Exists(RestaurantDefinitions.ScenePath(r.KitchenScene))).ToList();
            if (kitchens.Count > 0)
            {
                // Building opens new scenes: offer to save what's open first, and reopen it after.
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                var open = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
                Debug.Log($"[GeneratedScenes] Building the missing kitchens: {string.Join(", ", kitchens.Select(k => k.KitchenScene))}.");
                GreyboxKitchenBuilder.BuildMissingKitchens();
                if (!string.IsNullOrEmpty(open) && File.Exists(open))
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(open);
            }
            if (File.Exists(OfficeBuilder.ScenePath))
                return;
            Debug.Log($"[GeneratedScenes] {OfficeBuilder.ScenePath} isn't in git; building it now (a minute or two).");
            OfficeBuilder.Build();
        }
    }
}
