using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Renders the city miniature's lots (Phase 6, milestone 2) to a PNG at 1920 × 1080, from the
    /// game's tilt: once as play frames the back room with chefs round the table, once close up.
    /// The grill shows as owned and the third lot as highlighted, so all three markers show.
    /// Menu: Tools/Game/Capture Miniature, or -executeMethod Game.Editor.MiniatureCapture.CaptureFromCommandLine -captureFolder &lt;folder&gt;.
    /// </summary>
    public static class MiniatureCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        public static void CaptureFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureFolder");
            Capture(index >= 0 && index + 1 < args.Length ? args[index + 1] : "Screenshots/Office");
        }

        [MenuItem("Tools/Game/Capture Miniature")]
        public static void CaptureToScreenshots() => Capture("Screenshots/Office");

        private static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(OfficeBuilder.ScenePath, OpenSceneMode.Single);
            var lots = GameObject.Find("Rooms/BackRoom/MiniatureCity/Lots");
            if (lots == null)
                throw new InvalidOperationException("No lots on the miniature: run Tools/Game/Build Office.");
            for (var i = 0; i < lots.transform.childCount; i++)
            {
                var pin = lots.transform.GetChild(i);
                Show(pin, "Owned", i == 0);
                Show(pin, "ForSale", i != 0);
                Show(pin, "Highlight", i == 2);
            }
            var camera = Camera.main;
            camera.aspect = Width / (float)Height;
            var table = new Vector3(13.4f, 0.9f, 9.0f);
            var rotation = Quaternion.Euler(55f, 0f, 0f);
            Render(camera, table + rotation * new Vector3(0f, 0f, -14f), rotation, Path.Combine(folder, "miniature_room.png"));
            Render(camera, table + rotation * new Vector3(0f, 0f, -5.5f), rotation, Path.Combine(folder, "miniature_close.png"));
            Debug.Log($"[MiniatureCapture] Saved to {folder}");
        }

        private static void Show(Transform pin, string part, bool on)
        {
            var child = pin.Find(part);
            if (child != null)
                child.gameObject.SetActive(on);
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
