using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Plays the kitchen's one-off sounds. Listens to the bell, the cutting boards and the customers
    /// itself; other views call <see cref="Play"/>. Nothing in the simulation or gameplay knows sound exists.
    /// </summary>
    public sealed class KitchenAudio : SfxPlayer
    {
        [SerializeField] private KitchenCompositionRoot _root;
        private readonly Dictionary<PrepStation, System.Action<ChopGrade>> _boards = new Dictionary<PrepStation, System.Action<ChopGrade>>();
        private readonly Dictionary<StorageStation, System.Action<int>> _stockings = new Dictionary<StorageStation, System.Action<int>>();
        private readonly Dictionary<FridgeDoorView, (System.Action Opened, System.Action Closed)> _fridges =
            new Dictionary<FridgeDoorView, (System.Action, System.Action)>();
        private KitchenServices _services;
        private StreetDoor _streetDoor;

        private void Start()
        {
            _services = _root.Services;
            _services.Bell.Rang += HandleBell;
            foreach (var station in _services.Stations.All)
                HandleStationAdded(station);
            _services.Stations.Added += HandleStationAdded;
            _services.Stations.Removed += HandleStationRemoved;

            _streetDoor = FindFirstObjectByType<StreetDoor>();
            _services.ServiceReplaced += HandleServiceReplaced;
            HandleServiceReplaced(null, _services.Service);
        }

        private void OnDestroy()
        {
            if (_services == null)
                return;

            HandleServiceReplaced(_services.Service, null);
            _services.ServiceReplaced -= HandleServiceReplaced;
            _services.Bell.Rang -= HandleBell;
            _services.Stations.Added -= HandleStationAdded;
            _services.Stations.Removed -= HandleStationRemoved;
            foreach (var board in _boards)
                board.Key.Chopped -= board.Value;
            foreach (var fridge in _fridges)
            {
                if (fridge.Key == null)
                    continue;
                fridge.Key.Opened -= fridge.Value.Opened;
                fridge.Key.Closed -= fridge.Value.Closed;
            }
        }

        // Each day has its own service, so the sounds move over to the new one.
        private void HandleServiceReplaced(ServiceSimulation old, ServiceSimulation next)
        {
            if (old != null)
            {
                old.Arrived -= HandleCustomerArrived;
                old.Paid -= HandleCustomerPaid;
                old.Deliveries.Ordered -= HandleDeliveryOrdered;
                old.Deliveries.CourierDue -= HandleCourierDue;
            }
            if (next != null)
            {
                next.Arrived += HandleCustomerArrived;
                next.Paid += HandleCustomerPaid;
                next.Deliveries.Ordered += HandleDeliveryOrdered;
                next.Deliveries.CourierDue += HandleCourierDue;
            }
        }

        // The tablet pings from the counter whose place the order claimed (decision 2026-11-12).
        private void HandleDeliveryOrdered(DeliveryOrder order)
        {
            var counter = _services.Stations.All.OfType<DeliveryCounterStation>().FirstOrDefault(c => c.AnchorOfSlot(order.Slot) != null);
            Play(Sounds.DeliveryPing, counter != null ? counter.Tablet.position : transform.position);
        }

        // The scooter's horn from the street, as it comes into view.
        private void HandleCourierDue(DeliveryOrder order) =>
            Play(Sounds.CourierHorn, _streetDoor != null ? _streetDoor.transform.position : transform.position);

        private void HandleCustomerArrived(Customer customer) =>
            Play(Sounds.Chime, _streetDoor != null ? _streetDoor.transform.position : transform.position);

        private void HandleCustomerPaid(Customer customer) => PlayCentred(Sounds.Till);

        private void HandleBell(ItemSlot slot)
        {
            var counter = _services.Stations.All.OfType<CounterStation>().FirstOrDefault(c => c.Slot == slot);
            Play(Sounds.Bell, counter != null ? counter.transform.position : transform.position);
        }

        private void HandleStationAdded(Interactable station)
        {
            if (station.TryGetComponent(out FridgeDoorView fridge) && !_fridges.ContainsKey(fridge))
            {
                System.Action opened = () => Play(Sounds.FridgeOpen, fridge.transform.position);
                System.Action closed = () => Play(Sounds.FridgeClose, fridge.transform.position);
                _fridges[fridge] = (opened, closed);
                fridge.Opened += opened;
                fridge.Closed += closed;
            }

            // A market stall: the till, at the stall, for every purchase.
            if (station is StallStation stall)
                stall.Bought += _ => Play(Sounds.Till, stall.transform.position);

            // Bought stock arriving: set down where it went, fridge or shelf (decision 2026-11-10).
            if (station is StorageStation storage && !_stockings.ContainsKey(storage))
            {
                System.Action<int> stocked = _ => Play(Sounds.Clack, storage.transform.position);
                _stockings[storage] = stocked;
                storage.Stocked += stocked;
            }

            if (!(station is PrepStation board) || _boards.ContainsKey(board))
                return;

            System.Action<ChopGrade> handler = grade => Play(ChopCue(grade), board.transform.position);
            _boards[board] = handler;
            board.Chopped += handler;
        }

        private void HandleStationRemoved(Interactable station)
        {
            if (station.TryGetComponent(out FridgeDoorView fridge) && _fridges.TryGetValue(fridge, out var handlers))
            {
                fridge.Opened -= handlers.Opened;
                fridge.Closed -= handlers.Closed;
                _fridges.Remove(fridge);
            }

            if (station is StorageStation storage && _stockings.TryGetValue(storage, out var stocked))
            {
                storage.Stocked -= stocked;
                _stockings.Remove(storage);
            }

            if (station is PrepStation board && _boards.TryGetValue(board, out var handler))
            {
                board.Chopped -= handler;
                _boards.Remove(board);
            }
        }

        // Rhythm you can hear: a clean knock on the beat, duller either side of it, a skid when it's way off.
        private SoundCue ChopCue(ChopGrade grade)
        {
            switch (grade)
            {
                case ChopGrade.Good:
                    return Sounds.ChopClean;
                case ChopGrade.Early:
                case ChopGrade.Late:
                    return Sounds.ChopOff;
                default:
                    return Sounds.ChopMiss;
            }
        }
    }
}
