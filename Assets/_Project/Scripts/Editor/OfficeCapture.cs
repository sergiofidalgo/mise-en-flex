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

        /// <summary>
        /// The terrace shot (decision 2026-11-10), as the camera holds it with a chef out there, and
        /// the same with the rail's pigeons caught along their flight off it. Run with
        /// -executeMethod Game.Editor.OfficeCapture.CaptureTerraceFromCommandLine -captureFolder &lt;folder&gt;.
        /// </summary>
        public static void CaptureTerraceFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            CaptureTerrace(index >= 0 && index + 1 < args.Length ? args[index + 1] : "Screenshots/Office");
        }

        [MenuItem("Tools/Game/Capture Office Terrace")]
        public static void CaptureTerraceToScreenshots() => CaptureTerrace("Screenshots/Office");

        private static void CaptureTerrace(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(OfficeBuilder.ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            camera.aspect = Width / (float)Height;
            var rotation = Quaternion.Euler(Tilt, 0f, 0f);
            var follow = new SerializedObject(camera.GetComponent<OfficeCamera>());
            var min = follow.FindProperty("_terraceMin").vector2Value;
            var max = follow.FindProperty("_terraceMax").vector2Value;
            var chef = new System.Collections.Generic.List<System.Numerics.Vector2> { new System.Numerics.Vector2(3f, 16f) };
            var shot = Game.Simulation.Movement.TerraceFraming.Fit(chef, new System.Numerics.Vector2(min.x, min.y), new System.Numerics.Vector2(max.x, max.y),
                follow.FindProperty("_beyondRail").floatValue, follow.FindProperty("_doorwaySlack").floatValue, Tilt, camera.fieldOfView, camera.aspect,
                follow.FindProperty("_margin").floatValue, follow.FindProperty("_minHeight").floatValue, follow.FindProperty("_maxHeight").floatValue,
                follow.FindProperty("_screenTop").floatValue);
            var at = new Vector3(shot.X, shot.Y, shot.Z);
            Render(camera, at, rotation, Path.Combine(folder, "office_terrace_vista.png"));
            Debug.Log($"[OfficeCapture] Terrace shot at {at}");

            // A chef by the tailor's west window, without and with the lean towards it (decision 2026-11-14).
            var floorMin = follow.FindProperty("_floorMin").vector2Value;
            var floorMax = follow.FindProperty("_floorMax").vector2Value;
            var byWindow = new System.Collections.Generic.List<System.Numerics.Vector2> { new System.Numerics.Vector2(floorMin.x + 1f, 8f) };
            var plain = Game.Simulation.Movement.CameraFraming.Fit(byWindow, Tilt, camera.fieldOfView, camera.aspect, follow.FindProperty("_margin").floatValue,
                follow.FindProperty("_minHeight").floatValue, follow.FindProperty("_maxHeight").floatValue, screenTop: follow.FindProperty("_screenTop").floatValue,
                buildingMin: new System.Numerics.Vector2(floorMin.x, floorMin.y), buildingMax: new System.Numerics.Vector2(floorMax.x, floorMax.y));
            var leaned = Game.Simulation.Movement.WindowLean.Apply(plain, -1, follow.FindProperty("_insideLean").floatValue, byWindow, Tilt, camera.fieldOfView,
                camera.aspect, follow.FindProperty("_leanKeepMargin").floatValue, follow.FindProperty("_screenTop").floatValue);
            Render(camera, new Vector3(plain.X, plain.Y, plain.Z), rotation, Path.Combine(folder, "office_window_before.png"));
            Render(camera, new Vector3(leaned.X, leaned.Y, leaned.Z), rotation, Path.Combine(folder, "office_window_lean.png"));

            // The rail's pigeons a third and two thirds of the way along their flight, wings out,
            // as if a chef had stepped up from the south.
            var view = UnityEngine.Object.FindFirstObjectByType<Game.Presentation.Office.Ambience.TerraceBirdsView>();
            var birds = new SerializedObject(view).FindProperty("_birds");
            var settings = new Game.Simulation.Office.BirdSettings();
            for (var i = 0; i < birds.arraySize; i++)
            {
                var bird = birds.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (bird == null)
                    continue;
                var perch = new System.Numerics.Vector3(bird.position.x, bird.position.y, bird.position.z);
                var way = Game.Simulation.Office.TerraceBird.FleeDirection(new System.Numerics.Vector2(perch.X, perch.Z),
                    new System.Numerics.Vector2(perch.X - 1f, perch.Z - 1.5f), settings.Outward, settings.OutwardWeight);
                var p = Game.Simulation.Office.TerraceBird.Arc(perch, way, i % 2 == 0 ? 0.15f : 0.35f, settings);
                bird.SetPositionAndRotation(new Vector3(p.X, p.Y, p.Z), Quaternion.LookRotation(new Vector3(way.X, 0f, way.Y)));
                foreach (Transform part in bird.Find("Bird"))
                    if (part.name.StartsWith("Wing_"))
                        part.gameObject.SetActive(true);
            }
            Render(camera, at, rotation, Path.Combine(folder, "office_terrace_birds.png"));
            Debug.Log($"[OfficeCapture] Saved terrace shots to {folder}");
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

            Game.Presentation.LampLighting.SetBandMix(0f); // the floor as it plays: smooth lamps
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
            // Straight down on the closed street: who rides and walks where.
            Render(camera, new Vector3(0f, -78f, -24f), Quaternion.Euler(90f, 0f, 0f), Path.Combine(folder, "street_plan.png"));
            OpeningFrames(camera, folder);
            Debug.Log($"[OfficeCapture] Saved to {folder}");
        }

        // The opening's flight and climb, sampled along the scene's own path, in the street's thinner haze.
        private static void OpeningFrames(Camera camera, string folder)
        {
            FreezeAMoment();
            FreezeTheRest();
            Game.Presentation.LampLighting.SetBandMix(1f); // the street as it plays: flat-banded lamps
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
            var riders = UnityEngine.Object.FindObjectsByType<Game.Presentation.Office.Ambience.RouteMover>(FindObjectsSortMode.InstanceID);
            foreach (var t in new[] { 0f, 0.5f, 1f })
            {
                // The couriers where they'd be that far into the approach (8 s), a few seconds after the scene starts.
                foreach (var rider in riders)
                    rider.Freeze(4f + t * 8f);
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

            // The climb as the game plays it (OpeningPath.Climb): up beside the lift, over the cornice,
            // onto the floor's wide shot. The cab is put where it would be beside the camera
            // (OpeningElevator), and the helicopter where its pass would have it (OpeningFlyby).
            var lift = UnityEngine.Object.FindFirstObjectByType<OpeningElevator>();
            var flyby = UnityEngine.Object.FindFirstObjectByType<OpeningFlyby>();
            var landing = camera.GetComponent<OfficeCamera>().Wanted();
            var landingRotation = Quaternion.Euler(Tilt, 0f, 0f);
            var whole = new CameraPose[path.Length + 1];
            path.CopyTo(whole, 0);
            whole[whole.Length - 1] = new CameraPose(landing, landing + landingRotation * Vector3.forward * 30f);
            var turnFrom = so.FindProperty("_turnFrom").floatValue;
            var climbSeconds = so.FindProperty("_climbSeconds").floatValue;
            var flybyFrom = so.FindProperty("_flybyFrom").floatValue;
            foreach (var t in new[] { 0.15f, 0.3f, 0.45f, 0.55f, 0.62f, 0.7f, 0.78f, 0.86f, 0.93f, 1f })
            {
                var position = OpeningPath.Climb(whole, landingRotation, turnFrom, t, out var rotation);
                if (lift != null && lift.Cab != null)
                    lift.Cab.position = new Vector3(lift.Cab.position.x, lift.HeightAt(position.y, t), lift.Cab.position.z);
                if (flyby != null && t >= flybyFrom)
                    flyby.Place((t - flybyFrom) * climbSeconds);
                Game.Presentation.LampLighting.SetBandMix(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(so.FindProperty("_bandsFadeFrom").floatValue, so.FindProperty("_bandsFadeTo").floatValue, t)));
                RenderSettings.fogStartDistance = Mathf.Lerp(fog.x, officeFog.x, OpeningPath.ClimbEase(t));
                RenderSettings.fogEndDistance = Mathf.Lerp(fog.y, officeFog.y, OpeningPath.ClimbEase(t));
                Render(camera, position, rotation, Path.Combine(folder, $"opening_climb_{Mathf.RoundToInt(t * 100):000}.png"));
            }
            // The same street and floors with the lamps' light in flat steps (LampLighting's trial
            // dial), to judge beside the smooth ones.
            RenderSettings.fogStartDistance = fog.x;
            RenderSettings.fogEndDistance = fog.y;
            Game.Presentation.LampLighting.SetBandMix(1f);
            foreach (var bands in new[] { 0f, 3f, 5f })
            {
                Shader.SetGlobalFloat("_GameLampBands", bands);
                var stop = flight[flight.Length - 1];
                Render(camera, stop.Position, stop.Rotation, Path.Combine(folder, $"look_street_bands{bands:0}.png"));
                var position = OpeningPath.Climb(whole, landingRotation, turnFrom, 0.45f, out var rotation);
                if (lift != null && lift.Cab != null)
                    lift.Cab.position = new Vector3(lift.Cab.position.x, lift.HeightAt(position.y, 0.45f), lift.Cab.position.z);
                Render(camera, position, rotation, Path.Combine(folder, $"look_floors_bands{bands:0}.png"));
            }
            // Back to the scene's own setting.
            var lamps = UnityEngine.Object.FindFirstObjectByType<Game.Presentation.LampLighting>();
            if (lamps != null)
            {
                lamps.enabled = false;
                lamps.enabled = true;
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
                var on = i % 9 == 4; // a flash is lit for a blink every few seconds: few are on in any one frame
                if (so.FindProperty("_light").objectReferenceValue is Light light)
                    light.enabled = on;
                if (so.FindProperty("_spark").objectReferenceValue is GameObject spark)
                {
                    spark.SetActive(on);
                    // A flare faces the camera, which looks north here, at its full size.
                    spark.transform.rotation = Quaternion.identity;
                    var parent = spark.transform.parent;
                    // The size of its parent, whichever way round: some of the tower's rooms are mirrored.
                    spark.transform.localScale = Vector3.one * (so.FindProperty("_sparkSize").floatValue / Mathf.Max(1e-4f, parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f));
                }
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

        // The rest of a believable instant: the heart streams part-way up.
        private static void FreezeTheRest()
        {
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
