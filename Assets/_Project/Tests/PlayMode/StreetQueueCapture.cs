using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: stills of customers walking in along the street and queueing outside on the
    /// pavement (decision 2026-11-11), from the game camera, rendered off screen so batch mode works.
    /// Explicit, so it only runs when asked for by name:
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.StreetQueueCapture [-captureFolder folder]
    /// </summary>
    [Explicit("Stills of the street queue; run it by name.")]
    public class StreetQueueCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [UnityTest]
        public IEnumerator CaptureTheStreetQueue()
        {
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            var days = root.Services.Days;
            days.Advance();
            days.Advance();
            yield return null;

            // Enough guests at once to fill the stools and leave three queueing.
            var service = root.Services.Service;
            var guests = Enumerable.Range(0, service.SeatCount + 3).Select(_ => Next(service)).ToList();
            var folder = Folder();
            yield return new WaitForSeconds(5f);
            Shoot(Path.Combine(folder, "street_walking_in.png"));

            var until = Time.time + 45f;
            while (guests.Count(g => g.State == CustomerState.Queueing) < 3 && Time.time < until)
                yield return null;
            yield return new WaitForSeconds(4f);
            Shoot(Path.Combine(folder, "street_queue.png"));
        }

        private static Customer Next(ServiceSimulation service)
        {
            var count = service.Customers.Count;
            while (service.Customers.Count == count)
                service.Tick(1f);
            return service.Customers.Last();
        }

        private static void Shoot(string path)
        {
            var camera = Camera.main;
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
