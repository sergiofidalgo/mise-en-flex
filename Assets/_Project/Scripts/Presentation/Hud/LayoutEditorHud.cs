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
        [SerializeField] private Color _blocked = new Color(0.95f, 0.30f, 0.26f);
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
                }

                // What's carried shows for itself, turning red where it can't go; the frame is for choosing.
                view.Frame.SetActive(cursor.Held == null);
                var position = cursor.Held != null ? cursor.Landing : cursor.Position;
                var color = _settings.GetPlayerColor(cursor.Player.playerIndex);
                view.Frame.transform.position = new Vector3(position.X, 0.02f, position.Y);
                foreach (var part in view.Frame.GetComponentsInChildren<Renderer>())
                {
                    part.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, color);
                    part.SetPropertyBlock(_block);
                }

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
                    Placement.OutsideTheKitchen => "not in this room",
                    Placement.OnAnotherStation => "something's there",
                    Placement.SomeoneInTheWay => "someone's in the way",
                    Placement.CantAfford => "not enough money",
                    Placement.NotOnAWall => "it goes on an outside wall",
                    _ => null,
                };
                return problem != null
                    ? $"{HudStyle.Colored(problem, _blocked)}\n[LB/RB] rotate   {Glyph(PromptControl.East)} cancel"
                    : cursor.Held is StreetDoor
                        ? $"{Glyph(PromptControl.South)} place   {Glyph(PromptControl.East)} cancel"
                        : $"{Glyph(PromptControl.South)} place   [LB/RB] rotate   {Glyph(PromptControl.East)} cancel";
            }

            var always = $"{Glyph(PromptControl.North)} shop   [Select] back to chef";
            var under = _editor.StationUnder(cursor);
            if (under == null)
                return always;
            var sale = SaleText(cursor, under);
            var move = _editor.CanMove(under) ? $"{Glyph(PromptControl.South)} move {under.name}" : under.name;
            return $"{move}{(sale != null ? "   " + sale : "")}\n{always}";
        }

        private string SaleText(LayoutEditor.Cursor cursor, Interactable station)
        {
            var problem = _editor.SaleProblemOf(cursor, station) switch
            {
                SaleProblem.NotForSale => "",
                SaleProblem.NotEmpty => "clear it to sell",
                SaleProblem.InUse => "in use",
                SaleProblem.LastOne => "your last one",
                _ => null,
            };
            if (problem == null)
                return $"{Glyph(PromptControl.West)} sell {Money(_editor.SellPriceOf(station))}";
            return problem.Length > 0 ? HudStyle.Colored(problem, _blocked) : null;
        }

        private string ShopText(LayoutEditor.Cursor cursor)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine($"{Money(_root.Services.Days.Balance)} to spend");
            for (var i = 0; i < _editor.Shop.Count; i++)
            {
                var entry = _editor.Shop[i];
                var line = $"{(i == cursor.ShopIndex ? "> " : "  ")}{entry.DisplayName}  {Money(entry.Price)}";
                text.AppendLine(_editor.CanAfford(entry) ? line : HudStyle.Colored(line, _blocked));
            }
            text.Append($"{Glyph(PromptControl.South)} buy   {Glyph(PromptControl.East)} close");
            return text.ToString();
        }

        private string Money(float amount) => $"{_root.Services.ServiceConfig.Currency}{amount:0}";

        private static string Glyph(PromptControl control) =>
            $"[{HudStyle.Glyph(control, true)}]";
    }
}
