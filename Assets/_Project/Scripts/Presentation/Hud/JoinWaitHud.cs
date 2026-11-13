using Game.Data;
using Game.Gameplay;
using Game.Simulation.Days;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// Someone presses A to join a kitchen during service or the Review (the user's call,
    /// 2026-11-13): a short line at the top of the screen tells them to wait until it's over.
    /// </summary>
    public sealed class JoinWaitHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenJoinGate _gate;

        [Tooltip("Real seconds the message stays up after the last refused press.")]
        [SerializeField, Min(0.5f)] private float _seconds = 2.5f;
        [Tooltip("Canvas pixels below the top of the screen.")]
        [SerializeField, Min(0f)] private float _topMargin = 24f;
        [SerializeField, Min(8f)] private float _fontSize = 30f;
        [SerializeField] private Vector2 _padding = new Vector2(26f, 12f);

        private WorldLabel _label;
        private string _text;
        private float _until = -1f;

        private void Start()
        {
            _label = WorldLabel.Create(_style, _canvas, "JoinWait", _padding, new Vector2(0.5f, 1f));
            _label.FontSize = _fontSize;
            _label.Font = _style.Bold;
            _gate.Refused += HandleRefused;
        }

        private void OnDestroy()
        {
            if (_gate != null)
                _gate.Refused -= HandleRefused;
        }

        private void HandleRefused(DayPhase phase)
        {
            _text = Tr.Get(JoinRules.WaitMessage(phase));
            _until = Time.unscaledTime + _seconds;
        }

        private void LateUpdate()
        {
            if (_label == null)
                return;
            if (Time.unscaledTime > _until)
            {
                _label.Hide();
                return;
            }
            _label.ShowAt(_text, new Vector2(0f, _canvas.rect.height / 2f - _topMargin));
        }
    }
}
