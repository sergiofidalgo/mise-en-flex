using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A 1920×1080 screenshot of a scene with an overlay canvas on top, for comparing screens with
    /// their mockups (capture tools and tests use it; the game doesn't). Batch mode can't grab the
    /// screen, and drawing the canvas through the scene camera would run the ink pass over the UI,
    /// so the scene and the UI are drawn apart and composited: the UI once over black and once over
    /// white, whose difference is how see-through each pixel is.
    /// Outside Play mode only: in Play mode the canvas comes out empty through a hand-driven camera
    /// (tried 2026-11-09), so Play mode captures grab the real frame in a visible editor instead.
    /// </summary>
    public static class UiCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;

        public static void Save(Camera sceneCamera, Canvas canvas, string file)
        {
            using var session = Prepare(canvas);
            session.Save(sceneCamera, file);
        }

        /// <summary>Puts the canvas in front of a bare 1920×1080 camera, until the session is disposed.</summary>
        public static Session Prepare(Canvas canvas) => new Session(canvas);

        public sealed class Session : IDisposable
        {
            private readonly Canvas _canvas;
            private readonly RenderMode _mode;
            private readonly int _layer;
            private readonly CanvasScaler _scaler;
            private readonly bool _scalerOn;
            private readonly float _scale;
            private readonly Camera _camera;
            private readonly RenderTexture _target;

            internal Session(Canvas canvas)
            {
                _canvas = canvas;
                _mode = canvas.renderMode;
                _layer = canvas.gameObject.layer;
                // The capture is 1920×1080, the layout's own size, so the UI is drawn at scale 1
                // (in batch mode the screen is 640×480, which the scaler would follow).
                _scaler = canvas.GetComponent<CanvasScaler>();
                _scalerOn = _scaler != null && _scaler.enabled;
                _scale = canvas.scaleFactor;
                if (_scaler != null)
                    _scaler.enabled = false;
                canvas.scaleFactor = 1f;

                _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                _camera = new GameObject("CaptureUiCamera").AddComponent<Camera>();
                var uiLayer = LayerMask.NameToLayer("UI");
                _camera.cullingMask = 1 << uiLayer;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = Color.black;
                _camera.nearClipPlane = 0.01f;
                _camera.farClipPlane = 10f;
                _camera.targetTexture = _target;
                var data = _camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                canvas.gameObject.layer = uiLayer;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _camera;
                canvas.planeDistance = 1f;
            }

            public void Save(Camera sceneCamera, string file)
            {
                _canvas.enabled = false;
                var scene = Render(sceneCamera, null);
                _canvas.enabled = true;
                _camera.backgroundColor = Color.black;
                var overBlack = Render(_camera, _target);
                _camera.backgroundColor = Color.white;
                var overWhite = Render(_camera, _target);

                var result = new Color32[scene.Length];
                for (var i = 0; i < scene.Length; i++)
                {
                    // Over black the UI gives colour × coverage; white shows through by (1 − coverage).
                    var see = (overWhite[i].r - overBlack[i].r + overWhite[i].g - overBlack[i].g + overWhite[i].b - overBlack[i].b) / (3f * 255f);
                    see = Mathf.Clamp01(see);
                    result[i] = new Color32(
                        (byte)Mathf.Clamp(overBlack[i].r + see * scene[i].r, 0f, 255f),
                        (byte)Mathf.Clamp(overBlack[i].g + see * scene[i].g, 0f, 255f),
                        (byte)Mathf.Clamp(overBlack[i].b + see * scene[i].b, 0f, 255f),
                        255);
                }
                var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                image.SetPixels32(result);
                image.Apply();
                System.IO.File.WriteAllBytes(file, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }

            public void Dispose()
            {
                _canvas.renderMode = _mode;
                _canvas.worldCamera = null;
                _canvas.gameObject.layer = _layer;
                _canvas.enabled = true;
                _canvas.scaleFactor = _scale;
                if (_scaler != null)
                    _scaler.enabled = _scalerOn;
                _camera.targetTexture = null;
                Object.DestroyImmediate(_camera.gameObject);
                _target.Release();
                Object.DestroyImmediate(_target);
            }
        }

        // Renders into target, or into a fresh 1920×1080 texture when it's null.
        private static Color32[] Render(Camera camera, RenderTexture target)
        {
            var own = target == null;
            if (own)
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render(); // a throwaway frame: shader variants not compiled yet come out black in it
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            camera.targetTexture = previous;
            var pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            if (own)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            return pixels;
        }
    }
}
