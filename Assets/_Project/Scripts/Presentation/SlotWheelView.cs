using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>
    /// The ingredient wheel in the scene (decision 2026-11-11), replacing the flat card for an open
    /// slot menu: storage or a stall with RT held, or a tray's menu. Four chunky tiles stand over
    /// the station in a diamond laid out like the face buttons (A below, B right, X left, Y above),
    /// each with the ingredient's picture, how many are left, and its button in Xbox colours on its
    /// outer edge. Empty slots are dimmed. A ring in the player's colour marks whose wheel it is.
    /// It pops in like the gauges, a tile pops when something is taken from it, it faces the camera
    /// (so it reads at every rotation), it's sized on screen in pixels, and it stays inside the screen.
    /// </summary>
    public sealed class SlotWheelView : MonoBehaviour
    {
        private const int TileOrder = 40;
        private const int ButtonOrder = 44;
        private static readonly int ZTestId = Shader.PropertyToID("unity_GUIZTestMode");

        [SerializeField] private Material _onTop;
        [Tooltip("Game/FX/HintOnTop: the pictures, drawn over the chef.")]
        [SerializeField] private Material _iconMaterial;
        [SerializeField] private IngredientIcons _icons;
        [SerializeField] private HudStyle _style;
        [SerializeField] private CoopSettings _settings;

        [Header("Look")]
        [Tooltip("Pixels across the whole wheel on a 1080p screen.")]
        [SerializeField, Min(40f)] private float _pixels = 190f;
        [Tooltip("Metres above the station's top where the wheel's middle sits.")]
        [SerializeField, Min(0f)] private float _above = 0.45f;
        [SerializeField] private Color _tile = new Color32(0xEA, 0xDF, 0xC6, 0xFF);
        [SerializeField] private Color _emptyTile = new Color32(0x8B, 0x7E, 0x6C, 0xFF);
        [SerializeField, Min(0.01f)] private float _popInSeconds = 0.16f;
        [SerializeField, Min(0.01f)] private float _takeSeconds = 0.25f;

        /// <summary>True while a wheel is in the scene: the flat card stays away.</summary>
        public static bool Present { get; private set; }

        private readonly Dictionary<PlayerInput, Kit> _kits = new Dictionary<PlayerInput, Kit>();
        private readonly List<PromptLine> _lines = new List<PromptLine>();
        private readonly Dictionary<Interactable, float> _tops = new Dictionary<Interactable, float>();
        private Material _iconOnTop;
        private Material _letters;

        // Tile centres in wheel units, in PromptControl order: South, East, West, North.
        private static readonly Vector2[] Places = { new Vector2(0f, -0.27f), new Vector2(0.27f, 0f), new Vector2(-0.27f, 0f), new Vector2(0f, 0.27f) };

        private sealed class Kit
        {
            public Transform Root;
            public Renderer Ring;
            public Tile[] Tiles;
            public float Shown;
        }

        private sealed class Tile
        {
            public Transform Root;
            public Renderer Body;
            public SpriteRenderer Icon;
            public TextMeshPro Count;
            public Transform Badge;
            public TextMeshPro Label;
            public Transform Button;
            public string Shown;
            public int LastCount = -1;
            public float Pop;
        }

        /// <summary>Players whose wheel is showing, for tests.</summary>
        public int Showing
        {
            get
            {
                var count = 0;
                foreach (var kit in _kits.Values)
                    if (kit.Shown > 0f)
                        count++;
                return count;
            }
        }

        private void OnEnable() => Present = true;

        private void OnDisable() => Present = false;

        private void LateUpdate()
        {
            var camera = Camera.main;
            var dt = Time.unscaledDeltaTime;
            foreach (var player in PlayerInput.all)
            {
                if (!_kits.TryGetValue(player, out var kit))
                    _kits[player] = kit = Build(player.playerIndex);
                var station = Lines(player);
                var wanted = station != null && camera != null && _lines.Count > 0;
                kit.Shown = Mathf.MoveTowards(kit.Shown, wanted ? 1f : 0f, dt / _popInSeconds);
                kit.Root.gameObject.SetActive(kit.Shown > 0f);
                if (!wanted || kit.Shown <= 0f)
                {
                    if (!wanted)
                        foreach (var tile in kit.Tiles)
                            tile.LastCount = -1;
                    continue;
                }

                var where = station.transform.position + Vector3.up * (Top(station) + _above);
                TimingGaugeKit.FaceCamera(kit.Root, camera, where,
                    TimingGaugeKit.MetresFor(camera, where, _pixels) * Mathf.Max(0.001f, TimingGaugeKit.EaseOutBack(kit.Shown)), 0.12f);
                for (var i = 0; i < kit.Tiles.Length; i++)
                    DrawTile(kit.Tiles[i], i < _lines.Count ? _lines[i] : (PromptLine?)null, dt);
            }
        }

        // The open menu's lines for this player, and the station it's at, or null.
        private Interactable Lines(PlayerInput player)
        {
            _lines.Clear();
            if (!player.TryGetComponent(out PlayerInteractor chef) || !chef.CanAct || chef.IsReviewing || chef.Target == null)
                return null;
            if (chef.IsTrayMenuOpen)
                chef.DescribeTrayMenu(_lines);
            else if (chef.Target is IStationPrompt prompt && prompt.PromptAsDiamond(chef))
                prompt.DescribeActions(chef, _lines);
            return _lines.Count > 0 ? chef.Target : null;
        }

        private void DrawTile(Tile tile, PromptLine? line, float dt)
        {
            var available = line.HasValue && line.Value.Available && line.Value.Label != "—";
            TimingGaugeKit.Paint(tile.Body, available ? _tile : _emptyTile);

            // The picture, or the words when there's no picture (a tray's plate).
            var icon = line.HasValue && line.Value.IconId != null && _icons != null ? _icons.Find(line.Value.IconId) : null;
            tile.Icon.sprite = icon;
            tile.Icon.enabled = icon != null;
            tile.Icon.color = available ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (icon != null)
            {
                var size = Mathf.Max(icon.bounds.size.x, icon.bounds.size.y);
                tile.Icon.transform.localScale = Vector3.one * (0.17f / Mathf.Max(size, 0.001f));
            }
            var label = line.HasValue && icon == null && line.Value.Label != "—" ? line.Value.Label : string.Empty;
            if (tile.Label.text != label)
                tile.Label.text = label;
            var number = ParseCount(line.HasValue ? line.Value.IconLabel ?? string.Empty : string.Empty);
            var count = number >= 0 ? $"×{number}" : string.Empty;
            if (tile.Count.text != count)
                tile.Count.text = count;
            tile.Badge.gameObject.SetActive(number > 0);

            // Something taken: the tile pops.
            if (tile.LastCount >= 0 && number < tile.LastCount)
                tile.Pop = _takeSeconds;
            tile.LastCount = number;
            tile.Pop = Mathf.Max(0f, tile.Pop - dt);
            var pop = tile.Pop / _takeSeconds;
            tile.Root.localScale = Vector3.one * (1f + 0.25f * pop * pop);
            tile.Button.gameObject.SetActive(line.HasValue);
        }

        private static int ParseCount(string label)
        {
            var digits = label.TrimStart('×', 'x', ' ');
            return int.TryParse(digits, out var n) ? n : -1;
        }

        private float Top(Interactable station)
        {
            if (_tops.TryGetValue(station, out var top))
                return top;
            top = 0.9f;
            foreach (var renderer in station.GetComponentsInChildren<MeshRenderer>())
                top = Mathf.Max(top, renderer.bounds.max.y - station.transform.position.y);
            _tops[station] = top;
            return top;
        }

        private Kit Build(int playerIndex)
        {
            var root = new GameObject($"SlotWheel_P{playerIndex + 1}") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(transform, false);
            var kit = new Kit { Root = root, Tiles = new Tile[4] };

            // The hub: a ring in the player's colour round an Ink disc, whose wheel it is.
            var colour = _settings != null ? _settings.GetPlayerColor(playerIndex) : Color.white;
            kit.Ring = TimingGaugeKit.Disc(root, "Ring", _onTop, colour, TileOrder - 2);
            kit.Ring.transform.localScale = new Vector3(0.2f, 0.01f, 0.2f);
            var hub = TimingGaugeKit.Disc(root, "Hub", _onTop, TimingGaugeKit.Ink, TileOrder - 1);
            hub.transform.localScale = new Vector3(0.13f, 0.01f, 0.13f);
            hub.transform.localPosition = new Vector3(0f, 0f, -0.005f);

            for (var i = 0; i < kit.Tiles.Length; i++)
                kit.Tiles[i] = BuildTile(root, (PromptControl)i);
            root.gameObject.SetActive(false);
            return kit;
        }

        private Tile BuildTile(Transform parent, PromptControl control)
        {
            var tile = new Tile { Root = new GameObject($"Tile_{control}") { hideFlags = HideFlags.DontSave }.transform };
            tile.Root.SetParent(parent, false);
            var at = Places[(int)control];
            tile.Root.localPosition = new Vector3(at.x, at.y, -0.01f);

            // Square tiles with rounded corners, close together (the user's call: not circles, and
            // less disjointed than square-cornered tiles far apart).
            var rim = TimingGaugeKit.Piece(tile.Root, "Rim", TimingGaugeKit.RoundedRect(0.27f, 0.27f, 0.06f), _onTop, TimingGaugeKit.Ink, TileOrder);
            tile.Body = TimingGaugeKit.Piece(tile.Root, "Body", TimingGaugeKit.RoundedRect(0.24f, 0.24f, 0.045f), _onTop, _tile, TileOrder + 1);
            tile.Body.transform.localPosition = new Vector3(0f, 0f, -0.01f);

            var icon = new GameObject("Icon") { hideFlags = HideFlags.DontSave };
            icon.transform.SetParent(tile.Root, false);
            icon.transform.localPosition = new Vector3(-0.012f, 0.025f, -0.03f);
            tile.Icon = icon.AddComponent<SpriteRenderer>();
            tile.Icon.sharedMaterial = IconMaterial();
            tile.Icon.sortingOrder = TileOrder + 2;
            tile.Icon.shadowCastingMode = ShadowCastingMode.Off;

            // How many are left, "×6", in the tile's bottom-right corner, big enough to read at a
            // glance (the user's call: the corner count, bigger).
            tile.Badge = new GameObject("Count") { hideFlags = HideFlags.DontSave }.transform;
            tile.Badge.SetParent(tile.Root, false);
            tile.Count = Text(tile.Badge, "Count", new Vector3(0.008f, -0.07f, -0.04f), 0.095f, TimingGaugeKit.Ink, TextAlignmentOptions.Right);
            tile.Count.rectTransform.sizeDelta = new Vector2(0.21f, 0.12f);
            tile.Label = Text(tile.Root, "Label", new Vector3(0f, 0f, -0.04f), 0.045f, TimingGaugeKit.Ink, TextAlignmentOptions.Center);

            // The face button on the tile's outer edge, as on the pad.
            var outward = at.normalized;
            tile.Button = FaceButton(tile.Root, control);
            tile.Button.localPosition = new Vector3(outward.x * 0.14f, outward.y * 0.14f, -0.05f);
            tile.Button.localScale = Vector3.one * 0.1f;
            return tile;
        }

        // A face button: a disc in its Xbox colour with an Ink rim and its letter.
        private Transform FaceButton(Transform parent, PromptControl control)
        {
            var root = new GameObject("Button") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(parent, false);
            var rim = TimingGaugeKit.Disc(root, "Rim", _onTop, TimingGaugeKit.Ink, ButtonOrder);
            rim.transform.localScale = new Vector3(1f, 0.05f, 1f);
            var colour = _style != null ? _style.BadgeColor(control, true) : Color.grey;
            var face = TimingGaugeKit.Disc(root, "Face", _onTop, colour, ButtonOrder + 1);
            face.transform.localScale = new Vector3(0.82f, 0.05f, 0.82f);
            face.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            var letter = control switch
            {
                PromptControl.South => "A",
                PromptControl.East => "B",
                PromptControl.West => "X",
                _ => "Y",
            };
            Text(root, "Letter", new Vector3(0f, 0f, -0.05f), 0.62f, Color.white, TextAlignmentOptions.Center).text = letter;
            return root;
        }

        private TextMeshPro Text(Transform parent, string name, Vector3 at, float height, Color colour, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var text = go.AddComponent<TextMeshPro>();
            if (_style != null)
                text.font = _style.Bold;
            text.fontSharedMaterial = LetterMaterial(text.font);
            text.color = colour;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(height * 4f, height * 1.4f);
            text.enableAutoSizing = false;
            // Font size in world units: TMP's 10 is about one unit's cap height... measured once.
            text.fontSize = 10f;
            text.text = "8";
            text.ForceMeshUpdate();
            var measured = text.textBounds.size.y;
            if (measured > 0.0001f)
                text.fontSize *= height * 0.72f / measured;
            text.text = string.Empty;
            text.sortingOrder = ButtonOrder + 4;
            return text;
        }

        // The pictures and the letters are drawn after the toon tiles (both draw over the chef).
        private Material IconMaterial()
        {
            if (_iconOnTop != null || _iconMaterial == null)
                return _iconOnTop != null ? _iconOnTop : _iconMaterial;
            _iconOnTop = new Material(_iconMaterial) { name = "WheelIcons" };
            _iconOnTop.renderQueue = (_onTop != null ? _onTop.renderQueue : 3400) + 3;
            return _iconOnTop;
        }

        private Material LetterMaterial(TMP_FontAsset font)
        {
            if (_letters != null || font == null)
                return _letters != null ? _letters : font?.material;
            _letters = new Material(font.material) { name = "WheelLetters" };
            _letters.SetFloat(ZTestId, (float)CompareFunction.Always);
            _letters.renderQueue = (_onTop != null ? _onTop.renderQueue : 3400) + 6;
            return _letters;
        }

        private void OnDestroy()
        {
            if (_iconOnTop != null)
                Destroy(_iconOnTop);
            if (_letters != null)
                Destroy(_letters);
        }
    }
}
