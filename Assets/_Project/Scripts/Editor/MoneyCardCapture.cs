using System;
using System.IO;
using Game.Presentation.Hud;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 1920×1080 stills of the money card (decision 2026-11-10): in the office beside the player bar
    /// (one chef joined), settled; and in the grill's kitchen mid roll-up, mid shake and owed to
    /// the boss, with the day card bottom left. Outside Play mode, so the card is drawn as it would be at that moment.
    /// </summary>
    public static class MoneyCardCapture
    {
        private const string KitchenScene = "Assets/_Project/Scenes/Kitchen_Greybox.unity";

        [MenuItem("Tools/Game/Art/Capture Money Card")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save money card captures", "", "");
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

            // The office: the floor and its player bar as TailorCapture draws them, the card over it.
            TailorCapture.Capture(folder, barOnly: true);
            var barFile = Path.Combine(folder, "office_player_bar_one_joined.png");
            var office = Find();
            var background = Pixels(barFile);
            File.Delete(barFile);
            Over(office, background, Path.Combine(folder, "office_money_card.png"));

            // The office bar as a second player names themselves under their card (2026-11-13).
            var naming = new Game.Simulation.Characters.ChefNaming("Rosa", Game.Data.GuestNames.All["en"], null, 3);
            naming.Move(0, 1);
            TailorCapture.Capture(folder, barOnly: true, naming);

            // The kitchen: a payment rolling in, money going out, and owed to the boss.
            EditorSceneManager.OpenScene(KitchenScene, OpenSceneMode.Single);
            var kitchen = Find();
            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("The kitchen has no main camera: run Tools/Game/Build Greybox Kitchen.");
            var day = UnityEngine.Object.FindFirstObjectByType<DayCardHud>();
            if (day == null)
                throw new InvalidOperationException("No day card in the kitchen: run Tools/Game/Build Greybox Kitchen.");
            day.Preview(3, DayPhase.Service, ServicePhase.Open, 18f, 42f * 60f); // builds its canvas
            var settings = MoneyTickerSettings.Default;
            Shot(kitchen, camera, 1284f, 1250f, settings.RollSeconds * 0.4f, Path.Combine(folder, "kitchen_money_rolling_up.png"));
            Shot(kitchen, camera, 1180f, 1250f, settings.ShakeSeconds / (4f * settings.ShakeWiggles), Path.Combine(folder, "kitchen_money_shake_down.png"));
            Shot(kitchen, camera, -120f, null, 0f, Path.Combine(folder, "kitchen_money_owed.png"));

            // Both cards together: the money as in service, the day card over that still.
            var plain = Path.Combine(folder, "money_only.tmp.png");
            Shot(kitchen, camera, 1250f, null, 0f, plain);
            var money = Pixels(plain);
            File.Delete(plain);
            DayShot(day, money, 3, DayPhase.Service, ServicePhase.Open, 42f, Path.Combine(folder, "kitchen_money_and_day.png"));
            DayShot(day, money, 3, DayPhase.Service, ServicePhase.LastOrders, 168f, Path.Combine(folder, "kitchen_day_last_orders.png"));
            DayShot(day, money, 4, DayPhase.Prep, ServicePhase.Open, 0f, Path.Combine(folder, "kitchen_day_prep.png"));
            Debug.Log($"[MoneyCardCapture] Saved to {folder}");
        }

        private static MoneyCardHud Find()
        {
            var card = UnityEngine.Object.FindFirstObjectByType<MoneyCardHud>();
            if (card == null)
                throw new InvalidOperationException("No money card in the scene: rebuild it with its builder.");
            return card;
        }

        private static void Shot(MoneyCardHud card, Camera camera, float balance, float? from, float seconds, string file)
        {
            card.Preview(balance); // builds the card's canvas
            var canvas = card.GetComponentInChildren<Canvas>(true);
            using var session = UiCapture.Prepare(canvas);
            card.Preview(balance, from, seconds);
            session.Save(camera, file);
        }

        private static void DayShot(DayCardHud day, Color32[] background, int number, DayPhase phase, ServicePhase service, float gameMinutes, string file)
        {
            var canvas = day.GetComponentInChildren<Canvas>(true);
            using var session = UiCapture.Prepare(canvas);
            day.Preview(number, phase, service, 18f, gameMinutes * 60f);
            session.SaveOver(background, file);
        }

        private static void Over(MoneyCardHud card, Color32[] background, string file)
        {
            card.Preview(1250f); // builds the card's canvas
            var canvas = card.GetComponentInChildren<Canvas>(true);
            using var session = UiCapture.Prepare(canvas);
            card.Preview(1250f);
            session.SaveOver(background, file);
        }

        internal static Color32[] Pixels(string file)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.LoadImage(File.ReadAllBytes(file));
            var pixels = image.GetPixels32();
            UnityEngine.Object.DestroyImmediate(image);
            return pixels;
        }
    }
}
