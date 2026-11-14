using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The cue that shows a stuck player which way to push (the wash game's soap, decision
    /// 2026-11-09): a flat, top-down thumbstick from the control-glyph kit (a light ring and a cap
    /// marked L), sitting in the centre of the wash ring where the soap has to go. The cap slides
    /// the way to push and springs back, again and again. Light and flat on purpose (the user's
    /// call: the three-quarter stick looked too strong), HUD art never lit or hidden by the scene.
    /// The kit's three-quarter stick is kept for button prompts; its builder lives here too.
    /// </summary>
    public sealed class StickHintHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [Tooltip("The flat stick's ring, seen from above.")]
        [SerializeField] private Sprite _ring;
        [Tooltip("The flat stick's cap, which slides to show the push.")]
        [SerializeField] private Sprite _capTop;

        [Tooltip("Canvas pixels: the ring's width.")]
        [SerializeField, Min(16f)] private float _size = 84f;
        [Tooltip("Canvas pixels the cap travels at the far end of a push.")]
        [SerializeField, Min(0f)] private float _travel = 18f;
        [Tooltip("Canvas pixels from the middle the cap circles at for a spin: out to the ring's edge, so the way round is plain (the user's call, 2026-11-14).")]
        [SerializeField, Min(0f)] private float _spinTravel = 26f;
        [Tooltip("Pushes per second (and turns, when the view doesn't give its pace).")]
        [SerializeField, Min(0f)] private float _pushes = 1.6f;
        [Tooltip("How see-through the cue is at most, so the target shows through it.")]
        [SerializeField, Range(0f, 1f)] private float _opacity = 0.9f;

        private readonly List<Hint> _hints = new List<Hint>();
        private readonly List<IStickHintSource> _sources = new List<IStickHintSource>();
        private float _nextLook;
        private float? _push;

        private sealed class Hint
        {
            public RectTransform Root;
            public RectTransform Cap;
            public CanvasGroup Group;
        }

        private void LateUpdate() => Refresh(Camera.main);

        /// <summary>
        /// Places every hint for <paramref name="camera"/>. Captures call it outside Play mode, with
        /// <paramref name="push"/> fixing how far the cap is pushed (0..1).
        /// </summary>
        public void Refresh(Camera camera, float? push = null)
        {
            if (camera == null)
                return;
            _push = push;

            // Sinks and cleaning come and go with the layout editor: look for them now and then.
            if (Time.unscaledTime >= _nextLook || !Application.isPlaying)
            {
                _nextLook = Time.unscaledTime + 1f;
                _sources.Clear();
                foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (behaviour is IStickHintSource source)
                        _sources.Add(source);
                }
            }

            var used = 0;
            foreach (var source in _sources)
            {
                if (source == null || (source is Object unity && unity == null))
                    continue;
                for (var i = 0; i < source.HintCount; i++)
                {
                    var wanted = source.HintAt(i);
                    if (wanted.Amount <= 0f)
                        continue;
                    var hint = used < _hints.Count ? _hints[used] : Build();
                    used++;
                    hint.Group.alpha = wanted.Amount * _opacity;
                    hint.Root.gameObject.SetActive(true);
                    Place(hint, wanted, camera);
                }
            }
            for (var i = used; i < _hints.Count; i++)
                _hints[i].Root.gameObject.SetActive(false);
        }

        private void Place(Hint hint, StickHint wanted, Camera camera)
        {
            var amount = wanted.Amount;
            // From the camera's viewport (fractions of its view) to the canvas, which covers it:
            // the same whatever the window's or a capture's pixel size.
            var at = camera.WorldToViewportPoint(wanted.World);
            var to = camera.WorldToViewportPoint(wanted.World + wanted.Towards);
            if (at.z <= 0f)
            {
                hint.Root.gameObject.SetActive(false);
                return;
            }
            var canvasSize = _canvas.rect.size;
            var local = Vector2.Scale((Vector2)at - new Vector2(0.5f, 0.5f), canvasSize);
            hint.Root.anchoredPosition = local;
            // Pops in big and settles, so it's noticed at once (the user's call, 2026-11-10).
            hint.Root.localScale = Vector3.one * (1f + 0.5f * (1f - amount) * (1f - amount));
            HudBounds.KeepInside(hint.Root, _canvas, _style != null ? _style.ScreenMargin : 12f);

            switch (wanted.Motion)
            {
                case StickMotion.Spin:
                {
                    // Round the ring's edge from the top, the way and at the pace that counts (the
                    // view's), so the cue and the ring's pacer tell the same story.
                    var loops = wanted.LoopsPerSecond > 0f ? wanted.LoopsPerSecond : _pushes;
                    var turn = Mathf.PI * 0.5f + wanted.Way * (_push ?? Time.unscaledTime * loops) * Mathf.PI * 2f;
                    hint.Cap.anchoredPosition = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * _spinTravel;
                    break;
                }
                case StickMotion.Wag:
                {
                    // Side to side, easing at each end like a hand sweeping.
                    var swing = Mathf.Sin((_push ?? Time.unscaledTime * (wanted.LoopsPerSecond > 0f ? wanted.LoopsPerSecond : _pushes)) * Mathf.PI * 2f);
                    hint.Cap.anchoredPosition = new Vector2(swing * _travel, 0f);
                    break;
                }
                default:
                {
                    // Push, hold, let go: the cap eases out the way to go and springs back.
                    var direction = Vector2.Scale((Vector2)(to - at), canvasSize).normalized;
                    var phase = Mathf.Repeat(Time.unscaledTime * _pushes, 1f);
                    var push = _push ?? (phase < 0.45f ? Mathf.SmoothStep(0f, 1f, phase / 0.45f) : phase < 0.7f ? 1f : Mathf.SmoothStep(1f, 0f, (phase - 0.7f) / 0.3f));
                    hint.Cap.anchoredPosition = direction * (_travel * push);
                    break;
                }
            }
        }

        private Hint Build()
        {
            var root = new GameObject("StickHint", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(_canvas, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(_size, _size);
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var cap = CreateTopStick(rect, _size, _ring, _capTop, _style != null ? _style.DisplayFont : null);
            var hint = new Hint { Root = rect, Cap = cap, Group = group };
            _hints.Add(hint);
            return hint;
        }

        /// <summary>The flat, top-down stick under <paramref name="parent"/>; returns its cap. Shared with the kit's sheet.</summary>
        public static RectTransform CreateTopStick(RectTransform parent, float size, Sprite ring, Sprite capSprite, TMP_FontAsset font, string letter = "L")
        {
            Image(parent, "Ring", ring, Vector2.zero, new Vector2(size, size));
            var cap = Image(parent, "Cap", capSprite, Vector2.zero, new Vector2(size * 0.6f, size * 0.6f));
            var label = new GameObject(letter, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(cap, false);
            label.rectTransform.sizeDelta = new Vector2(size * 0.6f, size * 0.6f);
            if (font != null)
                label.font = font;
            label.text = letter;
            label.fontSize = size * 0.34f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.16f, 0.15f, 0.17f);
            label.raycastTarget = false;
            return cap;
        }

        // Proportions of the stick glyph, as shares of its width, from the kit's sprites: the
        // base's well, the cap riding above it with a clear post between (as on the reference).
        private const float CapWidth = 0.66f;
        private const float CapLift = 0.3f;
        private const float BaseDrop = 0.12f;
        // The cap sprite is 260×190 with its top's centre 17 px above the middle; the base sprite
        // 300×170 with its hole's centre 13 px above.
        private static float CapHeight(float size) => size * CapWidth * 190f / 260f;
        private static float CapTopOffset(float size) => CapHeight(size) * 17f / 190f;
        private static float HoleY(float size) => -size * BaseDrop + size * (170f / 300f) * 13f / 170f;

        /// <summary>Places the cap pushed by <paramref name="offset"/> and the post leaning after it.</summary>
        public static void Pose(RectTransform post, RectTransform cap, float size, Vector2 offset)
        {
            cap.anchoredPosition = new Vector2(0f, size * CapLift) + offset;
            var foot = new Vector2(0f, HoleY(size));
            var top = cap.anchoredPosition - new Vector2(0f, CapTopOffset(size));
            var span = top - foot;
            post.anchoredPosition = (foot + top) * 0.5f;
            post.sizeDelta = new Vector2(size * 0.16f, Mathf.Max(size * 0.1f, span.magnitude + size * 0.06f));
            post.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-span.x, span.y) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Builds the stick glyph under <paramref name="parent"/>: base, post, cap and its L.
        /// Shared with the kit's sheet capture, so the two can't drift apart.
        /// </summary>
        public static (RectTransform Post, RectTransform Cap) CreateStick(RectTransform parent, float size, Sprite baseSprite, Sprite postSprite, Sprite capSprite, TMP_FontAsset font, string letter = "L", Sprite plate = null)
        {
            if (plate != null)
                Image(parent, "Plate", plate, new Vector2(0f, -size * BaseDrop - size * 0.02f), new Vector2(size * 1.25f, size * 1.25f * 190f / 340f));
            Image(parent, "Base", baseSprite, new Vector2(0f, -size * BaseDrop), new Vector2(size, size * 170f / 300f));
            var post = Image(parent, "Post", postSprite, Vector2.zero, new Vector2(size * 0.16f, size * 0.3f));
            var cap = Image(parent, "Cap", capSprite, new Vector2(0f, size * CapLift), new Vector2(size * CapWidth, CapHeight(size)));

            // The letter on the cap's dished top, in the HUD's display face, squashed like the
            // top, which is seen at a slant.
            var label = new GameObject(letter, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(cap, false);
            var labelRect = label.rectTransform;
            labelRect.anchoredPosition = new Vector2(0f, CapTopOffset(size));
            labelRect.sizeDelta = new Vector2(size * 0.5f, size * 0.3f);
            labelRect.localScale = new Vector3(1f, 0.62f, 1f);
            if (font != null)
                label.font = font;
            label.text = letter;
            label.fontSize = size * 0.3f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            Pose(post, cap, size, Vector2.zero);
            return (post, cap);
        }

        private static RectTransform Image(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
