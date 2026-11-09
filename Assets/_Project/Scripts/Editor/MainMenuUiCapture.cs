using System;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Saving;
using Game.Presentation.Hud;
using Game.Presentation.Menu;
using Game.Simulation.Saving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            Canvas canvas = null;
            try
            {
                canvas = Object.FindFirstObjectByType<MainMenu>().DrawForCapture(onCards, card);
                UiCapture.Save(Camera.main, canvas, file);
            }
            finally
            {
                if (canvas != null)
                    Object.DestroyImmediate(canvas.gameObject);
                SaveFile.DirectoryOverride = previous;
            }
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
