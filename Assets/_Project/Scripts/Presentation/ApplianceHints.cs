using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Cleaning;
using Game.Gameplay.Stations;
using Game.Presentation.Hud;
using Game.Simulation.Cleaning;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Presentation
{
    /// <summary>What a hint on an appliance teaches. Each is learnt, and fades, on its own.</summary>
    public enum ApplianceHint
    {
        /// <summary>LT/RT at a hob: heat down and up.</summary>
        Heat,
        /// <summary>LT/RT at storage or a stall: hold to browse its slots.</summary>
        Browse,
        /// <summary>X at a hob or the grill with food in the pan: flip.</summary>
        Flip,
        /// <summary>X at something dirty, a sink with plates, or a fire: clean.</summary>
        Clean,
    }

    /// <summary>
    /// The only control hints left in the kitchen (the 2026-11-11 playtest round): small button
    /// pictures sitting on the appliance's top, LT RT for heat and browsing, X for flipping and
    /// cleaning. A hint fades in while a chef is at the appliance, facing it, and it does something
    /// there; it stops showing for that player once they've done it <see cref="_usesBeforeFading"/>
    /// times. The pictures are the control-glyph kit's dark bodies with the letter on top, in the
    /// scene (on the station, following it when it's moved) rather than on a panel. They stand on
    /// the appliance's top at the edge away from the chef, LT RT then X, facing the camera, so they
    /// read at any of the 8 rotations with the chef on any side: from the overhead camera the far
    /// edge is past the chef's head, not under it. Drawn over the chef anyway, as the wash ring is.
    /// </summary>
    public sealed class ApplianceHints : MonoBehaviour
    {
        private const string HeatDownActionName = "HeatDown";
        private const string HeatUpActionName = "HeatUp";
        private const float TriggerPressed = 0.5f;
        private static readonly int ZTestId = Shader.PropertyToID("unity_GUIZTestMode");

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private HudStyle _style;
        [Tooltip("The kit's round button body (TEX_Glyph_Button).")]
        [SerializeField] private Sprite _button;
        [Tooltip("The kit's trigger body (TEX_Glyph_Trigger).")]
        [SerializeField] private Sprite _trigger;
        [Tooltip("Game/FX/HintOnTop: unlit, drawn over the chef.")]
        [SerializeField] private Material _material;
        [Tooltip("Game/ToonOnTop: when set, LT RT are drawn as toon keys like the gauges' X (decision 2026-11-11) instead of the glyph sprites.")]
        [SerializeField] private Material _onTop;

        [Header("Learning")]
        [Tooltip("Times a player does an action before its hint stops showing for them.")]
        [SerializeField, Min(1)] private int _usesBeforeFading = 3;

        [Header("Look")]
        [Tooltip("Height of a button picture (m) as built; it's then scaled to _pixels on screen.")]
        [SerializeField, Min(0.05f)] private float _size = 0.32f;
        [Tooltip("Height of a button picture on a 1080p screen, whatever the camera's distance: the X hint's size (the user's call, 2026-11-11: 32).")]
        [SerializeField, Min(8f)] private float _pixels = 32f;
        [Tooltip("Gap between the appliance's top and the bottom of a picture (m).")]
        [SerializeField, Min(0f)] private float _lift = 0.03f;
        [Tooltip("How far in from the far edge the pictures stand (m).")]
        [SerializeField, Min(0f)] private float _inset = 0.12f;
        [Tooltip("Gap between the LT RT picture and the X picture (m).")]
        [SerializeField, Min(0f)] private float _gap = 0.05f;
        [Tooltip("Seconds to fade in or out.")]
        [SerializeField, Min(0.01f)] private float _fade = 0.2f;
        [Tooltip("Size a picture pops in from, as a share of its size.")]
        [SerializeField, Range(0.3f, 1f)] private float _popFrom = 0.7f;
        [SerializeField] private Color _triggerLetters = Color.white;

        private readonly Dictionary<Interactable, Pair> _pairs = new Dictionary<Interactable, Pair>();
        private readonly Dictionary<(int Player, ApplianceHint Hint), int> _uses = new Dictionary<(int, ApplianceHint), int>();
        private readonly Dictionary<PlayerInteractor, Watch> _watched = new Dictionary<PlayerInteractor, Watch>();
        private readonly Dictionary<Interactable, (bool Triggers, bool X, PlayerInteractor Chef)> _wanted = new Dictionary<Interactable, (bool, bool, PlayerInteractor)>();
        private readonly List<PromptLine> _lines = new List<PromptLine>();
        private readonly List<Interactable> _gone = new List<Interactable>();
        private Material _letterMaterial;

        /// <summary>Times a player has done what a hint teaches, for tests.</summary>
        public int UsesOf(int player, ApplianceHint hint) => _uses.TryGetValue((player, hint), out var n) ? n : 0;

        /// <summary>Whether a station's LT RT or X picture is showing now (fading counts), for tests.</summary>
        public bool IsShowing(Interactable station, bool triggers) =>
            _pairs.TryGetValue(station, out var pair) && (triggers ? pair.Triggers : pair.X).Shown > 0f;

        /// <summary>The number of uses after which a hint stops showing (tunable).</summary>
        public int UsesBeforeFading => _usesBeforeFading;

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _root == null || _root.Services == null)
                return;

            _wanted.Clear();
            foreach (var player in PlayerInput.all)
            {
                if (!player.TryGetComponent(out PlayerInteractor chef))
                    continue;
                Learn(chef, player);
                var station = chef.Target;
                if (station == null || !chef.CanAct || chef.IsReviewing || chef.IsManaging || chef.IsBusy
                    || (chef.Cleaning != null && chef.Cleaning.IsCleaning))
                    continue;
                var (triggers, x) = HintsFor(chef, station);
                // Not while doing it (a trigger pulled, the slot menu open), and not at a hob whose
                // food shows its crust strip: the strip is the interface then (the user's call).
                if (triggers.HasValue && (chef.IsSlotMenuOpen || TriggerPulled(chef) || Cooking(station)))
                    triggers = null;
                triggers = triggers.HasValue && UsesOf(chef.PlayerIndex, triggers.Value) < _usesBeforeFading ? triggers : null;
                x = x.HasValue && UsesOf(chef.PlayerIndex, x.Value) < _usesBeforeFading ? x : null;
                if (triggers == null && x == null)
                    continue;
                _wanted.TryGetValue(station, out var before);
                _wanted[station] = (before.Triggers || triggers != null, before.X || x != null, before.Chef != null ? before.Chef : chef);
            }

            foreach (var pair in _wanted)
                if (!_pairs.ContainsKey(pair.Key))
                    _pairs[pair.Key] = Build(pair.Key);

            _gone.Clear();
            foreach (var entry in _pairs)
            {
                var station = entry.Key;
                if (station == null)
                {
                    _gone.Add(station);
                    continue;
                }
                var pair = entry.Value;
                _wanted.TryGetValue(station, out var wanted);
                if (wanted.Chef != null)
                    pair.Chef = wanted.Chef.transform.position;
                Tick(pair.Triggers, wanted.Triggers);
                Tick(pair.X, wanted.X);
                if (pair.Triggers.Shown > 0f || pair.X.Shown > 0f)
                    Place(station, pair, camera);
            }
            foreach (var station in _gone)
            {
                if (_pairs[station].Root != null)
                    Destroy(_pairs[station].Root.gameObject);
                _pairs.Remove(station);
            }
        }

        // ---- Which hints, and learning them ----

        // The LT RT and X hints this chef could use at this station right now, if any.
        private (ApplianceHint? Triggers, ApplianceHint? X) HintsFor(PlayerInteractor chef, Interactable station)
        {
            ApplianceHint? triggers = null, x = null;
            _lines.Clear();
            if (station is IStationPrompt prompt)
                prompt.DescribeActions(chef, _lines);
            foreach (var line in _lines)
            {
                if (line.Control == PromptControl.Triggers && line.Available)
                {
                    if (station is IHeatControl)
                        triggers = ApplianceHint.Heat;
                    else if (station is ISlotMenuStation)
                        triggers = ApplianceHint.Browse;
                }
                else if (line.Control == PromptControl.West && line.Available)
                {
                    // Every X is drawn where it acts now (decision 2026-11-11): flipping's on the
                    // crust strip, cleaning's on the mess (CleanHintView). None here.
                }
            }
            return (triggers, x);
        }

        private bool TriggerPulled(PlayerInteractor chef) => _watched.TryGetValue(chef, out var watch) && watch.Pulled;

        // Food in this hob's pan (or on this grill) that shows the crust or doneness strip.
        private bool Cooking(Interactable station)
        {
            var pan = station is HobStation hob ? hob.Hob?.Pan : null;
            if (pan == null)
                return false;
            foreach (var food in pan.Contents)
                if (food != null && (Game.Simulation.Cooking.FlipGauge.Shows(food) || Game.Simulation.Cooking.FlipGauge.ShowsStages(food)))
                    return true;
            return false;
        }

        // Counts each time a player does what a hint teaches, wherever they do it.
        private void Learn(PlayerInteractor chef, PlayerInput player)
        {
            if (!_watched.TryGetValue(chef, out var watch))
            {
                watch = new Watch
                {
                    Down = player.actions.FindAction(HeatDownActionName),
                    Up = player.actions.FindAction(HeatUpActionName),
                };
                var index = chef.PlayerIndex;
                chef.Acted += station =>
                {
                    if (station is HobStation hob && !hob.IsOnFire || station is GrillStation)
                        Count(index, ApplianceHint.Flip);
                    else if (station is SinkStation)
                        Count(index, ApplianceHint.Clean);
                };
                _watched[chef] = watch;
            }

            var pulled = (watch.Down != null && watch.Down.ReadValue<float>() >= TriggerPressed)
                || (watch.Up != null && watch.Up.ReadValue<float>() >= TriggerPressed);
            if (pulled && !watch.Pulled && chef.CanAct)
            {
                if (chef.Target is IHeatControl)
                    Count(chef.PlayerIndex, ApplianceHint.Heat);
                else if (chef.Target is ISlotMenuStation)
                    Count(chef.PlayerIndex, ApplianceHint.Browse);
            }
            watch.Pulled = pulled;

            var cleaning = chef.Cleaning != null && chef.Cleaning.IsCleaning;
            if (cleaning && !watch.Cleaning)
                Count(chef.PlayerIndex, ApplianceHint.Clean);
            watch.Cleaning = cleaning;
        }

        /// <summary>Counts one use of what a hint teaches (also for tests).</summary>
        public void Count(int player, ApplianceHint hint) => _uses[(player, hint)] = UsesOf(player, hint) + 1;

        // ---- Drawing ----

        private void Tick(Badge badge, bool wanted)
        {
            badge.Shown = Mathf.MoveTowards(badge.Shown, wanted ? 1f : 0f, Time.unscaledDeltaTime / _fade);
            var visible = badge.Shown > 0f;
            if (badge.Root.gameObject.activeSelf != visible)
                badge.Root.gameObject.SetActive(visible);
            if (!visible)
                return;
            // Eased, popping up a little from smaller, so it arrives rather than blinks on.
            var eased = Mathf.SmoothStep(0f, 1f, badge.Shown);
            badge.Root.localScale = Vector3.one * Mathf.Lerp(_popFrom, 1f, eased);
            foreach (var body in badge.Bodies)
            {
                var colour = body.color;
                colour.a = eased;
                body.color = colour;
            }
            foreach (var letter in badge.Letters)
                letter.alpha = eased;
        }

        // On the top of the appliance, at the edge across from the chef, LT RT on the screen's left
        // and X on its right. The edge comes from the station's own shape, so a station turned 45°
        // still gets the edge across from the chef. Whichever side the chef is on, that edge shows
        // past them from the camera (or in front of them, with the chef behind the counter).
        private void Place(Interactable station, Pair pair, Camera camera)
        {
            var t = station.transform;
            var local = t.InverseTransformPoint(pair.Chef);
            var half = pair.Extents;
            var centre = pair.Centre;
            // The edge nearest the chef, in the station's own frame (scaled so a long counter's
            // ends don't win over its front); the pictures go on the opposite one.
            var dx = (local.x - centre.x) / Mathf.Max(half.x, 0.01f);
            var dz = (local.z - centre.z) / Mathf.Max(half.z, 0.01f);
            Vector3 edge, along;
            if (Mathf.Abs(dx) > Mathf.Abs(dz))
            {
                edge = new Vector3(centre.x - Mathf.Sign(dx) * Mathf.Max(half.x - _inset, 0f), 0f, centre.z);
                along = Vector3.forward;
            }
            else
            {
                edge = new Vector3(centre.x, 0f, centre.z - Mathf.Sign(dz) * Mathf.Max(half.z - _inset, 0f));
                along = Vector3.right;
            }

            var alongWorld = t.TransformDirection(along);
            alongWorld.y = 0f;
            alongWorld.Normalize();
            if (Vector3.Dot(alongWorld, camera.transform.right) < 0f)
                alongWorld = -alongWorld;
            var middle = t.TransformPoint(edge);
            middle.y = t.position.y + pair.Top + _lift + _size / 2f;

            // Side by side as one group, centred on the edge: LT RT, a gap, X.
            var both = pair.Triggers.Width + _gap + pair.X.Width;
            var left = middle - alongWorld * (both / 2f);
            var facing = camera.transform.rotation;
            // Sized on screen like the gauges' X, so it's the same small button in any kitchen.
            // In world size: the hints hang under this object, which may itself be scaled.
            var scale = TimingGaugeKit.MetresFor(camera, middle, _pixels) / _size / Mathf.Max(0.001f, transform.lossyScale.x);
            var triggersAt = left + alongWorld * (pair.Triggers.Width / 2f);
            // A gas hob's LT RT sit by its heat knob: what they turn.
            var knob = station.transform.Find(HobView.KnobName);
            if (knob != null)
                triggersAt = knob.position + Vector3.up * (_lift + _size * scale / 2f) + camera.transform.up * (_size * scale * 0.6f);
            pair.Triggers.Root.SetPositionAndRotation(triggersAt, facing);
            pair.X.Root.SetPositionAndRotation(left + alongWorld * (both - pair.X.Width / 2f), facing);
            // Only badges Tick has just sized this frame (a hidden one keeps its scale).
            if (pair.Triggers.Shown > 0f)
                pair.Triggers.Root.localScale *= scale;
            if (pair.X.Shown > 0f)
                pair.X.Root.localScale *= scale;
        }

        private Pair Build(Interactable station)
        {
            // Placed on the station each frame rather than parented to it, so a station's own scale
            // can't stretch the pictures; it's dropped when the station goes (sold).
            var root = new GameObject($"ApplianceHints_{station.name}").transform;
            root.SetParent(transform, false);
            var pair = new Pair { Root = root };
            Measure(station, pair);

            // LT RT: two trigger bodies side by side, letters on top.
            pair.Triggers = new Badge { Root = new GameObject("Hint_LT_RT").transform };
            pair.Triggers.Root.SetParent(root, false);
            if (_onTop != null)
            {
                // Toon keys, the same family as the gauges' X: a dark key with an Ink rim each.
                var key = _size * 0.9f;
                for (var side = -1; side <= 1; side += 2)
                {
                    var rim = TimingGaugeKit.Block(pair.Triggers.Root, "KeyRim", _onTop, TimingGaugeKit.Ink, 30);
                    TimingGaugeKit.Place(rim, new Vector2(side * key * 0.56f, 0f), new Vector2(key + _size * 0.1f, _size * 1.1f), 0.01f, 0f);
                    var body = TimingGaugeKit.Block(pair.Triggers.Root, "Key", _onTop, new Color(0.2f, 0.21f, 0.24f), 31);
                    TimingGaugeKit.Place(body, new Vector2(side * key * 0.56f, 0f), new Vector2(key, _size), 0.01f, -0.01f);
                }
                pair.Triggers.Width = key * 2.12f;
                Letter(pair.Triggers, "LT", new Vector3(-key * 0.56f, 0f, -0.02f), _triggerLetters, 0.42f);
                Letter(pair.Triggers, "RT", new Vector3(key * 0.56f, 0f, -0.02f), _triggerLetters, 0.42f);
                BuildX(pair);
                pair.Triggers.Root.gameObject.SetActive(false);
                pair.X.Root.gameObject.SetActive(false);
                return pair;
            }
            var triggerWidth = Body(pair.Triggers, _trigger, new Vector3(-0.5f, 0f, 0f));
            Body(pair.Triggers, _trigger, new Vector3(0.5f, 0f, 0f), triggerWidth);
            pair.Triggers.Width = triggerWidth * 1.92f;
            Letter(pair.Triggers, "LT", new Vector3(-triggerWidth * 0.46f, -_size * 0.06f, 0f), _triggerLetters, 0.42f);
            Letter(pair.Triggers, "RT", new Vector3(triggerWidth * 0.46f, -_size * 0.06f, 0f), _triggerLetters, 0.42f);

            BuildX(pair);
            pair.Triggers.Root.gameObject.SetActive(false);
            pair.X.Root.gameObject.SetActive(false);
            return pair;
        }

        // X: the round body, the letter in Xbox blue (unused since every X moved to where it acts).
        private void BuildX(Pair pair)
        {
            pair.X = new Badge { Root = new GameObject("Hint_X").transform };
            pair.X.Root.SetParent(pair.Root, false);
            pair.X.Width = Body(pair.X, _button, Vector3.zero);
            var blue = _style != null ? _style.BadgeColor(PromptControl.West, true) : new Color(0.25f, 0.55f, 0.95f);
            Letter(pair.X, "X", new Vector3(0f, _size * 0.02f, 0f), blue, 0.62f);
        }

        // A kit body scaled to the hint's height; returns its width (m). Placed by its width when
        // the offset is given in widths (the trigger pair).
        private float Body(Badge badge, Sprite sprite, Vector3 offsetInWidths, float width = -1f)
        {
            var go = new GameObject(sprite != null ? sprite.name : "Body");
            go.transform.SetParent(badge.Root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (_material != null)
                renderer.sharedMaterial = _material;
            renderer.sortingOrder = 20;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var spriteHeight = sprite != null ? sprite.bounds.size.y : 1f;
            var scale = _size / Mathf.Max(spriteHeight, 0.001f);
            go.transform.localScale = Vector3.one * scale;
            if (width < 0f)
                width = sprite != null ? sprite.bounds.size.x * scale : _size;
            go.transform.localPosition = offsetInWidths * width * 0.92f;
            badge.Bodies.Add(renderer);
            return width;
        }

        private void Letter(Badge badge, string text, Vector3 position, Color colour, float heightShare)
        {
            var go = new GameObject("Letter_" + text);
            go.transform.SetParent(badge.Root, false);
            go.transform.localPosition = position + Vector3.back * 0.002f;
            var label = go.AddComponent<TextMeshPro>();
            if (_style != null)
                label.font = _style.Bold;
            label.fontSharedMaterial = LetterMaterial(label.font);
            label.text = text;
            label.color = colour;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 10f;
            label.rectTransform.sizeDelta = new Vector2(_size * 2f, _size);
            // Sized by its cap height rather than the font size, so letters fill the body the same in any font.
            label.ForceMeshUpdate();
            var height = label.textBounds.size.y;
            if (height > 0.0001f)
                label.fontSize *= _size * heightShare / height;
            // Over the bodies: above the toon keys' order (30, 31) when they're used.
            label.sortingOrder = _onTop != null ? 35 : 21;
            badge.Letters.Add(label);
        }

        // The font's own material, drawn after the bodies and, like them, over the chef.
        private Material LetterMaterial(TMP_FontAsset font)
        {
            if (_letterMaterial != null || font == null)
                return _letterMaterial != null ? _letterMaterial : font?.material;
            _letterMaterial = new Material(font.material) { name = "HintLetters" };
            _letterMaterial.SetFloat(ZTestId, (float)CompareFunction.Always);
            _letterMaterial.renderQueue = (_onTop != null ? _onTop.renderQueue + 5 : (_material != null ? _material.renderQueue : 3020)) + 1;
            return _letterMaterial;
        }

        // The station's footprint in its own frame, from its meshes, and its work surface's height
        // (furniture is static in shape, so once).
        private static void Measure(Interactable station, Pair pair)
        {
            var t = station.transform;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var filter in station.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                var bounds = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one);
                    var p = t.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            if (min.x > max.x)
            {
                min = new Vector3(-0.4f, 0f, -0.4f);
                max = new Vector3(0.4f, 0.9f, 0.4f);
            }
            pair.Centre = (min + max) / 2f;
            pair.Extents = (max - min) / 2f;
            // Standing on the work surface (where the station's items sit), not on whatever is tallest:
            // a hob's hood or a shelf's top would put them up on the wall.
            pair.Top = station.ItemAnchor != null
                ? station.ItemAnchor.position.y - t.position.y
                : Mathf.Min(max.y * t.lossyScale.y, 1f);
        }

        private void OnDestroy()
        {
            if (_letterMaterial != null)
                Destroy(_letterMaterial);
        }

        private sealed class Badge
        {
            public Transform Root;
            public float Shown;
            public float Width;
            public readonly List<SpriteRenderer> Bodies = new List<SpriteRenderer>();
            public readonly List<TextMeshPro> Letters = new List<TextMeshPro>();
        }

        private sealed class Pair
        {
            public Transform Root;
            public Badge Triggers;
            public Badge X;
            public Vector3 Centre;
            public Vector3 Extents;
            public float Top;
            public Vector3 Chef;
        }

        private sealed class Watch
        {
            public InputAction Down;
            public InputAction Up;
            public bool Pulled;
            public bool Cleaning;
        }
    }
}
