using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Each player's cursor in management mode: a frame on the floor in their colour, red where a
    /// carried station can't go, with a short label of what the buttons do there and why not.
    /// </summary>
    public sealed class LayoutEditorHud : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private LayoutEditor _editor;
        [SerializeField] private CoopSettings _settings;
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

                var blocked = cursor.Held != null && cursor.Result != Placement.Ok;
                var position = cursor.Held != null ? cursor.Landing : cursor.Position;
                var color = blocked ? _blocked : _settings.GetPlayerColor(cursor.Player.playerIndex);
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
            if (cursor.Held != null)
            {
                var problem = cursor.Result switch
                {
                    Placement.OutsideTheKitchen => "outside the kitchen",
                    Placement.OnAnotherStation => "something's there",
                    Placement.SomeoneInTheWay => "someone's in the way",
                    Placement.BlocksAStation => "that would block a station",
                    _ => null,
                };
                return problem != null
                    ? $"{HudStyle.Colored(problem, _blocked)}\n{Glyph(PromptControl.East)} cancel"
                    : $"{Glyph(PromptControl.South)} place   {Glyph(PromptControl.East)} cancel";
            }

            var under = _editor.StationUnder(cursor);
            return under != null
                ? $"{Glyph(PromptControl.South)} move {under.name}   {Glyph(PromptControl.East)} back to chef"
                : $"{Glyph(PromptControl.East)} back to chef";
        }

        private static string Glyph(PromptControl control) =>
            $"[{HudStyle.Glyph(control, true)} / {HudStyle.Glyph(control, false)}]";
    }
}
