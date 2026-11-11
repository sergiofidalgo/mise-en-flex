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
using SimVector2 = System.Numerics.Vector2;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: screenshots of the players' foot rings (decision 2026-11-11): four chefs in Prep,
    /// two of them rearranging with their rings under their cursors, and the four in the office.
    /// Explicit, so it only runs when asked for:
    /// Unity -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.FootRingCapture [-captureFolder folder]
    /// (no -batchmode: it grabs the screen).
    /// </summary>
    [Explicit("Screenshots of the foot rings; run it by name.")]
    public class FootRingCapture
    {
        [UnityTest]
        public IEnumerator CaptureTheFootRings()
        {
            Assume.That(Application.isBatchMode, Is.False, "Run it in a visible editor (without -batchmode): it grabs the screen.");
            var folder = Folder();
            var pads = Enumerable.Range(0, 4).Select(_ => InputSystem.AddDevice<Gamepad>()).ToList();
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "foot ring capture");
#endif

            // The kitchen in Prep: every chef on their ring.
            PlayerRoster.Carry(pads.Select((pad, i) => new PlayerRoster.Seat(i, "Gamepad", new InputDevice[] { pad })));
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return Frames(10);
            var players = PlayerInput.all.OrderBy(p => p.playerIndex).ToList();
            Assert.That(players.Count, Is.EqualTo(4));
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            root.Services.Days.Advance(); // Plan to Prep
            yield return Shoot(Path.Combine(folder, "foot_rings_prep.png"), 60);

            // Players 1 and 2 rearrange: their rings leave their chefs for their cursors, one on
            // the open floor and one over the island counter (drawn over it).
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var first = editor.Enter(players[0]);
            var at = players[0].transform.position;
            first.Position = new SimVector2(at.x + 1.2f, at.z - 1.6f);
            var second = editor.Enter(players[1]);
            at = players[1].transform.position;
            second.Position = new SimVector2(at.x - 2.4f, at.z + 0.6f);
            yield return Shoot(Path.Combine(folder, "foot_rings_rearranging.png"), 30);
            editor.Leave(players[0]);
            editor.Leave(players[1]);

            // Back to the office, as from the pause menu: the same four players rejoin at once.
            PlayerRoster.CarryEveryone();
            yield return SceneManager.LoadSceneAsync("Office_Greybox");
            yield return Frames(30);
            players = PlayerInput.all.OrderBy(p => p.playerIndex).ToList();
            Debug.Log($"[FootRingCapture] office fog {RenderSettings.fog} {RenderSettings.fogMode} {RenderSettings.fogStartDistance}-{RenderSettings.fogEndDistance} colour {RenderSettings.fogColor}");
            // Out of the lift, in a loose group in front of it, so all four rings show.
            var front = players[0].transform.position + players[0].transform.forward * 3f;
            for (var i = 0; i < players.Count; i++)
                Teleport(players[i], front + new Vector3((i - 1.5f) * 1.4f, 0f, (i % 2) * 1.2f));
            yield return Shoot(Path.Combine(folder, "foot_rings_office.png"), 120);

            foreach (var pad in pads)
                InputSystem.RemoveDevice(pad);
        }

        private static void Teleport(PlayerInput player, Vector3 position)
        {
            player.transform.position = position;
            Physics.SyncTransforms();
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
                yield return null;
        }

        private static IEnumerator Shoot(string file, int settle)
        {
            yield return Frames(settle);
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);
        }

        private static string Folder()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/FootRings");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
