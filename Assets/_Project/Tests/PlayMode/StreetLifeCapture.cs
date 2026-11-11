using System;
using System.Collections;
using System.IO;
using System.Reflection;
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
            var lamps = Object.FindFirstObjectByType<StreetLampsView>();
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

            SetHours(lamps, 25f); // never dark
            yield return new WaitForSeconds(9f);
            Shoot(Camera.main, Path.Combine(folder, "street_life_game_day.png"));
            Shoot(wide, Path.Combine(folder, "street_life_wide_day.png"));

            SetHours(lamps, 0f); // always dark: the lamps come on over a few seconds
            yield return new WaitForSeconds(9f);
            Shoot(Camera.main, Path.Combine(folder, "street_life_game_dusk.png"));
            Shoot(wide, Path.Combine(folder, "street_life_wide_dusk.png"));

            // Closer on the pavement by the sign, where the menu board stands.
            wide.transform.SetPositionAndRotation(new Vector3(3f, 7f, -17f), Quaternion.Euler(35f, 10f, 0f));
            yield return new WaitForSeconds(6f);
            Shoot(wide, Path.Combine(folder, "street_life_board.png"));
        }

        private static void SetHours(StreetLampsView lamps, float dusk)
        {
            if (lamps == null)
                return;
            typeof(StreetLampsView).GetField("_duskHour", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lamps, dusk);
            typeof(StreetLampsView).GetField("_dawnHour", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lamps, 0f);
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
