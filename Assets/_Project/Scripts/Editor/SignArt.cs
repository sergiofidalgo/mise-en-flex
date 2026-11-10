using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Signs for the avenue up to the boss's tower (user's note, 2026-11-09: "logos, food market
    /// stalls, 'We are open' or bars neon lights"). Two kinds, generated as textures so they can be
    /// tuned here as text:
    /// - **Neon words**: bent-tube letters with a soft halo, added to whatever is behind them
    ///   (additive), so they glow over a dark backing. Horizontal, or stacked down a blade sign.
    /// - **Light boxes**: a lit sign with an icon (a steaming bowl, a cocktail, a slice, a fish,
    ///   a burger, a coffee cup) and a word under it.
    /// Only plain words (OPEN, BAR, NOODLES...) and made-up names: never a real brand.
    /// </summary>
    public static class SignArt
    {
        private const string Folder = "Assets/_Project/Art/Textures/Signs";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const int Cell = 8; // pixels per font cell

        public enum Icon { Bowl, Cocktail, Slice, Fish, Burger, Cup }

        /// <summary>A neon word, glowing in <paramref name="colour"/>; <paramref name="vertical"/> stacks the letters.</summary>
        public static Material Neon(string word, Color colour, bool vertical = false)
        {
            var key = $"Neon_{Clean(word)}_{ColorUtility.ToHtmlStringRGB(colour)}{(vertical ? "_V" : "")}";
            var segments = Layout(word, vertical, out var cols, out var rows);
            var width = (cols + 2) * Cell;
            var height = (rows + 2) * Cell;
            var texture = Texture(key, width, height, (px, py) =>
            {
                // Distance in cells from the nearest tube, with the origin one cell in (the margin).
                var p = new Vector2(px / (float)Cell - 1f, rows - (py / (float)Cell - 1f));
                var d = float.MaxValue;
                foreach (var (a, b) in segments)
                    d = Mathf.Min(d, Segment(p, a, b));
                // A white-hot core, the colour around it, then a halo fading out.
                var core = Mathf.Clamp01((0.22f - d) / 0.08f);
                var tube = Mathf.Clamp01((0.42f - d) / 0.12f);
                var halo = Mathf.Exp(-Mathf.Max(0f, d - 0.3f) * 2.4f) * 0.55f;
                var rgb = Color.Lerp(colour, Color.white, core * 0.7f);
                var alpha = Mathf.Clamp01(Mathf.Max(tube, halo));
                return new Color(rgb.r, rgb.g, rgb.b, alpha);
            });
            return Additive(key, texture);
        }

        /// <summary>
        /// A neon outline of <paramref name="icon"/>: a tube bent round its edge with a halo, for
        /// shop walls (a cocktail over a bar, a cup over a café).
        /// </summary>
        public static Material NeonIcon(Icon icon, Color colour)
        {
            var key = $"NeonIcon_{icon}_{ColorUtility.ToHtmlStringRGB(colour)}";
            const int size = 96;
            // The icon's edge: filled pixels with an empty neighbour.
            var filled = new bool[size, size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                filled[x, y] = IconCovers(icon, new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f) * 1.25f);
            var edges = new List<Vector2Int>();
            for (var y = 1; y < size - 1; y++)
            for (var x = 1; x < size - 1; x++)
                if (filled[x, y] && (!filled[x - 1, y] || !filled[x + 1, y] || !filled[x, y - 1] || !filled[x, y + 1]))
                    edges.Add(new Vector2Int(x, y));
            var texture = Texture(key, size, size, (px, py) =>
            {
                var d = float.MaxValue;
                foreach (var e in edges)
                {
                    var dx = e.x - px;
                    var dy = e.y - py;
                    if (dx * dx + dy * dy < d)
                        d = dx * dx + dy * dy;
                }
                d = Mathf.Sqrt(d) / 3.2f; // in tube radii, as the letters
                var core = Mathf.Clamp01((0.22f - d * 0.32f) / 0.08f);
                var tube = Mathf.Clamp01((1.3f - d) / 0.4f);
                var halo = Mathf.Exp(-Mathf.Max(0f, d - 1f) * 0.35f) * 0.5f;
                var rgb = Color.Lerp(colour, Color.white, core * 0.7f);
                return new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(Mathf.Max(tube, halo)));
            });
            return Additive(key, texture);
        }

        /// <summary>The social-media shapes over the opening's phones and signs.</summary>
        public enum Social { Heart, Star, Comment }

        private static readonly Color SocialInk = new Color(0.16f, 0.09f, 0.12f);

        /// <summary>
        /// A bold social icon with a dark outline and a shine, big enough to read across the street
        /// (the user's note, 2026-11-10: the first badges barely stood out): a pink heart, a gold
        /// star, a blue comment bubble.
        /// </summary>
        public static Material SocialIcon(Social kind, bool particle = false)
        {
            var key = $"Social_{kind}";
            const int size = 128;
            var fill = kind == Social.Heart ? new Color(1f, 0.3f, 0.5f) : kind == Social.Star ? new Color(1f, 0.78f, 0.2f) : new Color(0.3f, 0.62f, 1f);
            bool Inside(Vector2 p, float grow)
            {
                p /= grow;
                switch (kind)
                {
                    case Social.Heart:
                        // The classic heart curve: (x² + y² − 1)³ − x²y³ ≤ 0.
                        var x = p.x * 2.9f;
                        var y = p.y * 2.9f + 0.25f;
                        var q = x * x + y * y - 1f;
                        return q * q * q - x * x * y * y * y <= 0f;
                    case Social.Star:
                        var angle = Mathf.Atan2(p.y, p.x) - Mathf.PI / 2f;
                        var wave = Mathf.Abs(Mathf.Repeat(angle / (2f * Mathf.PI / 5f), 1f) - 0.5f) * 2f; // 1 at a point, 0 between
                        return p.magnitude < Mathf.Lerp(0.18f, 0.42f, wave * wave);
                    default:
                        var bubble = Mathf.Abs(p.x) < 0.36f && Mathf.Abs(p.y - 0.06f) < 0.24f
                            && (Mathf.Abs(p.x) < 0.26f || Mathf.Abs(p.y - 0.06f) < 0.14f || new Vector2(Mathf.Abs(p.x) - 0.26f, Mathf.Abs(p.y - 0.06f) - 0.14f).magnitude < 0.1f);
                        var tail = p.y < -0.12f && p.y > -0.38f && p.x > -0.24f && p.x < -0.24f + (p.y + 0.38f) * 0.9f;
                        return bubble || tail;
                }
            }
            var texture = Texture(key, size, size, (px, py) =>
            {
                var p = new Vector2((px + 0.5f) / size - 0.5f, (py + 0.5f) / size - 0.5f);
                if (!Inside(p, 1f))
                    return new Color(SocialInk.r, SocialInk.g, SocialInk.b, 0f);
                if (!Inside(p, 0.8f))
                    return SocialInk; // the outline
                if (kind == Social.Comment)
                {
                    for (var d = -1; d <= 1; d++)
                        if ((p - new Vector2(d * 0.13f, 0.05f)).magnitude < 0.045f)
                            return Color.white;
                }
                // A shine up on the left.
                var shine = Mathf.Clamp01(1f - (p - new Vector2(-0.11f, 0.13f)).magnitude / 0.09f);
                return Color.Lerp(fill, Color.white, shine * 0.75f);
            });
            return Transparent(particle ? key + "_Particle" : key, texture, particle);
        }

        /// <summary>
        /// A camera flash's flare: a bright core, a soft bloom and four rays, added to what's behind it,
        /// so it's plain where a flash went off (the user's note, 2026-11-10).
        /// </summary>
        public static Material Flare()
        {
            const string key = "Flare";
            const int size = 96;
            var texture = Texture(key, size, size, (px, py) =>
            {
                var p = new Vector2((px + 0.5f) / size - 0.5f, (py + 0.5f) / size - 0.5f) * 2f;
                var r = p.magnitude;
                var core = Mathf.Exp(-r * r * 60f);
                var bloom = Mathf.Exp(-r * 5f) * 0.55f;
                // Rays along the axes, thin and fading out.
                var rays = (Mathf.Exp(-Mathf.Abs(p.y) * 45f) + Mathf.Exp(-Mathf.Abs(p.x) * 45f)) * Mathf.Clamp01(1f - r) * 0.8f;
                var a = Mathf.Clamp01(core + bloom + rays) * Mathf.Clamp01((1f - r) * 4f);
                return new Color(1f, 0.98f, 0.92f, a);
            });
            return Additive(key, texture);
        }

        /// <summary>A capsule in <paramref name="colour"/> with a dark outline: the back of a like or rating pill (3 : 1).</summary>
        public static Material Pill(Color colour)
        {
            var key = $"Pill_{ColorUtility.ToHtmlStringRGB(colour)}";
            const int width = 192, height = 64;
            var texture = Texture(key, width, height, (px, py) =>
            {
                var r = height / 2f;
                var cx = Mathf.Clamp(px + 0.5f, r, width - r);
                var d = new Vector2(px + 0.5f - cx, py + 0.5f - r).magnitude;
                if (d > r)
                    return new Color(SocialInk.r, SocialInk.g, SocialInk.b, 0f);
                return d > r - 6f ? SocialInk : colour;
            });
            return Transparent(key, texture, false);
        }

        /// <summary>A white speech card with a dark outline and a tail at the bottom left: a review bubble (2 : 1).</summary>
        public static Material ReviewCard()
        {
            const string key = "ReviewCard";
            const int width = 256, height = 128;
            var paper = new Color(1f, 0.98f, 0.94f);
            bool Inside(float x, float y, float inset)
            {
                // The card: a rounded rectangle over the top 100 px; the tail: a wedge under its left.
                const float radius = 22f;
                var left = 4f + inset;
                var right = width - 4f - inset;
                var bottom = 28f + inset;
                var top = height - 4f - inset;
                var cx = Mathf.Clamp(x, left + radius, right - radius);
                var cy = Mathf.Clamp(y, bottom + radius, top - radius);
                var card = x > left && x < right && y > bottom && y < top && new Vector2(x - cx, y - cy).magnitude < radius;
                var tail = y <= bottom + 8f && y > 2f + inset * 1.6f && x > 40f + inset && x < 40f + inset + (y - 2f - inset * 1.6f) * 1.1f;
                return card || tail;
            }
            var texture = Texture(key, width, height, (px, py) =>
            {
                var x = px + 0.5f;
                var y = py + 0.5f;
                if (!Inside(x, y, 0f))
                    return new Color(SocialInk.r, SocialInk.g, SocialInk.b, 0f);
                return Inside(x, y, 6f) ? paper : SocialInk;
            });
            return Transparent(key, texture, false);
        }

        /// <summary>A disc in <paramref name="colour"/>, for an avatar.</summary>
        public static Material Disc(Color colour)
        {
            var key = $"Disc_{ColorUtility.ToHtmlStringRGB(colour)}";
            const int size = 48;
            var texture = Texture(key, size, size, (px, py) =>
            {
                var d = new Vector2(px + 0.5f - size / 2f, py + 0.5f - size / 2f).magnitude;
                return new Color(colour.r, colour.g, colour.b, Mathf.Clamp01(size / 2f - 1f - d));
            });
            return Transparent(key, texture, false);
        }

        // Unlit and alpha-blended, over whatever is behind; `particle` uses the particle shader, so
        // a particle system's fade (its vertex colour) applies.
        private static Material Transparent(string name, Texture2D texture, bool particle)
        {
            var path = $"{MaterialsFolder}/MAT_Sign_{name}.mat";
            var shader = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f); // alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A light box: <paramref name="icon"/> in cream on <paramref name="colour"/>, the word below it.</summary>
        public static Material LightBox(Icon icon, string word, Color colour)
        {
            var key = $"Box_{icon}_{Clean(word)}_{ColorUtility.ToHtmlStringRGB(colour)}";
            const int width = 128, height = 160;
            var cream = new Color(1f, 0.95f, 0.85f);
            var ink = Color.Lerp(colour, Color.black, 0.55f);
            var segments = Layout(word, false, out var cols, out var rows);
            var textScale = Mathf.Min(1.4f, 104f / (cols * Cell)); // the word fits the box
            var texture = Texture(key, width, height, (px, py) =>
            {
                var x = px / (float)width;
                var y = py / (float)height;
                // A rounded frame in the darker tone, the lit panel inside.
                var edge = Mathf.Min(Mathf.Min(x, 1f - x) * width, Mathf.Min(y, 1f - y) * height);
                if (edge < 5f)
                    return ink;
                // The word along the bottom, in the font's strokes.
                var tx = (px - (width - cols * Cell * textScale) / 2f) / (Cell * textScale);
                var ty = rows - (py - 14f) / (Cell * textScale);
                if (ty > -0.6f && ty < rows + 0.6f)
                {
                    var d = float.MaxValue;
                    foreach (var (a, b) in segments)
                        d = Mathf.Min(d, Segment(new Vector2(tx, ty), a, b));
                    if (d < 0.42f)
                        return ink;
                }
                // The icon in the upper part, centred at (0.5, 0.62), in units of the box width.
                var p = new Vector2(x - 0.5f, (y - 0.62f) * height / width);
                return IconCovers(icon, p) ? cream : colour;
            });
            return Opaque(key, texture);
        }

        // ---- Icons ---------------------------------------------------------------------------------

        private static bool IconCovers(Icon icon, Vector2 p)
        {
            switch (icon)
            {
                case Icon.Bowl:
                    // A bowl (the lower half of a disc) with three wisps of steam over it.
                    if (p.y < -0.02f && p.y > -0.24f && p.magnitude < 0.3f)
                        return true;
                    for (var i = -1; i <= 1; i++)
                    {
                        var sx = i * 0.1f + Mathf.Sin(p.y * 28f) * 0.025f;
                        if (p.y > 0.02f && p.y < 0.3f && Mathf.Abs(p.x - sx) < 0.022f)
                            return true;
                    }
                    return false;
                case Icon.Cocktail:
                    // A martini glass: a triangle on a stem, with an olive.
                    if (p.y > 0.02f && p.y < 0.28f && Mathf.Abs(p.x) < (p.y - 0.02f) * 0.95f)
                        return true;
                    if (Mathf.Abs(p.x) < 0.018f && p.y > -0.2f && p.y <= 0.02f)
                        return true;
                    if (Mathf.Abs(p.x) < 0.12f && p.y > -0.24f && p.y < -0.2f)
                        return true;
                    return (p - new Vector2(0.09f, 0.3f)).magnitude < 0.045f;
                case Icon.Slice:
                    // A slice of pizza, point down, a crust along the top and three toppings cut out.
                    var inSlice = p.y > -0.26f && p.y < 0.2f && Mathf.Abs(p.x) < (p.y + 0.26f) * 0.55f;
                    var crust = p.y >= 0.2f && p.y < 0.27f && Mathf.Abs(p.x) < 0.27f;
                    var topping = (p - new Vector2(-0.06f, 0.08f)).magnitude < 0.04f || (p - new Vector2(0.07f, 0.02f)).magnitude < 0.035f
                        || (p - new Vector2(0f, -0.1f)).magnitude < 0.03f;
                    return crust || (inSlice && !topping);
                case Icon.Fish:
                    // A fish: an oval body, a tail, an eye cut out.
                    var body = (p.x / 0.24f) * (p.x / 0.24f) + (p.y / 0.12f) * (p.y / 0.12f) < 1f;
                    var tail = p.x > 0.2f && p.x < 0.34f && Mathf.Abs(p.y) < (p.x - 0.2f) * 1.1f;
                    var eye = (p - new Vector2(-0.13f, 0.03f)).magnitude < 0.025f;
                    return (body || tail) && !eye;
                case Icon.Burger:
                    // A burger: a domed bun, a wavy patty and the base.
                    var bun = p.y > 0.04f && p.y < 0.22f && (p.x * p.x) / (0.27f * 0.27f) + ((p.y - 0.04f) * (p.y - 0.04f)) / (0.18f * 0.18f) < 1f;
                    var patty = Mathf.Abs(p.x) < 0.27f && p.y < 0.0f + Mathf.Sin(p.x * 40f) * 0.012f && p.y > -0.07f;
                    var bottomBun = Mathf.Abs(p.x) < 0.25f && p.y < -0.1f && p.y > -0.18f;
                    return bun || patty || bottomBun;
                default:
                    // A coffee cup on a saucer, with a handle and steam.
                    var cup = p.y > -0.16f && p.y < 0.1f && Mathf.Abs(p.x) < 0.15f - (0.1f - p.y) * 0.12f;
                    var handle = Mathf.Abs((p - new Vector2(0.17f, -0.02f)).magnitude - 0.06f) < 0.018f && p.x > 0.15f;
                    var saucer = Mathf.Abs(p.x) < 0.26f && p.y > -0.21f && p.y < -0.17f;
                    var steam = p.y > 0.14f && p.y < 0.3f && Mathf.Abs(p.x - Mathf.Sin(p.y * 30f) * 0.03f) < 0.02f;
                    return cup || handle || saucer || steam;
            }
        }

        // ---- The font ----------------------------------------------------------------------------

        // 5 × 7 letters, drawn as tubes between neighbouring filled cells, as a neon bender would.
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".####", "#....", "#....", "#..##", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['\''] = new[] { "..#..", "..#..", ".....", ".....", ".....", ".....", "....." },
            ['&'] = new[] { ".##..", "#..#.", ".##..", ".#...", "#.#.#", "#..#.", ".##.#" },
        };

        // The tubes of a word: segments between neighbouring filled cells, in cell units (x right,
        // y down), each letter 6 cells apart (or 9 rows apart, stacked).
        private static List<(Vector2, Vector2)> Layout(string word, bool vertical, out int cols, out int rows)
        {
            var segments = new List<(Vector2, Vector2)>();
            var letters = word.ToUpperInvariant();
            for (var i = 0; i < letters.Length; i++)
            {
                if (!Glyphs.TryGetValue(letters[i], out var glyph))
                    continue; // a space, or a letter the font hasn't got
                var origin = vertical ? new Vector2(0f, i * 9f) : new Vector2(i * 6f, 0f);
                bool On(int c, int r) => r >= 0 && r < 7 && c >= 0 && c < 5 && glyph[r][c] == '#';
                for (var r = 0; r < 7; r++)
                for (var c = 0; c < 5; c++)
                {
                    if (!On(c, r))
                        continue;
                    var a = origin + new Vector2(c + 0.5f, r + 0.5f);
                    segments.Add((a, a)); // a lone cell is still a dot of tube
                    if (On(c + 1, r)) segments.Add((a, a + Vector2.right));
                    if (On(c, r + 1)) segments.Add((a, a + Vector2.up));
                    // Diagonals only where the corner isn't already turned by straight tubes.
                    if (On(c + 1, r + 1) && !On(c + 1, r) && !On(c, r + 1)) segments.Add((a, a + Vector2.one));
                    if (On(c - 1, r + 1) && !On(c - 1, r) && !On(c, r + 1)) segments.Add((a, a + new Vector2(-1f, 1f)));
                }
            }
            cols = vertical ? 5 : Mathf.Max(1, letters.Length * 6 - 1);
            rows = vertical ? Mathf.Max(1, letters.Length * 9 - 2) : 7;
            return segments;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static string Clean(string word) => word.Replace(" ", "").Replace("'", "").Replace("&", "And");

        // ---- Assets --------------------------------------------------------------------------------

        private static Texture2D Texture(string name, int width, int height, System.Func<int, int, Color> paint)
        {
            GreyboxAssets.EnsureFolder(Folder);
            var path = $"{Folder}/TEX_Sign_{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var isNew = texture == null || texture.width != width || texture.height != height;
            if (isNew)
            {
                if (texture != null)
                    AssetDatabase.DeleteAsset(path);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
                texture.SetPixel(px, py, paint(px, py));
            texture.Apply();
            if (isNew)
                AssetDatabase.CreateAsset(texture, path);
            else
                EditorUtility.SetDirty(texture);
            return texture;
        }

        // Unlit, added to what's behind: neon glows and never darkens.
        private static Material Additive(string name, Texture2D texture)
        {
            var material = Ensure(name);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back); // one face each way, never mirrored
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Unlit and opaque: a sign lit from inside.
        private static Material Opaque(string name, Texture2D texture)
        {
            var material = Ensure(name);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cull", (float)CullMode.Back); // one face each way, never mirrored
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Ensure(string name)
        {
            var path = $"{MaterialsFolder}/MAT_Sign_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}
