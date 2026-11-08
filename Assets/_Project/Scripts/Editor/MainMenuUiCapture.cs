using System;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Saving;
using Game.Presentation.Menu;
using Game.Simulation.Saving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Screenshots of the main menu UI at 1920×1080 without entering Play mode, to compare against
    /// the Art Director's mockups. It draws the menu over the night city with sample campaigns that
    /// mirror the mockup (two restaurants and a free slot, and a first-time player with none), kept
    /// in a throwaway folder so real saves are never touched. (MainMenuCapture renders the city alone.)
    /// Menu: Tools/Game/Capture Main Menu UI. Batch mode (editor closed):
    /// Unity -batchmode -projectPath . -executeMethod Game.Editor.MainMenuUiCapture.CaptureFromCommandLine
    ///       [-menuOut folder] [-menuPhotos folder with photo1.png, photo2.png] [-menuRebuild]
    /// </summary>
    public static class MainMenuUiCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Tools/Game/Capture Main Menu UI")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var folder = Path.GetFullPath("Screenshots/MainMenu");
            Capture(folder, null);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                if (Argument("-menuRebuild") != null)
                    MainMenuBuilder.Build();
                Capture(Argument("-menuOut") ?? Path.GetFullPath("Screenshots/MainMenu"), Argument("-menuPhotos"));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(string outFolder, string photos)
        {
            Directory.CreateDirectory(outFolder);
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath, OpenSceneMode.Single);

            var root = Path.Combine(Path.GetTempPath(), "MiseEnFlexMenuCapture");
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            var returning = Path.Combine(root, "returning");
            var newPlayer = Path.Combine(root, "new");
            WriteSampleCampaigns(returning, photos);
            Directory.CreateDirectory(newPlayer);

            Shoot(returning, Path.Combine(outFolder, "menu_returning.png"), false, 0);
            Shoot(returning, Path.Combine(outFolder, "menu_cards.png"), true, 1);
            Shoot(returning, Path.Combine(outFolder, "menu_free_slot.png"), true, 2);
            Shoot(newPlayer, Path.Combine(outFolder, "menu_new_player.png"), false, 0);
            Debug.Log($"[MainMenuUiCapture] Saved screenshots to {outFolder}.");

            // Leave the scene as it is on disk.
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath, OpenSceneMode.Single);
        }

        private static void Shoot(string saves, string file, bool onCards, int card)
        {
            var previous = SaveFile.DirectoryOverride;
            SaveFile.DirectoryOverride = saves;
            var camera = Camera.main;
            Canvas canvas = null;
            Camera uiCamera = null;
            try
            {
                var menu = Object.FindFirstObjectByType<MainMenu>();
                canvas = menu.DrawForCapture(onCards, card);

                // In the game the menu is an overlay drawn after the city's post-processing (the ink).
                // Rendering it through the city's camera would ink its edges too, so: the city alone,
                // then the UI through a bare camera, once over black and once over white. The two
                // differ only where the UI is see-through, which gives its coverage for compositing.
                canvas.enabled = false;
                var city = Render(camera);
                canvas.enabled = true;

                uiCamera = new GameObject("CaptureUiCamera").AddComponent<Camera>();
                uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                uiCamera.nearClipPlane = 0.01f;
                uiCamera.farClipPlane = 10f;
                var data = uiCamera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                canvas.gameObject.layer = LayerMask.NameToLayer("UI");
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;

                uiCamera.backgroundColor = new Color(0f, 0f, 0f, 1f);
                var overBlack = Render(uiCamera);
                uiCamera.backgroundColor = new Color(1f, 1f, 1f, 1f);
                var overWhite = Render(uiCamera);

                var result = new Color32[city.Length];
                for (var i = 0; i < city.Length; i++)
                {
                    // Over black the UI gives colour × coverage; white shows through by (1 − coverage).
                    var see = ((overWhite[i].r - overBlack[i].r) + (overWhite[i].g - overBlack[i].g) + (overWhite[i].b - overBlack[i].b)) / (3f * 255f);
                    see = Mathf.Clamp01(see);
                    result[i] = new Color32(
                        (byte)Mathf.Clamp(overBlack[i].r + see * city[i].r, 0f, 255f),
                        (byte)Mathf.Clamp(overBlack[i].g + see * city[i].g, 0f, 255f),
                        (byte)Mathf.Clamp(overBlack[i].b + see * city[i].b, 0f, 255f),
                        255);
                }
                var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                image.SetPixels32(result);
                image.Apply();
                File.WriteAllBytes(file, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                if (uiCamera != null)
                    Object.DestroyImmediate(uiCamera.gameObject);
                if (canvas != null)
                    Object.DestroyImmediate(canvas.gameObject);
                SaveFile.DirectoryOverride = previous;
            }
        }

        private static Color32[] Render(Camera camera)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render(); // a throwaway frame: shader variants not compiled yet come out black in it
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            camera.targetTexture = null;
            var pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
            return pixels;
        }

        // The mockup's two restaurants: slot 1 last played yesterday, slot 2 on 2 November, slot 3 free.
        private static void WriteSampleCampaigns(string root, string photos)
        {
            var previous = SaveFile.DirectoryOverride;
            SaveFile.DirectoryOverride = root;
            try
            {
                Write(0, new CampaignSave
                {
                    RestaurantName = "The Copper Pan",
                    StyleName = "Steakhouse",
                    Area = "Midwest",
                    Day = 11,
                    Balance = 2340f,
                    PlayedSeconds = 4 * 3600 + 12 * 60,
                    LastPlayedTicks = DateTime.Now.AddDays(-1).Ticks,
                    Cooks = new List<int> { 0, 1 },
                    Reviews = Reviews(213, i => i % 5 < 4 ? 4f : 3f, "@ines.eats", "Perfect medium-rare, worth the wait."),
                }, photos, "photo1.png");
                Write(1, new CampaignSave
                {
                    RestaurantName = "Low Tide Shack",
                    StyleName = "Seafood",
                    Area = "New England",
                    Day = 30,
                    Balance = 9815f,
                    PlayedSeconds = 11 * 3600 + 40 * 60,
                    LastPlayedTicks = new DateTime(2026, 11, 2, 21, 0, 0).Ticks,
                    Cooks = new List<int> { 0, 1, 2, 3 },
                    Reviews = Reviews(1305, i => i % 5 < 3 ? 4f : 5f, "@lobsterlenny", "Lobster roll of my life."),
                }, photos, "photo2.png");
            }
            finally
            {
                SaveFile.DirectoryOverride = previous;
            }
        }

        private static List<ReviewRecord> Reviews(int count, Func<int, float> stars, string bestGuest, string bestLine)
        {
            var reviews = new List<ReviewRecord>();
            for (var i = 0; i < count; i++)
                reviews.Add(new ReviewRecord { Day = i % 10, Stars = stars(i), Guest = $"guest{i}" });
            reviews.Add(new ReviewRecord { Day = 10, Stars = 5f, Guest = bestGuest, Line = bestLine });
            return reviews;
        }

        private static void Write(int slot, CampaignSave save, string photos, string photoName)
        {
            SaveSlots.File(slot).Write(save);
            var photo = string.IsNullOrEmpty(photos) ? null : Path.Combine(photos, photoName);
            if (photo != null && File.Exists(photo))
                File.Copy(photo, SaveSlots.PhotoPath(slot), true);
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            if (index < 0)
                return null;
            return index + 1 < args.Length && !args[index + 1].StartsWith("-") ? args[index + 1] : "";
        }
    }
}
