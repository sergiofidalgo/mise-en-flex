using Game.Data;
using Game.Gameplay.Office;
using Game.Presentation.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The miniature table's hint, bottom centre, while anyone is at it: how many are still to come,
    /// or, once everyone is there, "A Start the day". Xbox buttons only.
    /// </summary>
    public sealed class MiniatureHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.16f, 0.11f, 0.09f);
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
        private static readonly Color Green = new Color(0.42f, 0.68f, 0.29f); // Xbox A

        [SerializeField] private HudStyle _style;
        [SerializeField] private MiniatureTable _table;

        private RectTransform _card;
        private GameObject _badge;
        private TextMeshProUGUI _label;
        private int _shownAt = -1;
        private bool _shownReady;

        private void Awake()
        {
            var canvasObject = new GameObject("MiniatureCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var layout = UiKit.Rect(canvasObject.transform, "Layout", 0f, 0f, 1920f, 1080f);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.anchoredPosition = Vector2.zero;

            // Above the join slots, which sit along the bottom.
            _card = UiKit.Rect(layout, "Card", 660f, 830f, 600f, 76f);
            UiKit.RoundBox(_card, "Panel", new Color(Ink.r, Ink.g, Ink.b, 0.88f), 0f, 0f, 600f, 76f, 38f);
            _badge = UiKit.Rect(_card, "BadgeA", 14f, 12f, 52f, 52f).gameObject;
            UiKit.Ellipse(_badge.transform, "Disc", Green, 0f, 0f, 52f, 52f);
            UiKit.TextAt(_style.Bold, _badge.transform, "Letter", "A", 32f, Color.white, 26f, 38f, 52f, HorizontalAlignmentOptions.Center);
            _label = UiKit.TextAt(_style.Bold, _card, "Label", "", 32f, Cream, 80f, 50f, 500f);
            _card.gameObject.SetActive(false);
        }

        private void Update()
        {
            var show = _table != null && _table.AtTable > 0;
            _card.gameObject.SetActive(show);
            if (!show || (_table.AtTable == _shownAt && _table.Ready == _shownReady))
                return;
            _shownAt = _table.AtTable;
            _shownReady = _table.Ready;
            _badge.SetActive(_shownReady);
            _label.text = _shownReady
                ? Tr.Get("Start the day")
                : Tr.Format("Everyone to the table: {here} of {all}", ("here", _table.AtTable), ("all", _table.Joined));
            _label.rectTransform.anchoredPosition = new Vector2(_shownReady ? 80f : 30f, _label.rectTransform.anchoredPosition.y);
        }
    }
}
