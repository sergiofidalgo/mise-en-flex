using System;
using System.Collections;
using System.IO;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Gameplay.Stations;
using Game.Gameplay.Street;
using Game.Simulation.Days;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Not a test: stills of a chef hit by a car (in the air, then dazed on the pavement) and of a
    /// chef who walked off being carried back and set down (2026-11-12), from the game camera,
    /// rendered off screen so batch mode works. Explicit, so it only runs when asked for by name:
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.StreetMishapsCapture [-captureFolder folder]
    /// </summary>
    [Explicit("Stills of the street's mishaps; run it by name.")]
    public class StreetMishapsCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private Gamepad _pad;

        [TearDown]
        public void Release()
        {
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
            _pad = null;
        }

        [UnityTest]
        public IEnumerator CaptureTheMishaps()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync("Kitchen_Greybox");
            yield return null;
            var root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _pad = InputSystem.AddDevice<Gamepad>("MishapsCapturePad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            yield return null;
            yield return null;
            var chef = player.GetComponent<PlayerInteractor>();
            var movement = chef.GetComponent<PlayerMovement>();
            if (root.Services.Days.Phase == DayPhase.Plan)
                root.Services.Days.Advance();
            var folder = Folder();
            var hits = Object.FindFirstObjectByType<HitAndRun>();
            var leaving = Object.FindFirstObjectByType<SceneLeave>();
            var door = Object.FindFirstObjectByType<CustomerDirector>().Door;

            // Out on the pavement first, so the camera has settled on the street.
            movement.Teleport(new System.Numerics.Vector2(door.position.x - 2f, door.position.z - 2.5f));
            yield return new WaitForSeconds(3f);

            // In front of a car driving past the restaurant.
            RoadVehicle car = null;
            var until = Time.time + 25f;
            while (car == null && Time.time < until)
            {
                foreach (var candidate in Object.FindObjectsByType<RoadVehicle>(FindObjectsSortMode.None))
                    if (candidate.Speed > 2f && Mathf.Abs(candidate.transform.position.x - door.position.x) < 2.5f)
                        car = candidate;
                yield return null;
            }
            if (car != null)
            {
                var struck = false;
                var landed = false;
                hits.Struck += (_, _) => struck = true;
                hits.Landed += _ => landed = true;
                movement.Teleport(new System.Numerics.Vector2(car.transform.position.x + car.transform.forward.x * 3f, car.transform.position.z));
                until = Time.time + 3f;
                while (!struck && Time.time < until)
                    yield return null;
                yield return new WaitForSeconds(0.4f);
                Shoot(Path.Combine(folder, "mishap_1_thrown.png"));
                until = Time.time + 3f;
                while (!landed && Time.time < until)
                    yield return null;
                yield return new WaitForSeconds(0.6f);
                Shoot(Path.Combine(folder, "mishap_2_dazed.png"));
                until = Time.time + 5f;
                while (hits.IsDown(chef) && Time.time < until)
                    yield return null;
            }

            // Then off the west end of the pavement, to be carried back.
            var returned = false;
            leaving.Returned += _ => returned = true;
            chef.ReceiveItem(root.Services.CreateItem("Steak"), chef.transform.position);
            movement.Teleport(new System.Numerics.Vector2(door.position.x - 22f, door.position.z - 1.5f));
            yield return new WaitForSeconds(2.6f);
            Shoot(Path.Combine(folder, "mishap_3_carried.png"));
            until = Time.time + 15f;
            while (!returned && Time.time < until)
                yield return null;
            yield return new WaitForSeconds(0.5f);
            Shoot(Path.Combine(folder, "mishap_4_set_down.png"));
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
