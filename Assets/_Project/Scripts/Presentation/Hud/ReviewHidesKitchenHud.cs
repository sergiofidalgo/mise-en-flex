using Game.Gameplay;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// While the forkcast dashboard is up (the day's Review, and Plan until it's put away), the
    /// kitchen's own HUD goes: cutting dials, flip gauges, food tags, tickets, the guests' edge
    /// markers, the bell. They used to show over and around the dashboard as the day ended (the
    /// user's bug, 2026-11-11 playtest round). One group on the HUD canvas fades them all; the
    /// screens nested in it with their own canvas (the dashboard itself, the shop cards, the boss's
    /// phone) are left as they are.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class ReviewHidesKitchenHud : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [Tooltip("Seconds the kitchen HUD takes to come back once the dashboard is put away. It goes at once.")]
        [SerializeField, Min(0f)] private float _fadeBack = 0.25f;

        private CanvasGroup _group;
        private int _canvases = -1;

        /// <summary>Whether the kitchen HUD is hidden for the dashboard now, for tests.</summary>
        public bool Hidden => _group != null && _group.alpha <= 0f;

        private void Awake()
        {
            if (!TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void LateUpdate()
        {
            if (_root == null || _root.Services == null)
                return;
            KeepScreensApart();
            var open = _root.Services.Forkcast.IsOpen;
            _group.alpha = open ? 0f
                : _fadeBack <= 0f ? 1f
                : Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime / _fadeBack);
        }

        // Nested canvases are whole screens of their own (made at Start or later): each gets a group
        // that ignores this one, so hiding the kitchen HUD never hides them.
        private void KeepScreensApart()
        {
            var canvases = GetComponentsInChildren<Canvas>(true);
            if (canvases.Length == _canvases)
                return;
            _canvases = canvases.Length;
            foreach (var canvas in canvases)
            {
                if (canvas.gameObject == gameObject)
                    continue;
                if (!canvas.TryGetComponent(out CanvasGroup own))
                {
                    own = canvas.gameObject.AddComponent<CanvasGroup>();
                    own.interactable = true;
                    own.blocksRaycasts = true;
                }
                own.ignoreParentGroups = true;
            }
        }
    }
}
