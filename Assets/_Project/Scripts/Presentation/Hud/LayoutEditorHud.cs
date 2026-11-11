using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Furniture;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Each player's cursor in management mode: a frame on the floor in their colour while choosing
    /// what to pick up, with a short label of what the buttons do there and why not.
    /// Only Xbox buttons are named, whatever the player is using (decision 2026-11-04).
    /// </summary>
    public sealed class LayoutEditorHud : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private LayoutEditor _editor;
        [SerializeField] private CoopSettings _settings;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private GameObject _framePrefab;
        [Tooltip("The players' shop cards; without them the shop shows in the cursor's label.")]
        [SerializeField] private ManagementCardsHud _cards;
        [SerializeField] private Color _blocked = new Color(0.95f, 0.30f, 0.26f);
        [Tooltip("Allowed but costly: a piece outside its usual room (decisions 2026-11-11).")]
        [SerializeField] private Color _warning = new Color(1f, 0.72f, 0.22f);
        [Tooltip("Metres above the floor where the label sits.")]
        [SerializeField, Min(0f)] private float _labelHeight = 1.4f;
        [SerializeField] private Vector2 _padding = new Vector2(12f, 6f);

        private readonly Dictionary<PlayerInput, (GameObject Frame, WorldLabel Label)> _views = new Dictionary<PlayerInput, (GameObject, WorldLabel)>();
        private readonly List<PlayerInput> _gone = new List<PlayerInput>();
        private MaterialPropertyBlock _block;

        private void Awake() => _block = new MaterialPropertyBlock();

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            foreach (var cursor in _editor.Cursors)
            {
                if (!_views.TryGetValue(cursor.Player, out var view))
                {
                    view = (Instantiate(_framePrefab), WorldLabel.Create(_style, _canvas, $"LayoutCursor_P{cursor.Player.playerIndex + 1}", _padding, new Vector2(0.5f, 0f)));
                    _views[cursor.Player] = view;

                    // Tinted once: a player's colour never changes. (Searching the frame's parts
                    // every frame for up to four cursors only made garbage.)
                    var color = _settings.GetPlayerColor(cursor.Player.playerIndex);
                    foreach (var part in view.Frame.GetComponentsInChildren<Renderer>(true))
                    {
                        part.GetPropertyBlock(_block);
                        _block.SetColor(BaseColorId, color);
                        part.SetPropertyBlock(_block);
                    }
                }

                // What's carried shows for itself, turning red where it can't go; the frame is for choosing.
                view.Frame.SetActive(cursor.Held == null);
                var position = cursor.Held != null ? cursor.Landing : cursor.Position;
                view.Frame.transform.position = new Vector3(position.X, 0.02f, position.Y);

                // With the shop open its card says it all, so the label above the cursor steps aside.
                if (_cards != null && _cards.Shows(cursor))
                    view.Label.Hide();
                else
                    view.Label.Show(Text(cursor), new Vector3(position.X, _labelHeight, position.Y), camera, _canvas);
            }

            _gone.Clear();
            foreach (var player in _views.Keys)
            {
                if (!_editor.IsManaging(player))
                    _gone.Add(player);
            }
            foreach (var player in _gone)
            {
                Destroy(_views[player].Frame);
                Destroy(_views[player].Label.gameObject);
                _views.Remove(player);
            }
        }

        private string Text(LayoutEditor.Cursor cursor)
        {
            if (cursor.ShopOpen)
                return ShopText(cursor);

            if (cursor.Held != null)
            {
                var problem = cursor.Result switch
                {
                    Placement.OutsideTheKitchen => Tr.Get("not in this room"),
                    Placement.OnAnotherStation => Tr.Get("something's there"),
                    Placement.SomeoneInTheWay => Tr.Get("someone's in the way"),
                    Placement.CantAfford => Tr.Get("not enough money"),
                    Placement.NotOnAWall => Tr.Get("it goes on an outside wall"),
                    Placement.NoRoomForChairs => Tr.Get("no room for its chairs"),
                    Placement.NoFreeCounter => Tr.Get("no free counter for it"),
                    _ => null,
                };
                if (problem != null)
                    return $"{HudStyle.Colored(problem, _blocked)}\n[LB/RB] {Tr.Get("rotate")}   {Glyph(PromptControl.East)} {Tr.Get("cancel")}";
                // Shown while it's carried over the other room, before A: placing still takes one press.
                var mess = cursor.OutOfItsRoom ? MessWarning(cursor.Held) + "\n" : "";
                return mess + (cursor.Held is StreetDoor
                        ? $"{Glyph(PromptControl.South)} {Tr.Get("place")}   {Glyph(PromptControl.East)} {Tr.Get("cancel")}"
                        : $"{Glyph(PromptControl.South)} {Tr.Get("place")}   [LB/RB] {Tr.Get("rotate")}   {Glyph(PromptControl.East)} {Tr.Get("cancel")}");
            }

            // The shop's way in (D-pad down) is on the player's card, as a tab under it.
            var always = $"[Select] {Tr.Get("back to chef")}";
            var under = _editor.StationUnder(cursor);
            if (under == null)
                return always;
            var sale = SaleText(cursor, under);
            var move = $"{Glyph(PromptControl.South)} " + Tr.Format("move {what}", ("what", Tr.Get(under.name)));
            var chairs = under is TableStation table && table.HasChoiceOfChairs
                ? "[RB] " + Tr.Format("add chair {price}", ("price", Money(_editor.ChairPrice))) + "   [LB] " + Tr.Get("remove") + "   " + Tr.Format("{count} now", ("count", table.ChairCount)) + "\n"
                : "";
            var placedMess = _editor.IsOutOfItsRoom(under) ? MessWarning(under) + "\n" : "";
            return $"{placedMess}{move}{(sale != null ? "   " + sale : "")}\n{chairs}{always}";
        }

        // An appliance in the dining room, or a table in the kitchen: allowed, but it always makes mess.
        private string MessWarning(Interactable piece) => HudStyle.Colored(
            Tr.Get(piece.BelongsInDiningRoom ? "guests in the kitchen make mess" : "cooking in the dining room makes mess"), _warning);

        private string SaleText(LayoutEditor.Cursor cursor, Interactable station)
        {
            var problem = _editor.SaleProblemOf(cursor, station) switch
            {
                SaleProblem.NotForSale => "",
                SaleProblem.NotEmpty => Tr.Get("clear it to sell"),
                SaleProblem.InUse => Tr.Get("in use"),
                SaleProblem.LastOne => Tr.Get("your last one"),
                _ => null,
            };
            if (problem == null)
                return $"{Glyph(PromptControl.West)} " + Tr.Format("sell {price}", ("price", Money(_editor.SellPriceOf(station))));
            return problem.Length > 0 ? HudStyle.Colored(problem, _blocked) : null;
        }

        private string ShopText(LayoutEditor.Cursor cursor)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine(Tr.Format("{amount} to spend", ("amount", Money(_root.Services.Days.Balance))));
            var page = _editor.ShopPage(cursor);
            for (var i = 0; i < page.Count; i++)
            {
                var entry = page[i];
                var line = $"{(i == cursor.ShopIndex ? "> " : "  ")}{Tr.Get(entry.DisplayName)}  {Money(entry.Price)}";
                text.AppendLine(_editor.CanAfford(entry) ? line : HudStyle.Colored(line, _blocked));
            }
            // Kitchenware is bought on the spot, so why it wasn't shows here.
            if (cursor.Result == Placement.NoFreeCounter)
                text.AppendLine(HudStyle.Colored(Tr.Get("no free counter for it"), _blocked));
            else if (cursor.Result == Placement.CantAfford)
                text.AppendLine(HudStyle.Colored(Tr.Get("not enough money"), _blocked));
            text.Append($"{Glyph(PromptControl.South)} {Tr.Get("buy")}   {Glyph(PromptControl.East)} {Tr.Get("close")}");
            return text.ToString();
        }

        // In the player's language and currency (Locale).
        private static string Money(float amount) => Game.Data.Locale.Money(amount, Game.Data.MoneyStyle.Whole);

        private static string Glyph(PromptControl control) =>
            $"[{HudStyle.Glyph(control, true)}]";
    }
}
