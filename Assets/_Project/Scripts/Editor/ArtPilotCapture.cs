using System;
using System.IO;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Renders the pilot's hob, pan and steak at each cooking stage from the game camera's angle
    /// and heights, to judge readability without playing. Uses a temporary scene.
    /// Batch mode: -executeMethod Game.Editor.ArtPilotCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;
    /// </summary>
    public static class ArtPilotCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const float CameraPitch = 55f;     // CameraEuler in GreyboxKitchenBuilder
        private const float CameraFieldOfView = 45f;
        private const float StationSpacing = 1f;

        // Camera heights: close enough to check the models, the closest the game goes, and the
        // whole-restaurant height.
        // "behind_chef" shifts the hobs to the top of the screen, where the view is shallowest and
        // a chef in front hides the most.
        private static readonly (string Name, float Height, float LookAtZ)[] Views =
        {
            ("detail_2m", 2f, 0f), ("play_6.5m", 6.5f, 0f), ("wide_11m", 11f, 0f), ("behind_chef", 6.5f, -3f),
        };

        private const float DetailHeight = 2f;
        private const float PlayHeight = 6.5f;
        private const float VarietyRowX = 15f;
        private const float VarietySpacing = 0.34f;
        private const float VarietyCameraHeight = 1.5f;
        private const int VarietyCount = 6;
        private const float ChefDistanceInFront = 0.95f;

        // Browning as a fraction of burnt, then peak temperatures: sides, core. Hob dial: 0..1.
        private static readonly (string Name, float Browning, float SidePeak, float CorePeak, float Dial)[] Stages =
        {
            ("raw", 0f, 4f, 4f, 0f),
            ("rare", 0.35f, 120f, 50f, 0.5f),
            ("medium-rare", 0.45f, 125f, 55f, 0.6f),
            ("medium", 0.5f, 130f, 60f, 0.7f),
            ("well done", 0.75f, 140f, 72f, 0.85f),
            ("burnt", 1f, 160f, 80f, 1f),
        };

        [MenuItem("Tools/Game/Art/Capture Pilot Sheet")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save pilot captures", "", "");
            if (string.IsNullOrEmpty(folder) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture(folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -captureFolder <folder>.");
            Capture(args[index + 1]);
        }

        private static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // As in the kitchen: the same sun, on a floor of the same material.
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 5f;
            floor.GetComponent<Renderer>().sharedMaterial = LoadMaterial("MAT_Greybox_Floor");

            var hobPrefab = Load<GameObject>("Assets/_Project/Prefabs/Furniture/PF_Hob.prefab");
            var panPrefab = Load<GameObject>("Assets/_Project/Prefabs/Items/PF_Item_Pan.prefab");
            var steakPrefab = Load<GameObject>("Assets/_Project/Prefabs/Items/PF_Item_Steak.prefab");
            var chefPrefab = Load<GameObject>("Assets/_Project/Prefabs/PF_Player.prefab");

            // A separate row of raw steaks, away from the hobs, to compare their variety.
            for (var i = 0; i < VarietyCount; i++)
            {
                var position = new Vector3(VarietyRowX + (i - (VarietyCount - 1) / 2f) * VarietySpacing, 0f, 0f);
                var raw = UnityEngine.Object.Instantiate(steakPrefab, position, Quaternion.identity).GetComponent<IngredientView>();
                raw.ApplyVariation(i * 101 + 3);
                raw.ShowState(0f, 4f, 4f, 4f);
            }

            var steakViews = new System.Collections.Generic.List<(IngredientView View, (string Name, float Browning, float SidePeak, float CorePeak, float Dial) Stage)>();
            for (var i = 0; i < Stages.Length; i++)
            {
                var stage = Stages[i];
                var x = (i - (Stages.Length - 1) / 2f) * StationSpacing;
                var hob = UnityEngine.Object.Instantiate(hobPrefab, new Vector3(x, 0f, 0f), Quaternion.identity);
                SetHeat(hob, stage.Dial);
                if (i % 2 == 0)
                    UnityEngine.Object.Instantiate(chefPrefab, new Vector3(x, 0f, -ChefDistanceInFront), Quaternion.identity);

                var anchor = hob.transform.Find("ItemAnchor").position;
                var pan = UnityEngine.Object.Instantiate(panPrefab, anchor, Quaternion.identity);
                var panFloor = pan.transform.Find("Body").GetComponent<Renderer>().bounds.min.y + PanFloorHeight;
                var steak = UnityEngine.Object.Instantiate(steakPrefab, new Vector3(x, panFloor, 0f), Quaternion.identity);
                var steakView = steak.GetComponent<IngredientView>();
                steakView.ApplyVariation(i * 31 + 7);
                steakView.ShowState(stage.Browning, stage.SidePeak, stage.CorePeak, stage.SidePeak);
                steakViews.Add((steakView, stage));

                // Hide the chip: the question is whether the food reads without it.
                hob.transform.Find("DonenessChip").gameObject.SetActive(false);
            }

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = CameraFieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            foreach (var view in Views)
                Render(camera, target, new Vector3(0f, HobTop, view.LookAtZ), view.Height, Path.Combine(folder, $"pilot_{view.Name}.png"));
            Render(camera, target, new Vector3(VarietyRowX, 0f, 0f), VarietyCameraHeight, Path.Combine(folder, "pilot_variety.png"));

            // Before the first flip: only the underside has cooked, and the top is still raw.
            foreach (var (steakView, stage) in steakViews)
                steakView.ShowState(0f, stage.Browning, stage.SidePeak, stage.CorePeak, 4f);
            Render(camera, target, new Vector3(0f, HobTop, 0f), DetailHeight, Path.Combine(folder, "pilot_before_flip_detail.png"));
            Render(camera, target, new Vector3(0f, HobTop, 0f), PlayHeight, Path.Combine(folder, "pilot_before_flip_play.png"));
            target.Release();
            Debug.Log($"[ArtPilotCapture] Saved to {folder}");
        }

        private const float PanFloorHeight = 0.04f; // PanDepth in GreyboxKitchenware
        private const float HobTop = 0.9f;

        private static void Render(Camera camera, RenderTexture target, Vector3 lookAt, float height, string path)
        {
            // Pitched like the game camera.
            var back = (height - lookAt.y) / Mathf.Tan(CameraPitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(
                new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(CameraPitch, 0f, 0f));

            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }

        // HobView needs a running hob, so colour the burner directly from its tuned colours.
        private static void SetHeat(GameObject hob, float dial)
        {
            var so = new SerializedObject(hob.GetComponent<HobView>());
            var color = Color.Lerp(so.FindProperty("_burnerOff").colorValue, so.FindProperty("_burnerFull").colorValue, dial);
            var renderer = hob.transform.Find("Burner").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);

            var pips = so.FindProperty("_pips");
            var lit = Mathf.CeilToInt(dial * pips.arraySize - 0.001f);
            for (var i = 0; i < pips.arraySize; i++)
            {
                var pipColor = so.FindProperty(i < lit ? "_pipOn" : "_pipOff").colorValue;
                var pipName = ((Renderer)pips.GetArrayElementAtIndex(i).objectReferenceValue).name;
                var pipBlock = new MaterialPropertyBlock();
                pipBlock.SetColor("_BaseColor", pipColor);
                hob.transform.Find(pipName).GetComponent<Renderer>().SetPropertyBlock(pipBlock);
            }
        }

        private static Material LoadMaterial(string name) => Load<Material>($"Assets/_Project/Art/Materials/{name}.mat");

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException($"Couldn't load {path}. Run Tools/Game/Build Greybox Kitchen first.");
            return asset;
        }
    }
}
