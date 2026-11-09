using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Draws the control-glyph kit (decision 2026-11-09): sprites for showing controls apart from
    /// the game's look, flat and graphic like a pad's own glyphs, readable on any background. Dark
    /// glossy bodies, a thick near-black outline and a soft shadow under each; symbols (letters,
    /// L and R) are set in the HUD font on top, so one body serves every button. Drawn from signed
    /// distance shapes at 4× the size they're shown, so edges stay crisp. A first pass in code; the
    /// Art Director can replace the PNGs without code changes.
    /// Menu: Tools/Game/Art/Build Control Glyphs. Batch: -executeMethod Game.Editor.ControlGlyphArt.Build
    /// </summary>
    public static class ControlGlyphArt
    {
        public const string Folder = "Assets/_Project/Art/UI/Controls";

        public const string Button = "TEX_Glyph_Button";        // A, B, X, Y: a round body
        public const string Bumper = "TEX_Glyph_Bumper";        // LB, RB: a wide pill
        public const string Trigger = "TEX_Glyph_Trigger";      // LT, RT: a tall, rounded-top body
        public const string StickBase = "TEX_Glyph_StickBase";  // the stick, seen from three-quarters: its well
        public const string StickPost = "TEX_Glyph_StickPost";  // the post, stretched and turned to show a tilt
        public const string StickCap = "TEX_Glyph_StickCap";    // the cap, moved to show a tilt; L or R on top

        // The kit's palette: near-black outline, two dark greys for bodies, a lighter cap top.
        private static readonly Color Outline = new Color32(0x12, 0x13, 0x17, 0xFF);
        private static readonly Color Body = new Color32(0x2B, 0x2E, 0x35, 0xFF);
        private static readonly Color BodyDark = new Color32(0x1D, 0x1F, 0x24, 0xFF);
        private static readonly Color CapSide = new Color32(0x30, 0x33, 0x3B, 0xFF);
        private static readonly Color CapTop = new Color32(0x48, 0x4C, 0x56, 0xFF);
        private static readonly Color CapDish = new Color32(0x3B, 0x3F, 0x48, 0xFF);
        private static readonly Color Shine = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.35f);

        private const float Line = 7f;     // outline width, px at 4×
        private const float Soft = 1.4f;   // antialiasing, px

        [MenuItem("Tools/Game/Art/Build Control Glyphs")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Save(Button, 160, 160, DrawButton);
            Save(Bumper, 240, 120, DrawBumper);
            Save(Trigger, 150, 190, DrawTrigger);
            Save(StickBase, 300, 170, DrawStickBase);
            Save(StickPost, 80, 150, DrawStickPost);
            Save(StickCap, 260, 190, DrawStickCap);
            AssetDatabase.Refresh();
            foreach (var name in new[] { Button, Bumper, Trigger, StickBase, StickPost, StickCap })
                Import(name);
            Debug.Log($"[ControlGlyphArt] Drew the control glyphs into {Folder}.");
        }

        /// <summary>A kit sprite, imported crisp for UI (see MenuArt.Sprite).</summary>
        public static Sprite Sprite(string name) => MenuArt.Sprite(name, Folder);

        private static void Import(string name)
        {
            // Pivot in the middle; 4× art shown at 1×: MenuArt imports at 200 px per unit for 2× art,
            // and UI Images are sized by the views anyway.
            Sprite(name);
        }

        /// <summary>
        /// A sheet of the kit as players see it, on a dark and a light half to show it reads on
        /// both: A B X Y, LB RB, LT RT, and the stick at rest and pushed. To Screenshots/control-glyphs.png.
        /// Batch: -executeMethod Game.Editor.ControlGlyphArt.CaptureSheet
        /// </summary>
        public static void CaptureSheet()
        {
            Build();
            var style = GreyboxHud.EnsureStyle();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            var camera = new GameObject("Sheet Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
            // A light half behind the right side, to show the kit on bright scenery too.
            var light = GameObject.CreatePrimitive(PrimitiveType.Quad);
            light.transform.position = camera.transform.position + new Vector3(4f, 0f, 5f);
            light.transform.localScale = new Vector3(8f, 10f, 1f);
            var lightMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lightMaterial.SetColor("_BaseColor", new Color(0.92f, 0.87f, 0.78f));
            light.GetComponent<Renderer>().sharedMaterial = lightMaterial;

            var canvasObject = new GameObject("Sheet", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var root = (RectTransform)canvasObject.transform;

            for (var half = 0; half < 2; half++)
            {
                var x0 = half == 0 ? -720f : 240f;
                var letters = new[] { ("A", new Color(0.42f, 0.74f, 0.25f)), ("B", new Color(0.86f, 0.25f, 0.22f)), ("X", new Color(0.2f, 0.5f, 0.9f)), ("Y", new Color(0.96f, 0.76f, 0.17f)) };
                for (var i = 0; i < letters.Length; i++)
                    Glyph(root, style, Button, letters[i].Item1, letters[i].Item2, new Vector2(x0 + i * 120f, 300f), new Vector2(96f, 96f), 0.46f);
                Glyph(root, style, Bumper, "LB", Color.white, new Vector2(x0 + 40f, 140f), new Vector2(144f, 72f), 0.42f);
                Glyph(root, style, Bumper, "RB", Color.white, new Vector2(x0 + 220f, 140f), new Vector2(144f, 72f), 0.42f);
                Glyph(root, style, Trigger, "LT", Color.white, new Vector2(x0 + 360f, 140f), new Vector2(84f, 106f), 0.28f);
                Glyph(root, style, Trigger, "RT", Color.white, new Vector2(x0 + 470f, 140f), new Vector2(84f, 106f), 0.28f);
                for (var p = 0; p < 3; p++)
                    Stick(root, style, new Vector2(x0 + p * 170f, -120f), 140f, new Vector2(p - 1, 0f) * 26f);
            }

            var path = Path.GetFullPath("Screenshots/control-glyphs.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Game.Presentation.Hud.UiCapture.Save(camera, canvas, path);
            Debug.Log($"[ControlGlyphArt] Sheet at {path}.");
        }

        private static void Glyph(RectTransform root, Game.Presentation.Hud.HudStyle style, string sprite, string symbol, Color color, Vector2 at, Vector2 size, float textScale)
        {
            var body = Image(root, sprite, Sprite(sprite), at, size);
            var label = new GameObject(symbol, typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            label.transform.SetParent(body, false);
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = new Vector2(0f, size.y * 0.04f);
            label.font = style.DisplayFont;
            label.text = symbol;
            label.fontSize = size.y * textScale;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = color;
        }

        // The stick as StickHintHud draws it, with the cap pushed by `push` canvas pixels.
        private static void Stick(RectTransform root, Game.Presentation.Hud.HudStyle style, Vector2 at, float size, Vector2 push)
        {
            var group = new GameObject("Stick", typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(root, false);
            group.anchoredPosition = at;
            var (post, cap) = Game.Presentation.Hud.StickHintHud.CreateStick(group, size, Sprite(StickBase), Sprite(StickPost), Sprite(StickCap), style.DisplayFont);
            Game.Presentation.Hud.StickHintHud.Pose(post, cap, size, new Vector2(push.x, push.y * 0.7f));
        }

        private static RectTransform Image(RectTransform parent, string name, Sprite sprite, Vector2 at, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<UnityEngine.UI.Image>().sprite = sprite;
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            return rect;
        }

        // ---- The glyphs. Pixel space, y up, origin bottom left. ----

        private static void DrawButton(Canvas c)
        {
            var centre = new Vector2(80f, 84f);
            c.Layer(p => Circle(p, centre + new Vector2(0f, -8f), 64f), Shadow, 10f);
            c.Layer(p => Circle(p, centre, 64f), Outline);
            c.Layer(p => Circle(p, centre, 64f - Line), Body);
            // A lip at the bottom, so it reads as a raised button seen a little from above.
            c.Layer(p => Math.Max(Circle(p, centre, 64f - Line), -(Circle(p, centre + new Vector2(0f, 7f), 64f - Line))), BodyDark);
            c.Layer(p => Ellipse(p, centre + new Vector2(-10f, 26f), 30f, 13f), Shine, 4f);
        }

        private static void DrawBumper(Canvas c)
        {
            var centre = new Vector2(120f, 64f);
            var half = new Vector2(100f, 38f);
            c.Layer(p => RoundRect(p, centre + new Vector2(0f, -8f), half, 34f), Shadow, 10f);
            c.Layer(p => RoundRect(p, centre, half, 34f), Outline);
            c.Layer(p => RoundRect(p, centre, half - Vector2.one * Line, 34f - Line), Body);
            c.Layer(p => Math.Max(RoundRect(p, centre, half - Vector2.one * Line, 34f - Line), -RoundRect(p, centre + new Vector2(0f, 7f), half - Vector2.one * Line, 34f - Line)), BodyDark);
            c.Layer(p => Ellipse(p, centre + new Vector2(-30f, 16f), 46f, 9f), Shine, 4f);
        }

        private static void DrawTrigger(Canvas c)
        {
            // A tall body, round on top, flatter at the bottom: a trigger as seen on a pad's glyph.
            float Shape(Vector2 p, float grow) => Math.Min(
                Circle(p, new Vector2(75f, 112f), 52f + grow),
                RoundRect(p, new Vector2(75f, 76f), new Vector2(52f + grow, 40f + grow), 16f + grow));
            c.Layer(p => Shape(p + new Vector2(0f, 8f), 0f), Shadow, 10f);
            c.Layer(p => Shape(p, 0f), Outline);
            c.Layer(p => Shape(p, -Line), Body);
            c.Layer(p => Math.Max(Shape(p, -Line), -Shape(p - new Vector2(0f, 7f), -Line)), BodyDark);
            c.Layer(p => Ellipse(p, new Vector2(62f, 138f), 26f, 12f), Shine, 4f);
        }

        // The stick's well: a flat ring seen from three-quarters, with depth on its front.
        private static void DrawStickBase(Canvas c)
        {
            var top = new Vector2(150f, 98f);
            const float rx = 128f, ry = 52f, depth = 22f;
            float Body2(Vector2 p) => Math.Min(Math.Min(Ellipse(p, top, rx, ry), Ellipse(p, top - new Vector2(0f, depth), rx, ry)),
                RoundRect(p, top - new Vector2(0f, depth * 0.5f), new Vector2(rx, depth * 0.5f), 0f));
            c.Layer(p => Body2(p + new Vector2(0f, 10f)), Shadow, 12f);
            c.Layer(p => Body2(p) - Line, Outline);
            c.Layer(Body2, BodyDark);
            c.Layer(p => Ellipse(p, top, rx, ry), Body);
            // The hole the post rises from.
            c.Layer(p => Ellipse(p, top, 46f, 19f) - Line * 0.6f, Outline);
            c.Layer(p => Ellipse(p, top, 46f, 19f), BodyDark);
            c.Layer(p => Math.Max(Ellipse(p, top + new Vector2(-30f, 22f), 60f, 12f), -Ellipse(p, top, 46f, 19f)), Shine, 4f);
        }

        // The post, upright; the view stretches and turns it towards the cap.
        private static void DrawStickPost(Canvas c)
        {
            float Shape(Vector2 p) => RoundRect(p, new Vector2(40f, 75f), new Vector2(17f, 62f), 14f);
            c.Layer(p => Shape(p) - Line, Outline);
            c.Layer(Shape, CapSide);
            c.Layer(p => RoundRect(p, new Vector2(32f, 80f), new Vector2(4f, 50f), 4f), Shine, 2f);
        }

        // The cap: a short, smooth cylinder seen from three-quarters (no grip ridges, the user's
        // call), with a dished top for the L.
        private static void DrawStickCap(Canvas c)
        {
            var top = new Vector2(130f, 112f);
            const float rx = 112f, ry = 50f, depth = 34f;
            float Shape(Vector2 p) => Math.Min(Math.Min(Ellipse(p, top, rx, ry), Ellipse(p, top - new Vector2(0f, depth), rx, ry)),
                RoundRect(p, top - new Vector2(0f, depth * 0.5f), new Vector2(rx, depth * 0.5f), 0f));
            c.Layer(p => Shape(p + new Vector2(0f, 12f)), Shadow, 12f);
            c.Layer(p => Shape(p) - Line, Outline);
            c.Layer(Shape, CapSide);
            c.Layer(p => Ellipse(p, top, rx, ry) - Line * 0.6f, Outline);
            c.Layer(p => Ellipse(p, top, rx, ry), CapTop);
            c.Layer(p => Ellipse(p, top, rx * 0.72f, ry * 0.72f), CapDish);
            c.Layer(p => Math.Max(Ellipse(p, top + new Vector2(-28f, 24f), 62f, 13f), Ellipse(p, top, rx * 0.95f, ry * 0.95f)), Shine, 4f);
        }

        // ---- Signed distances (negative inside), in pixels. ----

        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        // Close enough to a true distance for the gentle ellipses here.
        private static float Ellipse(Vector2 p, Vector2 c, float rx, float ry)
        {
            var q = new Vector2((p.x - c.x) / rx, (p.y - c.y) / ry);
            return (q.magnitude - 1f) * Mathf.Min(rx, ry);
        }

        private static float RoundRect(Vector2 p, Vector2 c, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * radius;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private sealed class Canvas
        {
            public readonly int Width;
            public readonly int Height;
            public readonly Color[] Pixels;

            public Canvas(int width, int height)
            {
                Width = width;
                Height = height;
                Pixels = new Color[width * height];
            }

            /// <summary>Paints <paramref name="color"/> where the shape's distance is below 0, eased over <paramref name="soft"/> px.</summary>
            public void Layer(Func<Vector2, float> shape, Color color, float soft = Soft)
            {
                for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    var d = shape(new Vector2(x + 0.5f, y + 0.5f));
                    var cover = Mathf.Clamp01(0.5f - d / soft);
                    if (cover <= 0f)
                        continue;
                    var i = y * Width + x;
                    var a = color.a * cover;
                    var under = Pixels[i];
                    var outA = a + under.a * (1f - a);
                    var rgb = outA > 0f ? ((Vector4)color * a + (Vector4)under * (under.a * (1f - a))) / outA : Vector4.zero;
                    Pixels[i] = new Color(rgb.x, rgb.y, rgb.z, outA);
                }
            }
        }

        private static void Save(string name, int width, int height, Action<Canvas> draw)
        {
            var canvas = new Canvas(width, height);
            draw(canvas);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(canvas.Pixels);
            texture.Apply();
            File.WriteAllBytes($"{Folder}/{name}.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
