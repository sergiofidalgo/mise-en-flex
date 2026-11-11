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
        private const float RecipeListWidth = 470f, RecipeRow = 92f, DetailX = 1256f;
        private const float OpeningTop = 262f, OpeningHeight = 214f;
        private const float DetailTop = 262f, DetailBottom = 932f;
        private const float GroupHeader = 32f;

        [Header("Restaurant tab")]
        [Tooltip("Pixels between neighbouring numbers on the price dial.")]
        [SerializeField, Min(20f)] private float _dialSpacing = 118f;
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
        [Tooltip("Pitch of the dial's tick (the kitchen's clack).")]
        [SerializeField, Range(0.5f, 3f)] private float _dialTickPitch = 1.8f;
        [Tooltip("Seconds a wisp of steam takes to rise and fade.")]
        [SerializeField, Min(0.3f)] private float _steamSeconds = 2.6f;
        [Tooltip("Steam's colour at its thickest: a warm grey, so it shows on the cream panel as well as over the food.")]
        [SerializeField] private Color _steamColour = new Color(0.62f, 0.57f, 0.52f, 0.62f);
        [Tooltip("A card for a dish still to unlock: darker than the menu's, so the two groups never look alike.")]
        [SerializeField] private Color _lockedCard = Hex(0xe2d0ad);

        // The price dial: its numbers, where its middle is, and its spring (offset in steps, and speed).
        private readonly List<(TextMeshProUGUI Label, int Step)> _dial = new List<(TextMeshProUGUI, int)>();
        private float _dialCentreX, _dialBaseline;
        private float _dialOffset, _dialVelocity, _dialKick;
        private string _dialRecipe;
        private int _dialPrice = int.MinValue;
        private float _priceHeldSince;

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
                var price = Label(ExtraBold, _content, Money(book.PriceOf(recipe.Id)), 28f, _ink, right, y + 58f, 120f, HorizontalAlignmentOptions.Right, -0.6f);
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

            const float hero = 230f;
            var heroX = RightEdge - 6f - hero;
            const float heroY = top + 16f;

            // Where it stands, first: a green pill on the menu, a dark one with a padlock if not.
            if (on)
                StatusPill(warmingUp ? Tr.Get("NEW ON THE MENU") : Tr.Get("ON YOUR MENU"), warmingUp ? _gold : _good, warmingUp ? _ink : _paper, x, top + 22f, false);
            else
                StatusPill(Tr.Get("NOT ON YOUR MENU YET"), _ink, _paper, x, top + 22f, true);

            var title = Label(ExtraBold, _content, Tr.Get(recipe.Name), 34f, on ? _ink : _body, x, top + 92f, 600f, tracking: -0.8f);
            Fit(title, heroX + 24f - x, 22f);
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
            Label(Medium, _content, Tr.Format("about {cost} a serving", ("cost", Money(recipe.IngredientCost))), 17.33f, _body, x, top + 252f, heroX - x);

            DrawSteps(recipe, on, x, top + 290f, inner);
            UiKit.RoundBox(_content, "Rule", _rule, x, top + 452f, inner, 2f, 0f);

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
            const float row = 34f;
            var pitch = Mathf.Min(row, 140f / Mathf.Max(1, steps.Count));
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

        // The price as a dial, then the suggestion with X beside it, then what this price does.
        private void DrawPrice(RecipeBook book, Recipe recipe, float x, float inner, float top)
        {
            var price = book.PriceOf(recipe.Id);
            var ratio = book.RatioOf(recipe);
            Caps(_content, Tr.Get("PRICE"), x, top + 482f);

            // A new dish starts still; a new price on the same dish springs from where the old one stood.
            if (_dialRecipe != recipe.Id)
            {
                _dialRecipe = recipe.Id;
                _dialOffset = _dialVelocity = _dialKick = 0f;
            }
            else if (_dialPrice != int.MinValue && price != _dialPrice)
            {
                _dialOffset = Mathf.Clamp(_dialOffset + (price - _dialPrice), -2.5f, 2.5f);
                _dialKick = 1f;
                PlaySound(Audio != null ? Audio.Sounds.Clack : null, _dialTickPitch + (price > _dialPrice ? 0.08f : -0.08f));
            }
            _dialPrice = price;

            _dialCentreX = x + inner / 2f;
            _dialBaseline = top + 546f;
            // The window the numbers roll in: a well, darker at its ends, with a notch over the middle.
            UiKit.RoundBox(_content, "DialWell", Alpha(_cardLight, 0.85f), x, top + 492f, inner, 70f, 35f);
            UiKit.RoundBox(_content, "DialNotch", _gold, _dialCentreX - 26f, top + 492f, 52f, 5f, 2.5f);
            UiKit.RoundBox(_content, "DialPointer", _gold, _dialCentreX - 26f, top + 557f, 52f, 5f, 2.5f);
            for (var step = -3; step <= 3; step++)
            {
                if (price + step < 1)
                    continue;
                var label = Label(ExtraBold, _content, Money(price + step), 50f, _ink, _dialCentreX, _dialBaseline, 220f, HorizontalAlignmentOptions.Center, -1.2f);
                // Scaled about its middle; TextAt's box is centred on the baseline's height.
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _dial.Add((label, step));
            }
            Label(ExtraBold, _content, "‹", 40f, _muted, x + 16f, _dialBaseline - 4f, 30f);
            Label(ExtraBold, _content, "›", 40f, _muted, x + inner - 30f, _dialBaseline - 4f, 30f);
            PlaceDial();

            // The suggestion on its own line, X beside it when the price has moved off it.
            var suggested = Tr.Format("suggested {price}", ("price", Money(recipe.SuggestedPrice)));
            var line = Label(Medium, _content, suggested, 19.5f, _body, x, top + 598f, 260f);
            if (price != recipe.SuggestedPrice)
            {
                Label(Bold, _content, Percent(ratio), 19.5f, ratio > 1f ? _badText : _goodText, x + UiKit.Width(line) + 12f, top + 598f, 120f);
                Chip(_content, "X", Tr.Get("Suggested"), RightEdge - 28f, top + 570f);
            }

            // What this price does, in two short lines: guests and stars, then what it earns.
            var demand = book.BuildMenu().Demand;
            var change = Mathf.RoundToInt((demand - 1f) * 100f);
            var guests = change == 0 ? Tr.Get("Guests: as many as at the suggested prices")
                : change < 0 ? Tr.Format("Guests: about {percent}% fewer", ("percent", -change))
                : Tr.Format("Guests: about {percent}% more", ("percent", change));
            var shift = PriceResponse.Expectation(ratio, book.Pricing);
            var stars = Mathf.Abs(shift) < 0.05f ? Tr.Get("Stars: rated as the dish deserves")
                : shift < 0f ? Tr.Format("Stars: {stars} fewer for the same dish", ("stars", (-shift).ToString("0.#")))
                : Tr.Format("Stars: {stars} more for the same dish", ("stars", shift.ToString("0.#")));
            var guestsLabel = Label(Medium, _content, guests, 18.67f, change < 0 ? _badText : _ink, x, top + 632f, inner);
            Fit(guestsLabel, inner, 14f);
            var starsLabel = Label(Medium, _content, stars, 18.67f, shift < 0f ? _badText : _ink, x, top + 656f, inner * 0.62f);
            Fit(starsLabel, inner * 0.6f, 14f);
            Label(Bold, _content, Tr.Format("earns about {value}", ("value", Money(price - recipe.IngredientCost))), 18.67f, _goodText, x + inner, top + 656f, inner * 0.4f, HorizontalAlignmentOptions.Right);
        }

        private void DrawUnlock(Recipe recipe, float x, float inner, float top)
        {
            _dialRecipe = null;
            _dialPrice = int.MinValue;
            var affordable = Services.Days.Balance >= recipe.UnlockCost;
            Caps(_content, Tr.Get("UNLOCK"), x, top + 482f);
            UiKit.Picture(_content, "UnlockLock", PadlockSprite, x, top + 506f, 38f, 38f, affordable ? _ink : _badText);
            Label(ExtraBold, _content, Money(recipe.UnlockCost), 54f, affordable ? _ink : _badText, x + 46f, top + 546f, 220f, tracking: -1.5f);
            // A (a tap) unlocks: shown beside the price, as the rows show theirs.
            Chip(_content, "A", Tr.Get("Unlock"), x + 280f, top + 520f, fromLeft: true);
            Label(Medium, _content, Tr.Format("You have {amount}", ("amount", Money(Services.Days.Balance))), 19.5f, affordable ? _body : _badText, x + 280f, top + 508f, 230f);
            Label(Medium, _content, Tr.Format("suggested {price}", ("price", Money(recipe.SuggestedPrice))) + " · " +
                Tr.Format("earns about {value}", ("value", Money(recipe.SuggestedPrice - recipe.IngredientCost))), 19.5f, _ink, x, top + 604f, inner);
            if (_unlockResult == UnlockResult.CantAfford)
                Label(Bold, _content, Tr.Get("not enough money"), 19.5f, _badText, x, top + 640f, inner);
        }

        // The dial's numbers where the spring has them: the nearest to the middle biggest and
        // darkest, the rest shrinking and fading either side, so it reads as a wheel seen edge on.
        private void PlaceDial()
        {
            foreach (var (label, step) in _dial)
            {
                if (label == null)
                    continue;
                var at = step - _dialOffset;
                var distance = Mathf.Abs(at);
                // A wheel: the spacing closes up towards the edges.
                var x = _dialCentreX + Mathf.Sign(at) * _dialSpacing * (distance <= 1f ? distance : 1f + (distance - 1f) * 0.55f);
                var scale = Mathf.Lerp(1f, 0.42f, Mathf.Clamp01(distance / 2f));
                // The middle one punches up as it lands.
                scale *= 1f + _dialPunch * _dialKick * Mathf.Clamp01(1f - distance);
                // Past two steps out it's gone round the wheel: faded right away.
                var alpha = Mathf.Clamp01(1f - distance * 0.42f) * Mathf.Clamp01(2.5f - distance);
                // Every number's middle on one line: about a third of its size above the baseline,
                // scaled about the box's middle, which sits on the baseline.
                var middle = 50f * 0.35f;
                label.rectTransform.anchoredPosition = new Vector2(x, -(_dialBaseline - middle + middle * scale));
                label.rectTransform.localScale = new Vector3(scale, scale, 1f);
                label.color = Alpha(distance < 0.5f ? _ink : _body, alpha);
            }
        }

        // The price couldn't move that way (at its lowest or highest): the dial bumps and springs back.
        private void DialBump(int direction)
        {
            _dialVelocity -= direction * 9f;
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
            if (Mathf.Abs(_dialOffset) < 0.002f && Mathf.Abs(_dialVelocity) < 0.01f)
                _dialOffset = _dialVelocity = 0f;
        }

        /// <summary>The seconds to the next price step while the D-pad is held: slow at first, quicker the longer it's held.</summary>
        private float PriceRepeat(float now) =>
            Mathf.Lerp(_priceRepeatSlow, _priceRepeatInterval, Mathf.Clamp01((now - _priceHeldSince) / _priceAccelerateSeconds));

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
            if (_dial.Count > 0 && (Mathf.Abs(_dialOffset) > 0f || Mathf.Abs(_dialVelocity) > 0f || _dialKick > 0f))
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
                Label(SemiBold, _content, ServiceClock.Format(hour * 60f), small ? 14f : 16f, _muted, hx, y + height + (small ? 20f : 24f), 80f, HorizontalAlignmentOptions.Center);
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
