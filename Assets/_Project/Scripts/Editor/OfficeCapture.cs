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
            ("office_reception", new Vector2(0f, -4.5f), 14f),
            ("office_boss", new Vector2(0f, 4.5f), 14f),
            ("office_west", new Vector2(-8f, 0f), 18f),
            ("office_east", new Vector2(8f, 0f), 18f),
            ("office_terrace", new Vector2(4f, 10f), 16f),
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
            Debug.Log($"[OfficeCapture] Saved to {folder}");
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
