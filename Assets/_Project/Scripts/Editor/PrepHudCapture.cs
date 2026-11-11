using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Presentation.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Stills of the kitchen's Prep HUD (the user's design, 2026-11-11) at 1920×1080 without Play
    /// mode: the money card, Prep's two tiles bottom left, the player cards top left (a shop card open)
    /// and the ready bar along the bottom, with two and with four cooks. Menu: Tools/Game/Capture Prep HUD. Batch mode (editor closed):
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
            Directory.CreateDirectory(Path.Combine(Folder, "shop"));
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
                var cards = Find<ManagementCardsHud>();
                var catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>("Assets/_Project/Data/Settings/FurnitureCatalog.asset");
                var chefs = Chefs(4);
                ManagementCardsHud.View Shop(ShopSection section, int index, float balance) => ManagementCardsHud.PreviewView(catalog, section, index, balance);

                Shoot(camera, money, day, bar, cards, Row(chefs, 2), new Dictionary<int, float> { [1] = 0.34f }, new[] { 0 }, 5, 5, 1f, "prep_two_cooks.png");
                Shoot(camera, money, day, bar, cards, Row(chefs, 2, (0, Shop(ShopSection.Appliances, 0, 200f))), new Dictionary<int, float>(), new int[0], 5, 5, 1f, "prep_shop.png");
                Shoot(camera, money, day, bar, cards, Row(chefs, 4, (1, Shop(ShopSection.Furniture, 4, 676f)), (2, Shop(ShopSection.Kitchenware, 1, 676f))),
                    new Dictionary<int, float> { [1] = 0.6f }, new[] { 0, 2 }, 12, 14, 2.4f, "prep_four_cooks.png");
                Game.Data.Locale.Language = "es";
                Shoot(camera, money, day, bar, cards, Row(chefs, 2, (0, Shop(ShopSection.Appliances, 0, 200f))), new Dictionary<int, float> { [1] = 0.34f }, new[] { 0 }, 5, 5, 1f, "prep_shop_es.png");
                Game.Data.Locale.Language = "en";
                // Every piece's description, to see none runs past two lines.
                for (var i = 0; i < catalog.Entries.Length; i++)
                {
                    var entry = catalog.Entries[i];
                    var page = catalog.Entries.Where(e => e.Section == entry.Section).ToList();
                    Shoot(camera, money, day, bar, cards, Row(chefs, 1, (0, Shop(entry.Section, page.IndexOf(entry), 676f))), new Dictionary<int, float>(), new int[0], 5, 5, 1f, $"shop/{entry.Id.Replace(' ', '_')}.png", crop: true);
                }
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

        // Stand-in chefs for the player cards' stills, out of the camera's sight.
        private static Transform[] Chefs(int count)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PF_Player.prefab");
            var chefs = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                var chef = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                chef.transform.SetPositionAndRotation(new Vector3(200f + i * 4f, 0f, 200f), Quaternion.Euler(0f, 180f, 0f));
                // Dressed as each player slot's saved chef, as in the game and the office.
                chef.GetComponentInChildren<Game.Presentation.ChefWardrobe>(true)?.Show(Game.Gameplay.Saving.ChefProfileFile.Current.Look(i));
                chefs[i] = chef.transform;
            }
            return chefs;
        }

        private static List<(int, Transform, ManagementCardsHud.View)> Row(Transform[] chefs, int players, params (int Player, ManagementCardsHud.View Shop)[] shops) =>
            Enumerable.Range(0, players).Select(p => (p, chefs[p], shops.FirstOrDefault(s => s.Player == p).Shop)).ToList();

        // The scene with the money card, then the tiles, the player cards and the bar over that.
        private static void Shoot(Camera camera, MoneyCardHud money, DayCardHud day, PrepReadyBarHud bar, ManagementCardsHud cards,
            List<(int, Transform, ManagementCardsHud.View)> row, Dictionary<int, float> holding, int[] ready, int dayNumber, int guests, float groupSize, string file, bool crop = false)
        {
            var players = row.Select(r => r.Item1).ToArray();
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

            cards.Preview(row);
            var hud = cards.GetComponentInParent<Canvas>().rootCanvas;
            using (var session = UiCapture.Prepare(hud))
            {
                cards.Preview(row);
                session.SaveOver(Pixels(path), path);
            }

            var canvas = bar.Preview(players, ready, holding);
            using (var session = UiCapture.Prepare(canvas))
            {
                bar.Preview(players, ready, holding);
                session.SaveOver(Pixels(path), path);
            }
            if (crop)
                CropTopLeft(path, 420, 540);
        }

        // Keeps only the top-left corner, where the shop card is.
        private static void CropTopLeft(string path, int width, int height)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.LoadImage(File.ReadAllBytes(path));
            var corner = new Texture2D(width, height, TextureFormat.RGBA32, false);
            corner.SetPixels(image.GetPixels(0, image.height - height, width, height));
            corner.Apply();
            File.WriteAllBytes(path, corner.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(corner);
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
