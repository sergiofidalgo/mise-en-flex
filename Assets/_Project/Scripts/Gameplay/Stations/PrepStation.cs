using System;
using System.Collections.Generic;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Prep;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// The mise en place counter: a cutting board in the middle and four slots for prepared
    /// ingredients, laid out like the face buttons. A puts a whole ingredient on the board (or a
    /// prepared one back into its slot); holding the knife, X chops in time with the beat; with
    /// empty hands the face buttons take a portion from that slot (A takes from the board first).
    /// </summary>
    public sealed class PrepStation : Interactable, IFaceButtonTarget, IUsable, IStationPrompt
    {
        [Tooltip("Where the item on the cutting board sits.")]
        [SerializeField] private Transform _boardAnchor;

        [Tooltip("Slot bins in FaceButton order: South (A), East (B), West (X), North (Y).")]
        [SerializeField] private Transform[] _slotAnchors = new Transform[PrepCounter.SlotCount];

        private PrepCounter _counter;
        private PrepContainer _container;
        private KitchenServices _services;

        public PrepCounter Counter => _counter;

        public override IItemContainer Container => _container;

        public override Item HeldItem => _counter?.Current;

        public override Transform AnchorFor(Item item) => _boardAnchor;

        public bool PromptAsDiamond => true;

        /// <summary>Raised on every chop, for rhythm feedback.</summary>
        public event Action<ChopGrade> Chopped;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _counter = new PrepCounter(
                services.Equipment.PrepSlotCapacity,
                whole => services.Cooking.Transform(whole, services.Catalog.Find(whole.Properties.CutIntoTypeId).ToProperties()),
                services.Tuning.ChopGoodWindow,
                services.Tuning.ChopOkWindow);
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
            if (button == FaceButton.West && TryChop(player))
                return true;
            if (!player.Hands.IsEmpty || _counter == null)
                return false;

            if (button == FaceButton.South && _counter.Current != null)
            {
                player.ReceiveItem(_counter.Take(), _boardAnchor.position);
                return true;
            }

            var index = (int)button;
            if (!_counter.TryTakeFromSlot(index, out var portion))
                return false;
            player.ReceiveItem(portion, _slotAnchors[index].position);
            return true;
        }

        public bool TryUse(PlayerInteractor player) => TryChop(player);

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_counter == null)
                return;

            var canChop = player.Tool is Knife && _counter.CanChop;
            var emptyHands = player.Hands.IsEmpty;
            lines.Add(_counter.Current != null && emptyHands
                ? new PromptLine(PromptControl.South, $"take {_counter.Current.TypeId}")
                : SlotLine(PromptControl.South, 0, emptyHands));
            lines.Add(SlotLine(PromptControl.East, 1, emptyHands));
            lines.Add(canChop
                ? new PromptLine(PromptControl.West, $"chop {_counter.Cuts}/{_counter.CutsNeeded}")
                : SlotLine(PromptControl.West, 2, emptyHands));
            lines.Add(SlotLine(PromptControl.North, 3, emptyHands));
        }

        private PromptLine SlotLine(PromptControl control, int index, bool emptyHands)
        {
            var slot = _counter.Slots[index];
            return slot.Items.Count > 0
                ? new PromptLine(control, $"{slot.TypeId} ×{slot.Items.Count}", emptyHands)
                : new PromptLine(control, "—", available: false);
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

        private void HandleCutFinished(Ingredient product, bool stored)
        {
            // The whole ingredient left the simulation when it was cut; its view goes too.
            RefreshViews(_boardAnchor.position);
        }

        // Puts every view where the counter says its item is: the board, or stacked in its slot bin.
        private void RefreshViews(Vector3 spawnAt)
        {
            var views = _services.ItemViews;
            var move = _services.Tuning.ItemMove;
            if (_counter.Current != null)
                views.Place(_counter.Current, _boardAnchor, move, spawnAt);
            for (var i = 0; i < _counter.Slots.Count; i++)
            {
                foreach (var item in _counter.Slots[i].Items)
                    views.Place(item, _slotAnchors[i], move, spawnAt);
            }
            views.ReleaseMissing(_services.Cooking);
        }

        // Interact with food in hand: the counter decides board or slot; views follow.
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
