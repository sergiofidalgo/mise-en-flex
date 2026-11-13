using System;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using Game.Presentation.Menu;
using Game.Presentation.Office;
using Game.Simulation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// A 1920×1080 still of joining in the main menu (2026-11-13): the office's opening at the menu
    /// stop, the menu as a first-time player sees it, the player bar top right with player 1 in and
    /// player 2 just joined, and both their chefs in the glass lift. Outside Play mode, with no
    /// saves read (a throwaway empty folder). Tools/Game/Art/Capture Menu Join, or batch:
    /// -executeMethod Game.Editor.MenuJoinCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;.
    /// </summary>
    public static class MenuJoinCapture
    {
        [MenuItem("Tools/Game/Art/Capture Menu Join")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save the menu join capture", "", "");
            if (string.IsNullOrEmpty(folder) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -captureFolder <folder>.");
            Capture(args[index + 1]);
        }

        public static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(OfficeBuilder.ScenePath, OpenSceneMode.Single);
            var sequence = Object.FindFirstObjectByType<OpeningSequence>();
            var lift = Object.FindFirstObjectByType<OpeningElevator>();
            var bar = Object.FindFirstObjectByType<PlayerBarHud>();
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var camera = Camera.main;
            if (sequence == null || lift == null || lift.Rider == null || bar == null || menu == null || camera == null)
                throw new InvalidOperationException("The office isn't built: run Tools/Game/Build Office.");

            // The camera at the menu stop: the flight's last pose.
            var flight = new SerializedObject(sequence).FindProperty("_flight");
            var stop = flight.GetArrayElementAtIndex(flight.arraySize - 1);
            var pose = new CameraPose(stop.FindPropertyRelative("Position").vector3Value, stop.FindPropertyRelative("Target").vector3Value);
            camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            // Player 1 waiting in the lift, player 2 just joined beside them.
            var first = lift.Rider;
            first.GetComponent<OpeningChef>()?.Wear(0);
            var second = lift.AddRider(1);
            if (second != null)
            {
                second.localScale = first.localScale; // the pop plays only in Play mode
                second.GetComponent<OpeningChef>()?.Wear(1);
            }

            var saves = Path.Combine(Path.GetTempPath(), "MenuJoinCapture");
            if (Directory.Exists(saves))
                Directory.Delete(saves, true);
            Directory.CreateDirectory(saves);
            var previous = SaveFile.DirectoryOverride;
            SaveFile.DirectoryOverride = saves;
            try
            {
                var background = Path.Combine(folder, "menu.tmp.png");
                UiCapture.Save(camera, menu.DrawForCapture(), background);
                var pixels = MoneyCardCapture.Pixels(background);
                File.Delete(background);

                var staged = new Dictionary<int, (Transform, TailorSession)> { [0] = (first, null) };
                if (second != null)
                    staged[1] = (second, null);
                // Laid out at 1920×1080, not batch mode's small screen.
                var sized = new RenderTexture(1920, 1080, 24);
                camera.targetTexture = sized;
                bar.PreviewBar(staged, camera);
                camera.targetTexture = null;
                sized.Release();
                Debug.Log($"[MenuJoinCapture] Player 1's chef at viewport {camera.WorldToViewportPoint(first.position + Vector3.up)}, player 2's at {(second != null ? camera.WorldToViewportPoint(second.position + Vector3.up).ToString() : "none")}; cab at {lift.Cab.position}.");
                using var session = UiCapture.Prepare(bar.GetComponentInChildren<Canvas>(true));
                session.SaveOver(pixels, Path.Combine(folder, "menu_join.png"));
            }
            finally
            {
                SaveFile.DirectoryOverride = previous;
            }
            Debug.Log($"[MenuJoinCapture] Saved to {folder}");
        }
    }
}
