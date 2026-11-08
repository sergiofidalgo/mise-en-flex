using System.Collections.Generic;
using Game.Simulation.Items;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A plain counter top that holds one item: a plate, a tray, a container, or a knife. Counters are
    /// precious: each thing kept here is one less place to work (decision 2026-11-04). Dirty plates
    /// can also pile up here, a few to a counter (decision 2026-11-08), waiting for the sink. With a
    /// finished plate on it, Use rings the bell for it to go out. Cutting boards and knife racks are
    /// counter-sized pieces of their own (decision 2026-11-08), not things put on a counter.
    /// </summary>
    public sealed class CounterStation : Interactable, IStationPrompt, IUsable, IToolStation
    {
        [Tooltip("The kitchen's first knife lies here at the start.")]
        [SerializeField] private bool _startsWithKnife;

        [Tooltip("How many dirty plates can pile up on this counter.")]
        [SerializeField, Min(1)] private int _dirtyPlates = 3;

        [Tooltip("Metres between plates in a pile; about a plate's height.")]
        [SerializeField, Min(0f)] private float _pileStep = 0.035f;

        private ItemSlot _slot;
        private CounterContainer _container;
        private KitchenServices _services;
        private readonly List<Transform> _pileAnchors = new List<Transform>();

        public override IItemContainer Container => _container;

        /// <summary>What's on this counter, for staff who work from it.</summary>
        public ItemSlot Slot => _slot ??= new ItemSlot(_dirtyPlates);

        public override bool IsClear => Slot.Current == null;

        public bool PromptAsDiamond(PlayerInteractor player) => false;

        public override void Bind(KitchenServices services)
        {
            _services = services;
            _container = new CounterContainer(Slot);
            if (_startsWithKnife && Slot.Current == null)
            {
                var knife = new Knife();
                Slot.Put(knife);
                services.ItemViews.PlaceImmediately(knife, ItemAnchor);
            }
        }

        /// <summary>A plate in a pile sits on the one under it.</summary>
        public override Transform AnchorFor(Item item)
        {
            var level = Slot.IndexOf(item);
            if (level <= 0)
                return ItemAnchor;

            while (_pileAnchors.Count < level)
            {
                var anchor = new GameObject($"PileAnchor_{_pileAnchors.Count + 1}").transform;
                anchor.SetParent(ItemAnchor, false);
                anchor.localPosition = Vector3.up * (_pileStep * (_pileAnchors.Count + 1));
                _pileAnchors.Add(anchor);
            }
            return _pileAnchors[level - 1];
        }

        public bool TryUse(PlayerInteractor player) => _services.Bell.Ring(Slot);

        // A knife goes in the chef's tool slot, not their hands, so it's swapped here rather than by
        // the usual pick-up and put-down: taken even while carrying food, put down with empty hands.
        public bool TryExchangeTool(PlayerInteractor player)
        {
            if (Slot.Current is Knife && player.Tool == null)
            {
                player.ReceiveTool(Slot.Take(), ItemAnchor.position);
                return true;
            }

            if (Slot.Current == null && player.Tool is Knife && player.Hands.IsEmpty)
            {
                var knife = player.ReleaseTool();
                Slot.Put(knife);
                _services.ItemViews.Place(knife, ItemAnchor, _services.Tuning.ItemMove, ItemAnchor.position);
                return true;
            }

            return false;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (ReadyBell.CanRing(Slot))
                lines.Add(_services.Bell.IsRung(Slot)
                    ? new PromptLine(PromptControl.West, "order up", available: false)
                    : new PromptLine(PromptControl.West, "ring the bell"));

            var held = player.Hands.Current;
            if (Slot.Current is Knife)
            {
                if (player.Tool == null)
                    lines.Add(new PromptLine(PromptControl.South, "take knife"));
                return;
            }

            if (Slot.Current is FoodContainer container)
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

            // A dirty plate on a pile of dirty plates.
            if (held is Plate plate && plate.IsDirty && Slot.Current is Plate top && top.IsDirty)
                lines.Add(new PromptLine(PromptControl.South, $"pile up ({Slot.Count}/{Slot.MaxDirtyPlates})", Slot.CanPileOn(held)));
            else if (held != null && Slot.Current == null)
                lines.Add(new PromptLine(PromptControl.South, "put down"));
            else if (held == null && Slot.Current != null)
                lines.Add(new PromptLine(PromptControl.South, Slot.Count > 1 ? $"take ({Slot.Count})" : "take"));
            else if (held == null && Slot.Current == null && player.Tool is Knife)
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
