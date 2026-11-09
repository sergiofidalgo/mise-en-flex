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
        // Metres: the quad's side. The shader draws the rim at 0.84 of it, so the ring is about
        // 0.67 m across: a little over the 0.62 m deep basin, which the user wanted for
        // visibility (2026-11-09). It's drawn over everything, so the overhang shows on the rim.
        private const float Size = 0.8f;
        // Metres up from the sink's base: just over the basin's rim (floor 0.68 + depth 0.23, from
        // build_sink()). At water level the ring sat 12 cm below the opening players see, and the
        // 55° camera showed it shifted towards the camera and off centre; drawn over everything,
        // it can sit level with the rim, where it lines up with the opening.
        private const float RingHeight = 0.915f;

        [MenuItem("Tools/Game/Apply Wash Ring")]
        public static void ApplyFromMenu()
        {
            var count = ApplyToOpenScene();
            Debug.Log($"[WashRing] Ring on {count} sink(s); old wash HUD removed. Save the scene to keep it.");
        }

        private const string ToonPath = "Assets/_Project/Art/Materials/MAT_FX_StationToon.mat";

        /// <summary>
        /// The material the 3D kit's pieces share (Game/ToonOnTop: house toon shading, drawn over
        /// whatever is in front); each piece sets its own colour.
        /// </summary>
        public static Material EnsureToonMaterial()
        {
            var shader = Shader.Find("Game/ToonOnTop");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ToonPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ToonPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.renderQueue = -1; // the shader's own: after transparents
            EditorUtility.SetDirty(material);
            return material;
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
            material.SetFloat("_PointRadius", 0.16f);
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
            ring.transform.localPosition = new Vector3(water.localPosition.x, RingHeight, water.localPosition.z);
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
            view.FindProperty("_toon").objectReferenceValue = EnsureToonMaterial();
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Adds the stick hint to a HUD canvas, drawing the control glyphs first if needed.</summary>
        public static void AddStickHint(GameObject hud, Game.Presentation.Hud.HudStyle style)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>($"{ControlGlyphArt.Folder}/{ControlGlyphArt.StickPlate}.png") == null)
                ControlGlyphArt.Build();
            if (!hud.TryGetComponent(out Game.Presentation.Hud.StickHintHud hint))
                hint = hud.AddComponent<Game.Presentation.Hud.StickHintHud>();
            var so = new SerializedObject(hint);
            so.FindProperty("_canvas").objectReferenceValue = hud.GetComponent<RectTransform>();
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_base").objectReferenceValue = ControlGlyphArt.Sprite(ControlGlyphArt.StickBase);
            so.FindProperty("_post").objectReferenceValue = ControlGlyphArt.Sprite(ControlGlyphArt.StickPost);
            so.FindProperty("_cap").objectReferenceValue = ControlGlyphArt.Sprite(ControlGlyphArt.StickCap);
            so.FindProperty("_plate").objectReferenceValue = ControlGlyphArt.Sprite(ControlGlyphArt.StickPlate);
            so.FindProperty("_lift").floatValue = 0f;
            so.FindProperty("_size").floatValue = 60f;
            so.FindProperty("_travel").floatValue = 12f;
            so.ApplyModifiedPropertiesWithoutUndo();
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
                // A ring made at an older size takes the current one.
                var existing = sink.transform.Find(RingName);
                if (existing != null)
                {
                    existing.localScale = new Vector3(Size, Size, 1f) * InverseScale(sink.transform);
                    existing.localPosition = new Vector3(existing.localPosition.x, RingHeight, existing.localPosition.z);
                }
                if (sink.TryGetComponent(out SinkWashRingView view))
                {
                    var so = new SerializedObject(view);
                    so.FindProperty("_toon").objectReferenceValue = EnsureToonMaterial();
                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
                EditorSceneManager.MarkSceneDirty(sink.gameObject.scene);
            }

            // The stick hint lives on the HUD canvas (the control-glyph kit).
            foreach (var prompts in Object.FindObjectsByType<Game.Presentation.Hud.PromptHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                AddStickHint(prompts.gameObject, GreyboxHud.EnsureStyle());
                EditorSceneManager.MarkSceneDirty(prompts.gameObject.scene);
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

            // -90 turns the sink so the chef stands between it and the camera, back to the camera.
            var turns = new[] { 0f, 45f, -90f };
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
                    // Washing well: holding, the target filling. Off target: the stick hint up.
                    view.Preview(point, rate, progress, rim, rate >= 1f ? 0.6f : 0f, rate < 1f);
                    Render(camera, target, sink.transform.position + Vector3.up * 0.9f, CloseHeight, Path.Combine(folder, $"wash_{(int)turn}deg_{name}.png"));
                }
                Render(camera, target, sink.transform.position + new Vector3(-1.5f, 0.9f, -0.5f), PlayHeight, Path.Combine(folder, $"wash_{(int)turn}deg_play.png"));
            }
            // The stick hint is HUD art: composite the HUD over the scene for a stuck player.
            var hud = Object.FindFirstObjectByType<Game.Presentation.Hud.StickHintHud>();
            if (hud != null)
            {
                foreach (var turn in turns)
                {
                    sink.transform.rotation = start * Quaternion.Euler(0f, turn, 0f);
                    var front = -sink.transform.forward;
                    chef.transform.SetPositionAndRotation(sink.transform.position + front * StandDistance, Quaternion.LookRotation(-front, Vector3.up));
                    var lookAt = sink.transform.position + new Vector3(-1.5f, 0.9f, -0.5f);
                    var back = (PlayHeight - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
                    camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, PlayHeight, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));
                    view.Preview(new Vector2(-0.62f, 0.35f), 0f, 0.3f, rim, 0f, true);
                    hud.Refresh(camera, 0.8f);
                    Game.Presentation.Hud.UiCapture.Save(camera, hud.GetComponent<Canvas>(), Path.Combine(folder, $"wash_{(int)turn}deg_hint.png"));
                }
            }

            sink.transform.rotation = start;
            view.EndPreview();
            Object.DestroyImmediate(chef);
            Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[WashRing] Captured to {folder}.");
        }

        private static Texture2D RenderImage(Camera camera, RenderTexture target, Vector3 lookAt, float height, bool faceCamera = true)
        {
            var back = (height - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));
            if (faceCamera)
            {
                foreach (var view in Object.FindObjectsByType<SinkWashRingView>(FindObjectsSortMode.None))
                    view.FaceCamera();
            }
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
