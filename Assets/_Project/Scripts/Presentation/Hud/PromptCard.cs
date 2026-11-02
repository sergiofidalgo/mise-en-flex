using System.Collections.Generic;
using Game.Gameplay.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A player's prompt card above the station they face. A list of rows for most stations, or a
    /// diamond of four cells laid out like the face buttons for storage and the prep counter. It
    /// has an edge in the player's colour and fades in and out.
    /// </summary>
    public sealed class PromptCard : MonoBehaviour
    {
        [SerializeField] private HudStyle _style;
        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _edge;
        [SerializeField] private GameObject _list;
        [SerializeField] private PromptRow[] _rows = System.Array.Empty<PromptRow>();
        [SerializeField] private GameObject _diamond;

        [Tooltip("Diamond cells in PromptControl order: South, East, West, North.")]
        [SerializeField] private PromptRow[] _cells = System.Array.Empty<PromptRow>();

        private float _targetAlpha;

        public RectTransform Rect => _rect;

        public void Show(IReadOnlyList<PromptLine> lines, bool diamond, bool gamepad, Color playerColor)
        {
            _targetAlpha = 1f;
            _edge.color = playerColor;
            _list.SetActive(!diamond);
            _diamond.SetActive(diamond);

            if (diamond)
            {
                foreach (var cell in _cells)
                    cell.Hide();
                foreach (var line in lines)
                {
                    var index = (int)line.Control;
                    if (index < _cells.Length)
                        _cells[index].Show(line, gamepad);
                }
            }
            else
            {
                for (var i = 0; i < _rows.Length; i++)
                {
                    if (i < lines.Count)
                        _rows[i].Show(lines[i], gamepad);
                    else
                        _rows[i].Hide();
                }
            }
        }

        public void Hide() => _targetAlpha = 0f;

        private void Update()
        {
            var speed = _style.Fade > 0f ? 1f / _style.Fade : float.MaxValue;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, speed * Time.unscaledDeltaTime);
        }
    }
}
