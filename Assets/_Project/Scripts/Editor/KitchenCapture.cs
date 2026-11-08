using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Renders the built kitchen from the game camera's angle: the whole kitchen, and close views of
    /// chosen stations, to check art where it actually stands without playing. Outside Play mode,
    /// every plate and sample a station can show is visible, so piles show full.
    /// Batch mode: -executeMethod Game.Editor.KitchenCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;
    /// </summary>
    public static class KitchenCapture
    {
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const int Width = 1920;
        private const int Height = 1080;
        private const float Pitch = 55f;            // the game camera's
        private const float FieldOfView = 45f;
        private const float CloseHeight = 2.6f;
        private const float PlayHeight = 6.5f;      // the closest the game camera comes
        private const float WholeKitchenHeight = 11f;

        // Stations to look at closely, by name in the scene.
        private static readonly string[] CloseUps = { "Sink", "PlateStack", "WallCounter_01", "BoardCounter", "CuttingBoard" };

        [MenuItem("Tools/Game/Art/Capture Kitchen")]
        private static void CaptureFromMenu()
        {
            var folder = EditorUtility.SaveFolderPanel("Save kitchen captures", "", "");
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
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.fieldOfView = FieldOfView;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            Render(camera, target, new Vector3(0f, 0.9f, -1.5f), WholeKitchenHeight, Path.Combine(folder, "kitchen_whole.png"));
            Render(camera, target, new Vector3(0f, 0.9f, 0.3f), PlayHeight, Path.Combine(folder, "kitchen_play.png"));
            foreach (var name in CloseUps)
            {
                var station = GameObject.Find(name);
                if (station == null)
                    continue;
                Render(camera, target, station.transform.position + Vector3.up * 0.9f, CloseHeight,
                    Path.Combine(folder, $"close_{name}.png"));
            }

            target.Release();
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Debug.Log($"[KitchenCapture] Saved to {folder}");
        }

        // Pitched like the game camera, looking at a point from the given height.
        private static void Render(Camera camera, RenderTexture target, Vector3 lookAt, float height, string path)
        {
            var back = (height - lookAt.y) / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            camera.transform.SetPositionAndRotation(new Vector3(lookAt.x, height, lookAt.z - back), Quaternion.Euler(Pitch, 0f, 0f));

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
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
