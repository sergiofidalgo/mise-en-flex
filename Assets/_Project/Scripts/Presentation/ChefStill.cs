using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// A still of one chef's face for a player card, as the office's player bar takes them: a small
    /// camera in front of the chef renders only that chef, once, onto a backdrop, then stays off. Take
    /// it again when the chef joins, settles into their first pose, or changes their look.
    /// </summary>
    public sealed class ChefStill
    {
        // A layer nothing else uses: only the chef is moved onto it for the moment of the picture.
        private const int StillLayer = 31;
        // The face's framing, from the chef's feet (the office's compact card's).
        private const float FaceHeight = 1.38f;
        private const float FaceDistance = 1.05f;

        private readonly Camera _camera;
        private readonly List<Renderer> _parts = new List<Renderer>();
        private readonly List<(GameObject Part, int Layer)> _moved = new List<(GameObject, int)>();

        public ChefStill(Transform parent, string name)
        {
            Texture = new RenderTexture(360, 360, 24) { name = name, antiAliasing = 4 };
            _camera = new GameObject(name + "Camera").AddComponent<Camera>();
            _camera.transform.SetParent(parent, false);
            _camera.targetTexture = Texture;
            _camera.fieldOfView = 32f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 30f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.enabled = false;
        }

        /// <summary>The picture, square.</summary>
        public RenderTexture Texture { get; }

        /// <summary>Photographs <paramref name="chef"/>'s face, and only the chef, right now.</summary>
        public void Take(Transform chef, Color backdrop, int passes = 1)
        {
            var front = Vector3.ProjectOnPlane(chef.forward, Vector3.up).normalized;
            var target = chef.position + Vector3.up * FaceHeight;
            var from = target + front * FaceDistance + Vector3.up * 0.12f;
            _camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from, Vector3.up));
            _camera.cullingMask = 1 << StillLayer;
            _camera.backgroundColor = backdrop;
            _moved.Clear();
            chef.GetComponentsInChildren(true, _parts);
            foreach (var part in _parts)
            {
                _moved.Add((part.gameObject, part.gameObject.layer));
                part.gameObject.layer = StillLayer;
            }
            // Outside Play mode (a still for the art team) shaders may still be compiling on the first draw.
            for (var pass = 0; pass < passes; pass++)
            {
                var request = new RenderPipeline.StandardRequest { destination = Texture };
                if (RenderPipeline.SupportsRenderRequest(_camera, request))
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                else
                    _camera.Render();
            }
            foreach (var (part, layer) in _moved)
                if (part != null)
                    part.layer = layer;
            _moved.Clear();
        }

        public void Release()
        {
            if (_camera != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_camera.gameObject);
                else
                    Object.DestroyImmediate(_camera.gameObject);
            }
            Texture.Release();
        }
    }
}
