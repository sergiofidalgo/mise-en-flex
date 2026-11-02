using Game.Data;
using Game.Gameplay.CloseUp;
using Game.Simulation.Cooking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// Draws a player's close-up as a picture-in-picture panel near the station. The panel grows
    /// out of the station and shrinks back into it, so the eye follows it; it fades when the other
    /// player is behind it; and its border pulses when food elsewhere is about to burn.
    /// </summary>
    public sealed class CloseUpPanelView : MonoBehaviour
    {
        // Height above a station's base used as its on-screen anchor (roughly the counter top).
        private const float StationAnchorHeight = 0.9f;
        // Height above a chef's feet checked against the panel (roughly the head).
        private const float ChefCheckHeight = 1.5f;

        [SerializeField] private PlayerCloseUp _closeUp;
        [SerializeField] private TuningConfig _tuning;
        [SerializeField] private CoopSettings _settings;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RawImage _image;
        [SerializeField] private Image _border;

        [Header("Burning ping")]
        [Tooltip("Fraction of 'burnt' browning at which food elsewhere triggers the ping.")]
        [SerializeField, Range(0f, 1f)] private float _pingFrom = 0.75f;
        [SerializeField] private Color _pingColor = new Color(1f, 0.45f, 0.1f);
        [Tooltip("Pulses per second.")]
        [SerializeField, Min(0f)] private float _pingRate = 3f;

        [Tooltip("How fast the opacity follows fading in and out, per second.")]
        [SerializeField, Min(0f)] private float _fadeSpeed = 6f;

        private CloseUpStation _station;
        private float _openness;
        private float _opacity = 1f;
        private Color _playerColor;

        private void OnEnable()
        {
            _closeUp.Opened += HandleOpened;
            _group.alpha = 0f;
        }

        private void OnDisable()
        {
            _closeUp.Opened -= HandleOpened;
        }

        private void Start()
        {
            _playerColor = _settings.GetPlayerColor(_closeUp.PlayerIndex);
            _border.color = _playerColor;
        }

        private void HandleOpened(CloseUpStation station)
        {
            _station = station;
            _image.texture = _closeUp.Texture;
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (_station == null || camera == null)
                return;

            // Grow and shrink at a constant rate; the panel keeps showing the last frame while closing.
            var target = _closeUp.IsOpen ? 1f : 0f;
            _openness = Mathf.MoveTowards(_openness, target, Time.unscaledDeltaTime / _tuning.CloseUpTransition);
            if (_openness <= 0f && !_closeUp.IsOpen)
            {
                _group.alpha = 0f;
                _station = null;
                return;
            }

            var screen = new Vector2(Screen.width, Screen.height);
            var anchor = (Vector2)camera.WorldToScreenPoint(_station.transform.position + Vector3.up * StationAnchorHeight);
            var full = CloseUpPanelLayout.Place(anchor, screen, screen.x * _tuning.CloseUpPanelSize, _tuning.CloseUpPanelMargin);

            // Ease-out from a point at the station to the full panel.
            var eased = 1f - (1f - _openness) * (1f - _openness);
            var size = full.size * eased;
            var centre = Vector2.Lerp(anchor, full.center, eased);
            _panel.anchoredPosition = centre - size / 2f;
            _panel.sizeDelta = size;

            var targetOpacity = OtherChefBehind(camera, full) ? _tuning.CloseUpFadedOpacity : 1f;
            _opacity = Mathf.MoveTowards(_opacity, targetOpacity, _fadeSpeed * Time.unscaledDeltaTime);
            _group.alpha = _opacity;

            _border.color = FoodBurningElsewhere()
                ? Color.Lerp(_playerColor, _pingColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _pingRate * 2f * Mathf.PI))
                : _playerColor;
        }

        private bool OtherChefBehind(Camera camera, Rect panel)
        {
            foreach (var player in PlayerInput.all)
            {
                if (player.gameObject == _closeUp.gameObject)
                    continue;
                var point = camera.WorldToScreenPoint(player.transform.position + Vector3.up * ChefCheckHeight);
                if (point.z > 0f && panel.Contains(point))
                    return true;
            }
            return false;
        }

        private bool FoodBurningElsewhere()
        {
            var cooking = _closeUp.Services?.Cooking;
            if (cooking == null)
                return false;

            var ownPan = (_station.Station as Game.Gameplay.Stations.HobStation)?.Hob?.Pan;
            foreach (var pan in cooking.Pans)
            {
                if (pan == ownPan)
                    continue;
                foreach (var ingredient in pan.Contents)
                {
                    var burntAt = Mathf.Max(ingredient.Properties.BurntBrowning, 0.0001f);
                    if (ingredient.Browning(ingredient.DownSide) / burntAt >= _pingFrom)
                        return true;
                }
            }
            return false;
        }
    }
}
