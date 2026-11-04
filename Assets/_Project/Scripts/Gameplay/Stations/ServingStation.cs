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
    /// Somewhere a customer sits and is served: a pass counter or a table. A plate with food put
    /// here is served to the customer waiting at its seat, scored on the spot (serving temperature
    /// included, so getting there quickly matters). With nobody waiting, the plate stays, can be
    /// taken back, and goes to whoever sits down next, wanted or not. When they've finished, the
    /// food goes and the dirty plate stays until someone clears it. The next customer can still
    /// sit and order, but can't be served until it's cleared (decisions 2026-11-04).
    /// </summary>
    public abstract class ServingStation : Interactable, IStationPrompt
    {
        [Tooltip("Which of the service's seats this is (0 = leftmost stool). Tables get theirs when service opens.")]
        [SerializeField, Min(0)] private int _seatIndex;

        [Tooltip("Where the customer sits.")]
        [SerializeField] private Transform _seat;

        private readonly PassCounter _slot = new PassCounter();
        private KitchenServices _services;
        private Customer _eating;

        public int SeatIndex => _seatIndex;

        public Transform Seat => _seat;

        /// <summary>Nothing on it. Nobody's eating here between services, when furniture is sold.</summary>
        public override bool IsClear => _slot.Current == null;

        public void AssignSeat(int index) => _seatIndex = index;

        public override IItemContainer Container => _slot;

        /// <summary>For whoever clears up.</summary>
        public IDirtyPlateSource DirtyPlates => _slot;

        // Shown on the pass for the view, but not offered to the Interact rules (it isn't an
        // IItemHolder), so nothing can be poured onto or scooped off a served plate.
        public override Item HeldItem => _slot.Current;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        /// <summary>Raised when a plate is served, with its score.</summary>
        public event Action<ServingStation, DishScore> Served;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _slot.Placed += HandlePlaced;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_slot.Current == null)
                lines.Add(new PromptLine(PromptControl.South, ServeVerb(player.Hands.Current as Plate), _slot.CanAccept(player.Hands.Current)));
            else if (_slot.CanTake)
                lines.Add(new PromptLine(PromptControl.South, _slot.HasDirtyPlate ? "clear plate" : "take plate back", player.Hands.IsEmpty));
        }

        // Says whether the plate in hand is what the customer at this stool ordered, so a wrong
        // dish is a choice, not an accident (wrong dishes are still eaten, unhappily).
        private string ServeVerb(Plate plate)
        {
            var customer = _services?.Service.AtSeat(_seatIndex);
            if (plate == null || plate.Items.Count == 0)
                return "serve";
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

        private void OnDestroy()
        {
            _slot.Placed -= HandlePlaced;
        }

        private void HandlePlaced(Plate plate) => TryServe();

        private void Update()
        {
            if (_slot.IsWaitingForCustomer)
            {
                TryServe();
                return;
            }

            if (_eating == null || _eating.State == CustomerState.Eating)
                return;

            // Eaten: the food leaves the kitchen, and the dirty plate waits to be cleared.
            var plate = _slot.Current;
            foreach (var item in plate.Items)
            {
                if (item is Ingredient ingredient)
                    _services.Cooking.Remove(ingredient);
                _services.ItemViews.Release(item);
            }
            _slot.FinishEating();
            _eating = null;
        }

        // A customer at the stool takes the plate as it is now, whether or not it's what they
        // ordered, and however long it's been waiting. Scored against their order, on the spot.
        private void TryServe()
        {
            var customer = _services.Service.AtSeat(_seatIndex);
            if (customer == null || customer.State != CustomerState.Waiting)
                return;

            var plate = _slot.Current;
            var score = DishScorer.Score(plate, _services.Tuning.Quality(_services.Realism.CookingPrecision), customer.Order);
            if (!_services.Service.Serve(_seatIndex, plate, score))
                return;

            _slot.StartEating();
            _eating = customer;
            Served?.Invoke(this, score);
        }
    }
}
