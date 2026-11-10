using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: screenshots of the shop cards (2026-11-10), with four players shopping at once on
    /// different pages, and one player alone. Explicit, so it only runs when asked for:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ManagementCardsCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen).
    /// </summary>
    [Explicit("Screenshots of the shop cards; run it by name.")]
    public class ManagementCardsCapture
    {
        [UnityTest]
        public IEnumerator CaptureTheManagementCards()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");

            var manager = Object.FindFirstObjectByType<PlayerInputManager>();
            var players = Enumerable.Range(0, 4)
                .Select(_ => manager.JoinPlayer(controlScheme: "Gamepad", pairWithDevices: InputSystem.AddDevice<Gamepad>()))
                .ToList();
            for (var i = 0; i < 10; i++)
                yield return null;

            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var cursors = players.Select(p => editor.Enter(p)).ToList();
            // Each player on a different page of the shop, one with a refusal showing.
            Open(cursors[0], Game.Data.ShopSection.Furniture, 1);
            Open(cursors[1], Game.Data.ShopSection.Appliances, 2);
            Open(cursors[2], Game.Data.ShopSection.Kitchenware, 0);
            Open(cursors[3], Game.Data.ShopSection.Appliances, 3);
            cursors[3].Result = Game.Simulation.Movement.Placement.CantAfford;

            var folder = Folder();
            yield return Shoot(Path.Combine(folder, "shop_four_players.png"));

            foreach (var cursor in cursors.Skip(1))
                cursor.ShopOpen = false;
            yield return Shoot(Path.Combine(folder, "shop_one_player.png"));

            foreach (var gamepad in InputSystem.devices.OfType<Gamepad>().ToList())
                InputSystem.RemoveDevice(gamepad);
        }

        private static void Open(LayoutEditor.Cursor cursor, Game.Data.ShopSection section, int index)
        {
            cursor.ShopOpen = true;
            cursor.ShopSection = section;
            cursor.ShopIndex = index;
        }

        private static IEnumerator Shoot(string file)
        {
            // Long enough for the cards' opening pop to settle.
            for (var i = 0; i < 30; i++)
                yield return null;
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        private static string Folder()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/Management");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
