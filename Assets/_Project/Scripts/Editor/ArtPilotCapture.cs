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
        private static readonly (string Name, float Height)[] Views =
        {
            ("detail_2m", 2f), ("play_6.5m", 6.5f), ("wide_11m", 11f),
        };

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

            for (var i = 0; i < Stages.Length; i++)
            {
                var stage = Stages[i];
                var x = (i - (Stages.Length - 1) / 2f) * StationSpacing;
                var hob = UnityEngine.Object.Instantiate(hobPrefab, new Vector3(x, 0f, 0f), Quaternion.identity);
                SetHeat(hob, stage.Dial);

                var anchor = hob.transform.Find("ItemAnchor").position;
                var pan = UnityEngine.Object.Instantiate(panPrefab, anchor, Quaternion.identity);
                var panFloor = pan.transform.Find("Body").GetComponent<Renderer>().bounds.min.y + PanFloorHeight;
                var steak = UnityEngine.Object.Instantiate(steakPrefab, new Vector3(x, panFloor, 0f), Quaternion.identity);
                steak.GetComponent<IngredientView>().ShowState(stage.Browning, stage.SidePeak, stage.CorePeak, stage.SidePeak);

                // Hide the chip: the question is whether the food reads without it.
                hob.transform.Find("DonenessChip").gameObject.SetActive(false);
            }

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = CameraFieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            foreach (var view in Views)
                Render(camera, target, view.Height, Path.Combine(folder, $"pilot_{view.Name}.png"));
            target.Release();
            Debug.Log($"[ArtPilotCapture] Saved to {folder}");
        }

        private const float PanFloorHeight = 0.04f; // PanDepth in GreyboxKitchenware

        private static void Render(Camera camera, RenderTexture target, float height, string path)
        {
            // Pitched like the game camera, aimed at the top of the row of hobs.
            var lookAt = new Vector3(0f, 0.9f, 0f);
            var back = (height - lookAt.y) / Mathf.Tan(CameraPitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, height, lookAt.z - back), Quaternion.Euler(CameraPitch, 0f, 0f));

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
