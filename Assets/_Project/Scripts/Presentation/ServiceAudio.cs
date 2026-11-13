using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Gameplay.Debt;
using Game.Gameplay.Stations;
using Game.Simulation.Days;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The shape of the day: the countdown's ticks and the doors opening, a ticket for every new
    /// order, a sad little phrase when a party gives up, couriers' scooters pulling up and riding off,
    /// the boss's phone, and service closing.
    /// </summary>
    public sealed class ServiceAudio : MonoBehaviour
    {
        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private KitchenAudio _audio;
        [Tooltip("Pitch of the countdown's last tick, so the one before the doors open stands out.")]
        [SerializeField, Range(1f, 2f)] private float _lastTickPitch = 1.5f;

        private readonly HashSet<int> _ordered = new HashSet<int>();
        private KitchenServices _services;
        private ServiceSimulation _service;
        private CourierDirector _couriers;
        private StreetDoor _streetDoor;
        private int _shown;

        private void Start()
        {
            _services = _root.Services;
            _services.Countdown.Finished += HandleDoorsOpen;
            _services.Days.PhaseChanged += HandlePhaseChanged;
            _services.ServiceReplaced += HandleServiceReplaced;
            HandleServiceReplaced(null, _services.Service);
            _root.Phone.Rang += HandlePhone;

            _couriers = FindFirstObjectByType<CourierDirector>();
            if (_couriers != null)
                _couriers.PulledUp += HandlePulledUp;
            _streetDoor = FindFirstObjectByType<StreetDoor>();
        }

        private void OnDestroy()
        {
            if (_services == null)
                return;
            _services.Countdown.Finished -= HandleDoorsOpen;
            _services.Days.PhaseChanged -= HandlePhaseChanged;
            _services.ServiceReplaced -= HandleServiceReplaced;
            HandleServiceReplaced(_service, null);
            _root.Phone.Rang -= HandlePhone;
            if (_couriers != null)
                _couriers.PulledUp -= HandlePulledUp;
        }

        private void Update()
        {
            var shown = _services.Countdown.Shown;
            if (shown != _shown && shown > 0)
                _audio.PlayCentred(_audio.Sounds.Countdown, shown == 1 ? _lastTickPitch : 1f);
            _shown = shown;

            // A new order is a party sitting down to wait: one ticket however many of them there are.
            if (_service == null)
                return;
            foreach (var customer in _service.Customers)
            {
                if (customer.State == CustomerState.Waiting && customer.Order != null && _ordered.Add(customer.Id))
                    _audio.PlayCentred(_audio.Sounds.Ticket);
            }
        }

        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            if (old != null)
            {
                old.WalkedOut -= HandleWalkedOut;
                old.Deliveries.Collected -= HandleCollected;
            }
            _service = next;
            _ordered.Clear();
            if (next != null)
            {
                next.WalkedOut += HandleWalkedOut;
                next.Deliveries.Collected += HandleCollected;
            }
        }

        private void HandleDoorsOpen() => _audio.PlayCentred(_audio.Sounds.DoorsOpen);

        private void HandlePhaseChanged(DayPhase phase)
        {
            if (phase == DayPhase.Review)
                _audio.PlayCentred(_audio.Sounds.ServiceOver);
        }

        private void HandleWalkedOut(Customer customer) => _audio.Play(_audio.Sounds.Walkout, StreetPosition);

        private void HandlePulledUp(DeliveryOrder order) => _audio.Play(_audio.Sounds.ScooterIn, StreetPosition);

        private void HandleCollected(DeliveryOrder order) => _audio.Play(_audio.Sounds.ScooterOut, StreetPosition);

        private void HandlePhone(Game.Simulation.Campaign.BossCallKind kind, IReadOnlyList<string> lines) => _audio.PlayCentred(_audio.Sounds.Phone);

        private Vector3 StreetPosition => _streetDoor != null ? _streetDoor.transform.position : transform.position;
    }
}
