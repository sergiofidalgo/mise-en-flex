using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Stills of the boss where players see him (the user's playtest, 2026-11-11: his hat hid too
    /// much): behind his desk from the office camera, and his kitchen visitor (PF_BossVisitor) on the
    /// kitchen floor beside a chef, from the game camera. Nothing is saved.
    /// Batch mode: -executeMethod Game.Editor.BossCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;
    /// (add -name &lt;prefix&gt; to tell runs apart). The office shots need the office built.
    /// </summary>
    public static class BossCapture
    {
        private const string KitchenScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string BossPrefabPath = "Assets/_Project/Prefabs/PF_BossVisitor.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const int Width = 1920;
        private const int Height = 1080;
        private const float Pitch = 55f;     // both game cameras' tilt
        private const float FieldOfView = 45f;

        [MenuItem("Tools/Game/Art/Capture Boss")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save boss captures", "", "");
            if (string.IsNullOrEmpty(folder) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Capture(folder, "boss");
            EditorUtility.RevealInFinder(folder);
        }

        /// <summary>Rebuilds only the boss's and bodyguard's kitchen prefabs, as the kitchen builder does.</summary>
        [MenuItem("Tools/Game/Build Boss Visitors")]
        public static void BuildVisitors() => OfficeBuilder.BuildVisitorPrefabs();

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -captureFolder <folder>.");
            var name = Array.IndexOf(args, "-name");
            Capture(args[index + 1], name >= 0 && name + 1 < args.Length ? args[name + 1] : "boss");
        }

        /// <summary>
        /// Batch mode: rebuilds the visitor prefabs and the office from the builder's current look,
        /// then captures (-captureFolder, -name as above).
        /// </summary>
        public static void RebuildAndCaptureFromCommandLine()
        {
            BuildVisitors();
            OfficeBuilder.Build();
            CaptureFromCommandLine();
        }

        public static void Capture(string folder, string prefix)
        {
            Directory.CreateDirectory(folder);
            if (File.Exists(OfficeBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(OfficeBuilder.ScenePath, OpenSceneMode.Single);
                var boss = GameObject.Find("Boss");
                if (boss != null)
                {
                    var camera = Camera.main;
                    var at = boss.transform.position + Vector3.up * 1f;
                    Render(camera, at, 16f, Path.Combine(folder, $"{prefix}_office.png"));  // the office camera's room shot
                    Render(camera, at, 9f, Path.Combine(folder, $"{prefix}_office_play.png")); // as close as it comes with a chef there
                }
            }

            EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var kitchenCamera = new GameObject("Capture Camera").AddComponent<Camera>();
            kitchenCamera.fieldOfView = FieldOfView;
            HouseStyle.SmoothEdges(kitchenCamera);
            RenderLook.Backdrop(kitchenCamera);
            // In the dining room in front of the pass, facing the camera, a chef beside him for scale.
            var spot = new Vector3(0f, 0f, -2.6f);
            var visitor = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
            visitor.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, 180f, 0f));
            if (playerPrefab != null)
            {
                var chef = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                chef.transform.SetPositionAndRotation(spot + new Vector3(1.1f, 0f, 0f), Quaternion.Euler(0f, 200f, 0f));
            }
            Render(kitchenCamera, spot + Vector3.up * 0.9f, 6.5f, Path.Combine(folder, $"{prefix}_kitchen_play.png"));
            Render(kitchenCamera, spot + Vector3.up * 0.9f, 3.6f, Path.Combine(folder, $"{prefix}_kitchen_close.png"));
            Debug.Log($"[BossCapture] Saved to {folder}");
        }

        private static void Render(Camera camera, Vector3 lookAt, float height, string path)
        {
            var back = (height - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));
            camera.aspect = Width / (float)Height;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;
            for (var i = 0; i < 2; i++) // a throwaway first frame: shader variants not compiled yet come out black
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                    camera.Render();
            }
            camera.targetTexture = null;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
        }
    }
}
