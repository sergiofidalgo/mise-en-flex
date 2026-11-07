using System.Collections.Generic;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A plain counter top that holds one item: a plate, a tray, a container, or a knife. Counters are
    /// precious: each thing kept here is one less place to work (decision 2026-11-04). With a finished
    /// plate on it, Use rings the bell for it to go out. A cutting board or knife rack can sit on it
    /// (decision 2026-11-07), and then it's that instead: the counter can't be reached until it's moved.
    /// </summary>
    public sealed class CounterStation : Interactable, IStationPrompt, IUsable, IToolStation
    {
        [Tooltip("The kitchen's first knife lies here at the start.")]
        [SerializeField] private bool _startsWithKnife;

        private readonly ItemSlot _slot = new ItemSlot();
        private CounterContainer _container;
        private KitchenServices _services;

        public override IItemContainer Container => _container;

        /// <summary>What's on this counter, for staff who work from it.</summary>
        public ItemSlot Slot => _slot;

        /// <summary>The board or rack sitting on it, if any.</summary>
        public CounterTopStation Fixture { get; private set; }

        public bool IsCovered => Fixture != null;

        public override bool IsClear => _slot.Current == null && Fixture == null;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _container = new CounterContainer(_slot);
            if (_startsWithKnife && _slot.Current == null)
            {
                var knife = new Knife();
                _slot.Put(knife);
                services.ItemViews.PlaceImmediately(knife, ItemAnchor);
            }
        }

        /// <summary>A board or rack now sits here; null when it's taken off.</summary>
        internal void SetFixture(CounterTopStation fixture) => Fixture = fixture;

        public bool TryUse(PlayerInteractor player) => _services.Bell.Ring(_slot);

        // A knife goes in the chef's tool slot, not their hands, so it's swapped here rather than by
        // the usual pick-up and put-down: taken even while carrying food, put down with empty hands.
        public bool TryExchangeTool(PlayerInteractor player)
        {
            if (_slot.Current is Knife && player.Tool == null)
            {
                player.ReceiveTool(_slot.Take(), ItemAnchor.position);
                return true;
            }

            if (_slot.Current == null && player.Tool is Knife && player.Hands.IsEmpty)
            {
                var knife = player.ReleaseTool();
                _slot.Put(knife);
                _services.ItemViews.Place(knife, ItemAnchor, _services.Tuning.ItemMove, ItemAnchor.position);
                return true;
            }

            return false;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (ReadyBell.CanRing(_slot))
                lines.Add(_services.Bell.IsRung(_slot)
                    ? new PromptLine(PromptControl.West, "order up", available: false)
                    : new PromptLine(PromptControl.West, "ring the bell"));

            var held = player.Hands.Current;
            if (_slot.Current is Knife)
            {
                if (player.Tool == null)
                    lines.Add(new PromptLine(PromptControl.South, "take knife"));
                return;
            }

            if (_slot.Current is FoodContainer container)
            {
                var what = container.IsEmpty ? "empty" : $"{container.HoldsTypeId} ×{container.Items.Count}/{container.Capacity}";
                if (held == null)
                    lines.Add(new PromptLine(PromptControl.South, container.IsEmpty ? "take container" : $"take: {what}"));
                else if (held is IItemCollection)
                    lines.Add(new PromptLine(PromptControl.South, what));
                else
                    lines.Add(new PromptLine(PromptControl.South, $"add: {what}", container.CanAccept(held)));
                return;
            }

            if (held != null && _slot.Current == null)
                lines.Add(new PromptLine(PromptControl.South, "put down"));
            else if (held == null && _slot.Current != null)
                lines.Add(new PromptLine(PromptControl.South, "take"));
            else if (held == null && _slot.Current == null && player.Tool is Knife)
                lines.Add(new PromptLine(PromptControl.South, "put knife down"));
        }

        // The counter as Interact sees it: a knife lying here isn't picked up into the hands.
        private sealed class CounterContainer : IItemContainer, IItemHolder
        {
            private readonly ItemSlot _slot;

            public CounterContainer(ItemSlot slot) => _slot = slot;

            public Item Current => _slot.Current;

            public bool CanTake => _slot.CanTake && !(_slot.Current is Knife);

            public bool CanAccept(Item item) => _slot.CanAccept(item);

            public Item Take() => _slot.Take();

            public void Put(Item item) => _slot.Put(item);
        }
    }
}
