using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Stations;
using Game.Simulation.Campaign;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// The city miniature in the boss's back room (decision 2026-11-09; Phase 6, milestone 2): where
    /// restaurants are bought and the day starts. A player at the table presses A to look at the
    /// city: their chef stops and faces it, and their stick moves one highlight shared by everyone
    /// looking (the user's call, 2026-11-10) across the lots. A on a lot on sale buys it: that
    /// player names it on the letter grid and, unless its style is fixed, chooses a style. A on a
    /// restaurant already owned makes it current and starts the day there, once every joined player
    /// is at the table, with the same players on the same controllers (PlayerRoster). B steps back,
    /// and from browsing stops looking.
    /// <para>
    /// TODO(milestone 3): every restaurant opens today's kitchen scene; per-restaurant kitchens come next.
    /// </para>
    /// </summary>
    public sealed class MiniatureTable : MonoBehaviour
    {
        public enum Step
        {
            /// <summary>Nobody is looking at the city.</summary>
            Closed,
            Browsing,
            Naming,
            Styling,
        }

        private const string InteractAction = "Interact";
        private const string BackAction = "ButtonB";
        private const string DeleteAction = "Use"; // X
        private const string RerollAction = "ButtonY";
        private const string NavigateAction = "Navigate"; // the D-pad or arrows, not the stick (2026-11-10)

        [Tooltip("Half the size of the area around the table that counts as at it (m, local X and Z): the table plus an arm's reach.")]
        [SerializeField] private Vector2 _halfArea = new Vector2(3.4f, 2.5f);
        [SerializeField] private string _kitchenScene = "Kitchen_Greybox";
        [SerializeField] private OfficeCampaign _campaign;
        [Tooltip("Each lot's spot on the miniature, in the Miniature Config's order.")]
        [SerializeField] private Transform[] _pins = new Transform[0];
        [Tooltip("How far the stick must lean to step, from 0 to 1.")]
        [SerializeField, Range(0.1f, 0.95f)] private float _threshold = 0.5f;
        [Tooltip("On the letter grid, holding steps again after this long (s), then every repeat (s).")]
        [SerializeField, Min(0.02f)] private float _repeatDelay = 0.3f;
        [SerializeField, Min(0.02f)] private float _repeatEvery = 0.11f;

        private readonly List<PlayerInput> _lookers = new List<PlayerInput>();
        private readonly Dictionary<PlayerInput, Vector2Int> _held = new Dictionary<PlayerInput, Vector2Int>();
        private float _heldFor;
        private float _nextRepeat;
        private bool _leaving;
        private string _chosenName;
        private int _rolls;

        /// <summary>Raised just before the kitchen loads, for feedback.</summary>
        public event System.Action Leaving;

        /// <summary>Raised when the highlight, a key or a style moves, for a tick sound.</summary>
        public event System.Action Moved;

        /// <summary>Raised when A or X did something (a letter typed, a step taken), for feedback.</summary>
        public event System.Action Pressed;

        /// <summary>Raised when a press couldn't do anything (Done with no name, Open with someone missing).</summary>
        public event System.Action Refused;

        /// <summary>How many joined players are at the table.</summary>
        public int AtTable { get; private set; }

        /// <summary>How many players have joined.</summary>
        public int Joined => PlayerInput.all.Count;

        /// <summary>
        /// Everyone who has joined is at the table looking at the city: a day can start (user's
        /// call, 2026-11-09). A player elsewhere, or at the table without having pressed A, holds it.
        /// </summary>
        public bool Ready => Joined > 0 && _lookers.Count == Joined;

        public OfficeCampaign Campaign => _campaign;

        public Step Current { get; private set; }

        /// <summary>The highlighted lot, as an index into the campaign's lots.</summary>
        public int Selected { get; private set; }

        /// <summary>The players looking at the city.</summary>
        public IReadOnlyList<PlayerInput> Lookers => _lookers;

        /// <summary>The player naming and styling a purchase, or null.</summary>
        public PlayerInput Buyer { get; private set; }

        /// <summary>The name grid while naming.</summary>
        public NamePicker Picker { get; private set; }

        /// <summary>The name chosen, while choosing a style.</summary>
        public string ChosenName => _chosenName;

        /// <summary>While styling: the first is the previous style, kept; the rest are the config's styles.</summary>
        public IReadOnlyList<string> StyleOptions { get; private set; } = new string[0];

        public int StyleChoice { get; private set; }

        /// <summary>Each lot's spot on the miniature, in the lots' order (some may be missing).</summary>
        public IReadOnlyList<Transform> Pins => _pins;

        private void Update()
        {
            AtTable = 0;
            foreach (var player in PlayerInput.all)
                if (IsAt(player.transform.position))
                    AtTable++;
            if (_leaving || _campaign == null || _campaign.Lots.Count == 0)
                return;

            _lookers.RemoveAll(p => p == null);
            if (Buyer == null && (Current == Step.Naming || Current == Step.Styling))
                CancelBuying(); // the buyer left the game

            // Someone at the table who isn't looking starts looking with A.
            var joinedNow = new List<PlayerInput>();
            foreach (var player in PlayerInput.all)
            {
                if (_lookers.Contains(player) || !IsAt(player.transform.position) || !WasPressed(player, InteractAction))
                    continue;
                if (player.TryGetComponent<PlayerInteractor>(out var hands) && hands.IsBusy)
                    continue;
                Look(player, true);
                joinedNow.Add(player);
            }

            switch (Current)
            {
                case Step.Browsing:
                    Browse(joinedNow);
                    break;
                case Step.Naming:
                    Name();
                    break;
                case Step.Styling:
                    Style();
                    break;
            }
            if (_lookers.Count == 0)
                Current = Step.Closed;
        }

        private void OnDisable()
        {
            foreach (var player in _lookers.ToList())
                if (player != null)
                    Look(player, false);
        }

        // ---- Browsing ----

        private void Browse(List<PlayerInput> joinedNow)
        {
            foreach (var player in _lookers.ToList())
            {
                if (WasPressed(player, BackAction))
                {
                    Look(player, false);
                    continue;
                }
                var step = Push(player, repeat: false);
                if (step != Vector2Int.zero)
                {
                    var next = LotNavigation.Next(Spots(), Selected, step.x, step.y);
                    if (next != Selected && next >= 0)
                    {
                        Selected = next;
                        Moved?.Invoke();
                    }
                }
                if (joinedNow.Contains(player) || !WasPressed(player, InteractAction))
                    continue;
                if (_campaign.Owned(Selected) != null)
                    Open();
                else
                    StartBuying(player);
                if (_leaving || Current != Step.Browsing)
                    return;
            }
        }

        private void Open()
        {
            if (!Ready || !_campaign.Choose(Selected))
            {
                Refused?.Invoke();
                return;
            }
            _leaving = true;
            Leaving?.Invoke();
            foreach (var player in _lookers.ToList())
                Look(player, false); // they walk into the kitchen free
            PlayerRoster.CarryEveryone(); // the same players walk into the kitchen
            SceneManager.LoadScene(_kitchenScene);
        }

        private void StartBuying(PlayerInput player)
        {
            var lot = _campaign.Lots[Selected];
            Buyer = player;
            _chosenName = null;
            Picker = new NamePicker(lot.Name, Suggestions(lot), _campaign.Config.MaxNameLength);
            Current = Step.Naming;
            _held.Clear();
            Pressed?.Invoke();
        }

        // A few names, new ones on every roll, never the lot's own or a restaurant already owned.
        private IReadOnlyList<string> Suggestions(RestaurantSettings lot)
        {
            var taken = _campaign.Campaign.Restaurants.Select(r => r.Name).Append(lot.Name);
            var seed = unchecked(Selected * 7919 + ++_rolls * 104729 + System.Environment.TickCount);
            return RestaurantNaming.Suggest(_campaign.Config.SuggestedNames, seed, _campaign.Config.SuggestionsShown, taken);
        }

        // ---- Naming: only the buyer types; anyone else may still stop looking ----

        private void Name()
        {
            ReleaseOthers();
            if (Buyer == null)
                return;
            if (WasPressed(Buyer, BackAction))
            {
                CancelBuying();
                return;
            }
            if (WasPressed(Buyer, RerollAction))
            {
                Picker.SetSuggestions(Suggestions(_campaign.Lots[Selected]));
                Moved?.Invoke();
            }
            if (WasPressed(Buyer, DeleteAction))
            {
                if (Picker.Delete())
                    Pressed?.Invoke();
                else
                    Refused?.Invoke();
            }
            var step = Push(Buyer, repeat: true);
            if (step != Vector2Int.zero && Picker.Move(step.x, -step.y))
                Moved?.Invoke();
            if (!WasPressed(Buyer, InteractAction))
                return;

            var before = Picker.Text;
            var name = Picker.Press();
            if (name == null)
            {
                if (Picker.Text != before)
                    Pressed?.Invoke();
                else
                    Refused?.Invoke();
                return;
            }
            _chosenName = name;
            var lot = _campaign.Lots[Selected];
            if (lot.FixedStyle)
            {
                Buy(null);
                return;
            }
            var options = new List<string> { lot.PreviousStyle };
            options.AddRange(_campaign.Config.Styles.Where(s => !string.Equals(s, lot.PreviousStyle, System.StringComparison.OrdinalIgnoreCase)));
            StyleOptions = options;
            StyleChoice = 0;
            Current = Step.Styling;
            _held.Clear();
            Pressed?.Invoke();
        }

        // ---- Styling ----

        private void Style()
        {
            ReleaseOthers();
            if (Buyer == null)
                return;
            if (WasPressed(Buyer, BackAction))
            {
                Current = Step.Naming;
                _held.Clear();
                Moved?.Invoke();
                return;
            }
            var step = Push(Buyer, repeat: false);
            var along = step.x != 0 ? step.x : -step.y;
            if (along != 0 && StyleOptions.Count > 0)
            {
                StyleChoice = (StyleChoice + along + StyleOptions.Count) % StyleOptions.Count;
                Moved?.Invoke();
            }
            if (WasPressed(Buyer, InteractAction))
                Buy(StyleChoice == 0 ? null : StyleOptions[StyleChoice]);
        }

        private void Buy(string style)
        {
            _campaign.Buy(Selected, _chosenName, style);
            Buyer = null;
            Picker = null;
            Current = Step.Browsing;
            _held.Clear();
        }

        private void CancelBuying()
        {
            Buyer = null;
            Picker = null;
            Current = _lookers.Count > 0 ? Step.Browsing : Step.Closed;
            _held.Clear();
            Moved?.Invoke();
        }

        // Anyone but the buyer may press B to stop looking while a purchase is under way.
        private void ReleaseOthers()
        {
            foreach (var player in _lookers.ToList())
                if (player != Buyer && WasPressed(player, BackAction))
                    Look(player, false);
        }

        // ---- Helpers ----

        private void Look(PlayerInput player, bool looking)
        {
            if (looking)
            {
                if (_lookers.Count == 0)
                {
                    Current = Step.Browsing;
                    Selected = FirstSelected();
                }
                _lookers.Add(player);
            }
            else
            {
                _lookers.Remove(player);
                _held.Remove(player);
                if (player == Buyer)
                    CancelBuying();
            }
            if (player.TryGetComponent<PlayerInteractor>(out var hands))
                hands.IsBusy = looking;
            if (player.TryGetComponent<PlayerMovement>(out var movement))
            {
                var towards = transform.position - player.transform.position;
                movement.FacingOverride = looking ? Mathf.Atan2(towards.x, towards.z) * Mathf.Rad2Deg : (float?)null;
            }
        }

        // The current restaurant if there is one, else the first lot on sale (a new campaign's grill).
        private int FirstSelected()
        {
            var current = _campaign.Campaign.Current;
            for (var i = 0; i < _campaign.Lots.Count; i++)
                if (current != null && _campaign.Lots[i].Id == current.DefinitionId)
                    return i;
            for (var i = 0; i < _campaign.Lots.Count; i++)
                if (_campaign.Owned(i) == null)
                    return i;
            return 0;
        }

        private List<(float X, float Z)> Spots()
        {
            var spots = new List<(float X, float Z)>();
            for (var i = 0; i < _campaign.Lots.Count; i++)
            {
                var pin = i < _pins.Length ? _pins[i] : null;
                var at = pin != null ? pin.position : transform.position + Vector3.right * i; // a row, if the builder left pins out
                spots.Add((at.x, at.z));
            }
            return spots;
        }

        // The D-pad (or arrow keys) as one step (x right, y up): on the push, then, with repeat, again while held.
        private Vector2Int Push(PlayerInput player, bool repeat)
        {
            var value = player.actions[NavigateAction].ReadValue<Vector2>();
            var direction = Vector2Int.zero;
            if (Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y)) >= _threshold)
            {
                // The stronger axis wins, so a slightly diagonal push on the grid doesn't skip a row.
                direction = Mathf.Abs(value.x) >= Mathf.Abs(value.y)
                    ? new Vector2Int((int)Mathf.Sign(value.x), 0)
                    : new Vector2Int(0, (int)Mathf.Sign(value.y));
            }
            _held.TryGetValue(player, out var held);
            if (direction == Vector2Int.zero)
            {
                _held[player] = Vector2Int.zero;
                return Vector2Int.zero;
            }
            if (direction != held)
            {
                _held[player] = direction;
                _heldFor = 0f;
                _nextRepeat = _repeatDelay;
                return direction;
            }
            if (!repeat)
                return Vector2Int.zero;
            _heldFor += Time.deltaTime;
            if (_heldFor < _nextRepeat)
                return Vector2Int.zero;
            _nextRepeat += _repeatEvery;
            return direction;
        }


        private static bool WasPressed(PlayerInput player, string action) =>
            player.actions[action].WasPressedThisFrame();

        private bool IsAt(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            return Mathf.Abs(local.x) <= _halfArea.x && Mathf.Abs(local.z) <= _halfArea.y;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(_halfArea.x * 2f, 1f, _halfArea.y * 2f));
        }
    }
}
