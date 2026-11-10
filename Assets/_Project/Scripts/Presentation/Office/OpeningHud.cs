using Game.Data;
using Game.Presentation.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// What the opening shows over the camera (docs/opening-flow.md): a black veil for the fades,
    /// the save notice, and the "A Skip" hint during the approach, the chef's walk and the climb.
    /// Who has joined shows on the floor's own player bar (PlayerBarHud) once the camera lands.
    /// Drawn in the house UI kit and translated (Tr). Hints show Xbox buttons only.
    /// </summary>
    public sealed class OpeningHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.16f, 0.11f, 0.09f);
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
        private static readonly Color Green = new Color(0.42f, 0.68f, 0.29f); // Xbox A

        [SerializeField] private HudStyle _style;

        private Image _veil;
        private RectTransform _saveCard;
        private RectTransform _saveIcon;
        private RectTransform _skip;

        /// <summary>How dark the veil is: 1 black, 0 clear.</summary>
        public float Veil
        {
            set
            {
                if (_veil != null)
                    _veil.color = new Color(0f, 0f, 0f, Mathf.Clamp01(value));
            }
        }

        public bool SaveNoticeShown { set => _saveCard.gameObject.SetActive(value); }
        public bool SkipShown { set => _skip.gameObject.SetActive(value); }

        private void Awake() => Build();

        private void Update()
        {
            // The save icon turns, as it will whenever the game saves.
            if (_saveIcon != null && _saveIcon.gameObject.activeInHierarchy)
                _saveIcon.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 180f);
        }

        private void Build()
        {
            var canvasObject = new GameObject("OpeningCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // over the menu, for the fades
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = canvasObject.GetComponent<RectTransform>();

            _veil = UiKit.RoundBox(root, "Veil", Color.black, 0f, 0f, 0f, 0f, 0f);
            UiKit.Fill(_veil.rectTransform);

            var layout = UiKit.Rect(root, "Layout", 0f, 0f, 1920f, 1080f);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.anchoredPosition = Vector2.zero;

            // The save notice: the icon and what it means, centred on the black.
            _saveCard = UiKit.Rect(layout, "SaveNotice", 560f, 420f, 800f, 240f);
            var iconHolder = UiKit.Rect(_saveCard, "IconHolder", 0f, 40f, 120f, 120f);
            UiKit.StrokedCircle(iconHolder, "IconDisc", Ink, Cream, 6f, 60f, 60f, 104f);
            _saveIcon = UiKit.Rect(iconHolder, "IconSpin", 16f, 16f, 88f, 88f);
            _saveIcon.pivot = new Vector2(0.5f, 0.5f);
            _saveIcon.anchoredPosition = new Vector2(60f, -60f);
            UiKit.RoundBox(_saveIcon, "Tick", Cream, 36f, 2f, 16f, 30f, 8f);
            UiKit.RoundBox(_saveIcon, "TickFaint", new Color(Cream.r, Cream.g, Cream.b, 0.35f), 36f, 56f, 16f, 30f, 8f);
            UiKit.TextAt(_style.Bold, _saveCard, "Title", Tr.Get("When you see this, the game is saving."), 40f, Cream, 160f, 92f, 640f);
            UiKit.TextAt(_style.Medium, _saveCard, "Body", Tr.Get("Don't quit or turn off the game while it shows."), 30f, new Color(Cream.r, Cream.g, Cream.b, 0.8f), 160f, 140f, 640f);

            // "A Skip", bottom right, during the fly-through.
            _skip = UiKit.Rect(layout, "Skip", 1650f, 990f, 220f, 60f);
            Badge(_skip, 0f, 4f);
            UiKit.TextAt(_style.Bold, _skip, "Label", Tr.Get("Skip"), 32f, Cream, 66f, 40f, 150f);

            SaveNoticeShown = false;
            SkipShown = false;
        }

        // An Xbox A badge: a green disc with its letter.
        private GameObject Badge(RectTransform parent, float x, float y)
        {
            var badge = UiKit.Rect(parent, "BadgeA", x, y, 52f, 52f);
            UiKit.Ellipse(badge, "Disc", Green, 0f, 0f, 52f, 52f);
            UiKit.TextAt(_style.Bold, badge, "Letter", "A", 32f, Color.white, 26f, 38f, 52f, HorizontalAlignmentOptions.Center);
            return badge.gameObject;
        }
    }
}
