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
    /// and the chosen one in detail. Made more visual after the user's look at it (2026-11-11):
    /// <list type="bullet">
    /// <item>Each dish is its own plate (Tools/Game/Art/Render Dish Pictures), steaming while it's on
    /// the menu; a dish still to unlock sits cold and faded.</item>
    /// <item>Ingredients are chips with their pictures.</item>
    /// <item>The price is a dial: the price big in the middle, its neighbours shrinking away either
    /// side, rolling along as the D-pad steps it.</item>
    /// <item>Opening times are tiles with a sun, a moon or both, over the day as a strip of bars, one
    /// per half hour, as tall as the guests coming then: the rushes stand out, closed hours lie flat.</item>
    /// </list>
    /// </summary>
    public sealed partial class ReviewDashboard
    {
        private const float RecipeListWidth = 470f, RecipeRow = 92f, DetailX = 1256f;
        private const float OpeningTop = 262f, OpeningHeight = 214f;
        private const float DetailTop = 262f, DetailBottom = 932f;

        [Header("Restaurant tab")]
        [Tooltip("Pixels between neighbouring numbers on the price dial.")]
        [SerializeField, Min(20f)] private float _dialSpacing = 112f;
        [Tooltip("How quickly the dial rolls to the new price, per second (higher is snappier).")]
        [SerializeField, Min(1f)] private float _dialRollSpeed = 14f;
        [Tooltip("Seconds a wisp of steam takes to rise and fade.")]
        [SerializeField, Min(0.3f)] private float _steamSeconds = 2.6f;
        [Tooltip("Steam's colour at its thickest: a warm grey, so it shows on the cream panel as well as over the food.")]
        [SerializeField] private Color _steamColour = new Color(0.62f, 0.57f, 0.52f, 0.62f);

        // The price dial: its numbers, where its middle is, and how far it still has to roll.
        private readonly List<(TextMeshProUGUI Label, int Step)> _dial = new List<(TextMeshProUGUI, int)>();
        private float _dialCentreX, _dialBaseline;
        private float _dialOffset;
        private string _dialRecipe;
        private int _dialPrice = int.MinValue;

        // Steam over the plates on the menu, animated every frame.
        private readonly List<Wisp> _steam = new List<Wisp>();

        [Tooltip("Seconds an unlocked dish takes to warm up: its colour comes back, steam starts, sparks fly, its card pops.")]
        [SerializeField, Min(0.2f)] private float _unlockWarmSeconds = 1.3f;
        [Tooltip("How far the unlock sparks fly from the plate, in pixels.")]
        [SerializeField, Min(0f)] private float _unlockSparkReach = 170f;

        // The unlock moment (the user's pick, 2026-11-11): the dish warming up, and when it began.
        private string _warming;
        private float _warmStart;
        // What the warm-up animates, registered as the tab is drawn.
        private readonly List<Image> _warmPlates = new List<Image>();
        private readonly List<Image> _warmFlashes = new List<Image>();
        private readonly List<TextMeshProUGUI> _warmLabels = new List<TextMeshProUGUI>();
        private readonly List<(Image Image, Vector2 From, float Angle, float Size)> _sparks = new List<(Image, Vector2, float, float)>();

        // A dish still to unlock: faded, a little grey, cold.
        private static readonly Color ColdTint = new Color(0.82f, 0.78f, 0.72f, 0.75f);

        /// <summary>Whether a dish just unlocked is still warming up, for tests.</summary>
        public bool IsWarmingUp => _warming != null;

        /// <summary>Starts the unlock moment for a dish just unlocked.</summary>
        private void WarmUp(string recipeId)
        {
            _warming = recipeId;
            _warmStart = Time.unscaledTime;
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
            var chosen = Mathf.Clamp(Selected, 0, book.Recipes.Count - 1);

            // The list, scrolled only as far as the chosen recipe needs to show (ScrollWindow).
            var fits = Mathf.Max(1, Mathf.FloorToInt((FooterTop - 20f - listTop) / (RecipeRow + 12f)));
            var first = _recipeFirst = ScrollWindow.First(_recipeFirst, chosen, book.Recipes.Count, fits);
            var y = listTop;
            for (var i = first; i < book.Recipes.Count && i < first + fits; i++)
            {
                RecipeCard(book, book.Recipes[i], y, i == chosen && !_restaurant.OnChips);
                y += RecipeRow + 12f;
            }

            if (opening != null && _restaurant.OnChips)
                DrawOpeningDetail(opening);
            else
                DrawRecipe(book, book.Recipes[chosen]);
        }

        // ---- The menu ----

        private void RecipeCard(RecipeBook book, Recipe recipe, float y, bool chosen)
        {
            if (chosen)
                UiKit.RoundBox(_content, "Highlight", _gold, RightX - 5.67f, y - 5.67f, RecipeListWidth + 11.33f, RecipeRow + 11.33f, 20f);
            UiKit.StrokedBox(_content, "Recipe", _cardLight, chosen ? _gold : _cardEdge, 2.67f, RightX, y, RecipeListWidth, RecipeRow, 15f);
            var on = book.IsUnlocked(recipe.Id);
            var warmingUp = on && Warmth(recipe.Id) < 1f;
            // Just unlocked: the card lights up gold behind its plate and words.
            if (warmingUp)
                _warmFlashes.Add(UiKit.RoundBox(_content, "UnlockFlash", Alpha(_gold, 0f), RightX + 3f, y + 3f, RecipeListWidth - 6f, RecipeRow - 6f, 13f));

            // The plate (its picture has room above for steam, so it's drawn a little high); still to
            // unlock, it's faded and cold.
            const float plate = 104f;
            DishPicture(recipe, RightX + 2f, y - 18f, plate, on, steam: on && chosen);

            var textX = RightX + 108f;
            var name = Label(Bold, _content, Tr.Get(recipe.Name), 23f, on ? _ink : _body, textX, y + 38f, 400f);
            Fit(name, RecipeListWidth - (textX - RightX) - 104f, 16f);
            var status = on
                ? Tr.Format("On the menu · {price}", ("price", Money(book.PriceOf(recipe.Id))))
                : Tr.Format("Unlock · {price}", ("price", Money(recipe.UnlockCost)));
            var colour = on ? _goodText : Services.Days.Balance >= recipe.UnlockCost ? _muted : _badText;
            var line = Label(on ? SemiBold : Medium, _content, status, 19.5f, colour, textX, y + 70f, RecipeListWidth - (textX - RightX) - 20f);
            if (warmingUp)
            {
                // "On the menu" pops in from its left end.
                line.rectTransform.pivot = new Vector2(0f, 0.5f);
                line.rectTransform.anchoredPosition += new Vector2(0f, -line.rectTransform.sizeDelta.y / 2f);
                _warmLabels.Add(line);
            }
            ComplexityDots(recipe.Complexity, RightX + RecipeListWidth - 24f - 3f * 20f, y + 26f);
        }

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
                // Swells about its middle: the pivot moves there and the position follows.
                var rect = image.rectTransform;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition += new Vector2(rect.sizeDelta.x / 2f, -rect.sizeDelta.y / 2f);
                _warmPlates.Add(image);
            }
            // While it warms up, its steam starts (the chosen card's and the big plate's alike).
            if (steam || warmingUp)
                Steam(x + size / 2f, y + size * 0.5f, size * 0.6f, size * 0.24f, size > 120f ? 6 : 3, warmingUp);
            // The big plate throws gold sparks as it comes on.
            if (warmingUp && size > 120f)
                Sparks(x + size / 2f, y + size * 0.62f, 14);
        }

        // The chosen recipe: its plate, what it needs, its ingredients as chips, and its price on a
        // dial with what to expect.
        private void DrawRecipe(RecipeBook book, Recipe recipe)
        {
            const float width = RightEdge - DetailX, top = DetailTop;
            const float x = DetailX + 28f;
            var on = book.IsUnlocked(recipe.Id);
            UiKit.RoundBox(_content, "Detail", _card, DetailX, top, width, DetailBottom - top, 18f);

            const float hero = 230f;
            var heroX = RightEdge - 6f - hero;
            var title = Label(ExtraBold, _content, Tr.Get(recipe.Name), 34f, _ink, x, top + 54f, 600f, tracking: -0.8f);
            Fit(title, heroX + 30f - x, 22f);
            ComplexityDots(recipe.Complexity, x, top + 76f);
            var word = recipe.Complexity <= 1 ? Tr.Get("simple") : recipe.Complexity == 2 ? Tr.Get("some work") : Tr.Get("demanding");
            Label(Medium, _content, word, 20f, _body, x + 70f, top + 90f, 200f);

            // The plate, top right, steaming while it's on the menu (its picture leaves room above).
            DishPicture(recipe, heroX, top + 30f, hero, on, steam: on);

            if (_unlockResult == UnlockResult.CantAfford)
                Label(Bold, _content, Tr.Get("not enough money"), 20f, _badText, x, top + 124f, heroX - x - 10f);
            else if (_unlockResult == UnlockResult.Unlocked && on)
                Label(Bold, _content, Tr.Get("On the menu from tomorrow"), 20f, _goodText, x, top + 124f, heroX - x - 10f);

            Caps(_content, Tr.Get("NEEDS"), x, top + 162f);
            var px = x;
            foreach (var appliance in recipe.Appliances)
                px = Pill(_content, Tr.Get(appliance), SemiBold, 19.5f, _paper, _ink, px, top + 176f, 36f, 14f, 25f, heroX - 10f) + 10f;

            // The ingredients as chips with their pictures, under the plate.
            // The ingredients as chips, what they cost a serving after them.
            Caps(_content, Tr.Get("INGREDIENTS"), x, top + 262f);
            var ix = x;
            for (var i = 0; i < recipe.IngredientTypeIds.Count; i++)
                ix = IngredientChip(recipe.IngredientTypeIds[i], Tr.Get(recipe.Ingredients[i]), ix, top + 276f) + 10f;
            Label(Medium, _content, Tr.Format("about {cost} a serving", ("cost", Money(recipe.IngredientCost))), 18.67f, _body, ix + 4f, top + 310f, RightEdge - 28f - ix);
            UiKit.RoundBox(_content, "Rule", _rule, x, top + 352f, width - 56f, 2f, 0f);

            if (on)
                DrawPrice(book, recipe, x, width, top);
            else
                DrawUnlock(recipe, x, width, top);
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
        private void DrawPrice(RecipeBook book, Recipe recipe, float x, float width, float top)
        {
            var price = book.PriceOf(recipe.Id);
            var ratio = book.RatioOf(recipe);
            Caps(_content, Tr.Get("PRICE"), x, top + 388f);

            // A new dish starts still; a new price on the same dish rolls from where the old one stood.
            if (_dialRecipe != recipe.Id)
            {
                _dialRecipe = recipe.Id;
                _dialOffset = 0f;
            }
            else if (_dialPrice != int.MinValue && price != _dialPrice)
            {
                _dialOffset = Mathf.Clamp(_dialOffset + (price - _dialPrice), -3f, 3f);
            }
            _dialPrice = price;

            var inner = width - 56f;
            _dialCentreX = x + inner / 2f;
            _dialBaseline = top + 460f;
            // The window the numbers roll in: a well with the pointer under the middle.
            UiKit.RoundBox(_content, "DialWell", Alpha(_cardLight, 0.75f), x, top + 400f, inner, 76f, 38f);
            UiKit.RoundBox(_content, "DialPointer", _gold, _dialCentreX - 22f, top + 468f, 44f, 5f, 2.5f);
            for (var step = -3; step <= 3; step++)
            {
                if (price + step < 1)
                    continue;
                // The middle shows the money; its neighbours are just the numbers, like a wheel's.
                var text = step == 0 ? Money(price) : $"{price + step}";
                var label = Label(ExtraBold, _content, text, 58f, _ink, _dialCentreX, _dialBaseline, 200f, HorizontalAlignmentOptions.Center, -1.5f);
                // Scaled about its middle; TextAt's box is centred on the baseline's height.
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _dial.Add((label, step));
            }
            Label(ExtraBold, _content, "‹", 44f, _muted, x + 14f, _dialBaseline - 6f, 30f);
            Label(ExtraBold, _content, "›", 44f, _muted, x + inner - 30f, _dialBaseline - 6f, 30f);
            PlaceDial();

            // The suggestion on its own line, X beside it when the price has moved off it.
            var suggested = Tr.Format("suggested {price}", ("price", Money(recipe.SuggestedPrice)));
            var line = Label(Medium, _content, suggested, 20f, _body, x, top + 516f, 260f);
            if (price != recipe.SuggestedPrice)
            {
                Label(Bold, _content, Percent(ratio), 20f, ratio > 1f ? _badText : _goodText, x + UiKit.Width(line) + 12f, top + 516f, 120f);
                Chip(_content, "X", Tr.Get("Suggested"), RightEdge - 28f, top + 486f);
            }

            // What this price does: tonight's guests, from the whole menu's prices, and this dish's stars.
            Caps(_content, Tr.Get("AT THESE PRICES"), x, top + 568f);
            var demand = book.BuildMenu().Demand;
            var change = Mathf.RoundToInt((demand - 1f) * 100f);
            var guests = change == 0 ? Tr.Get("Guests: as many as at the suggested prices")
                : change < 0 ? Tr.Format("Guests: about {percent}% fewer", ("percent", -change))
                : Tr.Format("Guests: about {percent}% more", ("percent", change));
            Label(Medium, _content, guests, 21.33f, change < 0 ? _badText : _ink, x, top + 600f, inner);
            var shift = PriceResponse.Expectation(ratio, book.Pricing);
            var stars = Mathf.Abs(shift) < 0.05f ? Tr.Get("Stars: rated as the dish deserves")
                : shift < 0f ? Tr.Format("Stars: {stars} fewer for the same dish", ("stars", (-shift).ToString("0.#")))
                : Tr.Format("Stars: {stars} more for the same dish", ("stars", shift.ToString("0.#")));
            Label(Medium, _content, stars, 21.33f, shift < 0f ? _badText : _ink, x, top + 630f, inner);
            Label(Medium, _content, Tr.Format("earns about {value}", ("value", Money(price - recipe.IngredientCost))) + " " + Tr.Get("a serving"), 21.33f, _ink, x, top + 660f, inner);
        }

        private void DrawUnlock(Recipe recipe, float x, float width, float top)
        {
            _dialRecipe = null;
            _dialPrice = int.MinValue;
            var affordable = Services.Days.Balance >= recipe.UnlockCost;
            Caps(_content, Tr.Get("UNLOCK"), x, top + 388f);
            Label(ExtraBold, _content, Money(recipe.UnlockCost), 54f, affordable ? _ink : _badText, x, top + 454f, 260f, tracking: -1.5f);
            // A (a tap) unlocks: shown beside the price, as the rows show theirs.
            Chip(_content, "A", Tr.Get("Unlock"), x + 270f, top + 424f, fromLeft: true);
            Label(Medium, _content, Tr.Format("You have {amount}", ("amount", Money(Services.Days.Balance))), 20f, _body, x + 270f, top + 412f, 230f);
            Label(Medium, _content, Tr.Format("suggested {price}", ("price", Money(recipe.SuggestedPrice))) + " · " +
                Tr.Format("earns about {value}", ("value", Money(recipe.SuggestedPrice - recipe.IngredientCost))), 21.33f, _ink, x, top + 524f, width - 56f);
        }

        // The dial's numbers where the roll has them: the nearest to the middle biggest and darkest,
        // the rest shrinking and fading either side, so it reads as a wheel seen edge on.
        private void PlaceDial()
        {
            foreach (var (label, step) in _dial)
            {
                if (label == null)
                    continue;
                var at = step - _dialOffset;
                var distance = Mathf.Abs(at);
                // A wheel: the spacing closes up towards the edges.
                var x = _dialCentreX + Mathf.Sign(at) * _dialSpacing * (distance <= 1f ? distance : 1f + (distance - 1f) * 0.5f);
                var scale = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(distance / 2f));
                // Past two steps out it's gone round the wheel: faded right away.
                var alpha = Mathf.Clamp01(1f - distance * 0.38f) * Mathf.Clamp01(2.6f - distance);
                // Every number's middle on one line: the big one's (about a third of its size above
                // the baseline), scaled about the box's middle, which sits on the baseline.
                var middle = 58f * 0.35f;
                label.rectTransform.anchoredPosition = new Vector2(x, -(_dialBaseline - middle + middle * scale));
                label.rectTransform.localScale = new Vector3(scale, scale, 1f);
                label.color = Alpha(_ink, alpha * (distance < 0.5f ? 1f : 0.85f));
            }
        }

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
                var strength = wisp.Warming ? Warmth(_warming) : 1f;
                wisp.Image.color = new Color(_steamColour.r, _steamColour.g, _steamColour.b, _steamColour.a * fade * fade * strength);
            }
        }

        // Gold sparks bursting from a point, each on its own heading.
        private void Sparks(float x, float y, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var size = 16f + (i * 7 % 5) * 4f;
                var image = UiKit.Picture(_content, "Spark", Puff, x, y, size, size, Alpha(_gold, 0f));
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                // Spread round the top half mostly, as if thrown up from the plate.
                var angle = Mathf.Lerp(-200f, 20f, (i + 0.5f) / count) * Mathf.Deg2Rad + (i % 3 - 1) * 0.12f;
                _sparks.Add((image, new Vector2(x, y), angle, size));
            }
        }

        // The unlock moment, frame by frame: the plate warms, the card flashes and its line pops,
        // sparks fly out and fade. Ends by drawing the tab as it stays.
        private void AnimateWarmUp()
        {
            if (_warming == null)
                return;
            var t = Warmth(_warming);
            var warm = 1f - (1f - t) * (1f - t);
            foreach (var plate in _warmPlates)
            {
                if (plate == null)
                    continue;
                plate.color = Color.Lerp(ColdTint, Color.white, warm);
                // A little swell as it comes on.
                var swell = 1f + 0.08f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.6f));
                plate.rectTransform.localScale = new Vector3(swell, swell, 1f);
            }
            foreach (var flash in _warmFlashes)
            {
                if (flash != null)
                    flash.color = Alpha(_gold, 0.55f * (1f - t) * Mathf.Clamp01(t * 8f));
            }
            foreach (var label in _warmLabels)
            {
                if (label == null)
                    continue;
                var pop = 1f + 0.3f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 2.2f));
                label.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            foreach (var (image, from, angle, size) in _sparks)
            {
                if (image == null)
                    continue;
                var reach = _unlockSparkReach * (1f - (1f - t) * (1f - t) * (1f - t));
                // They fly out and drop a little, as thrown sparks do.
                var at = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach + new Vector2(0f, 60f * t * t);
                image.rectTransform.anchoredPosition = new Vector2(at.x, -at.y);
                var s = size * Mathf.Lerp(1.2f, 0.4f, t);
                image.rectTransform.sizeDelta = new Vector2(s, s);
                image.color = Alpha(Color.Lerp(_gold, Color.white, 0.35f), Mathf.Clamp01(t * 10f) * Mathf.Clamp01((1f - t) * 1.6f));
            }
            if (t >= 1f)
            {
                _warming = null;
                _dirty = true;
            }
        }

        /// <summary>Every frame while the Restaurant tab shows: the steam rises and the dial rolls.</summary>
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
            if (_dial.Count > 0 && Mathf.Abs(_dialOffset) > 0.001f)
            {
                _dialOffset = Mathf.Lerp(_dialOffset, 0f, 1f - Mathf.Exp(-_dialRollSpeed * dt));
                if (Mathf.Abs(_dialOffset) < 0.005f)
                    _dialOffset = 0f;
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

        private static Sprite _sun, _moon;

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
