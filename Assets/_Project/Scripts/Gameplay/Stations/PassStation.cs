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
    /// The pass: where finished plates leave the kitchen. With counter service (decision
    /// 2026-11-02) each pass counter faces one stool: a plate with food put here is served to the
    /// customer waiting there, scored on the spot (serving temperature included, so getting there
    /// quickly matters), and cleared when they've finished eating. With nobody waiting, it's
    /// scored and cleared after a short delay, as before customers existed.
    /// </summary>
    public sealed class PassStation : Interactable, IStationPrompt
    {
        [Tooltip("The stool this pass counter faces (0 = leftmost).")]
        [SerializeField, Min(0)] private int _seatIndex;

        private readonly PassSlot _slot = new PassSlot();
        private KitchenServices _services;
        private float _collectAt;
        private Customer _eating;

        public int SeatIndex => _seatIndex;

        public override IItemContainer Container => _slot;

        // Shown on the pass for the view, but not offered to the Interact rules (it isn't an
        // IItemHolder), so nothing can be poured onto or scooped off a served plate.
        public override Item HeldItem => _slot.Current;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        /// <summary>Raised when a plate is served, with its score.</summary>
        public event Action<PassStation, DishScore> Served;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _slot.Placed += HandlePlaced;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_slot.Current == null)
                lines.Add(new PromptLine(PromptControl.South, ServeVerb(player.Hands.Current as Plate), _slot.CanAccept(player.Hands.Current)));
        }

        // Says whether the plate in hand is what the customer at this stool ordered, so a wrong
        // dish is a choice, not an accident (wrong dishes are still eaten, unhappily).
        private string ServeVerb(Plate plate)
        {
            var customer = _services?.Service.AtSeat(_seatIndex);
            if (plate == null || plate.Items.Count == 0 || customer?.Order == null || customer.State != CustomerState.Waiting)
                return "serve";

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

        private void HandlePlaced(Plate plate)
        {
            // Scored against the order of whoever is waiting here; the house standard otherwise.
            var customer = _services.Service.AtSeat(_seatIndex);
            var order = customer != null && customer.State == CustomerState.Waiting ? customer.Order : null;
            var score = DishScorer.Score(plate, _services.Tuning.Quality(_services.Realism.CookingPrecision), order);
            _collectAt = Time.time + _services.Tuning.PassCollectDelay;
            _eating = _services.Service.Serve(_seatIndex, plate, score) ? customer : null;
            Served?.Invoke(this, score);
        }

        private void Update()
        {
            if (_slot.Current == null)
                return;

            // A customer's plate stays until they've finished; otherwise it's cleared after a moment.
            if (_eating != null ? _eating.State == CustomerState.Eating : Time.time < _collectAt)
                return;
            _eating = null;

            // Collected: the food leaves the kitchen and the plate goes with it.
            var plate = _slot.Collect();
            foreach (var item in plate.Items)
            {
                if (item is Ingredient ingredient)
                    _services.Cooking.Remove(ingredient);
                _services.ItemViews.Release(item);
            }
            _services.ItemViews.Release(plate);
        }

        // Only a plate with food on it can be served; nothing comes back off the pass.
        private sealed class PassSlot : IItemContainer
        {
            public Item Current { get; private set; }

            public event Action<Plate> Placed;

            public bool CanTake => false;

            public bool CanAccept(Item item) => Current == null && item is Plate plate && plate.Items.Count > 0;

            public Item Take() => throw new InvalidOperationException("Served plates can't be taken back.");

            public void Put(Item item)
            {
                if (!CanAccept(item))
                    throw new InvalidOperationException("Only a plate with food can be served.");
                Current = item;
                Placed?.Invoke((Plate)item);
            }

            public Plate Collect()
            {
                var plate = (Plate)Current;
                Current = null;
                return plate;
            }
        }
    }
}
