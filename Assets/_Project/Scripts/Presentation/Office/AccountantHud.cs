using Game.Data;
using Game.Gameplay.Office;
using Game.Presentation.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Office
{
    /// <summary>
    /// The accountant's books, bottom centre: "A Borrow money" when a player stands at the desk,
    /// and once they sit, the money and the loan, the amount picked and what it will cost, with
    /// "A Borrow" and "B Leave". Xbox buttons only, inside the 1920×1080 layout so no edge clips it.
    /// </summary>
    public sealed class AccountantHud : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.16f, 0.11f, 0.09f);
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
        private static readonly Color Sand = new Color(0.78f, 0.70f, 0.56f);
        private static readonly Color Gold = new Color(0.86f, 0.62f, 0.26f);
        private static readonly Color Bad = new Color(0.90f, 0.34f, 0.28f);
        private static readonly Color Green = new Color(0.42f, 0.68f, 0.29f); // Xbox A
        private static readonly Color Red = new Color(0.84f, 0.26f, 0.22f); // Xbox B

        private const float BooksWidth = 760f, BooksHeight = 268f;

        [SerializeField] private HudStyle _style;
        [SerializeField] private AccountantDesk _desk;

        private RectTransform _hint;
        private RectTransform _books;
        private TextMeshProUGUI _money;
        private TextMeshProUGUI _amount;
        private TextMeshProUGUI _cost;
        private TextMeshProUGUI _left;
        private TextMeshProUGUI _right;
        private float _bump;
        private string _shown;

        private void Awake()
        {
            var canvasObject = new GameObject("AccountantCanvas", typeof(RectTransform));
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

            // The hint, where the miniature's sits: above the join slots along the bottom.
            _hint = UiKit.Rect(layout, "Hint", 660f, 830f, 600f, 76f);
            UiKit.RoundBox(_hint, "Panel", new Color(Ink.r, Ink.g, Ink.b, 0.88f), 0f, 0f, 600f, 76f, 38f);
            Badge(_hint, "A", Green, 14f, 12f);
            UiKit.TextAt(_style.Bold, _hint, "Label", Tr.Get("Borrow money"), 32f, Cream, 80f, 50f, 500f);
            _hint.gameObject.SetActive(false);

            _books = UiKit.Rect(layout, "Books", (1920f - BooksWidth) / 2f, 1080f - 150f - BooksHeight, BooksWidth, BooksHeight);
            UiKit.RoundBox(_books, "Panel", new Color(Ink.r, Ink.g, Ink.b, 0.94f), 0f, 0f, BooksWidth, BooksHeight, 28f);
            UiKit.TextAt(_style.ExtraBold, _books, "Title", Tr.Get("THE ACCOUNTANT"), 22f, Gold, 36f, 48f, 400f);
            _money = UiKit.TextAt(_style.Medium, _books, "Money", "", 22f, Sand, BooksWidth - 36f, 48f, 480f, HorizontalAlignmentOptions.Right);
            _left = UiKit.TextAt(_style.ExtraBold, _books, "Less", "‹", 64f, Cream, BooksWidth / 2f - 180f, 128f, 60f, HorizontalAlignmentOptions.Center);
            _amount = UiKit.TextAt(_style.ExtraBold, _books, "Amount", "", 64f, Cream, BooksWidth / 2f, 128f, 300f, HorizontalAlignmentOptions.Center);
            _right = UiKit.TextAt(_style.ExtraBold, _books, "More", "›", 64f, Cream, BooksWidth / 2f + 180f, 128f, 60f, HorizontalAlignmentOptions.Center);
            _cost = UiKit.TextAt(_style.Medium, _books, "Cost", "", 22f, Sand, BooksWidth / 2f, 176f, BooksWidth - 72f, HorizontalAlignmentOptions.Center, lines: 2);
            Badge(_books, "A", Green, BooksWidth / 2f - 210f, BooksHeight - 66f);
            UiKit.TextAt(_style.Bold, _books, "Borrow", Tr.Get("Borrow"), 28f, Cream, BooksWidth / 2f - 146f, BooksHeight - 30f, 180f);
            Badge(_books, "B", Red, BooksWidth / 2f + 40f, BooksHeight - 66f);
            UiKit.TextAt(_style.Bold, _books, "Leave", Tr.Get("Leave"), 28f, Cream, BooksWidth / 2f + 104f, BooksHeight - 30f, 180f);
            _books.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (_desk == null)
                return;
            _desk.Stepped += step => _bump = 1f;
            _desk.Borrowed += amount => _bump = 1f;
        }

        private void Update()
        {
            var open = _desk != null && _desk.Borrower != null && _desk.Campaign != null;
            _hint.gameObject.SetActive(_desk != null && !open && _desk.Standing != null);
            _books.gameObject.SetActive(open);
            if (!open)
            {
                _shown = null;
                return;
            }

            // A small pop on every step or loan, so a press is felt.
            _bump = Mathf.MoveTowards(_bump, 0f, Time.unscaledDeltaTime * 5f);
            _amount.rectTransform.localScale = Vector3.one * (1f + 0.08f * _bump);

            var campaign = _desk.Campaign;
            var loan = campaign.ToLoan();
            var amounts = _desk.Amounts;
            var key = $"{campaign.Balance}|{loan.Owed}|{amounts.Count}|{_desk.Selected}";
            if (key == _shown)
                return;
            _shown = key;

            _money.text = loan.Owed > 0f
                ? Tr.Format("Money {money} · Loan {loan}", ("money", Whole(campaign.Balance)), ("loan", Whole(loan.Owed)))
                : Tr.Format("Money {money}", ("money", Whole(campaign.Balance)));
            if (amounts.Count == 0)
            {
                _amount.text = "—";
                _left.text = _right.text = "";
                _cost.text = Tr.Get("He won't lend more until you pay some back.");
                _cost.color = Bad;
                return;
            }
            var amount = amounts[_desk.Selected];
            _amount.text = Whole(amount);
            _left.text = _desk.Selected > 0 ? "‹" : "";
            _right.text = _desk.Selected < amounts.Count - 1 ? "›" : "";
            var after = new Game.Simulation.Campaign.LoanAccount(loan.Owed, loan.Instalment);
            after.Borrow(amount, _desk.Terms);
            _cost.text = Tr.Format("You'll owe {owed}, paid back {instalment} a collection", ("owed", Whole(after.Owed)), ("instalment", Whole(after.Instalment)));
            _cost.color = Sand;
        }

        private void Badge(RectTransform parent, string letter, Color colour, float x, float y)
        {
            var badge = UiKit.Rect(parent, "Badge" + letter, x, y, 52f, 52f);
            UiKit.Ellipse(badge, "Disc", colour, 0f, 0f, 52f, 52f);
            UiKit.TextAt(_style.Bold, badge, "Letter", letter, 32f, Color.white, 26f, 38f, 52f, HorizontalAlignmentOptions.Center);
        }

        // In the player's language and currency, as the dashboard shows money.
        private static string Whole(float amount) => Locale.Money(amount, MoneyStyle.Whole);
    }
}
