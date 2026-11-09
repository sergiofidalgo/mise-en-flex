using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Painted window glass for the night city (user's note, 2026-11-09: the windows lacked "the
    /// nice natural light texture"). Each lit window shows a room behind it: a lamp's warm glow
    /// falling off across it, curtains or blinds, a plant, a hanging lamp, or an office's cooler
    /// ceiling light. Dark windows hold the night sky's reflection. Plus a person's silhouette for
    /// the ones walking past (WindowSilhouette). Generated, so they can be tuned here as text.
    /// </summary>
    public static class WindowArt
    {
        private const string Folder = "Assets/_Project/Art/Textures/Windows";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const int Width = 48;
        private const int Height = 64;

        private static readonly Color Warm = new Color(1f, 0.82f, 0.5f);
        private static readonly Color Amber = new Color(1f, 0.62f, 0.3f);
        private static readonly Color CurtainRed = new Color(0.62f, 0.22f, 0.18f);
        private static readonly Color CurtainBlue = new Color(0.3f, 0.38f, 0.62f);
        private static readonly Color Office = new Color(0.82f, 0.92f, 1f);

        /// <summary>The lit windows' materials, one per room kind.</summary>
        public static Material[] LitWindows()
        {
            return new[]
            {
                Ensure("Lit_Curtains", (x, y) => Curtains(Room(x, y, 0.7f, 0.35f, Warm), x, CurtainRed, both: true)),
                Ensure("Lit_Blinds", (x, y) => Blinds(Room(x, y, 0.4f, 0.3f, Warm), y)),
                Ensure("Lit_Office", (x, y) => OfficeRoom(x, y)),
                Ensure("Lit_HangingLamp", (x, y) => HangingLamp(Room(x, y, 0.5f, 0.75f, Warm), x, y)),
                Ensure("Lit_Plant", (x, y) => Plant(Curtains(Room(x, y, 0.65f, 0.45f, Warm), x, CurtainBlue, both: false), x, y)),
                Ensure("Lit_Corner", (x, y) => Room(x, y, 0.85f, 0.2f, Amber) * 0.85f + Color.black * 0.15f),
            };
        }

        /// <summary>A dark window: the night sky's blue, with a pale streak of reflection.</summary>
        public static Material DarkWindow() => Ensure("Dark", (x, y) =>
        {
            var sky = Color.Lerp(new Color(0.08f, 0.1f, 0.2f), new Color(0.16f, 0.2f, 0.34f), y);
            var streak = Mathf.Clamp01(1f - Mathf.Abs((x + y * 0.6f) - 0.75f) * 9f) * 0.35f;
            return sky + new Color(streak, streak, streak * 1.2f, 0f);
        });

        /// <summary>A person seen against a lit room: a head over shoulders, cut out.</summary>
        public static Material Silhouette()
        {
            var material = Ensure("Silhouette", (x, y) =>
            {
                // A head above shoulders that widen into a body, in window space 0–1.
                var head = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.72f)) < 0.17f;
                var neck = Mathf.Abs(x - 0.5f) < 0.07f && y > 0.5f && y < 0.62f;
                var bodyHalf = Mathf.Lerp(0.42f, 0.3f, Mathf.InverseLerp(0.0f, 0.55f, y));
                var body = y < 0.55f && Mathf.Abs(x - 0.5f) < bodyHalf && y > 0f;
                var inside = head || neck || body;
                return new Color(0.28f, 0.17f, 0.12f, inside ? 1f : 0f);
            }, cutout: true);
            return material;
        }

        private static Texture2D Texture(string name, int width, int height, System.Func<float, float, Color> paint)
        {
            GreyboxAssets.EnsureFolder(Folder);
            var path = $"{Folder}/TEX_{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var isNew = texture == null;
            if (isNew)
                texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
                texture.SetPixel(px, py, paint((px + 0.5f) / width, (py + 0.5f) / height));
            texture.Apply();
            if (isNew)
                AssetDatabase.CreateAsset(texture, path);
            else
                EditorUtility.SetDirty(texture);
            return texture;
        }

        // ---- Painting --------------------------------------------------------------------------

        // A room lit by a lamp at (lx, ly): bright round the lamp, falling off to the corners,
        // and a little darker at the floor and ceiling.
        private static Color Room(float x, float y, float lx, float ly, Color light)
        {
            var d = Vector2.Distance(new Vector2(x, y * 1.3f), new Vector2(lx, ly * 1.3f));
            var glow = Mathf.Clamp01(1.15f - d * 1.4f);
            var edges = 1f - 0.25f * Mathf.Pow(Mathf.Abs(y - 0.5f) * 2f, 3f);
            var c = Color.Lerp(light * 0.45f, light, glow) * edges;
            c.a = 1f;
            return c;
        }

        // Drawn curtains with soft folds, at both sides or the right only.
        private static Color Curtains(Color room, float x, Color cloth, bool both)
        {
            var width = 0.2f;
            var inCurtain = x > 1f - width || (both && x < width);
            if (!inCurtain)
                return room;
            var fold = 0.75f + 0.25f * Mathf.Sin(x * 70f);
            var lit = Color.Lerp(cloth, room, 0.35f) * fold;
            lit.a = 1f;
            return lit;
        }

        // Venetian blinds pulled two-thirds of the way down: slats in the light.
        private static Color Blinds(Color room, float y)
        {
            if (y < 0.33f)
                return room;
            var slat = Mathf.Repeat(y * 22f, 1f) < 0.55f;
            var c = slat ? room * 0.78f : room * 1.02f;
            c.a = 1f;
            return c;
        }

        // An office: cool, evenly lit by ceiling panels, desks along the bottom.
        private static Color OfficeRoom(float x, float y)
        {
            var c = Office * (0.75f + 0.25f * y);
            if (y > 0.86f && Mathf.Repeat(x * 3f, 1f) > 0.15f)
                c = Color.white; // ceiling panels
            if (y < 0.22f && y > 0.12f)
                c = new Color(0.4f, 0.45f, 0.55f); // a desk's edge
            if (y < 0.4f && y > 0.22f && Mathf.Abs(Mathf.Repeat(x * 2f, 1f) - 0.5f) < 0.18f)
                c = new Color(0.3f, 0.5f, 0.75f); // monitors
            c.a = 1f;
            return c;
        }

        // A lamp hanging on its cord from the top of the window.
        private static Color HangingLamp(Color room, float x, float y)
        {
            if (Mathf.Abs(x - 0.5f) < 0.015f && y > 0.82f)
                return new Color(0.2f, 0.14f, 0.1f);
            var shade = y > 0.72f && y < 0.82f && Mathf.Abs(x - 0.5f) < (0.82f - y) * 1.4f + 0.04f;
            return shade ? new Color(1f, 0.95f, 0.8f) : room;
        }

        // A potted plant on the sill, in silhouette against the light.
        private static Color Plant(Color room, float x, float y)
        {
            var pot = y < 0.15f && Mathf.Abs(x - 0.28f) < 0.09f;
            var leaves = Vector2.Distance(new Vector2(x, y), new Vector2(0.28f, 0.25f)) < 0.14f ||
                         Vector2.Distance(new Vector2(x, y), new Vector2(0.2f, 0.33f)) < 0.09f ||
                         Vector2.Distance(new Vector2(x, y), new Vector2(0.36f, 0.32f)) < 0.08f;
            if (pot)
                return new Color(0.45f, 0.25f, 0.15f);
            return leaves ? new Color(0.18f, 0.3f, 0.15f) : room;
        }

        // ---- Assets ------------------------------------------------------------------------------

        // A window material (MAT_Unlit_Window_<name>) on its painted texture, made or repainted.
        private static Material Ensure(string name, System.Func<float, float, Color> paint, bool cutout = false)
        {
            GreyboxAssets.EnsureFolder(Folder);
            var texturePath = $"{Folder}/TEX_Window_{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var isNewTexture = texture == null;
            if (isNewTexture)
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var py = 0; py < Height; py++)
            for (var px = 0; px < Width; px++)
                texture.SetPixel(px, py, paint((px + 0.5f) / Width, (py + 0.5f) / Height));
            texture.Apply();
            if (isNewTexture)
                AssetDatabase.CreateAsset(texture, texturePath);
            else
                EditorUtility.SetDirty(texture);

            var materialPath = $"{MaterialsFolder}/MAT_Unlit_Window_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var isNewMaterial = material == null;
            if (isNewMaterial)
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            material.enableInstancing = true;
            if (isNewMaterial)
                AssetDatabase.CreateAsset(material, materialPath);
            else
                EditorUtility.SetDirty(material);
            return material;
        }
    }
}
