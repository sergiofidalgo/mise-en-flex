using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation
{
    /// <summary>
    /// Shows what the buttons do at the station each player is facing, as a small box above it
    /// in the player's colour, with glyphs for that player's device. Storage is laid out as the
    /// face-button diamond, matching the bins on the unit. Greybox UI (IMGUI); real UI comes with
    /// the art phase.
    /// </summary>
    public sealed class StationPromptOverlay : MonoBehaviour
    {
        // Height above a station's base where the prompt is anchored (above the counter top).
        private const float AnchorHeight = 1.3f;
        private const string GamepadScheme = "Gamepad";

        [SerializeField] private CoopSettings _settings;
        [SerializeField, Min(8)] private int _fontSize = 15;
        [SerializeField] private Color _unavailable = new Color(1f, 1f, 1f, 0.35f);
        [Tooltip("Pixels between two players' prompts on the same station.")]
        [SerializeField, Min(0f)] private float _playerOffset = 26f;

        private readonly List<PromptLine> _lines = new List<PromptLine>();
        private GUIStyle _box;
        private GUIStyle _label;

        private void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            _box ??= new GUIStyle(GUI.skin.box);
            _label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = _fontSize, richText = true };

            foreach (var player in PlayerInput.all)
            {
                if (!player.TryGetComponent(out PlayerInteractor interactor) || !(interactor.Target is IStationPrompt prompt))
                    continue;

                _lines.Clear();
                prompt.DescribeActions(interactor, _lines);
                if (_lines.Count == 0)
                    continue;

                var world = interactor.Target.transform.position + Vector3.up * AnchorHeight;
                var screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                    continue;

                var gamepad = player.currentControlScheme == GamepadScheme;
                var color = _settings.GetPlayerColor(player.playerIndex);
                var centre = new Vector2(screen.x, Screen.height - screen.y - player.playerIndex * _playerOffset);
                if (prompt.PromptAsDiamond)
                    DrawDiamond(centre, color, gamepad);
                else
                    DrawList(centre, color, gamepad);
            }
        }

        private void DrawList(Vector2 bottomCentre, Color color, bool gamepad)
        {
            var lineHeight = _fontSize + 6f;
            var width = 0f;
            foreach (var line in _lines)
                width = Mathf.Max(width, _label.CalcSize(new GUIContent(Text(line, gamepad))).x);
            width += 16f;
            var height = lineHeight * _lines.Count + 8f;

            var rect = new Rect(bottomCentre.x - width / 2f, bottomCentre.y - height, width, height);
            DrawBox(rect, color);
            for (var i = 0; i < _lines.Count; i++)
                DrawLine(new Rect(rect.x, rect.y + 4f + i * lineHeight, width, lineHeight), _lines[i], gamepad);
        }

        // Y on top, X left, B right, A bottom: the same layout as the controller and the bins.
        private void DrawDiamond(Vector2 bottomCentre, Color color, bool gamepad)
        {
            var lineHeight = _fontSize + 6f;
            var cell = 0f;
            foreach (var line in _lines)
                cell = Mathf.Max(cell, _label.CalcSize(new GUIContent(Text(line, gamepad))).x);
            cell += 12f;
            var width = cell * 2f + 8f;
            var height = lineHeight * 3f + 8f;

            var rect = new Rect(bottomCentre.x - width / 2f, bottomCentre.y - height, width, height);
            DrawBox(rect, color);
            var top = rect.y + 4f;
            var middleX = rect.x + width / 2f - cell / 2f;
            foreach (var line in _lines)
            {
                var cellRect = line.Control switch
                {
                    PromptControl.North => new Rect(middleX, top, cell, lineHeight),
                    PromptControl.West => new Rect(rect.x + 4f, top + lineHeight, cell, lineHeight),
                    PromptControl.East => new Rect(rect.x + 4f + cell, top + lineHeight, cell, lineHeight),
                    _ => new Rect(middleX, top + lineHeight * 2f, cell, lineHeight),
                };
                DrawLine(cellRect, line, gamepad);
            }
        }

        private void DrawBox(Rect rect, Color color)
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Box(rect, GUIContent.none, _box);
            GUI.Box(rect, GUIContent.none, _box); // second pass makes the tint read on bright floors
            GUI.backgroundColor = previous;
        }

        private void DrawLine(Rect rect, PromptLine line, bool gamepad)
        {
            var previous = GUI.contentColor;
            GUI.contentColor = line.Available ? Color.white : _unavailable;
            GUI.Label(rect, Text(line, gamepad), _label);
            GUI.contentColor = previous;
        }

        private static string Text(PromptLine line, bool gamepad) => $"<b>{Glyph(line.Control, gamepad)}</b>  {line.Label}";

        // Keyboard mirrors the face buttons as W/A/D/X with Q/E for the triggers (decision 2026-11-02).
        private static string Glyph(PromptControl control, bool gamepad) => control switch
        {
            PromptControl.South => gamepad ? "A" : "X",
            PromptControl.East => gamepad ? "B" : "D",
            PromptControl.West => gamepad ? "X" : "A",
            PromptControl.North => gamepad ? "Y" : "W",
            _ => gamepad ? "LT/RT" : "Q/E",
        };
    }
}
