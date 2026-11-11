using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>The grade a timed press earned, as the gauges show it (with a shape as well as a colour).</summary>
    public enum PressGrade
    {
        Good,
        Off,
        Bad,
    }

    /// <summary>
    /// Shared pieces for the timing gauges, the flip's crust strip and the cutting dial (decision
    /// 2026-11-11): chunky toon blocks in the scene, drawn over whatever stands in front
    /// (<c>Game/ToonOnTop</c>, as the wash and cleaning rings), with an Ink outline, the Xbox X button
    /// on the window to press in, and grades shown as a shape and a colour (✓, –, ✕) so they read
    /// without colour too. The gauges face the camera, so they read the same at every rotation.
    /// </summary>
    public static class TimingGaugeKit
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;
        private static Mesh _cube;
        private static Mesh _cylinder;

        // Art bible palette.
        public static readonly Color Ink = new Color32(0x3A, 0x24, 0x14, 0xFF);
        public static readonly Color WoodDark = new Color32(0x5C, 0x3A, 0x21, 0xFF);
        public static readonly Color Cream = new Color32(0xEA, 0xDF, 0xC6, 0xFF);
        public static readonly Color Good = new Color32(0x6A, 0xAE, 0x4A, 0xFF);
        public static readonly Color Warn = new Color32(0xE0, 0xB3, 0x41, 0xFF);
        public static readonly Color Bad = new Color32(0xD9, 0x49, 0x3B, 0xFF);
        public static readonly Color XboxX = new Color32(0x3B, 0x72, 0xD9, 0xFF);

        public static Color GradeColour(PressGrade grade) => grade == PressGrade.Good ? Good : grade == PressGrade.Off ? Warn : Bad;

        /// <summary>A unit cube (1 m a side, centred) as a toon piece.</summary>
        public static Renderer Block(Transform parent, string name, Material material, Color color, int order)
        {
            _cube ??= BuiltIn(PrimitiveType.Cube);
            return Piece(parent, name, _cube, material, color, order);
        }

        /// <summary>A disc: a unit cylinder turned to face along the parent's -Z (the camera).</summary>
        public static Renderer Disc(Transform parent, string name, Material material, Color color, int order)
        {
            _cylinder ??= BuiltIn(PrimitiveType.Cylinder);
            var disc = Piece(parent, name, _cylinder, material, color, order);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return disc;
        }

        public static Renderer Piece(Transform parent, string name, Mesh mesh, Material material, Color color, int order)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Paint(renderer, color);
            return renderer;
        }

        public static void Paint(Renderer renderer, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>A block sized and placed in its parent's units (x right, y up, z depth towards the back).</summary>
        public static void Place(Renderer block, Vector2 centre, Vector2 size, float depth, float z = 0f)
        {
            block.transform.localPosition = new Vector3(centre.x, centre.y, z);
            block.transform.localScale = new Vector3(Mathf.Max(0.0001f, size.x), Mathf.Max(0.0001f, size.y), depth);
        }

        /// <summary>
        /// The Xbox X button: a blue disc with an Ink rim and a white cross. Built centred on its root,
        /// one unit across.
        /// </summary>
        public static Transform XButton(Transform parent, Material material, int order)
        {
            var root = new GameObject("XButton") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(parent, false);
            var rim = Disc(root, "Rim", material, Ink, order);
            rim.transform.localScale = new Vector3(1f, 0.05f, 1f);
            var face = Disc(root, "Face", material, XboxX, order + 1);
            face.transform.localScale = new Vector3(0.82f, 0.05f, 0.82f);
            face.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            for (var i = 0; i < 2; i++)
            {
                var bar = Block(root, "Cross", material, Color.white, order + 2);
                bar.transform.localPosition = new Vector3(0f, 0f, -0.04f);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                bar.transform.localScale = new Vector3(0.5f, 0.12f, 0.02f);
            }
            return root;
        }

        /// <summary>
        /// A grade mark one unit across: ✓ (good), – (off) or ✕ (bad), white on a disc of the grade's
        /// colour with an Ink rim. Rebuilds its strokes when the grade changes.
        /// </summary>
        public static Transform Mark(Transform parent, Material material, int order)
        {
            var root = new GameObject("Mark") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(parent, false);
            var rim = Disc(root, "Rim", material, Ink, order);
            rim.transform.localScale = new Vector3(1f, 0.05f, 1f);
            var face = Disc(root, "Face", material, Good, order + 1);
            face.transform.localScale = new Vector3(0.84f, 0.05f, 0.84f);
            face.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            for (var i = 0; i < 2; i++)
                Block(root, "Stroke", material, Color.white, order + 2).transform.localPosition = new Vector3(0f, 0f, -0.04f);
            return root;
        }

        public static void SetMark(Transform mark, PressGrade grade)
        {
            Paint(mark.GetChild(1).GetComponent<Renderer>(), GradeColour(grade));
            var a = mark.GetChild(2);
            var b = mark.GetChild(3);
            switch (grade)
            {
                case PressGrade.Good:
                    // A tick: a short stroke down-right into a long one up-right.
                    a.localPosition = new Vector3(-0.12f, -0.04f, -0.04f);
                    a.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    a.localScale = new Vector3(0.26f, 0.11f, 0.02f);
                    b.localPosition = new Vector3(0.08f, 0.04f, -0.04f);
                    b.localRotation = Quaternion.Euler(0f, 0f, 50f);
                    b.localScale = new Vector3(0.46f, 0.11f, 0.02f);
                    b.gameObject.SetActive(true);
                    break;
                case PressGrade.Off:
                    a.localPosition = new Vector3(0f, 0f, -0.04f);
                    a.localRotation = Quaternion.identity;
                    a.localScale = new Vector3(0.48f, 0.12f, 0.02f);
                    b.gameObject.SetActive(false);
                    break;
                default:
                    a.localPosition = b.localPosition = new Vector3(0f, 0f, -0.04f);
                    a.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    b.localRotation = Quaternion.Euler(0f, 0f, -45f);
                    a.localScale = b.localScale = new Vector3(0.5f, 0.12f, 0.02f);
                    b.gameObject.SetActive(true);
                    break;
            }
        }

        /// <summary>
        /// Metres that look <paramref name="pixels"/> tall on a 1080-pixel-high screen at a world
        /// point: gauges are sized on screen, so they read the same in every kitchen and camera.
        /// </summary>
        public static float MetresFor(Camera camera, Vector3 world, float pixels)
        {
            if (camera == null)
                return pixels / 100f;
            var depth = Mathf.Max(0.1f, Vector3.Dot(world - camera.transform.position, camera.transform.forward));
            var screenHeight = camera.orthographic ? camera.orthographicSize * 2f : 2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return pixels / 1080f * screenHeight;
        }

        /// <summary>
        /// Puts a gauge's root at a world point, facing the camera and kept inside the screen (the HUD
        /// is never clipped), at <paramref name="size"/> metres per unit.
        /// </summary>
        public static void FaceCamera(Transform root, Camera camera, Vector3 world, float size, float marginViewport = 0.06f)
        {
            if (camera != null)
            {
                var viewport = camera.WorldToViewportPoint(world);
                if (viewport.z > 0f)
                {
                    viewport.x = Mathf.Clamp(viewport.x, marginViewport, 1f - marginViewport);
                    viewport.y = Mathf.Clamp(viewport.y, marginViewport, 1f - marginViewport);
                    world = camera.ViewportToWorldPoint(viewport);
                }
                root.rotation = camera.transform.rotation;
            }
            root.position = world;
            root.localScale = Vector3.one * size;
        }

        /// <summary>
        /// A screen-plane offset of <paramref name="distance"/> metres pointing from the chef across
        /// the station (away from them), so the gauge is never under the chef's body; straight up the
        /// screen when there's no chef near.
        /// </summary>
        public static Vector3 AwayFromChef(Vector3 station, Vector3? chef, Camera camera, float distance)
        {
            var up = camera.transform.up;
            var right = camera.transform.right;
            var screen = new Vector2(0f, 1f);
            if (chef.HasValue)
            {
                var across = station - chef.Value;
                across.y = 0f;
                var flatUp = Vector3.ProjectOnPlane(up, Vector3.up).normalized;
                var flat = new Vector2(Vector3.Dot(across, right), Vector3.Dot(across, flatUp));
                if (flat.sqrMagnitude > 0.01f)
                    screen = flat.normalized;
            }
            return (right * screen.x + up * screen.y) * distance;
        }

        /// <summary>The nearest chef within <paramref name="reach"/> metres of a point, or null.</summary>
        public static Vector3? NearestChef(Vector3 at, float reach)
        {
            Vector3? best = null;
            var bestDistance = reach * reach;
            foreach (var player in UnityEngine.InputSystem.PlayerInput.all)
            {
                var delta = player.transform.position - at;
                delta.y = 0f;
                if (delta.sqrMagnitude >= bestDistance)
                    continue;
                bestDistance = delta.sqrMagnitude;
                best = player.transform.position;
            }
            return best;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.70158f;
            var u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        private static Mesh BuiltIn(PrimitiveType type)
        {
            var probe = GameObject.CreatePrimitive(type);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying)
                Object.Destroy(probe);
            else
                Object.DestroyImmediate(probe);
            return mesh;
        }
    }
}
