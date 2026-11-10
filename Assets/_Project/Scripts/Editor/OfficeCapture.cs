using System;
using System.IO;
using Game.Presentation.Office;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Renders the office greybox to PNGs at 1920 × 1080: the wide shot before anyone joins, and
    /// play-height shots over each part of the floor, at the camera's own tilt. Run with
    /// -executeMethod Game.Editor.OfficeCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;.
    /// </summary>
    public static class OfficeCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const float Tilt = 55f;

        // Play-height shots: where on the floor the view centres, and the camera's height.
        private static readonly (string Name, Vector2 Target, float Height)[] Shots =
        {
            ("office_reception", new Vector2(0f, -8.5f), 16f),
            ("office_boss", new Vector2(0f, 8f), 16f),
            ("office_lounge", new Vector2(-12f, -9f), 16f),
            ("office_kitchen_tailor", new Vector2(-12f, 3f), 18f),
            ("office_accountant_hall", new Vector2(12f, -5f), 18f),
            ("office_backroom", new Vector2(12f, 8f), 16f),
            ("office_terrace", new Vector2(6f, 16f), 16f),
            ("office_play", new Vector2(0f, -7f), 11f),
        };

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -captureFolder <folder>.");
            Capture(args[index + 1]);
        }

        [MenuItem("Tools/Game/Capture Office")]
        public static void CaptureToScreenshots() => Capture("Screenshots/Office");

        private static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(OfficeBuilder.ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            camera.aspect = Width / (float)Height;
            var rotation = Quaternion.Euler(Tilt, 0f, 0f);

            Render(camera, camera.GetComponent<OfficeCamera>().Wanted(), rotation, Path.Combine(folder, "office_wide.png"));
            var back = 1f / Mathf.Tan(Tilt * Mathf.Deg2Rad);
            foreach (var (name, target, height) in Shots)
                Render(camera, new Vector3(target.x, height, target.y - height * back), rotation, Path.Combine(folder, name + ".png"));
            // Two players far apart (the elevator and the terrace): the follow camera must fit both.
            var spread = new System.Collections.Generic.List<System.Numerics.Vector2> { new System.Numerics.Vector2(0f, -12f), new System.Numerics.Vector2(9f, 18f) };
            var fit = Game.Simulation.Movement.CameraFraming.Fit(spread, Tilt, camera.fieldOfView, camera.aspect, 3f, 10f, 55f, screenTop: 0.15f,
                buildingMin: new System.Numerics.Vector2(-17.5f, -13.5f), buildingMax: new System.Numerics.Vector2(17.5f, 19.5f));
            Render(camera, new Vector3(fit.X, fit.Y, fit.Z), rotation, Path.Combine(folder, "office_two_players.png"));
            // The opening's street, 120 m below the floor: looking along it, and up at the tower.
            Render(camera, new Vector3(40f, -118.2f, -24f), Quaternion.Euler(4f, -90f, 0f), Path.Combine(folder, "street_along.png"));
            Render(camera, new Vector3(14f, -116.5f, -36f), Quaternion.Euler(-12f, -20f, 0f), Path.Combine(folder, "street_lobby.png"));
            OpeningFrames(camera, folder);
            Debug.Log($"[OfficeCapture] Saved to {folder}");
        }

        // The opening's flight and climb, sampled along the scene's own path, in the street's thinner haze.
        private static void OpeningFrames(Camera camera, string folder)
        {
            FreezeAMoment();
            FreezeTheRest();
            var sequence = UnityEngine.Object.FindFirstObjectByType<OpeningSequence>();
            if (sequence == null)
                return;
            var so = new SerializedObject(sequence);
            var flight = ReadPoses(so.FindProperty("_flight"));
            var climb = ReadPoses(so.FindProperty("_climb"));
            var fog = so.FindProperty("_streetFog").vector2Value;
            var officeFog = new Vector2(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            RenderSettings.fogStartDistance = fog.x;
            RenderSettings.fogEndDistance = fog.y;
            foreach (var t in new[] { 0f, 0.5f, 1f })
            {
                var pose = OpeningPath.SampleEven(flight, t);
                Render(camera, pose.Position, pose.Rotation, Path.Combine(folder, $"opening_flight_{Mathf.RoundToInt(t * 100):000}.png"));
            }
            var path = new CameraPose[climb.Length + 1];
            path[0] = flight[flight.Length - 1];
            climb.CopyTo(path, 1);
            // The menu over the stop, as a new player sees it (no campaigns yet): how much of the street it leaves in view.
            var menu = UnityEngine.Object.FindFirstObjectByType<Game.Presentation.Menu.MainMenu>();
            if (menu != null)
            {
                var stopPose = flight[flight.Length - 1];
                camera.transform.SetPositionAndRotation(stopPose.Position, stopPose.Rotation);
                var previous = Game.Gameplay.Saving.SaveFile.DirectoryOverride;
                var empty = Path.Combine(Path.GetTempPath(), "MiseEnFlexOpeningCapture");
                if (Directory.Exists(empty))
                    Directory.Delete(empty, true);
                Directory.CreateDirectory(empty);
                Game.Gameplay.Saving.SaveFile.DirectoryOverride = empty;
                Canvas canvas = null;
                try
                {
                    canvas = menu.DrawForCapture();
                    Game.Presentation.Hud.UiCapture.Save(camera, canvas, Path.Combine(folder, "opening_menu.png"));
                }
                finally
                {
                    if (canvas != null)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    Game.Gameplay.Saving.SaveFile.DirectoryOverride = previous;
                }
            }

            // The glass lift rides with the camera on the climb (OpeningElevator): put the cab where it would be.
            var lift = UnityEngine.Object.FindFirstObjectByType<OpeningElevator>();
            var liftSo = lift != null ? new SerializedObject(lift) : null;
            // The chef walking in (OpeningChef): partway along the walk, watched from the stop.
            var chef = UnityEngine.Object.FindFirstObjectByType<OpeningChef>();
            if (chef != null)
            {
                var chefSo = new SerializedObject(chef);
                var walkList = chefSo.FindProperty("_path");
                var walk = new Vector3[walkList.arraySize];
                for (var i = 0; i < walk.Length; i++)
                    walk[i] = walkList.GetArrayElementAtIndex(i).vector3Value;
                var stop = flight[flight.Length - 1];
                foreach (var share in new[] { 0.35f, 0.8f })
                {
                    chef.transform.position = OpeningPath.PointAlong(walk, OpeningPath.Length(walk) * share, out var heading);
                    chef.transform.rotation = Quaternion.LookRotation(new Vector3(heading.x, 0f, heading.z));
                    // The camera holds still at the stop while the chef walks in.
                    Render(camera, stop.Position, stop.Rotation, Path.Combine(folder, $"opening_entering_{Mathf.RoundToInt(share * 100):000}.png"));
                }
            }
            void PlaceCab(float cameraHeight)
            {
                if (liftSo == null)
                    return;
                var cab = (Transform)liftSo.FindProperty("_cab").objectReferenceValue;
                var y = Mathf.Clamp(cameraHeight + liftSo.FindProperty("_lead").floatValue, liftSo.FindProperty("_bottom").floatValue, liftSo.FindProperty("_top").floatValue);
                cab.position = new Vector3(cab.position.x, y, cab.position.z);
                // The chef rides in it, looking out.
                if (chef != null)
                    chef.transform.SetPositionAndRotation(new Vector3(cab.position.x, y, cab.position.z), Quaternion.LookRotation(Vector3.back));
            }
            for (var i = 1; i < path.Length; i++)
            {
                PlaceCab(path[i].Position.y);
                Render(camera, path[i].Position, path[i].Rotation, Path.Combine(folder, $"opening_climb_{i}.png"));
            }
            // Halfway between climb poses too, where the curve bends.
            for (var i = 1; i <= 4; i++)
            {
                var pose = OpeningPath.SampleEven(path, i / 5f);
                PlaceCab(pose.Position.y);
                Render(camera, pose.Position, pose.Rotation, Path.Combine(folder, $"opening_climbing_{i * 20:000}.png"));
            }
            RenderSettings.fogStartDistance = officeFog.x;
            RenderSettings.fogEndDistance = officeFog.y;
        }

        // A believable instant for stills: in the editor every flash would be on and every pop
        // showing all its icons at once. One flash in five going off, every other phone popping.
        private static void FreezeAMoment()
        {
            var flashes = UnityEngine.Object.FindObjectsByType<Game.Presentation.Office.Ambience.CameraFlash>(FindObjectsSortMode.InstanceID);
            for (var i = 0; i < flashes.Length; i++)
            {
                var so = new SerializedObject(flashes[i]);
                var on = i % 5 == 0;
                if (so.FindProperty("_light").objectReferenceValue is Light light)
                    light.enabled = on;
                if (so.FindProperty("_spark").objectReferenceValue is GameObject spark)
                    spark.SetActive(on);
            }
            var pops = UnityEngine.Object.FindObjectsByType<Game.Presentation.Office.Ambience.SocialPop>(FindObjectsSortMode.InstanceID);
            for (var i = 0; i < pops.Length; i++)
            {
                var so = new SerializedObject(pops[i]);
                var icons = so.FindProperty("_icons");
                var size = so.FindProperty("_size").floatValue;
                var rise = so.FindProperty("_rise").floatValue;
                for (var k = 0; k < icons.arraySize; k++)
                {
                    var icon = icons.GetArrayElementAtIndex(k).objectReferenceValue as GameObject;
                    if (icon == null)
                        continue;
                    var show = i % 2 == 0 && k == (i / 2) % icons.arraySize;
                    icon.SetActive(show);
                    icon.transform.localPosition = Vector3.up * rise * 0.6f;
                    icon.transform.localScale = Vector3.one * size;
                    icon.transform.rotation = Quaternion.identity; // the opening's camera looks north
                }
            }
        }

        // The rest of a believable instant: the feed screen drawn, the heart streams part-way up.
        private static void FreezeTheRest()
        {
            foreach (var screen in UnityEngine.Object.FindObjectsByType<FeedScreen>(FindObjectsSortMode.InstanceID))
                screen.BuildForCapture();
            var streams = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.InstanceID);
            for (var i = 0; i < streams.Length; i++)
            {
                if (streams[i].name != "Hearts")
                    continue;
                // As if its burst began a second ago (every other one), whenever it's due.
                var main = streams[i].main;
                var emission = streams[i].emission;
                var burst = emission.GetBurst(0);
                streams[i].Simulate(burst.time + (i % 2 == 0 ? 1.3f : 0.2f), true, true);
            }
        }

        private static CameraPose[] ReadPoses(SerializedProperty list)
        {
            var poses = new CameraPose[list.arraySize];
            for (var i = 0; i < poses.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                poses[i] = new CameraPose(item.FindPropertyRelative("Position").vector3Value, item.FindPropertyRelative("Target").vector3Value);
            }
            return poses;
        }

        private static void Render(Camera camera, Vector3 position, Quaternion rotation, string file)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render(); // a throwaway frame: shader variants not compiled yet come out black in it
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
        }
    }
}
