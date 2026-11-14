using System;
using System.IO;
using Game.Data;
using Game.Presentation.Hud;
using Game.Simulation.Service;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// 1920×1080 stills of the service timeline, the day card in service (2026-11-14), over the
    /// kitchen with the money card: a dinner, its rush callout, last orders, closed, an all-day
    /// service and a lunch, and a Spanish one. Each also cropped to the card's corner. Outside Play mode.
    /// </summary>
    public static class ServiceClockCapture
    {
        private const string Folder = "Screenshots/ServiceClock";
        private const string ConfigPath = "Assets/_Project/Data/Settings/ServiceConfig.asset";

        [MenuItem("Tools/Game/Art/Capture Service Clock")]
        private static void CaptureFromMenu()
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

        /// <summary>A service's settings for one of the configured opening slots, by its id.</summary>
        public static ServiceSettings Settings(string slot)
        {
            var config = AssetDatabase.LoadAssetAtPath<ServiceConfig>(ConfigPath);
            if (config == null)
                throw new InvalidOperationException($"No service config at {ConfigPath}.");
            var opening = config.NewOpeningTimes();
            var index = -1;
            for (var i = 0; i < opening.Slots.Count; i++)
            {
                if (opening.Slots[i].Id == slot)
                    index = i;
            }
            var settings = new ServiceSettings();
            opening.Apply(settings, index);
            return settings;
        }

        private static void Capture()
        {
            Directory.CreateDirectory(Folder);
            var language = Locale.Language;
            try
            {
                Locale.Language = "en";
                EditorSceneManager.OpenScene(GreyboxKitchenBuilder.ScenePath, OpenSceneMode.Single);
                var camera = Camera.main;
                if (camera == null)
                    throw new InvalidOperationException("The kitchen has no main camera: run Tools/Game/Build Greybox Kitchen.");
                var money = Object.FindFirstObjectByType<MoneyCardHud>();
                var day = Object.FindFirstObjectByType<DayCardHud>();
                if (money == null || day == null)
                    throw new InvalidOperationException("The kitchen is missing its money or day card: run Tools/Game/Build Greybox Kitchen.");

                Shoot(camera, money, day, "dinner", "Dinner", 18.5f, ServicePhase.Open, null, "dinner_1830.png");
                Shoot(camera, money, day, "dinner", "Dinner", 20.4f, ServicePhase.Open, 0.3f, "dinner_rush_callout.png");
                Shoot(camera, money, day, "dinner", "Dinner", 21.6f, ServicePhase.Open, null, "dinner_2136.png");
                Shoot(camera, money, day, "dinner", "Dinner", 23.75f, ServicePhase.LastOrders, null, "dinner_last_orders.png");
                Shoot(camera, money, day, "dinner", "Dinner", 24f, ServicePhase.Closed, null, "dinner_closed.png");
                Shoot(camera, money, day, "allday", "All day", 13.4f, ServicePhase.Open, 1.2f, "allday_lunch_rush.png");
                Shoot(camera, money, day, "allday", "All day", 17.4f, ServicePhase.Open, null, "allday_1724.png");
                Shoot(camera, money, day, "lunch", "Lunch", 15f, ServicePhase.Open, null, "lunch_1500.png");
                Locale.Language = "es";
                Shoot(camera, money, day, "dinner", Tr.Get("Dinner"), 23.75f, ServicePhase.LastOrders, null, "dinner_last_orders_es.png");
                Shoot(camera, money, day, "dinner", Tr.Get("Dinner"), 20.4f, ServicePhase.Open, 0.6f, "dinner_rush_callout_es.png");
                Debug.Log($"[ServiceClockCapture] Saved to {Folder}.");
            }
            finally
            {
                Locale.Language = language;
            }
        }

        private static void Shoot(Camera camera, MoneyCardHud money, DayCardHud day, string slot, string name, float hour, ServicePhase phase, float? callout, string file)
        {
            var settings = Settings(slot);
            var elapsed = (hour - settings.Clock.OpensAtHour) * settings.Clock.SecondsPerHour;
            var path = Path.Combine(Folder, file);
            money.Preview(1250f);
            using (var session = UiCapture.Prepare(money.GetComponentInChildren<Canvas>(true)))
            {
                money.Preview(1250f);
                session.Save(camera, path);
            }
            day.PreviewService(5, name, settings, phase, elapsed, callout);
            using (var session = UiCapture.Prepare(day.GetComponentInChildren<Canvas>(true)))
            {
                day.PreviewService(5, name, settings, phase, elapsed, callout);
                session.SaveOver(MoneyCardCapture.Pixels(path), path);
            }
            Crop(path, Path.Combine(Folder, "corner_" + file), 560, 260);
        }

        // The bottom-left corner, where the card is.
        private static void Crop(string from, string to, int width, int height)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.LoadImage(File.ReadAllBytes(from));
            var corner = new Texture2D(width, height, TextureFormat.RGBA32, false);
            corner.SetPixels(image.GetPixels(0, 0, width, height));
            corner.Apply();
            File.WriteAllBytes(to, corner.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(corner);
        }
    }
}
