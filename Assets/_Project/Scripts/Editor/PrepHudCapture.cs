using System;
using System.Collections.Generic;
using System.IO;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Stills of the kitchen's Prep HUD (the user's design, 2026-11-11) at 1920×1080 without Play
    /// mode: the money card, Prep's two tiles bottom left and the ready bar along the bottom, with two
    /// and with four cooks. Menu: Tools/Game/Capture Prep HUD. Batch mode (editor closed):
    /// Unity -batchmode -projectPath . -executeMethod Game.Editor.PrepHudCapture.CaptureFromCommandLine
    /// </summary>
    public static class PrepHudCapture
    {
        private const string Folder = "Screenshots/Prep";

        [MenuItem("Tools/Game/Capture Prep HUD")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture();
            EditorUtility.RevealInFinder(Path.GetFullPath(Folder));
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture()
        {
            Directory.CreateDirectory(Folder);
            var language = Game.Data.Locale.Language;
            try
            {
                Game.Data.Locale.Language = "en";
                EditorSceneManager.OpenScene(GreyboxKitchenBuilder.ScenePath, OpenSceneMode.Single);
                var camera = Camera.main;
                if (camera == null)
                    throw new InvalidOperationException("The kitchen has no main camera: run Tools/Game/Build Greybox Kitchen.");
                var money = Find<MoneyCardHud>();
                var day = Find<DayCardHud>();
                var bar = Find<PrepReadyBarHud>();

                Shoot(camera, money, day, bar, new[] { 0, 1 }, new[] { 0 }, new Dictionary<int, float> { [1] = 0.34f }, 5, 5, 1f, "prep_two_cooks.png");
                Shoot(camera, money, day, bar, new[] { 0, 1, 2, 3 }, new[] { 0, 2 }, new Dictionary<int, float> { [1] = 0.6f }, 12, 14, 2.4f, "prep_four_cooks.png");
                Game.Data.Locale.Language = "es";
                Shoot(camera, money, day, bar, new[] { 0, 1 }, new[] { 0 }, new Dictionary<int, float> { [1] = 0.34f }, 5, 5, 1f, "prep_two_cooks_es.png");
                Debug.Log($"[PrepHudCapture] Saved to {Folder}.");
            }
            finally
            {
                Game.Data.Locale.Language = language;
            }
        }

        private static T Find<T>() where T : Object
        {
            var found = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (found == null)
                throw new InvalidOperationException($"No {typeof(T).Name} in the kitchen: run Tools/Game/Build Greybox Kitchen.");
            return found;
        }

        // The scene with the money card, then the tiles over that, then the bar over that.
        private static void Shoot(Camera camera, MoneyCardHud money, DayCardHud day, PrepReadyBarHud bar,
            int[] players, int[] ready, Dictionary<int, float> holding, int dayNumber, int guests, float groupSize, string file)
        {
            var path = Path.Combine(Folder, file);
            money.Preview(676f);
            using (var session = UiCapture.Prepare(money.GetComponentInChildren<Canvas>(true)))
            {
                money.Preview(676f);
                session.Save(camera, path);
            }

            day.PreviewPrep(dayNumber, 18f, guests, groupSize);
            using (var session = UiCapture.Prepare(day.GetComponentInChildren<Canvas>(true)))
            {
                day.PreviewPrep(dayNumber, 18f, guests, groupSize);
                session.SaveOver(Pixels(path), path);
            }

            var canvas = bar.Preview(players, ready, holding);
            using (var session = UiCapture.Prepare(canvas))
            {
                bar.Preview(players, ready, holding);
                session.SaveOver(Pixels(path), path);
            }
        }

        private static Color32[] Pixels(string file)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.LoadImage(File.ReadAllBytes(file));
            var pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            return pixels;
        }
    }
}
