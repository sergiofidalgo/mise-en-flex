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
    /// and blocks the next one until someone moves it (decision 2026-11-04). It sits on a counter and
    /// is moved only in management mode (decision 2026-11-07).
    ///
    /// Sharing its counter with a knife rack (decision 2026-11-08), it answers for the rack too, all on
    /// A: with empty hands and no knife, A takes a knife first; holding the knife, A takes what's on the
    /// board, and hangs the knife back when the board is empty. With food in hand, A puts it on the
    /// board, and only if the board can't take it, swaps the knife as the rack does.
    /// </summary>
    public sealed class PrepStation : CounterTopStation, IFaceButtonTarget, IUsable, IStationPrompt, IToolStation
    {
        [Tooltip("Where the item on the cutting board sits.")]
        [SerializeField] private Transform _boardAnchor;

        [Tooltip("Where the board sits on its counter when a knife rack shares it: forward, clear of the knives.")]
        [SerializeField] private Vector3 _sharedOffset = new Vector3(0f, 0f, -0.25f);

        private PrepCounter _counter;
        private PrepContainer _container;
        private KitchenServices _services;

        public PrepCounter Counter => _counter;

        public override IItemContainer Container => _container;

        public override Item HeldItem => _counter?.Current;

        public override Transform AnchorFor(Item item) => _boardAnchor;

        public override bool IsClear => _counter == null || _counter.Current == null;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        /// <summary>The knife rack sharing this board's counter, if any.</summary>
        public KnifeRackStation Rack => OnCounter != null ? OnCounter.Rack : null;

        /// <summary>Moves the board forward on its counter while a rack shares it, back to the middle when not.</summary>
        internal void SitForward(bool forward) => transform.localPosition = forward ? _sharedOffset : Vector3.zero;

        /// <summary>Raised on every chop, for rhythm feedback.</summary>
        public event Action<ChopGrade> Chopped;

        public override void Bind(KitchenServices services)
        {
            base.Bind(services);
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
            if (button != FaceButton.South || !player.Hands.IsEmpty)
                return false;

            // Empty hands: a knife first if the chef has none, then what's on the board, then the
            // knife goes back, so chopping and taking the cut never fight over A.
            var rack = Rack;
            if (rack != null && player.Tool == null && rack.KnifeCount > 0)
                return rack.TryExchangeTool(player);
            if (_counter.Current != null)
            {
                player.ReceiveItem(_counter.Take(), _boardAnchor.position);
                return true;
            }
            return rack != null && player.Tool is Knife && rack.TryExchangeTool(player);
        }

        // With food in hand, once the board has refused it: the rack swaps the knife as it would alone.
        public bool TryExchangeTool(PlayerInteractor player) => Rack != null && Rack.TryExchangeTool(player);

        public bool TryUse(PlayerInteractor player) => TryChop(player);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_counter == null)
                return;

            var emptyHands = player.Hands.IsEmpty;
            var rack = Rack;
            if (player.Tool is Knife && _counter.CanChop)
                lines.Add(new PromptLine(PromptControl.West, $"cut {_counter.Cuts}/{_counter.CutsNeeded}"));

            // The same order TryPress and Interact follow, so the prompt always names what A will do.
            if (emptyHands)
            {
                if (rack != null && player.Tool == null && rack.KnifeCount > 0)
                    lines.Add(new PromptLine(PromptControl.South, "take knife"));
                else if (_counter.Current != null)
                    lines.Add(new PromptLine(PromptControl.South, $"take {_counter.Current.TypeId}"));
                else if (rack != null && player.Tool is Knife)
                    lines.Add(new PromptLine(PromptControl.South, "hang knife", rack.KnifeCount < rack.Capacity));
                else if (rack != null && player.Tool == null)
                    lines.Add(new PromptLine(PromptControl.South, "take knife", available: false));
                return;
            }

            var fits = _counter.CanAccept(player.Hands.Current);
            if (!fits && rack != null && player.Tool == null && rack.KnifeCount > 0)
                lines.Add(new PromptLine(PromptControl.South, "take knife"));
            else if (!fits && rack != null && player.Tool is Knife && rack.KnifeCount < rack.Capacity)
                lines.Add(new PromptLine(PromptControl.South, "hang knife"));
            else
                lines.Add(new PromptLine(PromptControl.South, "put on board", fits));
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
