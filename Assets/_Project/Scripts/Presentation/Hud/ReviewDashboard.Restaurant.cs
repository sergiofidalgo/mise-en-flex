using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Simulation.Cookbook;
using Game.Simulation.Days;
using Game.Simulation.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// forkcast's Restaurant tab (the Cookbook tab until 2026-11-11): the opening times over the menu,
    /// and the chosen one in detail. After the user's looks at it (2026-11-11):
    /// <list type="bullet">
    /// <item>The list is in two groups, ON YOUR MENU and TO UNLOCK, that look nothing alike: a dish on
    /// the menu is bright, steaming, with its price; one still to unlock is a darker card, its plate
    /// cold behind a padlock, with its unlock price on a tag.</item>
    /// <item>Every dish explains how it's cooked, step by step, owned or not.</item>
    /// <item>The price is a dial that springs from number to number with a tick, speeding up as the
    /// D-pad is held, and bumps at its ends.</item>
    /// <item>Unlocking is a moment: the plate leaps and glows, sparks and a ring burst out, a
    /// "New on the menu" stamp slams down, with a sizzle and the till.</item>
    /// <item>Opening times are tiles with a sun, a moon or both, over the day as a strip of bars.</item>
    /// </list>
    /// </summary>
    public sealed partial class ReviewDashboard
    {
        // Wider since the left column narrowed (2026-11-12): the list, then the dish's detail to the edge.
        private const float RecipeListWidth = ColumnWidth, RecipeRow = 92f, DetailX = RightX + ColumnWidth + ColumnGap;
        private const float OpeningTop = 262f, OpeningHeight = 214f;
        private const float DetailTop = 262f, DetailBottom = 932f;
        private const float GroupHeader = 32f;

        [Header("Restaurant tab")]
        [Tooltip("Pixels between neighbouring numbers on the price dial.")]
        [SerializeField, Min(20f)] private float _dialSpacing = 90f;
        [Tooltip("How stiff the dial's spring is: higher snaps to the new number faster.")]
        [SerializeField, Min(10f)] private float _dialStiffness = 420f;
        [Tooltip("How much the dial's spring is damped: 1 settles without overshoot, lower overshoots a little like a detent.")]
        [SerializeField, Range(0.2f, 1.5f)] private float _dialDamping = 0.62f;
        [Tooltip("How much the middle number punches up on each step.")]
        [SerializeField, Range(0f, 0.5f)] private float _dialPunch = 0.16f;
        [Tooltip("Seconds between price steps when the D-pad is first held (after the delay); it speeds up to Price Repeat Interval.")]
        [SerializeField, Min(0.02f)] private float _priceRepeatSlow = 0.16f;
        [Tooltip("Seconds of holding before the price steps at its fastest.")]
        [SerializeField, Min(0.1f)] private float _priceAccelerateSeconds = 1.6f;
        [Tooltip("Seconds between price steps at their fastest, after the D-pad has been held a while. Steps are 10 cents, so held long it sweeps about 3 a second.")]
        [SerializeField, Min(0.01f)] private float _priceRepeatFastest = 0.03f;
        [Tooltip("Pitch of the dial's tick (the kitchen's clack).")]
        [SerializeField, Range(0.5f, 3f)] private float _dialTickPitch = 1.8f;
        [Tooltip("Seconds a wisp of steam takes to rise and fade.")]
        [SerializeField, Min(0.3f)] private float _steamSeconds = 2.6f;
        [Tooltip("Steam's colour at its thickest: a warm grey, so it shows on the cream panel as well as over the food.")]
        [SerializeField] private Color _steamColour = new Color(0.62f, 0.57f, 0.52f, 0.62f);
        [Tooltip("A card for a dish still to unlock: darker than the menu's, so the two groups never look alike.")]
        [SerializeField] private Color _lockedCard = Hex(0xe2d0ad);

        // The price dial: its numbers, where its middle is, and its spring (offset in steps, and speed).
        private readonly List<(TextMeshProUGUI Label, float Step)> _dial = new List<(TextMeshProUGUI, float)>();
        private float _dialCentreX, _dialBaseline;
        private float _dialOffset, _dialVelocity, _dialKick;
        private string _dialRecipe;
        private float _dialPrice = float.NaN;
        private float _priceHeldSince;
        // The ruler under the numbers, the suggestion's star on it, the gold flash as the price lands
        // on the suggestion, and the consequence chips that punch with each step.
        private readonly List<(Image Tick, float At)> _ticks = new List<(Image, float)>();
        private readonly List<RectTransform> _effects = new List<RectTransform>();
        private Image _suggestionMark, _dialWell, _dialSnapGlow;
        private float _dialSuggested, _dialSnap;

        // Steam over the plates on the menu, animated every frame.
        private readonly List<Wisp> _steam = new List<Wisp>();

        [Tooltip("Seconds the unlock moment lasts.")]
        [SerializeField, Min(0.2f)] private float _unlockWarmSeconds = 1.9f;
        [Tooltip("How far the unlock sparks fly from the plate, in pixels.")]
        [SerializeField, Min(0f)] private float _unlockSparkReach = 190f;
        [Tooltip("How big the plate leaps at the start of the unlock moment (1 = no leap).")]
        [SerializeField, Range(1f, 2f)] private float _unlockLeap = 1.45f;

        // The unlock moment (the user's pick, 2026-11-11): the dish warming up, and when it began.
        private string _warming;
        private float _warmStart;
        // What the warm-up animates, registered as the tab is drawn.
        private readonly List<(Image Image, float Leap)> _warmPlates = new List<(Image, float)>();
        private readonly List<Image> _warmFlashes = new List<Image>();
        private readonly List<TextMeshProUGUI> _warmLabels = new List<TextMeshProUGUI>();
        private readonly List<(Image Image, Vector2 From, float Angle, float Size)> _sparks = new List<(Image, Vector2, float, float)>();
        private readonly List<(Image Image, float Size)> _rings = new List<(Image, float)>();
        private RectTransform _stamp;

        // A dish still to unlock: faded, a little grey, cold.
        private static readonly Color ColdTint = new Color(0.78f, 0.74f, 0.68f, 0.7f);

        private KitchenAudio _audio;
        private KitchenAudio Audio => _audio != null ? _audio : _audio = FindFirstObjectByType<KitchenAudio>();

        // Sounds from the dashboard come from the middle of the screen.
        private void PlaySound(SoundCue cue, float pitch = 1f)
        {
            if (Audio != null && cue != null && Camera.main != null)
                Audio.Play(cue, Camera.main.transform.position, pitch);
        }

        /// <summary>Whether a dish just unlocked is still warming up, for tests.</summary>
        public bool IsWarmingUp => _warming != null;

        /// <summary>Starts the unlock moment for a dish just unlocked: a sizzle, then the till.</summary>
        private void WarmUp(string recipeId)
        {
            _warming = recipeId;
            _warmStart = Time.unscaledTime;
            if (Audio != null)
            {
                PlaySound(Audio.Sounds.SizzleHit);
                PlaySound(Audio.Sounds.Till, 1.15f);
            }
        }

        // 0..1 through the warm-up of this dish; 1 for any other dish, or none.
        private float Warmth(string recipeId) =>
            _warming == null || recipeId != _warming ? 1f
            : _warmFrozenAt >= 0f ? _warmFrozenAt
            : Mathf.Clamp01((Time.unscaledTime - _warmStart) / _unlockWarmSeconds);

        // For a still: the unlock moment held at one point (-1 runs it as usual).
        private float _warmFrozenAt = -1f;

        /// <summary>For the capture test: shows the unlock moment of a dish, held <paramref name="progress"/> of the way through.</summary>
        public void HoldUnlockForCapture(string recipeId, float progress)
        {
            _warming = recipeId;
            _warmFrozenAt = Mathf.Clamp(progress, 0f, 0.99f);
            Refresh();
            AnimateWarmUp();
        }

        private sealed class Wisp
        {
            public Image Image;
            public Vector2 Base;   // where it rises from (top-left frame)
            public float Rise;     // how far it rises
            public float Size;     // its size at the start
            public float Phase;    // 0..1 along its life at time zero
            public bool Warming;   // fades in with the unlock moment
        }

        private void DrawCookbook()
        {
            _dial.Clear();
            _ticks.Clear();
            _effects.Clear();
            _suggestionMark = _dialWell = _dialSnapGlow = null;
            _steam.Clear();
            _warmPlates.Clear();
            _warmFlashes.Clear();
            _warmLabels.Clear();
            _sparks.Clear();
            _rings.Clear();
            _stamp = null;
            var opening = Services.Opening;
            var listTop = 262f;
            if (opening != null)
            {
                DrawOpeningCard(opening);
                listTop = OpeningTop + OpeningHeight + 16f;
            }

            var book = Services.Cookbook;
            if (book == null || book.Recipes.Count == 0)
            {
                Label(Serif, _content, Tr.Get("No cookbook here."), 26.67f, _muted, RightX, listTop + 58f);
                if (opening != null)
                    DrawOpeningDetail(opening);
                return;
            }
            var listed = book.OnMenuFirst();
            var owned = listed.Count(r => book.IsUnlocked(r.Id));
            var chosen = Mathf.Clamp(Selected, 0, listed.Count - 1);

            // Each group opens with its heading: rows are as tall as their card, plus a heading if
            // they start a group. Scrolled only as far as the chosen dish needs (ScrollWindow).
            bool StartsGroup(int i) => i == 0 || i == owned;
            var heights = Enumerable.Range(0, listed.Count).Select(i => RecipeRow + 12f + (StartsGroup(i) ? GroupHeader : 0f)).ToList();
            var first = _recipeFirst = ScrollWindow.First(_recipeFirst, chosen, heights, FooterTop - 8f - listTop);
            var y = listTop;
            for (var i = first; i < listed.Count && y + RecipeRow <= FooterTop - 8f; i++)
            {
                if (StartsGroup(i))
                {
                    var onMenu = i < owned;
                    var count = onMenu ? owned : listed.Count - owned;
                    Caps(_content, (onMenu ? Tr.Get("ON YOUR MENU") : Tr.Get("TO UNLOCK")) + $" · {count}", RightX + 4f, y + 20f, onMenu ? _goodText : _muted);
                    y += GroupHeader;
                    if (y + RecipeRow > FooterTop - 8f)
                        break;
                }
                RecipeCard(book, listed[i], y, i == chosen && !_restaurant.OnChips);
                y += RecipeRow + 12f;
            }

            if (opening != null && _restaurant.OnChips)
                DrawOpeningDetail(opening);
            else
                DrawRecipe(book, listed[chosen]);
        }

        // ---- The menu ----

        // A dish on the menu is a bright card with its plate steaming and its price; one still to
        // unlock is a darker card, its plate cold behind a padlock, its unlock price on a dark tag.
        private void RecipeCard(RecipeBook book, Recipe recipe, float y, bool chosen)
        {
            var on = book.IsUnlocked(recipe.Id);
            var warmingUp = on && Warmth(recipe.Id) < 1f;
            if (chosen)
                UiKit.RoundBox(_content, "Highlight", _gold, RightX - 5.67f, y - 5.67f, RecipeListWidth + 11.33f, RecipeRow + 11.33f, 20f);
            UiKit.StrokedBox(_content, "Recipe", on ? _cardLight : _lockedCard, chosen ? _gold : on ? _cardEdge : Alpha(_sand, 0.6f), 2.67f, RightX, y, RecipeListWidth, RecipeRow, 15f);
            // Just unlocked: the card lights up gold behind its plate and words.
            if (warmingUp)
                _warmFlashes.Add(UiKit.RoundBox(_content, "UnlockFlash", Alpha(_gold, 0f), RightX + 3f, y + 3f, RecipeListWidth - 6f, RecipeRow - 6f, 13f));

            // The plate (its picture has room above for steam, so it's drawn a little high).
            const float plate = 104f;
            DishPicture(recipe, RightX + 2f, y - 18f, plate, on, steam: on && chosen);
            if (!on)
                Padlock(RightX + 70f, y + 52f, 30f);

            var textX = RightX + 108f;
            var right = RightX + RecipeListWidth - 18f;
            var name = Label(Bold, _content, Tr.Get(recipe.Name), 23f, on ? _ink : _body, textX, y + 38f, 400f);
            if (on)
            {
                // The price it sells at, big on the right; under the name, that it's on.
                var price = Label(ExtraBold, _content, PriceText(book.PriceOf(recipe.Id)), 28f, _ink, right, y + 58f, 120f, HorizontalAlignmentOptions.Right, -0.6f);
                Fit(name, right - UiKit.Width(price) - 14f - textX, 16f);
                UiKit.Ellipse(_content, "OnDot", _good, textX, y + 59f, 12f, 12f);
                var line = Label(SemiBold, _content, Tr.Get("On the menu"), 19.5f, _goodText, textX + 20f, y + 71f, 240f);
                if (warmingUp)
                {
                    // "On the menu" pops in from its left end.
                    line.rectTransform.pivot = new Vector2(0f, 0.5f);
                    line.rectTransform.anchoredPosition += new Vector2(0f, -line.rectTransform.sizeDelta.y / 2f);
                    _warmLabels.Add(line);
                }
            }
            else
            {
                // The unlock price on a dark tag; red when it's more than there is.
                var affordable = Services.Days.Balance >= recipe.UnlockCost;
                var tagLabel = Label(ExtraBold, _content, Money(recipe.UnlockCost), 21.33f, affordable ? _paper : _alarm, 0f, y + 66f, 120f);
                var tagWidth = UiKit.Width(tagLabel) + 46f;
                var tag = UiKit.RoundBox(_content, "PriceTag", affordable ? _ink : _alarmDark, right - tagWidth, y + 42f, tagWidth, 34f, 17f);
                tag.transform.SetSiblingIndex(tagLabel.transform.GetSiblingIndex());
                UiKit.Picture(_content, "TagLock", PadlockSprite, right - tagWidth + 10f, y + 49f, 20f, 20f, affordable ? _gold : _alarm);
                tagLabel.rectTransform.anchoredPosition = new Vector2(right - tagWidth + 34f, tagLabel.rectTransform.anchoredPosition.y);
                Fit(name, right - textX - 8f, 16f);
                ComplexityDots(recipe.Complexity, textX, y + 58f);
                Label(Medium, _content, ComplexityWord(recipe), 18.67f, _muted, textX + 66f, y + 71f, 180f);
            }
        }

        private static string ComplexityWord(Recipe recipe) =>
            recipe.Complexity <= 1 ? Tr.Get("simple") : recipe.Complexity == 2 ? Tr.Get("some work") : Tr.Get("demanding");

        // Shrinks a one-line label until it fits the width, down to a smallest size.
        private static void Fit(TextMeshProUGUI label, float width, float smallest)
        {
            var natural = UiKit.Width(label);
            if (natural > width)
                label.fontSize = Mathf.Max(smallest, label.fontSize * width / natural);
        }

        private void ComplexityDots(int complexity, float x, float y)
        {
            for (var i = 0; i < 3; i++)
                UiKit.Ellipse(_content, "Complexity", i < complexity ? _gold : _rule, x + i * 20f, y, 14f, 14f);
        }

        // A padlock on a dark round, centred at (cx, cy).
        private void Padlock(float cx, float cy, float size)
        {
            UiKit.Ellipse(_content, "LockBack", _ink, cx - size / 2f, cy - size / 2f, size, size);
            UiKit.Picture(_content, "Lock", PadlockSprite, cx - size * 0.32f, cy - size * 0.34f, size * 0.64f, size * 0.64f, _gold);
        }

        // A dish's plate, square, its top-left at (x, y). Without a rendered picture, its main
        // ingredient's icon stands in. Steam rises from it when asked.
        private void DishPicture(Recipe recipe, float x, float y, float size, bool on, bool steam)
        {
            var picture = _style.Dish(recipe.Id);
            var warmth = on ? Warmth(recipe.Id) : 0f;
            var tint = Color.Lerp(ColdTint, Color.white, warmth);
            Image image = null;
            if (picture != null)
                image = UiKit.Picture(_content, "Dish", picture, x, y, size, size, tint);
            else if (_icons != null && _icons.Find(recipe.MainTypeId) is Sprite icon)
                image = UiKit.Picture(_content, "Dish", icon, x + size * 0.15f, y + size * 0.15f, size * 0.7f, size * 0.7f, tint);
            var warmingUp = on && warmth < 1f;
            if (warmingUp && image != null)
            {
                // Leaps about its middle: the pivot moves there and the position follows.
                var rect = image.rectTransform;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition += new Vector2(rect.sizeDelta.x / 2f, -rect.sizeDelta.y / 2f);
                _warmPlates.Add((image, size > 120f ? _unlockLeap : 1f + (_unlockLeap - 1f) * 0.5f));
            }
            // While it warms up, its steam starts (the chosen card's and the big plate's alike).
            if (steam || warmingUp)
                Steam(x + size / 2f, y + size * 0.5f, size * 0.6f, size * 0.24f, size > 120f ? 6 : 3, warmingUp);
            // The big plate throws gold sparks as it comes on.
            if (warmingUp && size > 120f)
                Sparks(x + size / 2f, y + size * 0.62f, 26);
        }

        // The chosen dish: whether it's on the menu, its plate, its ingredients as chips, how it's
        // cooked step by step, and then its price on a dial, or what unlocking it costs.
        private void DrawRecipe(RecipeBook book, Recipe recipe)
        {
            const float width = RightEdge - DetailX, top = DetailTop;
            const float x = DetailX + 28f;
            var inner = width - 56f;
            var on = book.IsUnlocked(recipe.Id);
            var warmingUp = on && Warmth(recipe.Id) < 1f;
            UiKit.RoundBox(_content, "Detail", on ? _card : Color.Lerp(_card, _lockedCard, 0.7f), DetailX, top, width, DetailBottom - top, 18f);

            // The plate big, pushed into the panel's top-right corner (the user's calls, 2026-11-11).
            // The picture has clear margins round the plate (its plate spans about 18–81% across
            // and 31–78% down, room left above for steam): placed so the plate itself sits about
            // 22 px in from the corner. Its steam may rise past the card's top.
            const float hero = 300f;
            var heroX = RightEdge - 22f - hero * 0.81f;
            var heroY = top + 22f - hero * 0.31f;
            var plateLeft = heroX + hero * 0.18f;

            // Where it stands, first: a green pill on the menu, a dark one with a padlock if not.
            if (on)
                StatusPill(warmingUp ? Tr.Get("NEW ON THE MENU") : Tr.Get("ON YOUR MENU"), warmingUp ? _gold : _good, warmingUp ? _ink : _paper, x, top + 22f, false);
            else
                StatusPill(Tr.Get("NOT ON YOUR MENU YET"), _ink, _paper, x, top + 22f, true);

            var title = Label(ExtraBold, _content, Tr.Get(recipe.Name), 34f, on ? _ink : _body, x, top + 92f, 600f, tracking: -0.8f);
            Fit(title, plateLeft - 12f - x, 22f);
            ComplexityDots(recipe.Complexity, x, top + 106f);
            Label(Medium, _content, ComplexityWord(recipe), 20f, _body, x + 70f, top + 120f, 200f);

            // Behind the plate while it comes on: a glow and a ring bursting out.
            if (warmingUp)
                Burst(heroX + hero / 2f, heroY + hero * 0.62f, hero);
            DishPicture(recipe, heroX, heroY, hero, on, steam: on);
            if (!on)
                Padlock(heroX + hero * 0.72f, heroY + hero * 0.78f, 56f);
            if (warmingUp)
                Stamp(heroX + hero / 2f, heroY + hero * 0.9f);

            // The ingredients as chips, what they cost a serving under them.
            Caps(_content, Tr.Get("INGREDIENTS"), x, top + 162f);
            var ix = x;
            for (var i = 0; i < recipe.IngredientTypeIds.Count; i++)
                ix = IngredientChip(recipe.IngredientTypeIds[i], Tr.Get(recipe.Ingredients[i]), ix, top + 174f) + 8f;
            // What a serving's ingredients cost, named plainly (the user's call, 2026-11-11).
            Label(Medium, _content, Tr.Format("Kitchen costs {cost}", ("cost", Money(recipe.IngredientCost))), 17.33f, _body, x, top + 252f, plateLeft - x);

            DrawSteps(recipe, on, x, top + 290f, inner);
            UiKit.RoundBox(_content, "Rule", _rule, x, top + PriceTop - 8f, inner, 2f, 0f);

            if (on)
                DrawPrice(book, recipe, x, inner, top);
            else
                DrawUnlock(recipe, x, inner, top);
        }

        // A pill with words (and a padlock, if asked), its left edge at x and its top at y.
        private void StatusPill(string text, Color back, Color fore, float x, float y, bool locked)
        {
            var label = Label(Bold, _content, text, 15f, fore, 0f, y + 20f, 400f, tracking: 1.2f);
            var lead = locked ? 30f : 14f;
            var widthOf = UiKit.Width(label) + lead + 14f;
            var pill = UiKit.RoundBox(_content, "Status", back, x, y, widthOf, 28f, 14f);
            pill.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            if (locked)
                UiKit.Picture(_content, "StatusLock", PadlockSprite, x + 9f, y + 5f, 17f, 17f, _gold);
            label.rectTransform.anchoredPosition = new Vector2(x + lead, label.rectTransform.anchoredPosition.y);
        }

        // HOW TO COOK: a numbered row per step, its station on a chip, then what to do. A dish on the
        // menu has gold numbers; one still to learn, quiet ones.
        private void DrawSteps(Recipe recipe, bool on, float x, float top, float width)
        {
            Caps(_content, Tr.Get("HOW TO COOK"), x, top);
            var steps = recipe.Steps;
            if (steps.Count == 0)
            {
                Label(Serif, _content, Tr.Get("Learn it in the kitchen."), 20f, _muted, x, top + 36f, width);
                return;
            }
            // Room for six steps (the user's call, 2026-11-11): 30 px each then, up to 34 with fewer.
            const float row = 34f;
            var pitch = Mathf.Min(row, 168f / Mathf.Max(1, steps.Count));
            for (var i = 0; i < steps.Count; i++)
            {
                var y = top + 14f + i * pitch;
                UiKit.Ellipse(_content, "StepNumber", on ? _gold : _sand, x, y, 26f, 26f);
                Label(ExtraBold, _content, $"{i + 1}", 15f, _ink, x + 13f, y + 19f, 26f, HorizontalAlignmentOptions.Center);
                var station = Label(Bold, _content, Tr.Get(steps[i].Station), 15f, _body, 0f, y + 19f, 140f);
                var chipWidth = UiKit.Width(station) + 18f;
                var chip = UiKit.RoundBox(_content, "Station", _paper, x + 34f, y + 1f, chipWidth, 24f, 12f);
                chip.transform.SetSiblingIndex(station.transform.GetSiblingIndex());
                station.rectTransform.anchoredPosition = new Vector2(x + 43f, station.rectTransform.anchoredPosition.y);
                var text = Label(Medium, _content, Tr.Get(steps[i].Text), 18.67f, on ? _ink : _body, x + 34f + chipWidth + 10f, y + 20f, 400f);
                Fit(text, width - (34f + chipWidth + 10f), 14f);
            }
        }

        // An ingredient as a chip: its picture on a light round, then its name. Returns its right edge.
        private float IngredientChip(string typeId, string name, float x, float y)
        {
            const float height = 54f;
            var icon = _icons != null ? _icons.Find(typeId) : null;
            var label = Label(Bold, _content, name, 20f, _ink, 0f, y + 34f, 260f);
            var width = 8f + (icon != null ? 46f : 10f) + UiKit.Width(label) + 18f;
            var box = UiKit.StrokedBox(_content, "Ingredient", _cardLight, _cardEdge, 2f, x, y, width, height, height / 2f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            if (icon != null)
            {
                UiKit.Ellipse(_content, "IconBack", _card, x + 6f, y + 6f, 42f, 42f);
                UiKit.Picture(_content, "Icon", icon, x + 5f, y + 5f, 44f, 44f);
            }
            label.transform.SetAsLastSibling();
            label.rectTransform.anchoredPosition = new Vector2(x + 8f + (icon != null ? 46f : 10f), label.rectTransform.anchoredPosition.y);
            return x + width;
        }

        // Where the price area starts, under the steps (room for six, 2026-11-11).
        // Low enough for six steps over it, high enough that the chips under the dial sit as far
        // from the panel's bottom as the panel's sides are (the user's note, 2026-11-11).
        private const float PriceTop = 488f;

        // The price (the user's calls, 2026-11-11): the heading with the exact price; a dial of whole
        // numbers whose needle moves in steps of 10 cents, so a price can sit between two numbers
        // ($24.30 is three tenths of the way from 24 to 25); then what the price does, as chips that
        // fit their words, with X back to the suggestion at the row's far right once it's moved.
        private void DrawPrice(RecipeBook book, Recipe recipe, float x, float inner, float top)
        {
            var price = book.PriceOf(recipe.Id);
            var y0 = top + PriceTop;
            var heading = Caps(_content, Tr.Get("PRICE"), x, y0 + 22f);
            Label(ExtraBold, _content, PriceText(price), 22f, _ink, x + UiKit.Width(heading) + 12f, y0 + 24f, 200f, tracking: -0.3f);

            // A new dish starts still; a new price on the same dish springs from where the old one
            // stood. The dial's unit is a whole number, so a 10-cent step moves it a tenth of a gap.
            if (_dialRecipe != recipe.Id)
            {
                _dialRecipe = recipe.Id;
                _dialOffset = _dialVelocity = _dialKick = 0f;
            }
            else if (!float.IsNaN(_dialPrice) && Mathf.Abs(price - _dialPrice) > 0.001f)
            {
                _dialOffset = Mathf.Clamp(_dialOffset + (price - _dialPrice), -2.5f, 2.5f);
                _dialKick = 1f;
                // Crossing a whole number is a detent: a full tick. A step between them is a lighter, higher one.
                var crossed = Mathf.FloorToInt(price + 0.001f) != Mathf.FloorToInt(_dialPrice + 0.001f);
                PlaySound(Audio != null ? Audio.Sounds.Clack : null, crossed ? _dialTickPitch : _dialTickPitch * 1.45f);
                // Landing on the suggestion clicks into place: a chime and a gold flash.
                if (Mathf.Abs(price - recipe.SuggestedPrice) < 0.001f)
                {
                    _dialSnap = 1f;
                    PlaySound(Audio != null ? Audio.Sounds.Chime : null, 1.4f);
                }
            }
            _dialPrice = price;
            _dialSuggested = recipe.SuggestedPrice - price;

            _dialCentreX = x + inner / 2f;
            _dialBaseline = y0 + 80f;
            // The window the numbers roll in: a well with the needle over the middle, a ruler of ticks
            // along its foot (whole numbers and halves) that slides with them, and the suggestion's star.
            _dialWell = UiKit.RoundBox(_content, "DialWell", Alpha(_cardLight, 0.9f), x, y0 + 40f, inner, 64f, 22f);
            _dialSnapGlow = UiKit.RoundBox(_content, "DialSnap", Alpha(_gold, 0f), x, y0 + 40f, inner, 64f, 22f);
            var whole = Mathf.FloorToInt(price + 0.001f);
            _ticks.Clear();
            for (var n = whole - 3; n <= whole + 4; n++)
            {
                for (var half = 0; half < 2; half++)
                {
                    var major = half == 0;
                    var tick = UiKit.RoundBox(_content, "Tick", _sand, 0f, y0 + (major ? 89f : 93f), major ? 3f : 2f, major ? 12f : 8f, 1f);
                    tick.rectTransform.pivot = new Vector2(0.5f, 1f);
                    _ticks.Add((tick, n + half * 0.5f - price));
                }
            }
            _suggestionMark = UiKit.Picture(_content, "SuggestedMark", StarSprite, 0f, y0 + 86f, 16f, 16f, _gold);
            _suggestionMark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // Only whole numbers on the drum, so it stays calm; the needle shows the tenths.
            for (var n = whole - 3; n <= whole + 4; n++)
            {
                if (n < book.Pricing.MinPrice - 0.001f)
                    continue;
                var label = Label(ExtraBold, _content, DialNumber(n), DialSize, _ink, _dialCentreX, _dialBaseline, 220f, HorizontalAlignmentOptions.Center, -1.2f);
                // Scaled about its middle; TextAt's box is centred on the baseline's height.
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _dial.Add((label, n - price));
            }
            // The needle, over everything: a gold line down the middle with a cap on top.
            UiKit.RoundBox(_content, "DialNeedle", Alpha(_gold, 0.9f), _dialCentreX - 1.5f, y0 + 44f, 3f, 56f, 1.5f);
            UiKit.RoundBox(_content, "DialNotch", _gold, _dialCentreX - 5f, y0 + 38f, 10f, 10f, 5f);
            PlaceDial();

            // What this price does (the user's calls, 2026-11-11): guests (0% when unchanged) and the
            // profit on each serving, as chips that fit their words, side by side.
            var demand = book.BuildMenu().Demand;
            var change = Mathf.RoundToInt((demand - 1f) * 100f);
            var earns = price - recipe.IngredientCost;
            const float gap = 10f, chipHeight = 44f;
            var chipTop = y0 + 112f;
            _effects.Clear();
            var cx = Effect(x, chipTop, chipHeight, GuestSprite, _body,
                change > 0 ? $"+{change}%" : change < 0 ? $"−{-change}%" : "0%", Tr.Get("guests"),
                change > 0 ? _goodText : change < 0 ? _badText : _ink);
            Effect(cx + gap, chipTop, chipHeight, null, _gold,
                // With its cents, so it moves with every 10-cent step of the price (2026-11-12).
                PriceText(earns), Tr.Get("Profit"), earns > 0f ? _goodText : _badText);
            // Off the suggestion, X takes it back: a small badge (X and the suggestion's star) at the far right.
            if (Mathf.Abs(price - recipe.SuggestedPrice) > 0.001f)
                SuggestionBadge(x + inner, chipTop + 3f);
        }

        // One consequence of the price, as wide as its words: an icon, the value big, a word small.
        // It punches as the price moves. Returns its right edge.
        private float Effect(float x, float y, float height, Sprite icon, Color iconColour, string value, string word, Color valueColour)
        {
            var group = UiKit.Rect(_content, "Effect", x, y, 10f, height);
            var label = Label(ExtraBold, group, value, 22f, valueColour, 44f, height / 2f + 8f, 200f, tracking: -0.3f);
            var valueWidth = UiKit.Width(label);
            var caption = Label(SemiBold, group, word, 13.33f, _muted, 44f + valueWidth + 6f, height / 2f + 7f, 120f);
            var width = 44f + valueWidth + 6f + UiKit.Width(caption) + 16f;
            var back = UiKit.StrokedBox(group, "EffectBack", _cardLight, _cardEdge, 2f, 0f, 0f, width, height, height / 2f);
            back.transform.parent.GetChild(back.transform.GetSiblingIndex() - 1).SetAsFirstSibling();
            back.transform.SetSiblingIndex(1);
            if (icon != null)
            {
                UiKit.Picture(group, "EffectIcon", icon, 12f, (height - 24f) / 2f, 24f, 24f, iconColour);
            }
            else
            {
                // The money chip's coin.
                UiKit.StrokedCircle(group, "Coin", Color.Lerp(_style.MoneyEdge, Color.white, 0.25f), Color.Lerp(_style.MoneyEdge, Color.black, 0.3f), 2.5f, 12f, (height - 24f) / 2f, 24f);
                UiKit.Ellipse(group, "CoinFace", Color.Lerp(_style.MoneyEdge, Color.black, 0.15f), 18f, (height - 24f) / 2f + 6f, 12f, 12f);
            }
            label.transform.SetAsLastSibling();
            caption.transform.SetAsLastSibling();
            // Punches about its middle.
            group.sizeDelta = new Vector2(width, height);
            group.pivot = new Vector2(0.5f, 0.5f);
            group.anchoredPosition += new Vector2(width / 2f, -height / 2f);
            _effects.Add(group);
            return x + width;
        }

        private void DrawUnlock(Recipe recipe, float x, float inner, float top)
        {
            _dialRecipe = null;
            _dialPrice = float.NaN;
            var y0 = top + PriceTop;
            var affordable = Services.Days.Balance >= recipe.UnlockCost;
            Caps(_content, Tr.Get("UNLOCK"), x, y0 + 22f);
            UiKit.Picture(_content, "UnlockLock", PadlockSprite, x, y0 + 44f, 38f, 38f, affordable ? _ink : _badText);
            Label(ExtraBold, _content, Money(recipe.UnlockCost), 54f, affordable ? _ink : _badText, x + 46f, y0 + 84f, 220f, tracking: -1.5f);
            // A (a tap) unlocks: shown beside the price, as the rows show theirs.
            Chip(_content, "A", Tr.Get("Unlock"), x + 280f, y0 + 56f, fromLeft: true);
            Label(Medium, _content, Tr.Format("You have {amount}", ("amount", Money(Services.Days.Balance))), 19.5f, affordable ? _body : _badText, x + 280f, y0 + 46f, 230f);
            Label(Medium, _content, Tr.Format("suggested {price}", ("price", Money(recipe.SuggestedPrice))) + " · " +
                Tr.Format("earns about {value}", ("value", Money(recipe.SuggestedPrice - recipe.IngredientCost))), 19.5f, _ink, x, y0 + 134f, inner);
            if (_unlockResult == UnlockResult.CantAfford)
                Label(Bold, _content, Tr.Get("not enough money"), 19.5f, _badText, x, y0 + 162f, inner);
        }

        // The dial's numbers where the spring has them: the nearest to the middle biggest and
        // darkest, the rest shrinking and fading either side, so it reads as a wheel seen edge on.
        // The ruler's ticks and the suggestion's star ride along with them.
        private void PlaceDial()
        {
            foreach (var (label, step) in _dial)
            {
                if (label == null)
                    continue;
                // A higher price brings the higher numbers in from the right (the offset is how far
                // it still has to roll, in whole numbers).
                var at = step + _dialOffset;
                var distance = Mathf.Abs(at);
                // Five numbers read (the user's call, 2026-11-11): the middle, then two either side,
                // smaller and lighter; the third ones out only fade in and out at the edges.
                // Every number the same size; only how strongly it shows changes (the user's call,
                // 2026-11-11). The middle one punches up as it lands.
                var scale = 1f + _dialPunch * _dialKick * Mathf.Clamp01(1f - distance);
                var alpha = (distance <= 1f ? Mathf.Lerp(1f, 0.42f, distance) : Mathf.Lerp(0.42f, 0.2f, Mathf.Clamp01(distance - 1f)))
                    * Mathf.Clamp01((2.5f - distance) * 2.5f);
                // The whole number the price is on (24 for 24.0 to 24.9) shows at full strength, wherever
                // the needle sits between it and the next (the user's call, 2026-11-12). It follows the
                // needle as it rolls: the price under the needle is the price less what's still to roll.
                if (!float.IsNaN(_dialPrice) && Mathf.Abs(step + _dialPrice - Mathf.Floor(_dialPrice - _dialOffset + 0.001f)) < 0.01f)
                    alpha = 1f;
                // Every number's middle on one line: about a third of its size above the baseline,
                // scaled about the box's middle, which sits on the baseline.
                var middle = DialSize * 0.35f;
                label.rectTransform.anchoredPosition = new Vector2(DialX(at), -(_dialBaseline - middle + middle * scale));
                label.rectTransform.localScale = new Vector3(scale, scale, 1f);
                // On the suggestion, the middle number turns gold as it clicks in.
                var colour = Color.Lerp(_ink, Color.Lerp(_gold, _ink, 0.35f), _dialSnap * Mathf.Clamp01(1f - distance * 2f));
                label.color = Alpha(colour, alpha);
            }
            foreach (var (tick, at) in _ticks)
            {
                if (tick == null)
                    continue;
                var position = at + _dialOffset;
                var x = DialX(position);
                tick.rectTransform.anchoredPosition = new Vector2(x, tick.rectTransform.anchoredPosition.y);
                tick.color = Alpha(_sand, Mathf.Clamp01((2.5f - Mathf.Abs(position)) * 2.5f) * 0.9f);
            }
            if (_suggestionMark != null)
            {
                var position = _dialSuggested + _dialOffset;
                _suggestionMark.rectTransform.anchoredPosition = new Vector2(DialX(position), _suggestionMark.rectTransform.anchoredPosition.y);
                _suggestionMark.color = Alpha(_gold, Mathf.Clamp01((2.5f - Mathf.Abs(position)) * 2.5f));
                var s = 1f + 0.6f * _dialSnap;
                _suggestionMark.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            if (_dialSnapGlow != null)
                _dialSnapGlow.color = Alpha(_gold, 0.35f * _dialSnap);
            foreach (var effect in _effects)
            {
                if (effect == null)
                    continue;
                var punch = 1f + 0.08f * _dialKick;
                effect.localScale = new Vector3(punch, punch, 1f);
            }
        }

        private const float DialSize = 40f;

        // A number on the dial without its currency and without a trailing zero: "25", "24.5"
        // (the user's call, 2026-11-11), with the language's own separators ("24,5" in Spanish).
        private static string DialNumber(float value)
        {
            var text = Locale.Money(value, MoneyStyle.Auto).Replace(Locale.CurrencySymbol, "").Trim();
            // "24.50" → "24.5"; whole amounts already have no decimals.
            return text.Length > 3 && text.EndsWith("0") && !char.IsDigit(text[text.Length - 3]) ? text.Substring(0, text.Length - 1) : text;
        }

        // A small badge for X: back to the suggested price, drawn as the suggestion's star. Right edge at right.
        private void SuggestionBadge(float right, float top)
        {
            // The X glyph is 30 px: the badge is sized round it.
            const float height = 38f, width = 72f;
            var x = right - width;
            UiKit.StrokedBox(_content, "SuggestionBadge", Alpha(_cardLight, 0.8f), _cardEdge, 2f, x, top, width, height, height / 2f);
            ChipKey(_content, "X", x + 5f, top + (height - 30f) / 2f, 30f);
            UiKit.Picture(_content, "SuggestionStar", StarSprite, x + 42f, top + (height - 20f) / 2f, 20f, 20f, _gold);
        }

        // Where on the dial a number sits, steps from the middle: the spacing closes up towards the
        // edges, as on a wheel seen edge on.
        private float DialX(float at)
        {
            var distance = Mathf.Abs(at);
            // Evenly spaced now that every number is the same size.
            return _dialCentreX + at * _dialSpacing;
        }

        // The price couldn't move that way (at its lowest or highest): the dial bumps and springs back.
        private void DialBump(int direction)
        {
            // A push the way it can't go: the drum shifts that way a little and springs back.
            _dialVelocity -= direction * 4f;
            PlaySound(Audio != null ? Audio.Sounds.Clack : null, _dialTickPitch * 0.6f);
        }

        // The dial's spring: a little under-damped, so each step lands with a small overshoot, like a detent.
        private void SpringDial(float dt)
        {
            var damping = 2f * Mathf.Sqrt(_dialStiffness) * _dialDamping;
            // Small steps, so a slow frame can't blow the spring up.
            var steps = Mathf.CeilToInt(dt / 0.008f);
            var h = dt / Mathf.Max(1, steps);
            for (var i = 0; i < steps; i++)
            {
                _dialVelocity += (-_dialStiffness * _dialOffset - damping * _dialVelocity) * h;
                _dialOffset += _dialVelocity * h;
            }
            _dialKick = Mathf.MoveTowards(_dialKick, 0f, dt * 5f);
            _dialSnap = Mathf.MoveTowards(_dialSnap, 0f, dt * 1.6f);
            if (Mathf.Abs(_dialOffset) < 0.002f && Mathf.Abs(_dialVelocity) < 0.01f)
                _dialOffset = _dialVelocity = 0f;
        }

        /// <summary>The seconds to the next price step while the D-pad is held: slow at first, quicker the longer it's held.</summary>
        private float PriceRepeat(float now) =>
            Mathf.Lerp(_priceRepeatSlow, _priceRepeatFastest, Mathf.Clamp01((now - _priceHeldSince) / _priceAccelerateSeconds));

        // ---- Steam ----

        private static Sprite _puff;

        // A soft round puff, made once: white, its edge fading to nothing.
        private static Sprite Puff
        {
            get
            {
                if (_puff != null)
                    return _puff;
                const int size = 48;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (var py = 0; py < size; py++)
                for (var px = 0; px < size; px++)
                {
                    var d = Vector2.Distance(new Vector2(px + 0.5f, py + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    var a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(px, py, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
                texture.Apply();
                _puff = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return _puff;
            }
        }

        // Wisps rising from (x, y), each on its own part of the cycle.
        private void Steam(float x, float y, float rise, float size, int wisps, bool warming = false)
        {
            for (var i = 0; i < wisps; i++)
            {
                var image = UiKit.Picture(_content, "Steam", Puff, x, y, size, size, Color.clear);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _steam.Add(new Wisp { Image = image, Base = new Vector2(x, y), Rise = rise, Size = size, Phase = (float)i / wisps, Warming = warming });
            }
            AnimateSteam();
        }

        // Each wisp rises, sways, swells and fades in and out over its life, then starts again.
        private void AnimateSteam()
        {
            var now = Time.unscaledTime;
            foreach (var wisp in _steam)
            {
                if (wisp.Image == null)
                    continue;
                var life = Mathf.Repeat(now / _steamSeconds + wisp.Phase, 1f);
                var sway = Mathf.Sin(now * 1.7f + wisp.Phase * 9f) * wisp.Size * 0.35f * life;
                var size = wisp.Size * Mathf.Lerp(0.7f, 1.9f, life);
                var rect = wisp.Image.rectTransform;
                rect.sizeDelta = new Vector2(size * 0.8f, size);
                // Centred on its point (pivot in the middle); the frame's y runs down, so rising is up.
                rect.anchoredPosition = new Vector2(wisp.Base.x + sway, -(wisp.Base.y - wisp.Rise * life));
                var fade = Mathf.Sin(Mathf.PI * life);
                var strength = wisp.Warming ? Mathf.Clamp01((Warmth(_warming) - 0.2f) / 0.6f) : 1f;
                wisp.Image.color = new Color(_steamColour.r, _steamColour.g, _steamColour.b, _steamColour.a * fade * fade * strength);
            }
        }

        // Gold sparks bursting from a point, each on its own heading.
        private void Sparks(float x, float y, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var size = 18f + (i * 7 % 5) * 5f;
                var image = UiKit.Picture(_content, "Spark", Puff, x, y, size, size, Alpha(_gold, 0f));
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                // All the way round, more of them upwards, as if thrown up from the plate.
                var angle = (i * 137.5f % 360f) * Mathf.Deg2Rad;
                if (Mathf.Sin(angle) > 0.3f && i % 2 == 0)
                    angle = -angle;
                _sparks.Add((image, new Vector2(x, y), angle, size));
            }
        }

        // Behind the big plate as it comes on: a warm glow and two rings bursting outwards.
        private void Burst(float cx, float cy, float size)
        {
            var glow = UiKit.Picture(_content, "Glow", Puff, cx, cy, size, size, Alpha(_gold, 0f));
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rings.Add((glow, size * 1.35f));
            // Kept within the panel: the plate sits near its right edge.
            for (var i = 0; i < 2; i++)
            {
                var ring = UiKit.Picture(_content, "Ring", i == 0 ? RingThick : RingThin, cx, cy, size, size, Alpha(_gold, 0f));
                ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _rings.Add((ring, size * (1.15f + i * 0.25f)));
            }
        }

        // "New on the menu!" on a gold label, slammed down across the plate.
        private void Stamp(float cx, float cy)
        {
            _stamp = UiKit.Rect(_content, "Stamp", cx, cy, 10f, 10f);
            _stamp.pivot = new Vector2(0.5f, 0.5f);
            // Children are placed from the stamp's top-left corner (5 px off its middle): close enough.
            var label = Label(ExtraBold, _stamp, Tr.Get("New on the menu!"), 24f, _ink, 0f, 9f, 400f, HorizontalAlignmentOptions.Center, -0.4f);
            var width = UiKit.Width(label) + 36f;
            UiKit.StrokedBox(_stamp, "StampBack", _gold, _ink, 3f, -width / 2f, -22f, width, 44f, 22f);
            // The words over the label's edge and fill.
            label.transform.SetAsLastSibling();
            _stamp.localRotation = Quaternion.Euler(0f, 0f, 7f);
            _stamp.localScale = Vector3.zero;
        }

        // The unlock moment, frame by frame: the plate leaps and settles as its colour comes back,
        // the glow and rings burst, sparks fly, the stamp slams down, the card flashes and its line
        // pops. Ends by drawing the tab as it stays.
        private void AnimateWarmUp()
        {
            if (_warming == null)
                return;
            var t = Warmth(_warming);
            var warm = 1f - (1f - t) * (1f - t);
            foreach (var (plate, leap) in _warmPlates)
            {
                if (plate == null)
                    continue;
                plate.color = Color.Lerp(ColdTint, Color.white, Mathf.Clamp01(t * 2.5f));
                // Up fast to the leap, then a springy settle back to its size.
                var up = Mathf.Clamp01(t / 0.16f);
                var settle = t < 0.16f ? 0f : (t - 0.16f) / 0.84f;
                var scale = t < 0.16f
                    ? Mathf.Lerp(1f, leap, 1f - (1f - up) * (1f - up))
                    : 1f + (leap - 1f) * Mathf.Exp(-6f * settle) * Mathf.Cos(settle * 14f);
                plate.rectTransform.localScale = new Vector3(scale, scale, 1f);
                plate.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Exp(-5f * t) * Mathf.Sin(t * 30f));
            }
            for (var i = 0; i < _rings.Count; i++)
            {
                var (image, size) = _rings[i];
                if (image == null)
                    continue;
                var grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t * (i == 0 ? 1.4f : 2.2f - i * 0.4f)), 3f);
                var s = Mathf.Lerp(size * 0.3f, size, grow);
                image.rectTransform.sizeDelta = new Vector2(s, s);
                image.color = Alpha(_gold, (i == 0 ? 0.7f : 0.9f) * (1f - grow));
            }
            foreach (var (image, from, angle, size) in _sparks)
            {
                if (image == null)
                    continue;
                var reach = _unlockSparkReach * (1f - Mathf.Pow(1f - Mathf.Clamp01(t * 1.3f), 3f));
                // They fly out and drop a little, as thrown sparks do.
                // Squeezed on the right, where the panel ends close to the plate.
                var heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (heading.x > 0f)
                    heading.x *= 0.45f;
                var at = from + heading * reach + new Vector2(0f, 90f * t * t);
                image.rectTransform.anchoredPosition = new Vector2(at.x, -at.y);
                var s = size * Mathf.Lerp(1.3f, 0.3f, t);
                image.rectTransform.sizeDelta = new Vector2(s, s);
                image.color = Alpha(Color.Lerp(_gold, Color.white, 0.35f), Mathf.Clamp01(t * 12f) * Mathf.Clamp01((1f - t) * 1.8f));
            }
            if (_stamp != null)
            {
                // Slams down from big between 10% and 25% of the way, holds, then fades at the end.
                var slam = Mathf.Clamp01((t - 0.1f) / 0.15f);
                var s = slam <= 0f ? 0f : Mathf.Lerp(2.4f, 1f, 1f - (1f - slam) * (1f - slam)) * (1f + 0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.25f) / 0.1f)));
                _stamp.localScale = new Vector3(s, s, 1f);
                foreach (var graphic in _stamp.GetComponentsInChildren<Graphic>())
                    graphic.canvasRenderer.SetAlpha(Mathf.Clamp01((1f - t) * 6f));
            }
            foreach (var flash in _warmFlashes)
            {
                if (flash != null)
                    flash.color = Alpha(_gold, 0.65f * (1f - warm) * Mathf.Clamp01(t * 8f));
            }
            foreach (var label in _warmLabels)
            {
                if (label == null)
                    continue;
                var pop = 1f + 0.35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 2.4f));
                label.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            if (t >= 1f)
            {
                _warming = null;
                _dirty = true;
            }
        }

        /// <summary>Every frame while the Restaurant tab shows: the steam rises, the dial springs, an unlock plays out.</summary>
        private void AnimateRestaurant(float dt)
        {
            if (_tab != Tab.Restaurant)
            {
                _warming = null;
                return;
            }
            AnimateWarmUp();
            if (_steam.Count > 0)
                AnimateSteam();
            if (_dial.Count > 0 && (Mathf.Abs(_dialOffset) > 0f || Mathf.Abs(_dialVelocity) > 0f || _dialKick > 0f || _dialSnap > 0f))
            {
                SpringDial(dt);
                PlaceDial();
            }
        }

        // ---- Opening times ----

        // The opening times as a card over the recipes: a tile per slot with its sun or moon, the
        // chosen one dark, a gold ring while the focus is on them; under them, the day as bars.
        private void DrawOpeningCard(OpeningTimes opening)
        {
            var focused = _restaurant.OnChips;
            if (focused)
                UiKit.RoundBox(_content, "Highlight", _gold, RightX - 5.67f, OpeningTop - 5.67f, RecipeListWidth + 11.33f, OpeningHeight + 11.33f, 20f);
            UiKit.StrokedBox(_content, "Opening", _cardLight, focused ? _gold : _cardEdge, 2.67f, RightX, OpeningTop, RecipeListWidth, OpeningHeight, 15f);
            Caps(_content, Tr.Get("OPENING TIMES"), RightX + 24f, OpeningTop + 32f);
            Label(SemiBold, _content, OpeningHours(opening.Slot), 17.33f, _muted, RightX + RecipeListWidth - 24f, OpeningTop + 32f, 200f, HorizontalAlignmentOptions.Right);

            const float gap = 10f, tileTop = OpeningTop + 46f, tileHeight = 76f;
            var count = opening.Slots.Count;
            var tileWidth = (RecipeListWidth - 48f - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++)
            {
                var x = RightX + 24f + i * (tileWidth + gap);
                var active = i == opening.Chosen;
                if (active && focused)
                    UiKit.RoundBox(_content, "Focus", _gold, x - 4f, tileTop - 4f, tileWidth + 8f, tileHeight + 8f, 18f);
                UiKit.RoundBox(_content, "Slot", active ? _ink : _card, x, tileTop, tileWidth, tileHeight, 14f);
                SlotIcon(opening.Slots[i], x + tileWidth / 2f, tileTop + 24f, 30f, active ? _gold : _body);
                var name = Label(Bold, _content, Tr.Get(opening.Slots[i].Name), 19.5f, active ? _paper : _ink, x + tileWidth / 2f, tileTop + 64f, tileWidth - 12f, HorizontalAlignmentOptions.Center);
                Fit(name, tileWidth - 16f, 14f);
            }

            DayStrip(opening, opening.Slot, RightX + 24f, OpeningTop + 136f, RecipeListWidth - 48f, 44f, true);
        }

        // The day from the earliest opening to the latest closing, as a bar per half hour: flat and
        // pale while closed, as tall as the guests coming while open, the rushes in gold. Labels under.
        private void DayStrip(OpeningTimes opening, OpeningSlot slot, float x, float y, float width, float height, bool small)
        {
            var from = opening.Slots.Min(s => s.Opens);
            var to = opening.Slots.Max(s => s.Closes);
            var bars = Mathf.Max(1, Mathf.RoundToInt((to - from) * 2f));
            var pitch = width / bars;
            var barWidth = Mathf.Max(3f, pitch - (small ? 3f : 5f));
            var rushes = opening.RushesIn(slot).ToList();
            for (var i = 0; i < bars; i++)
            {
                var hour = from + (i + 0.5f) / 2f;
                var open = hour >= slot.Opens && hour < slot.Closes;
                var rush = open && rushes.Any(r => hour >= r.From && hour < r.To);
                var h = !open ? 5f : rush ? height : height * 0.45f;
                var colour = !open ? _rule : rush ? _gold : _body;
                UiKit.RoundBox(_content, "Hour", colour, x + i * pitch, y + height - h, barWidth, h, Mathf.Min(barWidth, h) / 2f);
            }
            // Hours under it, every three (every two when big).
            var every = small ? 3 : 2;
            for (var hour = Mathf.CeilToInt(from); hour <= to; hour += every)
            {
                var hx = x + (hour - from) * 2f * pitch;
                // The end labels line up with the strip's ends rather than hanging past them
                // (the user's note, 2026-11-11); the rest are centred on their hour.
                var first = hour - from < 0.01f;
                var last = to - hour < 0.01f;
                var align = first ? HorizontalAlignmentOptions.Left : last ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Center;
                Label(SemiBold, _content, ServiceClock.Format(hour * 60f), small ? 14f : 16f, _muted, last ? x + width - (pitch - barWidth) : hx, y + height + (small ? 20f : 24f), 80f, align);
            }
        }

        // A sun for a slot that opens by day, a moon for one that closes at night; both for one that does both.
        private void SlotIcon(OpeningSlot slot, float cx, float cy, float size, Color colour)
        {
            var day = slot.Opens < 17f;
            var night = slot.Closes > 20f;
            if (day && night)
            {
                UiKit.Picture(_content, "Sun", SunSprite, cx - size * 0.95f, cy - size / 2f, size, size, colour);
                UiKit.Picture(_content, "Moon", MoonSprite, cx - size * 0.05f, cy - size / 2f, size, size, colour);
            }
            else
            {
                UiKit.Picture(_content, night ? "Moon" : "Sun", night ? MoonSprite : SunSprite, cx - size / 2f, cy - size / 2f, size, size, colour);
            }
        }

        // "18:00–00:00"
        private static string OpeningHours(OpeningSlot slot) =>
            $"{ServiceClock.Format(slot.Opens * 60f)}–{ServiceClock.Format(slot.Closes * 60f)}";

        // What the chosen opening times mean, in the detail panel while the focus is on them: the
        // day, how long service lasts for real, its rush hours and the guests to expect, as people.
        private void DrawOpeningDetail(OpeningTimes opening)
        {
            const float width = RightEdge - DetailX, top = DetailTop;
            const float x = DetailX + 28f;
            var inner = width - 56f;
            var slot = opening.Slot;
            UiKit.RoundBox(_content, "Detail", _card, DetailX, top, width, DetailBottom - top, 18f);
            SlotIcon(slot, RightEdge - 28f - 40f, top + 44f, 52f, _gold);
            Label(ExtraBold, _content, Tr.Get(slot.Name), 34f, _ink, x, top + 54f, inner - 110f, tracking: -0.8f);
            Label(ExtraBold, _content, OpeningHours(slot), 54f, _ink, x, top + 132f, inner, tracking: -1.5f);

            // Real minutes: an hour on the clock is SecondsPerHour in-game seconds, at TimeScale a real second.
            var realMinutes = slot.Hours * opening.SecondsPerHour / Mathf.Max(1f, Services.Tuning.TimeScale) / 60f;
            Label(Medium, _content, Tr.Format("Service lasts about {minutes} minutes", ("minutes", realMinutes.ToString("0.#"))), 21.33f, _body, x, top + 176f, inner);

            DayStrip(opening, slot, x, top + 206f, inner, 70f, false);

            Caps(_content, Tr.Get("RUSH HOURS"), x, top + 348f);
            var rushes = opening.RushesIn(slot).ToList();
            var rushText = rushes.Count == 0 ? Tr.Get("No rush: a steady trickle")
                : string.Join(" · ", rushes.Select(r => $"{ServiceClock.Format(r.From * 60f)}–{ServiceClock.Format(r.To * 60f)}"));
            Label(Bold, _content, rushText, 24f, _ink, x, top + 384f, inner);

            // The guests to expect, as little people: one figure each, up to three rows.
            Caps(_content, Tr.Get("AT THESE PRICES"), x, top + 444f);
            var guests = _root.ExpectedGuests(opening.Chosen);
            Label(Bold, _content, DayCardText.Guests(guests), 24f, _ink, x, top + 480f, inner);
            const float pitch = 26f;
            var perRow = Mathf.FloorToInt(inner / pitch);
            var shown = Mathf.Min(guests, perRow * 3);
            for (var i = 0; i < shown; i++)
            {
                var gx = x + (i % perRow) * pitch;
                var gy = top + 500f + (i / perRow) * 40f;
                var colour = UiKit.AvatarColour("guest" + i);
                UiKit.Ellipse(_content, "Head", colour, gx + 6f, gy, 12f, 12f);
                UiKit.RoundBox(_content, "Body", colour, gx + 2f, gy + 14f, 20f, 16f, 8f);
            }

            // When it counts: the service after this screen, today's in the morning, tomorrow's after a service.
            var when = Services.Days.Phase == DayPhase.Review ? Tr.Get("From tomorrow's service") : Tr.Get("From today's service");
            Label(SemiBold, _content, when, 21.33f, _goodText, x, DetailBottom - 30f, inner);
        }

        // ---- Drawn icons: a sun and a moon, made once, white to be tinted ----

        private static Sprite _sun, _moon, _padlock, _ringThick, _ringThin;

        private static Sprite _guest;

        // A person: a round head over rounded shoulders.
        private static Sprite GuestSprite => _guest ??= DrawIcon((p, size) =>
        {
            var u = p.x / size;
            var v = p.y / size;
            var head = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.7f)) < 0.2f;
            var body = v < 0.46f && v > 0.06f && Vector2.Distance(new Vector2(u, Mathf.Max(v, 0.2f)), new Vector2(0.5f, 0.2f)) < 0.36f;
            return head || body ? 1f : 0f;
        });

        // A padlock: a rounded body and the shackle's arch over it.
        private static Sprite PadlockSprite => _padlock ??= DrawIcon((p, size) =>
        {
            var u = p.x / size;
            var v = p.y / size;
            var body = u > 0.2f && u < 0.8f && v > 0.08f && v < 0.56f;
            var arch = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.56f));
            var shackle = v >= 0.5f && arch > 0.15f && arch < 0.26f;
            // A keyhole in the body.
            var hole = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.36f)) < 0.07f || (Mathf.Abs(u - 0.5f) < 0.03f && v > 0.2f && v < 0.36f);
            return (body && !hole) || shackle ? 1f : 0f;
        });

        // Rings for the unlock burst, drawn once: a thick one and a thin one.
        private static Sprite RingThick => _ringThick ??= DrawIcon((p, size) =>
        {
            var d = Vector2.Distance(p, new Vector2(size / 2f, size / 2f)) / (size / 2f);
            return d > 0.82f && d < 0.98f ? 1f : 0f;
        });

        private static Sprite RingThin => _ringThin ??= DrawIcon((p, size) =>
        {
            var d = Vector2.Distance(p, new Vector2(size / 2f, size / 2f)) / (size / 2f);
            return d > 0.92f && d < 0.98f ? 1f : 0f;
        });

        private static Sprite SunSprite => _sun ??= DrawIcon((p, size) =>
        {
            var centre = new Vector2(size / 2f, size / 2f);
            var d = Vector2.Distance(p, centre) / (size / 2f);
            if (d < 0.46f)
                return 1f;
            // Eight rays, each a short wedge outside the disc.
            var angle = Mathf.Atan2(p.y - centre.y, p.x - centre.x);
            var ray = Mathf.Abs(Mathf.Sin(angle * 4f));
            return d > 0.6f && d < 0.98f && ray < 0.28f ? 1f : 0f;
        });

        private static Sprite MoonSprite => _moon ??= DrawIcon((p, size) =>
        {
            var outer = Vector2.Distance(p, new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.44f);
            var bite = Vector2.Distance(p, new Vector2(size * 0.68f, size * 0.62f)) / (size * 0.36f);
            return outer < 1f && bite > 1f ? 1f : 0f;
        });

        // A white icon from a coverage rule, anti-aliased by sampling each pixel 16 times.
        private static Sprite DrawIcon(System.Func<Vector2, int, float> inside)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var py = 0; py < size; py++)
            for (var px = 0; px < size; px++)
            {
                var cover = 0f;
                for (var s = 0; s < 16; s++)
                    cover += inside(new Vector2(px + (s % 4 + 0.5f) / 4f, py + (s / 4 + 0.5f) / 4f), size);
                texture.SetPixel(px, py, new Color(1f, 1f, 1f, cover / 16f));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
