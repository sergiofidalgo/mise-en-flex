using System.IO;
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
            if (File.Exists(OfficeBuilder.ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            Debug.Log($"[GeneratedScenes] {OfficeBuilder.ScenePath} isn't in git; building it now (a minute or two).");
            OfficeBuilder.Build();
        }
    }
}
