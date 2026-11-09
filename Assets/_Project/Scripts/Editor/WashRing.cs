using System;
using System.IO;
using Game.Data;
using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// The washing game's ring on the sink's water (decision 2026-11-09), replacing the circle that
    /// floated above the chef. The kitchen builder adds it with the sink's water; the menu item adds
    /// it to the open scene without a rebuild and removes the old screen-space wash HUD.
    /// Batch: -executeMethod Game.Editor.WashRing.ApplyAndCapture -captureFolder &lt;folder&gt;
    /// </summary>
    public static class WashRing
    {
        private const string MaterialPath = "Assets/_Project/Art/Materials/MAT_FX_WashRing.mat";
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string RingName = "WashRing";
        // Metres: the quad's side. The shader draws the rim at 0.9 of it, so the ring is about
        // 0.54 m across, inside the 0.62 m deep basin at any turn of the sink.
        private const float Size = 0.6f;
        // Metres above the water, so the ring never z-fights it.
        private const float AboveWater = 0.004f;

        [MenuItem("Tools/Game/Apply Wash Ring")]
        public static void ApplyFromMenu()
        {
            var count = ApplyToOpenScene();
            Debug.Log($"[WashRing] Ring on {count} sink(s); old wash HUD removed. Save the scene to keep it.");
        }

        public static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Game/FX/WashRing"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            ApplyLook(material);
            return material;
        }

        // The look the user chose (2026-11-09): current size, a soft deep-blue outline rather than
        // Ink, thick segments. Set every time, so the asset never keeps an older look.
        private static void ApplyLook(Material material)
        {
            material.SetFloat("_RimRadius", 0.84f);
            material.SetFloat("_RimWidth", 0.19f);
            material.SetFloat("_Segments", 16f);
            material.SetFloat("_PointRadius", 0.12f);
            material.SetColor("_InkColor", new Color(0.12f, 0.19f, 0.29f));
            material.SetFloat("_OutlineStrength", 0.4f);
            EditorUtility.SetDirty(material);
        }

        /// <summary>Adds the ring and its view to a sink that has water; does nothing if it has one.</summary>
        public static void AddRing(SinkStation sink, CoopSettings settings)
        {
            if (sink.GetComponent<SinkWashRingView>() != null)
                return;
            var water = sink.transform.Find("Water");
            if (water == null)
            {
                Debug.LogWarning($"[WashRing] {sink.name} has no Water to put the ring on.");
                return;
            }

            var ring = GreyboxAssets.CreatePrimitive(PrimitiveType.Quad, RingName, sink.transform, EnsureMaterial(), keepCollider: false);
            ring.transform.localPosition = water.localPosition + Vector3.up * AboveWater;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(Size, Size, 1f) * InverseScale(sink.transform);
            var renderer = ring.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false; // shown while someone washes (SinkWashRingView)

            var view = new SerializedObject(sink.gameObject.AddComponent<SinkWashRingView>());
            view.FindProperty("_sink").objectReferenceValue = sink;
            view.FindProperty("_settings").objectReferenceValue = settings;
            view.FindProperty("_ring").objectReferenceValue = renderer;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        public static CoopSettings FindSettings()
        {
            var guids = AssetDatabase.FindAssets("t:CoopSettings");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<CoopSettings>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private static int ApplyToOpenScene()
        {
            EnsureMaterial();
            var settings = FindSettings();
            var count = 0;
            foreach (var sink in Object.FindObjectsByType<SinkStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                AddRing(sink, settings);
                if (sink.GetComponent<SinkWashRingView>() != null)
                    count++;
                EditorSceneManager.MarkSceneDirty(sink.gameObject.scene);
            }

            // The old circle above the chef: its script is gone, so remove what's left of it.
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas.gameObject) > 0)
                    EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            }
            return count;
        }

        // A sink scaled in the scene shouldn't stretch the ring.
        private static float InverseScale(Transform parent)
        {
            var scale = parent.lossyScale;
            return 1f / Mathf.Max(0.0001f, (scale.x + scale.z) / 2f);
        }

        // ---- Captures: the ring as it looks in play, at the sink's own angle and turned 45°. ----

        private const int Width = 1920;
        private const int Height = 1080;
        private const float Pitch = 55f;          // the game camera's
        private const float FieldOfView = 45f;
        private const float CloseHeight = 2.6f;
        private const float PlayHeight = 6.5f;
        // Metres from the sink's centre to where a chef stands to wash, along its front.
        private const float StandDistance = 0.75f;

        public static void ApplyAndCapture()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -captureFolder <folder>.");
            Capture(args[index + 1]);
        }

        /// <summary>Applies the ring to the kitchen, saves it, and writes the captures to Screenshots/wash-ring.</summary>
        [MenuItem("Tools/Game/Art/Capture Wash Ring")]
        private static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var folder = Path.GetFullPath("Screenshots/wash-ring");
            Capture(folder);
            EditorUtility.RevealInFinder(folder);
        }

        private static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyToOpenScene();
            EditorSceneManager.SaveScene(scene);

            var sink = Object.FindFirstObjectByType<SinkStation>();
            var view = sink.GetComponent<SinkWashRingView>();
            var settings = FindSettings();
            var rim = settings != null ? settings.GetPlayerColor(0) : new Color(0.91f, 0.53f, 0.23f);

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            camera.tag = "MainCamera"; // the view turns the ring to Camera.main
            HouseStyle.SmoothEdges(camera);
            RenderLook.Backdrop(camera);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };

            // A chef at the sink: the player prefab, standing at its front and facing it.
            var chef = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PF_Player.prefab"));
            Debug.Log($"[WashRing] Sink at {sink.transform.position}, forward {sink.transform.forward}.");

            var turns = new[] { 0f, 45f };
            var states = new[] { ("good", new Vector2(0.08f, 0.05f), 1f, 0.6f), ("bad", new Vector2(-0.62f, 0.35f), 0f, 0.3f) };
            var start = sink.transform.rotation;
            Render(camera, target, sink.transform.position + Vector3.up * 0.9f, CloseHeight, null); // warm-up frame
            foreach (var turn in turns)
            {
                sink.transform.rotation = start * Quaternion.Euler(0f, turn, 0f);
                // The sink's forward faces the wall it stands against; the chef stands on the room side.
                var front = -sink.transform.forward;
                chef.transform.SetPositionAndRotation(sink.transform.position + front * StandDistance, Quaternion.LookRotation(-front, Vector3.up));
                foreach (var (name, point, rate, progress) in states)
                {
                    view.Preview(point, rate, progress, rim, 0.25f, 0.55f);
                    Render(camera, target, sink.transform.position + Vector3.up * 0.9f, CloseHeight, Path.Combine(folder, $"wash_{(int)turn}deg_{name}.png"));
                }
                Render(camera, target, sink.transform.position + new Vector3(-1.5f, 0.9f, -0.5f), PlayHeight, Path.Combine(folder, $"wash_{(int)turn}deg_play.png"));
            }
            sink.transform.rotation = start;
            view.EndPreview();
            Object.DestroyImmediate(chef);
            Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[WashRing] Captured to {folder}.");
        }

        /// <summary>
        /// One image with the ring in four looks, 2×2, for choosing between them: size (share of the
        /// current) and outline (colour, strength). Off target, so the chevrons and the tilt show.
        /// Batch: -executeMethod Game.Editor.WashRing.CaptureVariants -captureFolder &lt;folder&gt;
        /// </summary>
        public static void CaptureVariants()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            var folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Screenshots/wash-ring");
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyToOpenScene();

            var soft = new Color(0.12f, 0.19f, 0.29f);   // deep water blue
            var ink = new Color(0.227f, 0.141f, 0.078f);
            var variants = new (float Size, Color Outline, float Strength)[]
            {
                (1f, ink, 1f),       // top left: as committed
                (1f, soft, 0.4f),    // top right: same size, soft outline
                (0.8f, soft, 0.4f),  // bottom left: smaller, soft outline
                (0.8f, soft, 0f),    // bottom right: smaller, no outline
            };

            var sink = Object.FindFirstObjectByType<SinkStation>();
            var view = sink.GetComponent<SinkWashRingView>();
            var ring = (Renderer)new SerializedObject(view).FindProperty("_ring").objectReferenceValue;
            var settings = FindSettings();
            var rim = settings != null ? settings.GetPlayerColor(0) : new Color(0.91f, 0.53f, 0.23f);
            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            camera.tag = "MainCamera";
            HouseStyle.SmoothEdges(camera);
            RenderLook.Backdrop(camera);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var chef = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PF_Player.prefab"));
            var front = -sink.transform.forward;
            chef.transform.SetPositionAndRotation(sink.transform.position + front * StandDistance, Quaternion.LookRotation(-front, Vector3.up));

            const int cellW = 760, cellH = 560;
            var sheet = new Texture2D(cellW * 2, cellH * 2, TextureFormat.RGB24, false);
            var lookAt = sink.transform.position + Vector3.up * 0.9f;
            var scale = ring.transform.localScale;
            Render(camera, target, lookAt, CloseHeight, null); // warm-up frame
            for (var i = 0; i < variants.Length; i++)
            {
                var (size, outline, strength) = variants[i];
                ring.transform.localScale = scale * size;
                view.Preview(new Vector2(-0.62f, 0.35f), 0f, 0.45f, rim, 0.25f, 0.55f);
                var block = new MaterialPropertyBlock();
                ring.GetPropertyBlock(block);
                block.SetColor("_InkColor", outline);
                block.SetFloat("_OutlineStrength", strength);
                ring.SetPropertyBlock(block);
                var image = RenderImage(camera, target, lookAt, CloseHeight);
                // Screen points for the capture's own size: only while the camera targets it.
                camera.targetTexture = target;
                var centre = camera.WorldToScreenPoint(ring.transform.position);
                camera.targetTexture = null;
                var x0 = Mathf.Clamp((int)centre.x - cellW / 2, 0, Width - cellW);
                var y0 = Mathf.Clamp((int)centre.y - cellH / 2, 0, Height - cellH);
                sheet.SetPixels((i % 2) * cellW, (1 - i / 2) * cellH, cellW, cellH, image.GetPixels(x0, y0, cellW, cellH));
                Object.DestroyImmediate(image);
            }
            ring.transform.localScale = scale;
            view.EndPreview();
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(folder, "wash_variants.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(chef);
            Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[WashRing] Variants captured to {folder}.");
        }

        private static Texture2D RenderImage(Camera camera, RenderTexture target, Vector3 lookAt, float height)
        {
            var back = (height - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));
            foreach (var view in Object.FindObjectsByType<SinkWashRingView>(FindObjectsSortMode.None))
                view.FaceCamera();
            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();
            camera.targetTexture = null;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        private static void Render(Camera camera, RenderTexture target, Vector3 lookAt, float height, string path)
        {
            var back = (height - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));
            // The view faces the ring from the camera it finds; refresh it for this position.
            foreach (var view in Object.FindObjectsByType<SinkWashRingView>(FindObjectsSortMode.None))
                view.FaceCamera();

            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();
            camera.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            if (path != null)
                File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
    }
}
