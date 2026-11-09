using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The animated thumbstick that shows a stuck player which way to push (the wash game's soap,
    /// decision 2026-11-09), drawn from the control-glyph kit: flat HUD art apart from the game's
    /// look, never lit or hidden by the scene. It sits on a cream plate on the world point a view
    /// asks for (the wash ring's rim, by the soap; the user's call) and stays on screen. The cap slides towards the way to push, the
    /// post leans after it and the L rides on top, again and again, like a tutorial.
    /// </summary>
    public sealed class StickHintHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private Sprite _base;
        [SerializeField] private Sprite _post;
        [SerializeField] private Sprite _cap;
        [Tooltip("The cream plate the stick sits on, so it reads on the ring's dark segments.")]
        [SerializeField] private Sprite _plate;

        [Tooltip("Canvas pixels: the glyph's width.")]
        [SerializeField, Min(16f)] private float _size = 60f;
        [Tooltip("Canvas pixels the cap travels at the far end of a push.")]
        [SerializeField, Min(0f)] private float _travel = 12f;
        [Tooltip("Pushes per second.")]
        [SerializeField, Min(0f)] private float _pushes = 1.6f;
        [Tooltip("Canvas pixels the glyph sits above the point it follows (on the ring's rim, the plate centres on it).")]
        [SerializeField] private float _lift = 0f;

        private readonly List<Hint> _hints = new List<Hint>();
        private readonly List<SinkWashRingView> _views = new List<SinkWashRingView>();
        private float _nextLook;
        private float? _push;

        private sealed class Hint
        {
            public RectTransform Root;
            public RectTransform Post;
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

            // Sinks come and go with the layout editor: look for them now and then.
            if (Time.unscaledTime >= _nextLook || !Application.isPlaying)
            {
                _nextLook = Time.unscaledTime + 1f;
                _views.Clear();
                _views.AddRange(FindObjectsByType<SinkWashRingView>(FindObjectsSortMode.None));
            }

            for (var i = 0; i < _views.Count; i++)
            {
                var hint = i < _hints.Count ? _hints[i] : Build();
                var view = _views[i];
                var amount = view != null ? view.HintAmount : 0f;
                hint.Group.alpha = amount;
                hint.Root.gameObject.SetActive(amount > 0f);
                if (amount <= 0f)
                    continue;
                Place(hint, view, camera, amount);
            }
            for (var i = _views.Count; i < _hints.Count; i++)
                _hints[i].Root.gameObject.SetActive(false);
        }

        private void Place(Hint hint, SinkWashRingView view, Camera camera, float amount)
        {
            var at = camera.WorldToScreenPoint(view.HintWorldPosition);
            var to = camera.WorldToScreenPoint(view.HintWorldPosition + view.HintTowardsWorld);
            if (at.z <= 0f)
            {
                hint.Root.gameObject.SetActive(false);
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, at, null, out var local);
            hint.Root.anchoredPosition = local + new Vector2(0f, _lift);
            var pop = 1f + 0.12f * (1f - amount); // a little overshoot as it appears
            hint.Root.localScale = Vector3.one * pop;
            HudBounds.KeepInside(hint.Root, _canvas, _style != null ? _style.ScreenMargin : 12f);

            // Push, hold, let go: the cap eases out towards the way to go and springs back.
            var direction = ((Vector2)(to - at)).normalized;
            var phase = Mathf.Repeat(Time.unscaledTime * _pushes, 1f);
            var push = _push ?? (phase < 0.45f ? Mathf.SmoothStep(0f, 1f, phase / 0.45f) : phase < 0.7f ? 1f : Mathf.SmoothStep(1f, 0f, (phase - 0.7f) / 0.3f));
            var offset = direction * (_travel * push);
            // On screen the stick is seen from above at a slant: sideways moves read fully, up and
            // down a little less.
            offset.y *= 0.7f;

            Pose(hint.Post, hint.Cap, _size, offset);
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

        private Hint Build()
        {
            var root = new GameObject("StickHint", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(_canvas, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(_size, _size);
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var (post, cap) = CreateStick(rect, _size, _base, _post, _cap, _style != null ? _style.DisplayFont : null, plate: _plate);

            var hint = new Hint { Root = rect, Post = post, Cap = cap, Group = group };
            _hints.Add(hint);
            return hint;
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
