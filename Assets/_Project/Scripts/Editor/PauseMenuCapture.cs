using System;
using System.IO;
using Game.Presentation.Hud;
using Game.Presentation.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Screenshots of the pause menu (decision 2026-11-10) at 1920×1080 without entering Play mode:
    /// its pages over the kitchen, the main page over the office, and the settings in Spanish.
    /// Menu: Tools/Game/Capture Pause Menu. Batch mode (editor closed):
    /// Unity -batchmode -projectPath . -executeMethod Game.Editor.PauseMenuCapture.CaptureFromCommandLine [-pauseOut folder]
    /// </summary>
    public static class PauseMenuCapture
    {
        [MenuItem("Tools/Game/Capture Pause Menu")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var folder = Path.GetFullPath("Screenshots/PauseMenu");
            Capture(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-pauseOut");
                Capture(index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/PauseMenu"));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            var language = Game.Data.Locale.Language;
            try
            {
                Game.Data.Locale.Language = "en";
                EditorSceneManager.OpenScene(GreyboxKitchenBuilder.ScenePath, OpenSceneMode.Single);
                Shoot(folder, "kitchen_main.png", PauseMenu.CapturePage.Main, owner: 0, unsaved: true, day: 3);
                Shoot(folder, "kitchen_settings.png", PauseMenu.CapturePage.Settings, owner: 0, unsaved: true, day: 3);
                Shoot(folder, "kitchen_advanced.png", PauseMenu.CapturePage.Advanced, owner: 1, unsaved: true, day: 3, row: 3);
                Shoot(folder, "kitchen_confirm_office.png", PauseMenu.CapturePage.ConfirmOffice, owner: 1, unsaved: true, day: 3);
                Game.Data.Locale.Language = "es";
                Shoot(folder, "kitchen_settings_es.png", PauseMenu.CapturePage.Settings, owner: 2, unsaved: true, day: 3);
                Game.Data.Locale.Language = "en";

                EditorSceneManager.OpenScene(OfficeBuilder.ScenePath, OpenSceneMode.Single);
                Shoot(folder, "office_main.png", PauseMenu.CapturePage.Main, owner: 2, unsaved: false, day: 1);
                Debug.Log($"[PauseMenuCapture] Saved screenshots to {folder}.");
            }
            finally
            {
                Game.Data.Locale.Language = language;
            }
        }

        private static void Shoot(string folder, string file, PauseMenu.CapturePage page, int owner, bool unsaved, int day, int row = 0)
        {
            var menu = Object.FindFirstObjectByType<PauseMenu>();
            if (menu == null)
                throw new InvalidOperationException("No PauseMenu in the scene: rebuild it (Tools/Game/Build Greybox Kitchen or Build Office).");
            Canvas canvas = null;
            try
            {
                canvas = menu.DrawForCapture(page, owner, unsaved, day, row);
                UiCapture.Save(Camera.main, canvas, Path.Combine(folder, file));
            }
            finally
            {
                if (canvas != null)
                    Object.DestroyImmediate(canvas.gameObject);
            }
        }
    }
}
