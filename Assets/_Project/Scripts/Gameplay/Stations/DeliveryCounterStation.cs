using System;
using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Saving;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The delivery counter (decision 2026-11-12): owning one puts the restaurant on the delivery
    /// app. Each of its places can hold one open order, marked by the order's bubble; a box (or a
    /// plate, which won't come back) put here goes to the place whose order it matches, or else the
    /// first with an order, or else any free one. The courier takes whatever stands on their
    /// order's place, right or wrong, and the food is scored as it will reach the customer: after
    /// the ride, which a box softens. Nothing's checked at the counter; the review tells.
    /// </summary>
    public sealed class DeliveryCounterStation : Interactable, IStationPrompt, ISavedContents
    {
        [Tooltip("Where each order's box stands. One open order per place.")]
        [SerializeField] private Transform[] _places = Array.Empty<Transform>();

        [Tooltip("The order tablet: where an order's bubble pops from and the ping sounds.")]
        [SerializeField] private Transform _tablet;

        private sealed class Place
        {
            public int Slot = -1;
            public Transform Anchor;
            public Plate Current;
        }

        private readonly List<Place> _slots = new List<Place>();
        private KitchenServices _services;
        private Top _top;
        private Plate _lastPlaced;

        public override IItemContainer Container => _top;

        public override bool BelongsInDiningRoom => true;

        /// <summary>How many orders it can hold open at once.</summary>
        public int PlaceCount => _places.Length;

        public Transform Tablet => _tablet != null ? _tablet : transform;

        public override bool IsClear
        {
            get
            {
                foreach (var place in _slots)
                {
                    if (place.Current != null)
                        return false;
                }
                return true;
            }
        }

        public override Item HeldItem
        {
            get
            {
                if (_lastPlaced != null && PlaceOf(_lastPlaced) != null)
                    return _lastPlaced;
                foreach (var place in _slots)
                {
                    if (place.Current != null)
                        return place.Current;
                }
                return null;
            }
        }

        /// <summary>Raised when a courier has taken an order from here.</summary>
        public event Action<DeliveryCounterStation, DeliveryOrder> PickedUp;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _top = new Top(this);
            _slots.Clear();
            foreach (var anchor in _places)
                _slots.Add(new Place { Anchor = anchor });
        }

        /// <summary>Numbers this counter's places for today's delivery orders, from <paramref name="first"/>.</summary>
        /// <returns>The next free number.</returns>
        public int AssignSlots(int first)
        {
            foreach (var place in _slots)
                place.Slot = first++;
            return first;
        }

        /// <summary>Where the box for the order holding <paramref name="slot"/> stands, or null if it isn't one of this counter's.</summary>
        public Transform AnchorOfSlot(int slot)
        {
            foreach (var place in _slots)
            {
                if (place.Slot == slot)
                    return place.Anchor;
            }
            return null;
        }

        /// <summary>True if something stands on the place of the order holding <paramref name="slot"/>.</summary>
        public bool HasFoodAt(int slot)
        {
            foreach (var place in _slots)
            {
                if (place.Slot == slot)
                    return place.Current != null;
            }
            return false;
        }

        public override Transform AnchorFor(Item item)
        {
            var place = PlaceOf(item);
            return place?.Anchor != null ? place.Anchor : ItemAnchor;
        }

        public IEnumerable<(int Slot, Item Item)> SavedItems
        {
            get
            {
                for (var i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Current != null)
                        yield return (i, _slots[i].Current);
                }
            }
        }

        public void ClearForLoad()
        {
        }

        public bool RestoreItem(int slot, Item item)
        {
            if (!(item is Plate plate) || !Fits(plate))
                return false;
            var place = slot >= 0 && slot < _slots.Count && _slots[slot].Current == null
                ? _slots[slot]
                : _slots.Find(p => p.Current == null);
            if (place == null)
                return false;
            place.Current = plate;
            _services.ItemViews.PlaceImmediately(plate, AnchorFor(plate));
            return true;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var held = player.Hands.Current;
            if (held == null)
            {
                if (_top.CanTake)
                    lines.Add(new PromptLine(PromptControl.South, Tr.Get("take back")));
                return;
            }
            if (!(held is Plate plate))
                return;
            lines.Add(new PromptLine(PromptControl.South, LeaveVerb(plate), _top.CanAccept(plate)));
        }

        // Says whether what's in hand is what the order it would go to asked for, so a wrong box is
        // a choice, not an accident (it's still taken, and reviewed).
        private string LeaveVerb(Plate plate)
        {
            if (plate.Items.Count == 0)
                return Tr.Get("for the courier");
            var order = OrderAt(BestPlaceFor(plate));
            if (order == null)
                return Tr.Get("leave here");
            return OrderCheck.Check(order.Order, plate, out _) switch
            {
                OrderMatch.Matches => Tr.Get("for the courier: as ordered"),
                OrderMatch.WrongDoneness => Tr.Format("for the courier: wanted {doneness}", ("doneness", Tr.Get(order.Order.Doneness.Name).ToLowerInvariant())),
                OrderMatch.MissingSide => Tr.Format("for the courier: no {what}", ("what", _services.Catalog.DisplayName(order.Order.SideTypeId).ToLowerInvariant())),
                _ => Tr.Format("for the courier: no {what}", ("what", _services.Catalog.DisplayName(order.Order.MainTypeId).ToLowerInvariant())),
            };
        }

        private static bool Fits(Plate plate) => !plate.IsDirty && plate.Items.Count > 0;

        private DeliveryOrder OrderAt(Place place)
        {
            var order = place != null ? _services?.Service.Deliveries.AtSlot(place.Slot) : null;
            return order != null && order.HoldsSlot ? order : null;
        }

        // Nobody has to aim at a place: the order it matches, or the one whose courier comes
        // soonest, or any free place.
        private Place BestPlaceFor(Plate plate)
        {
            Place soonest = null;
            Place free = null;
            foreach (var place in _slots)
            {
                if (place.Current != null)
                    continue;
                free ??= place;
                var order = OrderAt(place);
                if (order == null)
                    continue;
                if (OrderCheck.Check(order.Order, plate, out _) == OrderMatch.Matches)
                    return place;
                if (soonest == null || order.CourierIn < OrderAt(soonest).CourierIn)
                    soonest = place;
            }
            return soonest ?? free;
        }

        private Place PlaceOf(Item item)
        {
            foreach (var place in _slots)
            {
                if (place.Current == item)
                    return place;
            }
            return null;
        }

        private void Update()
        {
            if (_services == null)
                return;
            foreach (var place in _slots)
            {
                var order = OrderAt(place);
                if (order != null && order.State == DeliveryState.CourierWaiting && place.Current != null)
                    HandOver(place, order);
            }
        }

        // The courier takes it. The food is scored as the customer will find it: the ride cools it
        // as if it sat out for RideCoolingSeconds, and a box keeps most of that off.
        private void HandOver(Place place, DeliveryOrder order)
        {
            var plate = place.Current;
            var delivery = _services.Service.Settings.Delivery;
            var ride = delivery.RideCoolingSeconds * (plate.IsBox ? delivery.BoxCoolingFactor : 1f);
            foreach (var item in plate.Items)
            {
                if (item is Ingredient ingredient && ingredient.Pan == null)
                    _services.Cooking.CarryAway(ingredient, ride);
            }
            var score = DishScorer.Score(plate, _services.Tuning.Quality(_services.Realism.CookingPrecision), order.Order);
            if (!_services.Service.Deliveries.Collect(order, plate, score))
                return;

            // It leaves the kitchen for good, the plate too if that's what it went on.
            foreach (var item in plate.Items)
            {
                if (item is Ingredient ingredient)
                    _services.Cooking.Remove(ingredient);
                _services.ItemViews.Release(item);
            }
            _services.ItemViews.Release(plate);
            place.Current = null;
            if (_lastPlaced == plate)
                _lastPlaced = null;
            PickedUp?.Invoke(this, order);
        }

        // What Interact sees: one surface with room for a box per place.
        private sealed class Top : IPlateSource
        {
            private readonly DeliveryCounterStation _station;

            public Top(DeliveryCounterStation station) => _station = station;

            public bool CanTake
            {
                get
                {
                    foreach (var place in _station._slots)
                    {
                        if (place.Current != null)
                            return true;
                    }
                    return false;
                }
            }

            public bool CanAccept(Item item)
            {
                if (!(item is Plate plate) || !Fits(plate) || _station.PlaceOf(plate) != null)
                    return false;
                foreach (var place in _station._slots)
                {
                    if (place.Current == null)
                        return true;
                }
                return false;
            }

            // The one put down last comes back first: the likeliest mistake to undo.
            public Item Take()
            {
                var place = _station.PlaceOf(_station._lastPlaced) ?? _station._slots.Find(p => p.Current != null);
                if (place == null)
                    throw new InvalidOperationException("Nothing to take here.");
                var plate = place.Current;
                place.Current = null;
                _station._lastPlaced = null;
                return plate;
            }

            public void Put(Item item)
            {
                var plate = (Plate)item;
                var place = _station.BestPlaceFor(plate);
                place.Current = plate;
                _station._lastPlaced = plate;
            }
        }
    }
}
