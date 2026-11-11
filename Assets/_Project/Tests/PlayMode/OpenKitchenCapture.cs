using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using SimVector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: screenshots of rearranging after the decisions of 2026-11-11, a hob turned 45°
    /// and pushed into the east wall, the counter pushed against the dividing wall, a hob carried into
    /// the dining room and a table into the kitchen, both warning of mess. Explicit, so it only runs when asked for:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.OpenKitchenCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen).
    /// </summary>
    [Explicit("Screenshots of rearranging against the walls; run it by name.")]
    public class OpenKitchenCapture
    {
        [UnityTest]
        public IEnumerator CaptureRearrangingAgainstTheWalls()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");

            var manager = Object.FindFirstObjectByType<PlayerInputManager>();
            var player = manager.JoinPlayer(controlScheme: "Gamepad", pairWithDevices: InputSystem.AddDevice<Gamepad>());
            for (var i = 0; i < 10; i++)
                yield return null;

            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            root.Services.Days.Advance(); // rearranging is for Prep since 2026-11-10
            root.Services.Days.Receive(500f);
            yield return null;
            var cursor = editor.Enter(player);
            var folder = Folder();

            // A hob, turned 45°, pushed hard into the east wall: it stops by its corner.
            var hob = root.Services.Stations.All.OfType<HobStation>().First();
            cursor.Position = Floor(hob.transform.position);
            Assert.That(editor.PickUp(cursor), Is.True);
            editor.Rotate(cursor, 45f);
            Carry(editor, cursor, new SimVector2(20f, cursor.Position.Y));
            yield return Shoot(Path.Combine(folder, "hob_turned_against_east_wall.png"));

            // Square on, against the dividing wall from the kitchen side.
            editor.Rotate(cursor, -45f);
            Carry(editor, cursor, new SimVector2(0.4f, -2.95f));
            yield return Shoot(Path.Combine(folder, "hob_against_dividing_wall.png"));

            // Past the wall's middle it's in the dining room, warning of mess.
            Carry(editor, cursor, new SimVector2(1.5f, -5f));
            yield return Shoot(Path.Combine(folder, "hob_in_dining_room_warning.png"));
            editor.Cancel(cursor);

            // A table in the kitchen.
            cursor.Position = new SimVector2(0f, 0f);
            Assert.That(editor.Buy(cursor, editor.Shop.First(e => e.Id == "table")), Is.True);
            Carry(editor, cursor, new SimVector2(-0.6f, -1.8f));
            yield return Shoot(Path.Combine(folder, "table_in_kitchen_warning.png"));
            editor.Cancel(cursor);

            foreach (var gamepad in InputSystem.devices.OfType<Gamepad>().ToList())
                InputSystem.RemoveDevice(gamepad);
        }

        private static void Carry(LayoutEditor editor, LayoutEditor.Cursor cursor, SimVector2 to)
        {
            cursor.Position = to;
            editor.MoveCursor(cursor, SimVector2.Zero);
        }

        private static IEnumerator Shoot(string file)
        {
            // Long enough for the turn to ease round and the label to settle.
            for (var i = 0; i < 40; i++)
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
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/OpenKitchen");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static SimVector2 Floor(Vector3 position) => new SimVector2(position.x, position.z);
    }
}
