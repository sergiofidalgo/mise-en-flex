using System.Collections.Generic;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Developer overlay for tuning, organised by where things are so it reads at a glance:
    /// players (device, target, hands, tool, triggers), hobs (heat and pan temperature with the
    /// food in each pan), food in play (anywhere outside storage, with its location), and a one-line
    /// summary per storage unit. Values are coloured by state. F1 toggles it. Players read food by
    /// eye; this is for tuning and debugging.
    /// </summary>
    public sealed class CookingDebugOverlay : MonoBehaviour
    {
        private const float Margin = 10f;
        private const float MaxWidth = 860f;

        // Column widths for food rows, in pixels.
        private const float NameWidth = 110f;
        private const float DonenessWidth = 100f;
        private const float CoreWidth = 150f;
        private const float CrustWidth = 110f;
        private const float HotWidth = 70f;
        private const float FreshWidth = 70f;
        private const float LocationWidth = 150f;

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private bool _visible = true;
        [SerializeField, Min(8)] private int _fontSize = 12;
        [SerializeField, Range(0f, 1f)] private float _backgroundOpacity = 0.88f;

        private static readonly Color Dim = new Color(0.65f, 0.67f, 0.72f);
        private static readonly Color Good = new Color(0.55f, 0.88f, 0.55f);
        private static readonly Color Warn = new Color(0.95f, 0.78f, 0.38f);
        private static readonly Color Bad = new Color(0.98f, 0.45f, 0.40f);
        private static readonly Color Cold = new Color(0.55f, 0.72f, 0.95f);
        private static readonly Color Heading = new Color(0.85f, 0.88f, 0.95f);

        private static readonly Dictionary<string, Color> DonenessColours = new Dictionary<string, Color>
        {
            { "Raw", Cold },
            { "Rare", new Color(0.92f, 0.38f, 0.40f) },
            { "Medium-rare", new Color(0.96f, 0.58f, 0.65f) },
            { "Medium", new Color(0.88f, 0.66f, 0.52f) },
            { "Medium-well", new Color(0.75f, 0.62f, 0.50f) },
            { "Well done", new Color(0.65f, 0.62f, 0.60f) },
            { "Softened", new Color(0.92f, 0.85f, 0.55f) },
            { "Cooked", new Color(0.85f, 0.72f, 0.40f) },
        };

        private readonly Dictionary<Ingredient, string> _locations = new Dictionary<Ingredient, string>();
        private readonly HashSet<Ingredient> _stored = new HashSet<Ingredient>();
        private Interactable[] _stations;
        private GUIStyle _panel;
        private GUIStyle _text;
        private GUIStyle _bold;
        private GUIStyle _heading;
        private Texture2D _background;

        private void Start()
        {
            _stations = FindObjectsByType<Interactable>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
        }

        private void Update()
        {
            // Debug-only shortcut, deliberately outside the rebindable game controls.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                _visible = !_visible;
        }

        private void OnGUI()
        {
            var services = _root != null ? _root.Services : null;
            if (!_visible || services == null || _stations == null)
                return;

            EnsureStyles();
            MapLocations(services);

            var width = Mathf.Min(MaxWidth, Screen.width - 2f * Margin);
            GUILayout.BeginArea(new Rect(Margin, Margin, width, Screen.height - 2f * Margin));
            GUILayout.BeginVertical(_panel);

            var cooking = services.Cooking;
            var minutes = (int)(cooking.ElapsedSeconds / 60.0);
            GUILayout.Label($"<b>Kitchen debug</b>   game time {minutes / 60:00}:{minutes % 60:00}   ×{services.Tuning.TimeScale:0}   <color=#888>(F1 hides)</color>", _text);

            DrawPlayers();
            DrawHobs();
            DrawFoodInPlay(cooking);
            DrawStorage();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawPlayers()
        {
            Section("Players");
            foreach (var player in PlayerInput.all)
            {
                var interactor = player.GetComponent<PlayerInteractor>();
                var device = string.Join(", ", player.devices.Select(d => d.displayName));
                var target = interactor != null && interactor.Target != null ? interactor.Target.name : "nothing";
                var hands = interactor != null ? Describe(interactor.Hands.Current) : "?";
                var tool = interactor != null && interactor.Tool != null ? "  knife ✓" : string.Empty;
                var down = player.actions.FindAction("HeatDown")?.ReadValue<float>() ?? 0f;
                var up = player.actions.FindAction("HeatUp")?.ReadValue<float>() ?? 0f;
                GUILayout.Label(
                    $"<b>P{player.playerIndex + 1}</b>  {device}   facing <b>{target}</b>   holding <b>{hands}</b>{tool}   " +
                    $"<color=#888>LT {down:0.0}  RT {up:0.0}</color>", _text);
            }
        }

        private void DrawHobs()
        {
            Section("Hobs");
            var index = 0;
            foreach (var hob in _stations.OfType<HobStation>())
            {
                index++;
                if (hob.Hob == null)
                    continue;
                var pan = hob.Hob.Pan;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Hob {index}</b>", _text, GUILayout.Width(60f));
                GUILayout.Label($"heat {Bar(hob.Hob.Dial)} {hob.Hob.Dial:P0}", _text, GUILayout.Width(180f));
                GUILayout.Label(pan != null ? Coloured($"pan {pan.Temperature:0} °C  {PanState(pan.Temperature)}", PanColour(pan.Temperature)) : Coloured("no pan", Dim), _text);
                GUILayout.EndHorizontal();
                if (pan != null)
                {
                    foreach (var food in pan.Contents)
                        FoodRow(food, "   in pan");
                }
            }

            // Pans away from a hob (carried, on a counter): their heat matters too.
            foreach (var pan in _root.Services.Cooking.Pans)
            {
                if (_stations.OfType<HobStation>().Any(h => h.Hob?.Pan == pan))
                    continue;
                GUILayout.Label($"<b>Pan off the heat</b>  {Coloured($"{pan.Temperature:0} °C", PanColour(pan.Temperature))}  {Coloured(PanLocation(pan), Dim)}", _text);
                foreach (var food in pan.Contents)
                    FoodRow(food, "   in pan");
            }
        }

        private void DrawFoodInPlay(CookingSimulation cooking)
        {
            Section("Food in play");
            FoodHeader();
            var any = false;
            foreach (var food in cooking.Ingredients)
            {
                if (food.Pan != null || _stored.Contains(food))
                    continue;
                any = true;
                FoodRow(food, _locations.TryGetValue(food, out var where) ? where : "loose");
            }
            if (!any)
                GUILayout.Label(Coloured("nothing out of storage", Dim), _text);
        }

        private void DrawStorage()
        {
            Section("Storage");
            foreach (var storage in _stations.OfType<StorageStation>())
            {
                if (storage.Unit == null)
                    continue;
                var parts = storage.Unit.Slots
                    .Where(s => s.IsUsed)
                    .Select(s => s.Count > 0 ? $"{s.TypeId} ×{s.Count}" : Coloured($"{s.TypeId} ×0", Bad));
                var fresh = AverageFreshness(storage.Unit.Slots.SelectMany(SlotItems));
                GUILayout.Label($"<b>{storage.name}</b> {storage.Unit.Temperature:0} °C:  {string.Join(",  ", parts)}   {Coloured($"fresh {fresh:P0}", FreshColour(fresh))}", _text);
            }

            foreach (var prep in _stations.OfType<PrepStation>())
            {
                var counter = prep.Counter;
                if (counter == null)
                    continue;
                var slots = counter.Slots.Where(s => s.Items.Count > 0).Select(s => $"{s.TypeId} ×{s.Items.Count}").ToArray();
                var board = counter.Current != null ? $"board: {counter.Current.TypeId} ({counter.Cuts}/{counter.CutsNeeded} cuts)" : "board empty";
                GUILayout.Label($"<b>{prep.name}</b>  {board}   slots: {(slots.Length > 0 ? string.Join(",  ", slots) : "empty")}", _text);
            }
        }

        private void FoodHeader()
        {
            GUILayout.BeginHorizontal();
            Header("food", NameWidth);
            Header("doneness", DonenessWidth);
            Header("core now → peak", CoreWidth);
            Header("crust A / B", CrustWidth);
            Header("hot", HotWidth);
            Header("fresh", FreshWidth);
            Header("where", LocationWidth);
            Header("rest / cut", 0f);
            GUILayout.EndHorizontal();
        }

        private void FoodRow(Ingredient food, string location)
        {
            var p = food.Properties;
            var doneness = food.Doneness?.Name ?? (p.DonenessLevels.Count > 0 ? "Raw" : "—");
            var target = p.TargetCoreTemperature.HasValue ? $" <color=#888>/ {p.TargetCoreTemperature.Value:0}</color>" : string.Empty;
            var burntAt = Mathf.Max(p.BurntBrowning, 0.0001f);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{food.TypeId}</b>", _text, GUILayout.Width(NameWidth));
            GUILayout.Label(Coloured(doneness, DonenessColours.TryGetValue(doneness, out var c) ? c : Dim), _bold, GUILayout.Width(DonenessWidth));
            GUILayout.Label($"{food.CoreTemperature:0}° → {food.PeakCoreTemperature:0}°{target}", _text, GUILayout.Width(CoreWidth));
            GUILayout.Label(
                $"{Coloured($"{food.Browning(IngredientSide.A):0.0}", CrustColour(food.Browning(IngredientSide.A) / burntAt))} / " +
                $"{Coloured($"{food.Browning(IngredientSide.B):0.0}", CrustColour(food.Browning(IngredientSide.B) / burntAt))}" +
                (food.IsBurnt ? Coloured("  BURNT", Bad) : string.Empty),
                _text, GUILayout.Width(CrustWidth));
            GUILayout.Label(p.ServedHot ? Coloured($"{food.AverageTemperature:0} °C", HotColour(food.AverageTemperature, p)) : Coloured("—", Dim), _text, GUILayout.Width(HotWidth));
            GUILayout.Label(Coloured($"{food.Freshness:P0}", FreshColour(food.Freshness)), _text, GUILayout.Width(FreshWidth));
            GUILayout.Label(Coloured(location, Dim), _text, GUILayout.Width(LocationWidth));

            var extras = new List<string>();
            if (p.RestTargetSeconds > 0f && food.Doneness.HasValue)
                extras.Add($"rest {Mathf.Min(food.SecondsOutOfPan / p.RestTargetSeconds, 1f):P0}");
            if (food.CutEvenness.HasValue)
                extras.Add($"cut {food.CutEvenness.Value:P0}");
            GUILayout.Label(string.Join("  ", extras), _text);
            GUILayout.EndHorizontal();
        }

        // Where every ingredient outside a pan is, keyed for the food table; storage contents are
        // collected separately and summarised instead of listed.
        private void MapLocations(KitchenServices services)
        {
            _locations.Clear();
            _stored.Clear();

            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent(out PlayerInteractor interactor))
                    Locate(interactor.Hands.Current, $"P{player.playerIndex + 1} hands");
            }

            foreach (var station in _stations)
            {
                switch (station)
                {
                    case StorageStation storage when storage.Unit != null:
                        foreach (var food in storage.Unit.Slots.SelectMany(SlotItems))
                            _stored.Add(food);
                        break;
                    case PrepStation prep when prep.Counter != null:
                        foreach (var slot in prep.Counter.Slots)
                        {
                            foreach (var food in slot.Items)
                                _stored.Add(food);
                        }
                        Locate(prep.Counter.Current, $"{prep.name} board");
                        break;
                    default:
                        Locate(station.HeldItem, station.name);
                        break;
                }
            }
        }

        private void Locate(Item item, string where)
        {
            switch (item)
            {
                case Ingredient food:
                    _locations[food] = where;
                    break;
                case Plate plate:
                    foreach (var food in plate.Items.OfType<Ingredient>())
                        _locations[food] = $"plate · {where}";
                    break;
            }
        }

        private string PanLocation(Pan pan)
        {
            foreach (var player in PlayerInput.all)
            {
                if (player.TryGetComponent(out PlayerInteractor interactor) && interactor.Hands.Current == pan)
                    return $"P{player.playerIndex + 1} hands";
            }
            var station = _stations.FirstOrDefault(s => s.HeldItem == pan);
            return station != null ? station.name : "loose";
        }

        private static IEnumerable<Ingredient> SlotItems(Game.Simulation.Storage.StorageUnit.Slot slot) => slot.Stock;

        private static string Describe(Item item) => item switch
        {
            null => "nothing",
            Pan pan => pan.Contents.Count > 0 ? $"pan ({pan.Contents[0].TypeId})" : "empty pan",
            Plate plate => plate.Items.Count > 0 ? $"plate ({string.Join(", ", plate.Items.Select(i => i.TypeId))})" : "empty plate",
            _ => item.TypeId,
        };

        private void Section(string title)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title.ToUpperInvariant(), _heading);
        }

        private void Header(string title, float width)
        {
            var content = Coloured(title, Dim);
            if (width > 0f)
                GUILayout.Label(content, _text, GUILayout.Width(width));
            else
                GUILayout.Label(content, _text);
        }

        private static float AverageFreshness(IEnumerable<Ingredient> items)
        {
            var list = items.ToList();
            return list.Count > 0 ? list.Average(i => i.Freshness) : 1f;
        }

        private static string Bar(float fraction)
        {
            const int cells = 10;
            var filled = Mathf.RoundToInt(Mathf.Clamp01(fraction) * cells);
            return new string('|', filled) + $"<color=#555>{new string('|', cells - filled)}</color>";
        }

        private static string PanState(float temperature) =>
            temperature < 100f ? "cold" : temperature < 140f ? "warm, won't sear" : temperature < 230f ? "searing" : "smoking";

        private static Color PanColour(float temperature) =>
            temperature < 100f ? Dim : temperature < 140f ? Warn : temperature < 230f ? Good : Bad;

        // Browning as a fraction of burnt: pale, building, a good crust, then dark and burnt.
        private static Color CrustColour(float fractionOfBurnt) =>
            fractionOfBurnt < 0.15f ? Dim : fractionOfBurnt < 0.35f ? Warn : fractionOfBurnt < 0.7f ? Good : fractionOfBurnt < 1f ? Warn : Bad;

        private static Color HotColour(float temperature, IngredientProperties p) =>
            temperature >= p.ServingIdealTemperature ? Good : temperature > p.ServingColdTemperature ? Warn : Cold;

        private static Color FreshColour(float freshness) => freshness >= 0.85f ? Good : freshness >= 0.6f ? Warn : Bad;

        private static string Coloured(string text, Color colour) => $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{text}</color>";

        private void EnsureStyles()
        {
            if (_panel != null)
                return;

            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.11f, _backgroundOpacity));
            _background.Apply();

            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            _panel.normal.background = _background;
            _text = new GUIStyle(GUI.skin.label) { richText = true, fontSize = _fontSize, wordWrap = false, margin = new RectOffset(2, 2, 1, 1) };
            _text.normal.textColor = new Color(0.92f, 0.93f, 0.95f);
            _bold = new GUIStyle(_text) { fontStyle = FontStyle.Bold };
            _heading = new GUIStyle(_text) { fontStyle = FontStyle.Bold, fontSize = _fontSize - 1 };
            _heading.normal.textColor = Heading;
        }

        private void OnDestroy()
        {
            if (_background != null)
                Destroy(_background);
        }
    }
}
