using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// When a customer pays or walks out, a card appears where they were: stars, what they paid
    /// and their review line. It stays put for a few seconds rather than following them, because
    /// the walk to the door can be too short to read it (decision 2026-11-03).
    /// </summary>
    public sealed class ReviewPopupHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private CustomerDirector _director;

        [Tooltip("Real seconds a review card stays up.")]
        [SerializeField, Min(0f)] private float _seconds = 4f;
        [Tooltip("Metres above the customer's feet: above where the next customer's ticket goes.")]
        [SerializeField, Min(0f)] private float _height = 2.3f;
        [SerializeField] private Vector2 _padding = new Vector2(12f, 6f);

        private readonly List<Popup> _popups = new List<Popup>();
        private ServiceSimulation _service;
        // In the player's language and currency (Locale).
        private static string Money(float amount) => Game.Data.Locale.Money(amount, Game.Data.MoneyStyle.Whole);

        private void Start()
        {
            _service = _root.Services.Service;
            _service.Reviewed += HandleReviewed;
            _root.Services.ServiceReplaced += HandleServiceReplaced;
        }

        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            old.Reviewed -= HandleReviewed;
            next.Reviewed += HandleReviewed;
            _service = next;
        }

        private void OnDestroy()
        {
            if (_service != null)
            {
                _service.Reviewed -= HandleReviewed;
                _root.Services.ServiceReplaced -= HandleServiceReplaced;
            }
        }

        private void HandleReviewed(Review review)
        {
            if (!_director.Agents.TryGetValue(review.Customer, out var agent))
                return;

            var popup = new Popup
            {
                Label = WorldLabel.Create(_style, _canvas, $"Review_{review.Customer.Id}", _padding, new Vector2(0.5f, 0f)),
                Text = Describe(review),
                World = agent.transform.position + Vector3.up * _height,
                Until = Time.time + _seconds,
            };
            _popups.Add(popup);
        }

        private string Describe(Review review)
        {
            var stars = HudStyle.Colored($"{review.Stars:0.#} stars", _style.RatingColor(review.Stars / 5f));
            var headline = review.Customer.Bill is Bill bill
                ? $"{stars}  {Money(bill.Payment)} + {Money(bill.Tip)} tip"
                : $"{stars}  {HudStyle.Colored($"walked out! {Money(-review.Customer.WalkOutPenalty)}", _style.RatingColor(0f))}";
            var line = Game.Data.ReviewText.Line(review);
            return string.IsNullOrEmpty(line) ? headline : $"{headline}\n\"{line}\"";
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                var popup = _popups[i];
                if (Time.time < popup.Until)
                {
                    popup.Label.Show(popup.Text, popup.World, camera, _canvas);
                    continue;
                }

                popup.Label.Hide();
                // Leave time for the fade before removing the card.
                if (Time.time >= popup.Until + _style.Fade)
                {
                    Destroy(popup.Label.gameObject);
                    _popups.RemoveAt(i);
                }
            }
        }

        private sealed class Popup
        {
            public WorldLabel Label;
            public string Text;
            public Vector3 World;
            public float Until;
        }
    }
}
