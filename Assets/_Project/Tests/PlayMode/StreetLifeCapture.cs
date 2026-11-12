using System;
using System.Collections;
using System.IO;
using Game.Gameplay;
using Game.Presentation;
using Game.Presentation.Street;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: stills of the street's life (2026-11-11: passers-by, pigeons, lamps, the
    /// extractor), from the game camera and from a wider one over the road, by day and after dusk,
    /// rendered off screen so batch mode works. Explicit, so it only runs when asked for by name:
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.StreetLifeCapture [-captureFolder folder]
    /// </summary>
    [Explicit("Stills of the street's life; run it by name.")]
    public class StreetLifeCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [UnityTest]
        public IEnumerator CaptureTheStreetsLife()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var days = root.Services.Days;
            days.Advance();
            days.Advance();
            // The hobs on, so the extractor has something to show.
            foreach (var hob in root.Services.Cooking.Hobs)
                hob.Dial = 1f;
            var folder = Folder();

            // A second camera over the road, looking back at the restaurant: the whole street at once.
            var wide = Object.Instantiate(Camera.main.gameObject).GetComponent<Camera>();
            wide.tag = "Untagged";
            foreach (var behaviour in wide.GetComponents<MonoBehaviour>())
                if (behaviour is FramingCamera)
                    behaviour.enabled = false;
            if (wide.TryGetComponent<AudioListener>(out var ears))
                Object.Destroy(ears);
            wide.transform.SetPositionAndRotation(new Vector3(-2f, 17f, -27f), Quaternion.Euler(42f, 0f, 0f));
            wide.fieldOfView = 50f;

            // No chef is playing here, so the façade (which rises once they're all outside) is put up by hand.
            var facade = Object.FindFirstObjectByType<FacadeView>();
            if (facade != null)
            {
                facade.enabled = false;
                var rise = facade.transform.Find("Rise");
                rise.gameObject.SetActive(true);
                rise.localScale = Vector3.one;
            }

            // Lunch time, then on through the day: golden hour, dusk, night.
            DaylightView.Preview = 13f;
            yield return new WaitForSeconds(9f);
            Shoot(Camera.main, Path.Combine(folder, "street_life_game_day.png"));
            Shoot(wide, Path.Combine(folder, "street_life_wide_day.png"));
            foreach (var (hour, name) in new[] { (18.25f, "golden"), (19.5f, "dusk"), (22f, "night") })
            {
                DaylightView.Preview = hour;
                yield return new WaitForSeconds(hour > 19f ? 9f : 7f); // the light eases over; the lamps come on one by one
                Shoot(wide, Path.Combine(folder, $"street_life_wide_{name}.png"));
            }
            Shoot(Camera.main, Path.Combine(folder, "street_life_game_night.png"));
            wide.transform.SetPositionAndRotation(new Vector3(1f, 7.5f, -21f), Quaternion.Euler(24f, 0f, 0f));
            yield return null;
            Shoot(wide, Path.Combine(folder, "street_life_front_night.png"));
            DaylightView.Preview = 13f;
            yield return new WaitForSeconds(7f);

            // Closer on the pavement by the sign, where the menu board stands.
            wide.transform.SetPositionAndRotation(new Vector3(3f, 7f, -17f), Quaternion.Euler(35f, 10f, 0f));
            yield return new WaitForSeconds(6f);
            Shoot(wide, Path.Combine(folder, "street_life_board.png"));

            // The restaurant's front and the houses behind it, from across the road.
            wide.transform.SetPositionAndRotation(new Vector3(1f, 7.5f, -21f), Quaternion.Euler(24f, 0f, 0f));
            yield return null;
            Shoot(wide, Path.Combine(folder, "street_life_front.png"));

            // The houses behind, with their lane and gardens, seen over the kitchen.
            wide.transform.SetPositionAndRotation(new Vector3(-2f, 8f, -7f), Quaternion.Euler(27f, 0f, 0f));
            yield return null;
            Shoot(wide, Path.Combine(folder, "street_life_houses.png"));
            // The air conditioning's outdoor unit by the east wall, from the parking lot; and the bin by the west wall.
            wide.transform.SetPositionAndRotation(new Vector3(8.5f, 3f, -1.5f), Quaternion.Euler(27f, -55f, 0f));
            yield return null;
            Shoot(wide, Path.Combine(folder, "street_life_unit.png"));
            wide.transform.SetPositionAndRotation(new Vector3(-10f, 6f, -6.5f), Quaternion.Euler(38f, 42f, 0f));
            yield return null;
            Shoot(wide, Path.Combine(folder, "street_life_bin.png"));

            // The driveway into the parking lot, its dropped kerb and the crossing over it.
            wide.transform.SetPositionAndRotation(new Vector3(8f, 10f, -20f), Quaternion.Euler(42f, -4f, 0f));
            yield return null;
            Shoot(wide, Path.Combine(folder, "street_life_driveway.png"));

            // And on the plaza: the bench, the planters, the pigeons between the stalls.
            wide.transform.SetPositionAndRotation(new Vector3(-9f, 8f, -12f), Quaternion.Euler(40f, 0f, 0f));
            yield return null;
            Shoot(wide, Path.Combine(folder, "street_life_plaza.png"));
            DaylightView.Preview = null;
        }

        private static void Shoot(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
        }

        private static string Folder()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/Street");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
