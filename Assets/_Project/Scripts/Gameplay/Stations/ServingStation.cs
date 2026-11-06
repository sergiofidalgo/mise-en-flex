using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// Somewhere customers sit and are served: a pass counter or a table, with one plate's room per
    /// seat. A plate with food put here goes to the customer whose order it matches, or else the first
    /// waiting, and is scored on the spot (serving temperature included, so getting there quickly
    /// matters). With nobody waiting, the plate stays, can be taken back, and goes to whoever sits
    /// there next, wanted or not. When they've finished, the food goes and the dirty plate stays until
    /// someone clears it. The next customer can still sit and order, but that place can't be served
    /// until it's cleared (decisions 2026-11-04).
    /// </summary>
    public abstract class ServingStation : Interactable, IStationPrompt
    {
        /// <summary>One customer's place at this station.</summary>
        protected sealed class Place
        {
            public readonly PassCounter Slot = new PassCounter();
            public int SeatIndex;
            public Transform Seat;
            public Transform PlateAnchor;
            public Customer Eating;
        }

        private readonly List<Place> _places = new List<Place>();
        private KitchenServices _services;
        private ServingTop _top;
        private Plate _lastPlaced;

        protected KitchenServices Services => _services;

        protected IReadOnlyList<Place> Places => _places;

        public override IItemContainer Container => _top;

        /// <summary>How many customers can sit here.</summary>
        public int SeatCount => _places.Count;

        public bool HasDirtyPlate
        {
            get
            {
                foreach (var place in _places)
                {
                    if (place.Slot.HasDirtyPlate)
                        return true;
                }
                return false;
            }
        }

        // Nobody's eating here between services, when furniture is sold.
        public override bool IsClear
        {
            get
            {
                foreach (var place in _places)
                {
                    if (place.Slot.Current != null)
                        return false;
                }
                return true;
            }
        }

        /// <summary>The plate just put here, so it can be shown; otherwise any that's here.</summary>
        public override Item HeldItem
        {
            get
            {
                if (_lastPlaced != null && PlaceOf(_lastPlaced) != null)
                    return _lastPlaced;
                foreach (var place in _places)
                {
                    if (place.Slot.Current != null)
                        return place.Slot.Current;
                }
                return null;
            }
        }

        public override Transform AnchorFor(Item item)
        {
            var place = PlaceOf(item);
            return place?.PlateAnchor != null ? place.PlateAnchor : ItemAnchor;
        }

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        /// <summary>Raised when a plate is served, with its score.</summary>
        public event Action<ServingStation, DishScore> Served;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _top = new ServingTop(this);
        }

        protected Place AddPlace(Transform seat, Transform plateAnchor)
        {
            var place = new Place { Seat = seat, PlateAnchor = plateAnchor };
            place.Slot.Placed += plate => TryServe(place);
            _places.Add(place);
            return place;
        }

        protected void RemoveLastPlace() => _places.RemoveAt(_places.Count - 1);

        /// <summary>Numbers this station's seats for today's service, from <paramref name="first"/>.</summary>
        /// <returns>The next free number.</returns>
        public int AssignSeats(int first)
        {
            foreach (var place in _places)
                place.SeatIndex = first++;
            return first;
        }

        public IEnumerable<int> SeatIndexes
        {
            get
            {
                foreach (var place in _places)
                    yield return place.SeatIndex;
            }
        }

        /// <summary>Where the customer in this seat sits.</summary>
        public Vector3 SeatPosition(int seatIndex)
        {
            foreach (var place in _places)
            {
                if (place.SeatIndex == seatIndex)
                    return place.Seat.position;
            }
            return transform.position;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            var held = player.Hands.Current;
            if (held == null && _top.CanTake)
                lines.Add(new PromptLine(PromptControl.South, HasDirtyPlate ? "clear plate" : "take plate back"));
            else if (HasFreePlace)
                lines.Add(new PromptLine(PromptControl.South, ServeVerb(held as Plate), _top.CanAccept(held)));
            else if (_top.CanTake)
                lines.Add(new PromptLine(PromptControl.South, HasDirtyPlate ? "clear plate" : "take plate back", false));
        }

        private bool HasFreePlace
        {
            get
            {
                foreach (var place in _places)
                {
                    if (place.Slot.Current == null)
                        return true;
                }
                return false;
            }
        }

        // Says whether the plate in hand is what the customer it would go to ordered, so a wrong
        // dish is a choice, not an accident (wrong dishes are still eaten, unhappily).
        private string ServeVerb(Plate plate)
        {
            if (plate == null || plate.Items.Count == 0)
                return "serve";
            var place = BestPlaceFor(plate);
            var customer = place != null ? _services?.Service.AtSeat(place.SeatIndex) : null;
            if (customer?.Order == null || customer.State != CustomerState.Waiting)
                return "leave plate";

            var order = customer.Order;
            return OrderCheck.Check(order, plate, out _) switch
            {
                OrderMatch.Matches => "serve: as ordered",
                OrderMatch.WrongDoneness => $"serve: wanted {order.Doneness.Name}",
                OrderMatch.MissingSide => $"serve: no {_services.Catalog.DisplayName(order.SideTypeId).ToLowerInvariant()}",
                _ => $"serve: no {_services.Catalog.DisplayName(order.MainTypeId).ToLowerInvariant()}",
            };
        }

        // Nobody has to aim at a chair: the diner it matches, or the first waiting, or the first free.
        // A place with a dirty plate can't take it, even if that's who's waiting.
        private Place BestPlaceFor(Plate plate)
        {
            Place waiting = null;
            Place free = null;
            foreach (var place in _places)
            {
                if (place.Slot.Current != null)
                    continue;
                free ??= place;

                var customer = _services.Service.AtSeat(place.SeatIndex);
                if (customer == null || customer.State != CustomerState.Waiting)
                    continue;
                waiting ??= place;
                if (customer.Order != null && OrderCheck.Check(customer.Order, plate, out _) == OrderMatch.Matches)
                    return place;
            }
            return waiting ?? free;
        }

        private Place PlaceOf(Item item)
        {
            foreach (var place in _places)
            {
                if (place.Slot.Current == item)
                    return place;
            }
            return null;
        }

        private void Update()
        {
            foreach (var place in _places)
                UpdatePlace(place);
        }

        private void UpdatePlace(Place place)
        {
            if (place.Slot.IsWaitingForCustomer)
            {
                TryServe(place);
                return;
            }

            if (place.Eating == null || place.Eating.State == CustomerState.Eating)
                return;

            // Eaten: the food leaves the kitchen, and the dirty plate waits to be cleared.
            var plate = place.Slot.Current;
            foreach (var item in plate.Items)
            {
                if (item is Ingredient ingredient)
                    _services.Cooking.Remove(ingredient);
                _services.ItemViews.Release(item);
            }
            place.Slot.FinishEating();
            place.Eating = null;
        }

        // A customer at this seat takes the plate as it is now, whether or not it's what they
        // ordered, and however long it's been waiting. Scored against their order, on the spot.
        private void TryServe(Place place)
        {
            var customer = _services.Service.AtSeat(place.SeatIndex);
            if (customer == null || customer.State != CustomerState.Waiting)
                return;

            var plate = place.Slot.Current;
            var score = DishScorer.Score(plate, _services.Tuning.Quality(_services.Realism.CookingPrecision), customer.Order);
            if (!_services.Service.Serve(place.SeatIndex, plate, score))
                return;

            place.Slot.StartEating();
            place.Eating = customer;
            Served?.Invoke(this, score);
        }

        // What Interact sees: one surface with room for a plate per seat.
        private sealed class ServingTop : IPlateSource
        {
            private readonly ServingStation _station;

            public ServingTop(ServingStation station)
            {
                _station = station;
            }

            public bool CanTake
            {
                get
                {
                    foreach (var place in _station._places)
                    {
                        if (place.Slot.CanTake)
                            return true;
                    }
                    return false;
                }
            }

            public bool CanAccept(Item item)
            {
                foreach (var place in _station._places)
                {
                    if (place.Slot.CanAccept(item))
                        return true;
                }
                return false;
            }

            // Dirty plates first: clearing up is what empty hands are usually there for.
            public Item Take()
            {
                foreach (var place in _station._places)
                {
                    if (place.Slot.HasDirtyPlate)
                        return place.Slot.Take();
                }
                foreach (var place in _station._places)
                {
                    if (place.Slot.CanTake)
                        return place.Slot.Take();
                }
                throw new InvalidOperationException("Nothing to take here.");
            }

            public void Put(Item item)
            {
                var plate = (Plate)item;
                var place = _station.BestPlaceFor(plate);
                _station._lastPlaced = plate;
                place.Slot.Put(plate);
            }
        }
    }
}
