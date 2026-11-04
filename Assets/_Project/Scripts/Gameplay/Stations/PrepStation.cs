using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The cutting board. A puts a whole ingredient on it, or with empty hands takes what's there;
    /// holding the knife, X cuts when the dial's needle is in the sweet spot. The cut stays here
    /// and blocks the next one until someone moves it (decision 2026-11-04).
    /// </summary>
    public sealed class PrepStation : Interactable, IFaceButtonTarget, IUsable, IStationPrompt
    {
        [Tooltip("Where the item on the cutting board sits.")]
        [SerializeField] private Transform _boardAnchor;

        private PrepCounter _counter;
        private PrepContainer _container;
        private KitchenServices _services;

        public PrepCounter Counter => _counter;

        public override IItemContainer Container => _container;

        public override Item HeldItem => _counter?.Current;

        public override Transform AnchorFor(Item item) => _boardAnchor;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        /// <summary>Raised on every chop, for rhythm feedback.</summary>
        public event Action<ChopGrade> Chopped;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _counter = new PrepCounter(
                whole => services.Cooking.Transform(whole, services.Catalog.Find(whole.Properties.CutIntoTypeId).ToProperties()),
                services.Tuning.ChopPacing,
                services.Tuning.ChopSweepDegrees);
            _counter.CutFinished += HandleCutFinished;
            _container = new PrepContainer(this);
        }

        private void OnDestroy()
        {
            if (_counter != null)
                _counter.CutFinished -= HandleCutFinished;
        }

        public bool TryPress(FaceButton button, PlayerInteractor player)
        {
            if (_counter == null)
                return false;
            if (button == FaceButton.West && TryChop(player))
                return true;
            if (button == FaceButton.South && player.Hands.IsEmpty && _counter.Current != null)
            {
                player.ReceiveItem(_counter.Take(), _boardAnchor.position);
                return true;
            }
            return false;
        }

        public bool TryUse(PlayerInteractor player) => TryChop(player);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_counter == null)
                return;

            var emptyHands = player.Hands.IsEmpty;
            if (player.Tool is Knife && _counter.CanChop)
                lines.Add(new PromptLine(PromptControl.West, $"cut {_counter.Cuts}/{_counter.CutsNeeded}"));
            if (_counter.Current != null && emptyHands)
                lines.Add(new PromptLine(PromptControl.South, $"take {_counter.Current.TypeId}"));
            else if (!emptyHands)
                lines.Add(new PromptLine(PromptControl.South, "put on board", _counter.CanAccept(player.Hands.Current)));
        }

        private bool TryChop(PlayerInteractor player)
        {
            if (_counter == null || !(player.Tool is Knife) || !_counter.CanChop)
                return false;

            var grade = _counter.Chop(Time.unscaledTimeAsDouble);
            if (grade.HasValue)
                Chopped?.Invoke(grade.Value);
            return true;
        }

        private void HandleCutFinished(Ingredient product)
        {
            // The whole ingredient left the simulation when it was cut; its view goes too.
            RefreshViews(_boardAnchor.position);
        }

        // Puts the board's view where it belongs and drops the views of food that's gone.
        private void RefreshViews(Vector3 spawnAt)
        {
            var views = _services.ItemViews;
            if (_counter.Current != null)
                views.Place(_counter.Current, _boardAnchor, _services.Tuning.ItemMove, spawnAt);
            views.ReleaseMissing(_services.Cooking);
        }

        // Interact with food in hand: it goes on the board; views follow.
        private sealed class PrepContainer : IItemContainer, IItemHolder
        {
            private readonly PrepStation _station;

            public PrepContainer(PrepStation station) => _station = station;

            public Item Current => _station._counter.Current;

            public bool CanTake => _station._counter.CanTake;

            public bool CanAccept(Item item) => _station._counter.CanAccept(item);

            public Item Take() => _station._counter.Take();

            public void Put(Item item)
            {
                _station._counter.Put(item);
                _station.RefreshViews(_station._boardAnchor.position);
            }
        }
    }
}
