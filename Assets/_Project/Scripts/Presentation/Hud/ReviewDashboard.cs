using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Simulation.Days;
using Game.Simulation.Saving;
using Game.Simulation.Service;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// The Review dashboard (decisions 2026-11-06 and 2026-11-08, after the user's mockup; drawn to the
    /// Art Director's dashboard mockups, 2026-11-09): forkcast, a review-listing app over the kitchen
    /// while Review lasts. It's read-only, except for replying to reviews. LB and RB switch tabs, the
    /// stick browses reviews, Y opens the reply wheel, and Menu readies up for the next day as usual.
    /// This file holds the state, input and data; ReviewDashboard.Drawing.cs draws it.
    /// </summary>
    public sealed partial class ReviewDashboard : MonoBehaviour
    {
        private enum Tab
        {
            Overview,
            Reviews,
            Menu,
            Books,
        }

        // The Reviews tab's filters, in the order of their chips.
        private enum Filter
        {
            All,
            WalkOuts,
            Low,
            High,
            Unreplied,
        }

        // Day 1 is a Monday.
        private static string Weekday(int day) => ((day - 1) % 7) switch
        {
            0 => Game.Data.Tr.Get("Mon"),
            1 => Game.Data.Tr.Get("Tue"),
            2 => Game.Data.Tr.Get("Wed"),
            3 => Game.Data.Tr.Get("Thu"),
            4 => Game.Data.Tr.Get("Fri"),
            5 => Game.Data.Tr.Get("Sat"),
            _ => Game.Data.Tr.Get("Sun"),
        };

        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private DayLoop _dayLoop;
        [SerializeField] private CoopSettings _settings;
        [SerializeField] private Vector2Int _photoSize = new Vector2Int(1280, 680);

        [Header("Feel")]
        [Tooltip("How far the stick goes before it moves to the next review.")]
        [SerializeField, Range(0.1f, 1f)] private float _stickThreshold = 0.5f;
        [Tooltip("Real seconds between steps while the stick is held.")]
        [SerializeField, Min(0.05f)] private float _stickRepeat = 0.22f;

        // Named afresh (2026-11-09) so the scene's saved copies of the old impersonal lines give way.
        [Header("Replies ({0} is the guest's first name)")]
        [SerializeField] private string _apologiseReply = "Sorry, {0}. We'll do better next time.";
        [SerializeField] private string _thankReply = "Thank you, {0}! See you soon.";
        [SerializeField] private string _standGroundReply = "We stand by our kitchen.";

        private Tab _tab;
        private int _selected;
        private bool _choosingReply;
        private Filter _filter;
        // On the Reviews tab, the stick can rise from the top review to the filter chips.
        private bool _onFilters;
        private float _nextStickStep;
        private bool _dirty = true;
        private int _shownForDay = -1;

        private KitchenServices Services => _root.Services;

        private void Start()
        {
            Build();
            _canvasObject.SetActive(false);
            Services.Days.PhaseChanged += HandlePhaseChanged;
            if (Services.Days.Phase == DayPhase.Review)
                HandlePhaseChanged(DayPhase.Review);
        }

        private void OnDestroy()
        {
            if (_root != null && _root.Services != null)
                Services.Days.PhaseChanged -= HandlePhaseChanged;
            if (_photoTexture != null)
                _photoTexture.Release();
            if (_blurTexture != null)
                _blurTexture.Release();
        }

        private void HandlePhaseChanged(DayPhase phase)
        {
            var show = phase == DayPhase.Review;
            _canvasObject.SetActive(show);
            if (!show)
                return;

            // A new day's Review starts on its overview, with the photo taken as the doors close.
            if (_shownForDay != Services.Days.Day)
            {
                _shownForDay = Services.Days.Day;
                _tab = Tab.Overview;
                _selected = 0;
                _choosingReply = false;
                _filter = Filter.All;
                _onFilters = false;
                TakePhoto();
            }
            _dirty = true;
        }

        /// <summary>For the capture test: shows a tab with a review chosen and, if asked, the reply wheel open, at once.</summary>
        public Canvas ShowForCapture(int tab, int selected, bool choosingReply, int filter = 0, bool onFilters = false)
        {
            _canvasObject.SetActive(true);
            _tab = (Tab)Mathf.Clamp(tab, 0, 3);
            _selected = selected;
            _choosingReply = choosingReply;
            _filter = (Filter)Mathf.Clamp(filter, 0, 4);
            _onFilters = onFilters;
            Refresh();
            _dirty = false;
            return _canvasObject.GetComponent<Canvas>();
        }

        private void Update()
        {
            if (!_canvasObject.activeSelf)
                return;
            ReadInput();
            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }
            DrawHints();
        }

        // ---- Input ----

        private void ReadInput()
        {
            foreach (var player in PlayerInput.all)
            {
                if (!_dayLoop.WasHereLastFrame(player))
                    continue;
                var actions = player.actions;

                // The wheel puts each reply on the face button in its place: X left, Y top, A bottom.
                if (_choosingReply)
                {
                    if (Pressed(actions, "Use"))
                    {
                        Reply(ReviewReply.Apologise);
                        continue;
                    }
                    if (Pressed(actions, "ButtonY"))
                    {
                        Reply(ReviewReply.Thank);
                        continue;
                    }
                    if (Pressed(actions, "Interact"))
                    {
                        Reply(ReviewReply.StandYourGround);
                        continue;
                    }
                    if (Pressed(actions, "ButtonB"))
                    {
                        SetChoosing(false);
                        continue;
                    }
                }

                // Tabs and browsing close the wheel and carry on, as the hints promise.
                if (Pressed(actions, "RotateLeft"))
                    SwitchTab(-1);
                else if (Pressed(actions, "RotateRight"))
                    SwitchTab(1);
                else if (!_choosingReply && Pressed(actions, "ButtonY") && SelectedReview() is ReviewRecord review && review.Reply < 0)
                    SetChoosing(true);

                // The stick or the arrow keys (both are Move): up and down browse, left and right
                // change the filter while the chips have the focus.
                var move = actions["Move"].ReadValue<Vector2>();
                if (move.magnitude < _stickThreshold)
                {
                    _nextStickStep = 0f;
                }
                else if (Time.unscaledTime >= _nextStickStep)
                {
                    // A fresh push moves at once; holding repeats.
                    _nextStickStep = Time.unscaledTime + _stickRepeat;
                    if (Mathf.Abs(move.y) >= Mathf.Abs(move.x))
                        Browse(move.y > 0f ? -1 : 1);
                    else if (_onFilters)
                        ChangeFilter(move.x > 0f ? 1 : -1);
                }
            }
        }

        private static bool Pressed(InputActionAsset actions, string name)
        {
            var action = actions.FindAction(name);
            return action != null && action.WasPressedThisFrame();
        }

        private void SwitchTab(int step)
        {
            var count = System.Enum.GetValues(typeof(Tab)).Length;
            _tab = (Tab)(((int)_tab + step + count) % count);
            _selected = 0;
            _choosingReply = false;
            _onFilters = false;
            _dirty = true;
        }

        private void Browse(int step)
        {
            _choosingReply = false;
            if (_tab == Tab.Reviews)
            {
                // Up from the top review reaches the filters; down from them returns to the list.
                if (_onFilters)
                {
                    if (step > 0)
                    {
                        _onFilters = false;
                        _selected = 0;
                        _dirty = true;
                    }
                    return;
                }
                if (step < 0 && _selected == 0)
                {
                    _onFilters = true;
                    _dirty = true;
                    return;
                }
            }
            var count = ListedReviews().Count;
            if (count == 0)
                return;
            _selected = Mathf.Clamp(_selected + step, 0, count - 1);
            _dirty = true;
        }

        // Left and right along the chips; the list follows at once, from its top.
        private void ChangeFilter(int step)
        {
            var next = Mathf.Clamp((int)_filter + step, 0, System.Enum.GetValues(typeof(Filter)).Length - 1);
            if (next == (int)_filter)
                return;
            _filter = (Filter)next;
            _selected = 0;
            _dirty = true;
        }

        private void SetChoosing(bool choosing)
        {
            _choosingReply = choosing;
            _dirty = true;
        }

        private void Reply(ReviewReply reply)
        {
            var review = SelectedReview();
            if (review != null)
                _root.ReplyTo(review, reply);
            SetChoosing(false);
        }

        // ---- Data ----

        // Walk-outs first, then the lowest ratings: what needs a reply most (the mockup's sort). By the
        // guest's own stars, so a reply's nudge doesn't move a review while you're looking at it.
        private static IOrderedEnumerable<ReviewRecord> NeedsAttentionFirst(IEnumerable<ReviewRecord> reviews) =>
            reviews.OrderBy(r => r.WalkedOut ? 0 : 1).ThenBy(r => r.Stars).ThenBy(r => r.ArrivedAt);

        private List<ReviewRecord> TodaysReviews() =>
            NeedsAttentionFirst(Services.ReviewHistory.Where(r => r.Day == Services.Days.Day)).ToList();

        private List<ReviewRecord> ListedReviews() => _tab switch
        {
            Tab.Overview => TodaysReviews(),
            Tab.Reviews => AllReviews().Where(r => Matches(r, _filter)).ToList(),
            _ => new List<ReviewRecord>(),
        };

        // Today's first, needing attention first; then older days, newest first.
        private List<ReviewRecord> AllReviews() =>
            Services.ReviewHistory.GroupBy(r => r.Day).OrderByDescending(g => g.Key).SelectMany(g => NeedsAttentionFirst(g)).ToList();

        private static bool Matches(ReviewRecord review, Filter filter) => filter switch
        {
            Filter.WalkOuts => review.WalkedOut,
            Filter.Low => !review.WalkedOut && review.RatedStars < 3f,
            Filter.High => !review.WalkedOut && review.RatedStars >= 3f,
            Filter.Unreplied => review.Reply < 0,
            _ => true,
        };

        // Nothing is chosen while the filters have the focus, so Y does nothing there.
        private ReviewRecord SelectedReview()
        {
            if (_tab == Tab.Reviews && _onFilters)
                return null;
            var list = ListedReviews();
            return _selected >= 0 && _selected < list.Count ? list[_selected] : null;
        }

        // In the player's language and currency (Locale): "$2,340.50" in English, "2.340,50 €" in Spanish.
        private static string Money(float amount, bool cents = false) =>
            Money(amount, cents, false);

        private static string Money(float amount, bool cents, bool signed) =>
            Game.Data.Locale.Money(amount, cents ? Game.Data.MoneyStyle.Cents : Game.Data.MoneyStyle.Auto, signed);

        private string ClockAt(float serviceSeconds)
        {
            var minutes = Services.ServiceConfig.OpensAt * 60f + serviceSeconds / 60f;
            return $"{(int)(minutes / 60f) % 24:00}:{(int)minutes % 60:00}";
        }

        private static string ReasonFor(ReviewTopic topic) => topic switch
        {
            ReviewTopic.Praise => Game.Data.Tr.Get("Loved it"),
            ReviewTopic.Decent => Game.Data.Tr.Get("A decent meal"),
            ReviewTopic.Undercooked => Game.Data.Tr.Get("Undercooked"),
            ReviewTopic.Overcooked => Game.Data.Tr.Get("Overcooked"),
            ReviewTopic.Burnt => Game.Data.Tr.Get("Burnt"),
            ReviewTopic.NoMain => Game.Data.Tr.Get("No main on the plate"),
            ReviewTopic.NoSide => Game.Data.Tr.Get("Missing the side"),
            ReviewTopic.Cold => Game.Data.Tr.Get("Served cold"),
            ReviewTopic.PaleCrust => Game.Data.Tr.Get("Pale crust"),
            ReviewTopic.NotRested => Game.Data.Tr.Get("Not rested"),
            ReviewTopic.NotFresh => Game.Data.Tr.Get("Not fresh"),
            ReviewTopic.BadCut => Game.Data.Tr.Get("Rough cut"),
            ReviewTopic.SideOff => Game.Data.Tr.Get("The side was off"),
            ReviewTopic.SlowService => Game.Data.Tr.Get("Slow service"),
            ReviewTopic.WalkedOut => Game.Data.Tr.Get("Gave up waiting"),
            _ => topic.ToString(),
        };

        private string ReplyText(ReviewRecord review)
        {
            var first = Game.Data.GuestNames.FirstOf(review);
            var text = (ReviewReply)review.Reply switch
            {
                ReviewReply.Apologise => _apologiseReply,
                ReviewReply.Thank => _thankReply,
                _ => _standGroundReply,
            };
            return string.Format(text, first);
        }

        // ---- The photo ----

        // The dining room as the doors close, from the game camera, without the HUD. A small, soft copy
        // of it is the dashboard's backdrop: the kitchen, blurred, as in the mockup.
        private void TakePhoto()
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            if (_photoTexture == null)
                _photoTexture = new RenderTexture(_photoSize.x, _photoSize.y, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = _photoTexture;
            camera.Render();
            camera.targetTexture = previous;
            _photo.texture = _photoTexture;
            Blur();
            KeepPhotoForTheMenu();
        }

        // Halving twice and then down to a tiny texture, each step filtered, and stretched back up:
        // a cheap blur that costs nothing per frame.
        private void Blur()
        {
            if (_blurTexture == null)
            {
                _blurTexture = new RenderTexture(96, 54, 0) { filterMode = FilterMode.Bilinear };
            }
            var half = RenderTexture.GetTemporary(_photoSize.x / 4, _photoSize.y / 4, 0);
            var quarter = RenderTexture.GetTemporary(_photoSize.x / 8, _photoSize.y / 8, 0);
            half.filterMode = quarter.filterMode = FilterMode.Bilinear;
            Graphics.Blit(_photoTexture, half);
            Graphics.Blit(half, quarter);
            Graphics.Blit(quarter, _blurTexture);
            RenderTexture.ReleaseTemporary(half);
            RenderTexture.ReleaseTemporary(quarter);
            _backdrop.texture = _blurTexture;
            _backdrop.enabled = true;
        }

        // The slot's card in the main menu shows the latest close.
        private void KeepPhotoForTheMenu()
        {
            try
            {
                var previous = RenderTexture.active;
                RenderTexture.active = _photoTexture;
                var picture = new Texture2D(_photoTexture.width, _photoTexture.height, TextureFormat.RGB24, false);
                picture.ReadPixels(new UnityEngine.Rect(0, 0, _photoTexture.width, _photoTexture.height), 0, 0);
                picture.Apply();
                RenderTexture.active = previous;
                var path = Game.Gameplay.Saving.SaveSlots.PhotoPath(_root.Slot);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, picture.EncodeToJPG(85));
                Destroy(picture);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Couldn't keep the photo for the menu: {exception.Message}");
            }
        }

        // ---- A drawn star, shared with screens that have no star art ----

        private static Sprite _starSprite;

        // A five-pointed star, made once, anti-aliased by sampling each pixel several times.
        internal static Sprite StarSprite
        {
            get
            {
                if (_starSprite != null)
                    return _starSprite;
                const int size = 64;
                var corners = new Vector2[10];
                for (var i = 0; i < 10; i++)
                {
                    var angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    var radius = i % 2 == 0 ? 0.5f : 0.2f;
                    corners[i] = new Vector2(0.5f + radius * Mathf.Cos(angle), 0.53f + radius * Mathf.Sin(angle));
                }
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (var py = 0; py < size; py++)
                for (var px = 0; px < size; px++)
                {
                    var inside = 0;
                    for (var s = 0; s < 16; s++)
                    {
                        var point = new Vector2((px + (s % 4 + 0.5f) / 4f) / size, (py + (s / 4 + 0.5f) / 4f) / size);
                        if (Inside(point, corners))
                            inside++;
                    }
                    texture.SetPixel(px, py, new Color(1f, 1f, 1f, inside / 16f));
                }
                texture.Apply();
                _starSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return _starSprite;
            }
        }

        private static bool Inside(Vector2 point, Vector2[] polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
