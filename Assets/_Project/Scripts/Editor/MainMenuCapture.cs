using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Renders the main menu's night city through its own camera to a PNG, for checking the backdrop
    /// without the menu UI. Run with -executeMethod Game.Editor.MainMenuCapture.CaptureFromCommandLine
    /// -captureFolder &lt;folder&gt;.
    /// </summary>
    public static class MainMenuCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

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
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render(); // a throwaway frame: shader variants not compiled yet come out black in it
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, "menu_city.png"), image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            Debug.Log($"[MainMenuCapture] Saved to {folder}");
        }
    }
}
