using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using Game.Gameplay.Office;
using Game.Presentation;
using Game.Presentation.Office;
using Game.Simulation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// A still of the tailor's room with the player bar in every state (the user asked to see it):
    /// players 1 and 2 dressing on ladders (their cards grown), player 3 joined and standing at a
    /// ladder's foot (a compact card), player 4's slot empty (translucent), and the tailor measuring
    /// player 2. Staged by hand, as the bar and the tailor only run in Play mode. Nothing is saved.
    /// Batch mode: -executeMethod Game.Editor.TailorCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;
    /// (add -barOnly for the second still: one player joined and three open seats).
    /// </summary>
    public static class TailorCapture
    {
        private const string ScenePath = "Assets/_Project/Scenes/Office_Greybox.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string CoopSettingsPath = "Assets/_Project/Data/Settings/CoopSettings.asset";
        private const int Width = 1920;
        private const int Height = 1080;
        private const float Tilt = 55f;
        private const float CameraHeight = 9.5f;

        [MenuItem("Tools/Game/Art/Capture Tailor")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save tailor captures", "", "");
            if (string.IsNullOrEmpty(folder) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture(folder, false);
            EditorUtility.RevealInFinder(folder);
        }

        [MenuItem("Tools/Game/Art/Capture Player Bar (1 joined)")]
        private static void CaptureBarFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save player bar capture", "", "");
            if (string.IsNullOrEmpty(folder) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture(folder, true);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -captureFolder <folder>.");
            Capture(args[index + 1], Array.IndexOf(args, "-barOnly") >= 0);
        }

        /// <summary>
        /// <paramref name="barOnly"/>: just player 1, standing at a ladder's foot, and three open seats,
        /// the bar as most sessions start. Otherwise every state at once (see the class summary).
        /// </summary>
        public static void Capture(string folder, bool barOnly) => Capture(folder, barOnly, null);

        /// <summary>
        /// With <paramref name="naming"/>: the bar as a second player has just joined and names
        /// themselves under their card, player 1 standing at a ladder (2026-11-13).
        /// </summary>
        public static void Capture(string folder, bool barOnly, Game.Simulation.Characters.ChefNaming naming)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var coop = AssetDatabase.LoadAssetAtPath<CoopSettings>(CoopSettingsPath);
            var ladders = UnityEngine.Object.FindObjectsByType<TailorPlinth>(FindObjectsSortMode.None).OrderBy(l => l.transform.position.x).ToList();
            var hud = UnityEngine.Object.FindFirstObjectByType<PlayerBarHud>();
            if (ladders.Count == 0 || hud == null)
                throw new InvalidOperationException("No tailor in the office: run Tools/Game/Build Office.");

            // Three players: two dressing (one looking at a locked hat), one just standing at a foot.
            var stagings = new[]
            {
                (Look: new ChefLook { Hat = "toque", Face = "crop", Skin = "skin1", Outfit = "whites" }, Dressing: true, Section: WardrobeModule.Hat, Browse: "headdress"),
                (Look: new ChefLook { Hat = "cap", Face = "afro", Skin = "skin6", Outfit = "linecook", Accessory = "glasses" }, Dressing: true, Section: WardrobeModule.Outfit, Browse: "wok"),
                (Look: new ChefLook { Hat = "bandana", Face = "ginger", Skin = "skin0", Outfit = "pastry" }, Dressing: false, Section: WardrobeModule.Hat, Browse: ""),
            };
            var staged = new Dictionary<int, (Transform, TailorSession)>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var players = barOnly ? (naming != null ? 2 : 1) : stagings.Length;
            if (barOnly)
            {
                stagings[1] = (stagings[1].Look, false, WardrobeModule.Hat, "");
                stagings[0] = (stagings[2].Look, false, WardrobeModule.Hat, "");
            }
            for (var i = 0; i < players && i < ladders.Count; i++)
            {
                var (look, dressing, section, browse) = stagings[i];
                var chef = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                chef.transform.SetPositionAndRotation(dressing ? ladders[i].TopPoint : ladders[i].FootPoint, Quaternion.Euler(0f, 180f, 0f));
                var tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", coop.GetPlayerColor(i));
                var tinted = new SerializedObject(chef.GetComponent<PlayerVisual>()).FindProperty("_tintedRenderers");
                for (var t = 0; t < tinted.arraySize; t++)
                    ((Renderer)tinted.GetArrayElementAtIndex(t).objectReferenceValue)?.SetPropertyBlock(tint);

                var wardrobe = chef.GetComponentInChildren<ChefWardrobe>(true);
                if (!dressing)
                {
                    wardrobe.Show(look);
                    staged[i] = (chef.transform, null);
                    continue;
                }
                var session = new TailorSession(look, Array.Empty<string>());
                while (session.Module != section)
                    session.StepModule(1);
                for (var step = 0; step < 20 && session.Preview.Get(section) != browse; step++)
                    session.StepItem(1);
                staged[i] = (chef.transform, session);
            }

            // The tailor beside the second ladder, facing its chef, as if measuring.
            var tailor = GameObject.Find("TailorWalk");
            if (tailor != null && ladders.Count > 1 && !barOnly)
            {
                var ladder = ladders[1].transform;
                tailor.transform.position = ladder.position - ladder.forward * 0.5f + ladder.right * 0.62f;
                var look = ladders[1].TopPoint - tailor.transform.position;
                look.y = 0f;
                tailor.transform.rotation = Quaternion.LookRotation(look);
            }

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            HouseStyle.SmoothEdges(camera);
            var centre = new Vector3(-13.25f, 0.9f, 9.2f);
            var back = (CameraHeight - centre.y) / Mathf.Tan(Tilt * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(centre.x, CameraHeight, centre.z - back), Quaternion.Euler(Tilt, 0f, 0f));
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;

            hud.PreviewNaming(1, naming);
            var portraits = hud.PreviewBar(staged, camera);
            for (var pass = 0; pass < 2; pass++) // the first frame compiles shaders
            {
                foreach (var portrait in portraits)
                    Draw(portrait, portrait.targetTexture);
                Draw(camera, target);
            }
            Save(target, Path.Combine(folder, naming != null ? "office_naming.png" : barOnly ? "office_player_bar_one_joined.png" : "tailor_player_bar.png"));

            target.Release();
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[TailorCapture] Saved to {folder}");
        }

        private static void Draw(Camera camera, RenderTexture destination)
        {
            var request = new RenderPipeline.StandardRequest { destination = destination };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = destination;
                camera.Render();
            }
        }

        private static void Save(RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
