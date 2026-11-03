using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Customers;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation.Hud
{
    /// <summary>
    /// A ticket above each seated customer saying what they ordered, e.g. "Steak · Medium-rare
    /// + Onions", with the doneness in its HUD colour so it can be matched against the food tags,
    /// and a patience meter under it. Shown from when they sit down until they're served
    /// (decision 2026-11-02). Customers queueing by the door show only the meter.
    /// </summary>
    public sealed class OrderTicketHud : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private HudStyle _style;
        [SerializeField] private CustomerDirector _director;
        [SerializeField] private ItemCatalog _catalog;

        [Tooltip("Metres above a customer's feet where their ticket sits (just over their head).")]
        [SerializeField, Min(0f)] private float _height = 1.85f;
        [Tooltip("Canvas pixels of padding inside a ticket.")]
        [SerializeField] private Vector2 _padding = new Vector2(12f, 6f);

        private readonly Dictionary<Customer, WorldLabel> _tickets = new Dictionary<Customer, WorldLabel>();
        private readonly Dictionary<Order, string> _texts = new Dictionary<Order, string>();
        private readonly List<Customer> _stale = new List<Customer>();

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || _director == null)
                return;

            foreach (var pair in _director.Agents)
            {
                var customer = pair.Key;
                var ticket = TicketFor(customer);
                var above = pair.Value.transform.position + Vector3.up * _height;
                var meter = _style.PatienceColor(customer.Patience);
                if (customer.State == CustomerState.Waiting)
                    ticket.Show(customer.Order != null ? TextFor(customer.Order) : string.Empty, above, camera, _canvas, customer.Patience, meter);
                else if (customer.State == CustomerState.Queueing)
                    ticket.Show(string.Empty, above, camera, _canvas, customer.Patience, meter);
                else
                    ticket.Hide();
            }

            // Customers who have left the scene take their ticket with them.
            _stale.Clear();
            foreach (var customer in _tickets.Keys)
            {
                if (!_director.Agents.ContainsKey(customer))
                    _stale.Add(customer);
            }
            foreach (var customer in _stale)
            {
                Destroy(_tickets[customer].gameObject);
                _tickets.Remove(customer);
                if (customer.Order != null)
                    _texts.Remove(customer.Order);
            }
        }

        private WorldLabel TicketFor(Customer customer)
        {
            if (!_tickets.TryGetValue(customer, out var ticket))
            {
                ticket = WorldLabel.Create(_style, _canvas, $"Ticket_{customer.Id}", _padding, new Vector2(0.5f, 0f));
                _tickets[customer] = ticket;
            }
            return ticket;
        }

        private string TextFor(Order order)
        {
            if (_texts.TryGetValue(order, out var text))
                return text;
            var doneness = HudStyle.Colored(order.Doneness.Name, _style.DonenessColor(order.Doneness.Name));
            text = $"{Name(order.MainTypeId)} · {doneness}";
            if (order.SideTypeId != null)
                text += $"\n+ {Name(order.SideTypeId)}";
            _texts[order] = text;
            return text;
        }

        private string Name(string typeId) => _catalog != null ? _catalog.DisplayName(typeId) : typeId;
    }
}
