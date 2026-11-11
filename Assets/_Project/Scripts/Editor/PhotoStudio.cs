using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// A small photo studio for baking HUD pictures of 3D things (the customers' portraits, the
    /// shop's pieces): lights and a camera in a preview scene, and a render on a clear background.
    /// Shared so every baked picture is lit and matted the same way.
    /// </summary>
    internal static class PhotoStudio
    {
        public static void Light(Scene scene, string name, Quaternion rotation, float intensity, Color colour)
        {
            var light = new GameObject(name).AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional;
            light.transform.rotation = rotation;
            light.intensity = intensity;
            light.color = colour;
            light.shadows = LightShadows.None;
        }

        public static Camera Camera(Scene scene, float fieldOfView)
        {
            var camera = new GameObject("StudioCamera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            // As the game draws things: the ink outline and the grading come with post-processing,
            // which fills the background, so the clear background is recovered by Render below.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return camera;
        }

        /// <summary>A square target the pipeline can draw into (its MSAA must match the pipeline's, 4).</summary>
        public static RenderTexture Target(int size) =>
            new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };

        // Rendered on black and on white: where the two agree the subject covers the pixel, and how
        // far they differ (against how far the backgrounds differ) is how see-through it is. That
        // keeps the outline's soft edge without guessing a key colour.
        public static void Render(Camera camera, RenderTexture target, string path)
        {
            var size = target.width;
            var onBlack = Shoot(camera, target, Color.black);
            var onWhite = Shoot(camera, target, Color.white);
            // The backgrounds as they came out, after grading, from a corner.
            var black = onBlack[0];
            var white = onWhite[0];
            var spread = Mathf.Max(1f, (white.r - black.r + white.g - black.g + white.b - black.b) / 3f);
            var pixels = new Color32[onBlack.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var b = onBlack[i];
                var w = onWhite[i];
                var seen = ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f / spread;
                var alpha = Mathf.Clamp01(1f - seen);
                if (alpha < 0.01f)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }
                // The colour without the black background mixed in.
                byte Unmix(byte value, byte back) => (byte)Mathf.Clamp(Mathf.RoundToInt((value - back * (1f - alpha)) / alpha), 0, 255);
                pixels[i] = new Color32(Unmix(b.r, black.r), Unmix(b.g, black.g), Unmix(b.b, black.b), (byte)Mathf.RoundToInt(alpha * 255f));
            }
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        private static Color32[] Shoot(Camera camera, RenderTexture target, Color background)
        {
            var size = target.width;
            camera.backgroundColor = background;
            camera.targetTexture = target;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            var pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            return pixels;
        }
    }
}
