using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Graffiti for the street at the foot of the boss's tower (user's note, 2026-11-09: "start at a
    /// street, add clutter, movement, details, graffitis"). Bubble-letter tags with outlines, a
    /// highlight and drips, and murals in the game's colours. No real words or brands: tags are
    /// letter-shaped blobs, not spelled. Generated, so they can be tuned here as text. Materials are
    /// lit by the scene (Simple Lit), so street lamps and the moon fall on them like paint.
    /// </summary>
    public static class StreetArt
    {
        private const string Folder = "Assets/_Project/Art/Textures/Street";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";

        private static readonly Color[] Paint =
        {
            new Color(0.95f, 0.45f, 0.65f), new Color(0.35f, 0.8f, 0.85f), new Color(1f, 0.78f, 0.25f),
            new Color(0.55f, 0.85f, 0.4f), new Color(0.65f, 0.5f, 0.95f), new Color(0.98f, 0.55f, 0.25f),
        };
        private static readonly Color Ink = new Color(0.12f, 0.08f, 0.1f);

        /// <summary>A few tags, each its own colours and letters.</summary>
        public static Material[] Tags()
        {
            var tags = new Material[4];
            for (var i = 0; i < tags.Length; i++)
            {
                var seed = 31 + i * 17;
                var fill = Paint[i % Paint.Length];
                var outline = i % 2 == 0 ? Ink : Color.white;
                tags[i] = Ensure($"Tag{i}", 160, 64, (x, y) => Tag(x, y, seed, 3 + i % 3, fill, outline), cutout: true);
            }
            return tags;
        }

        /// <summary>Murals for side walls: a cowboy hat (the boss's neighbourhood), a sunset, a steak.</summary>
        public static Material[] Murals()
        {
            return new[]
            {
                Ensure("MuralHat", 128, 128, MuralHat, cutout: false),
                Ensure("MuralSunset", 128, 128, MuralSunset, cutout: false),
                Ensure("MuralSteak", 128, 128, MuralSteak, cutout: false),
            };
        }

        // ---- Painting -----------------------------------------------------------------------------

        // Bubble letters: each letter a few fat strokes (capsules) side by side, filled, outlined,
        // a shine along the top, drips under some.
        private static Color Tag(float x, float y, int seed, int letters, Color fill, Color outline)
        {
            var random = new System.Random(seed);
            var d = float.MaxValue;
            var width = 0.86f / letters;
            for (var l = 0; l < letters; l++)
            {
                var cx = 0.07f + width * (l + 0.5f);
                var tilt = ((float)random.NextDouble() - 0.5f) * 0.1f;
                var strokes = 2 + random.Next(2);
                for (var s = 0; s < strokes; s++)
                {
                    var ax = cx + ((float)random.NextDouble() - 0.5f) * width * 0.7f;
                    var ay = 0.25f + (float)random.NextDouble() * 0.15f;
                    var bx = cx + ((float)random.NextDouble() - 0.5f) * width * 0.7f + tilt;
                    var by = 0.7f + (float)random.NextDouble() * 0.12f;
                    d = Mathf.Min(d, Capsule(x, y * 0.4f, ax, ay * 0.4f, bx, by * 0.4f) - 0.06f * (width * 3f));
                }
            }
            var radius = 0f;
            var edge = 0.012f;
            var drips = Mathf.Repeat(x * 23f + seed, 1f) < 0.08f && y < 0.3f && y > 0.05f && Mathf.Repeat(x * 7f, 1f) < 0.5f;
            if (d < radius)
            {
                var c = fill;
                if (y > 0.62f) // the shine
                    c = Color.Lerp(fill, Color.white, 0.45f);
                c.a = 1f;
                return c;
            }
            if (d < radius + edge)
                return new Color(outline.r, outline.g, outline.b, 1f);
            if (drips)
                return new Color(fill.r * 0.9f, fill.g * 0.9f, fill.b * 0.9f, 1f);
            return Color.clear;
        }

        private static float Capsule(float px, float py, float ax, float ay, float bx, float by)
        {
            var pa = new Vector2(px - ax, py - ay);
            var ba = new Vector2(bx - ax, by - ay);
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(ba.sqrMagnitude, 1e-6f));
            return (pa - ba * h).magnitude;
        }

        private static Color MuralHat(float x, float y)
        {
            var c = Color.Lerp(new Color(0.15f, 0.42f, 0.45f), new Color(0.25f, 0.55f, 0.55f), y);
            if (Mathf.Repeat(x * 9f + y * 5f, 1f) < 0.03f && y > 0.7f)
                c = new Color(1f, 0.9f, 0.6f); // stars
            var brim = Mathf.Pow((x - 0.5f) / 0.42f, 2f) + Mathf.Pow((y - 0.4f) / 0.07f, 2f) < 1f;
            var crown = Mathf.Abs(x - 0.5f) < 0.2f - (y - 0.42f) * 0.15f && y > 0.4f && y < 0.66f;
            var dent = Mathf.Abs(x - 0.5f) < 0.05f && y > 0.6f;
            var band = crown && y < 0.47f;
            if (brim || (crown && !dent))
                c = band ? new Color(0.85f, 0.35f, 0.25f) : new Color(0.96f, 0.9f, 0.78f);
            return c;
        }

        private static Color MuralSunset(float x, float y)
        {
            var c = Color.Lerp(new Color(0.95f, 0.5f, 0.35f), new Color(0.55f, 0.3f, 0.6f), y);
            var sun = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.45f)) < 0.22f;
            if (sun && Mathf.Repeat(y * 14f, 1f) > 0.25f)
                c = new Color(1f, 0.85f, 0.35f);
            if (y < 0.3f)
                c = Mathf.Repeat(y * 12f + Mathf.Sin(x * 25f) * 0.4f, 1f) < 0.5f ? new Color(0.2f, 0.45f, 0.65f) : new Color(0.3f, 0.6f, 0.8f);
            return c;
        }

        private static Color MuralSteak(float x, float y)
        {
            var c = Mathf.Repeat(x * 6f + y * 6f, 1f) < 0.5f ? new Color(0.92f, 0.85f, 0.72f) : new Color(0.85f, 0.3f, 0.25f);
            var steak = Mathf.Pow((x - 0.5f) / 0.36f, 2f) + Mathf.Pow((y - 0.5f) / 0.24f, 2f) < 1f;
            var fat = Mathf.Pow((x - 0.5f) / 0.36f, 2f) + Mathf.Pow((y - 0.5f) / 0.24f, 2f) > 0.75f;
            var grill = Mathf.Repeat((x + y) * 8f, 1f) < 0.12f;
            if (steak)
                c = fat ? new Color(0.98f, 0.9f, 0.82f) : grill ? new Color(0.35f, 0.15f, 0.1f) : new Color(0.62f, 0.28f, 0.2f);
            return c;
        }

        // ---- Assets ------------------------------------------------------------------------------

        private static Material Ensure(string name, int width, int height, System.Func<float, float, Color> paint, bool cutout)
        {
            GreyboxAssets.EnsureFolder(Folder);
            var texturePath = $"{Folder}/TEX_Street_{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var isNewTexture = texture == null;
            if (isNewTexture)
                texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
                texture.SetPixel(px, py, paint((px + 0.5f) / width, (py + 0.5f) / height));
            texture.Apply();
            if (isNewTexture)
                AssetDatabase.CreateAsset(texture, texturePath);
            else
                EditorUtility.SetDirty(texture);

            var materialPath = $"{MaterialsFolder}/MAT_Lit_Street_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var isNewMaterial = material == null;
            if (isNewMaterial)
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            if (isNewMaterial)
                AssetDatabase.CreateAsset(material, materialPath);
            else
                EditorUtility.SetDirty(material);
            return material;
        }
    }
}
