using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Quality;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The pass: where finished plates leave the kitchen. A plate with food put here is served:
    /// it's scored on the spot (serving temperature included, so getting there quickly matters)
    /// and collected shortly after. Until customers exist (Phase 2) nobody eats it; the score is
    /// shown as a quality card.
    /// </summary>
    public sealed class PassStation : Interactable, IStationPrompt
    {
        private readonly PassSlot _slot = new PassSlot();
        private KitchenServices _services;
        private float _collectAt;

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
                lines.Add(new PromptLine(PromptControl.South, "serve", _slot.CanAccept(player.Hands.Current)));
        }

        private void OnDestroy()
        {
            _slot.Placed -= HandlePlaced;
        }

        private void HandlePlaced(Plate plate)
        {
            var score = DishScorer.Score(plate, _services.Tuning.Quality(_services.Realism.CookingPrecision));
            _collectAt = Time.time + _services.Tuning.PassCollectDelay;
            Served?.Invoke(this, score);
        }

        private void Update()
        {
            if (_slot.Current == null || Time.time < _collectAt)
                return;

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
