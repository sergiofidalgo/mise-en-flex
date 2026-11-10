using System;
using System.Collections.Generic;
using Game.Data;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Market;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A market stall on the street outside (Phase 5, milestone 3; decision 2026-11-10). It works like
    /// storage: holding RT opens its menu, and a face button buys that crate into empty hands. It's
    /// paid at once, as ingredients, even into debt. The crate is carried in by hand and unloaded at a
    /// fridge or shelf. Open in Prep and Service, like everything chefs work at; going out during
    /// service costs the time the walk takes.
    /// </summary>
    public sealed class StallStation : Interactable, IFaceButtonTarget, IStationPrompt, ISlotMenuStation
    {
        [Serializable]
        public sealed class Goods
        {
            [Tooltip("The ingredient's type id, e.g. \"Steak\".")]
            [SerializeField] private string _typeId;
            [SerializeField] private Grade _grade;

            public Goods() { }

            public Goods(string typeId, Grade grade)
            {
                _typeId = typeId;
                _grade = grade;
            }

            public string TypeId => _typeId;
            public Grade Grade => _grade;
        }

        [SerializeField] private MarketConfig _market;

        [Tooltip("Up to four, one per face button, in FaceButton order: South (A), East (B), West (X), North (Y).")]
        [SerializeField] private Goods[] _goods = Array.Empty<Goods>();

        private KitchenServices _services;

        /// <summary>Raised when a crate is bought, with what it cost.</summary>
        public event Action<float> Bought;

        public override IItemContainer Container => null;

        public bool PromptAsDiamond(PlayerInteractor player) => player.IsSlotMenuOpen;

        public override void Bind(KitchenServices services)
        {
            _services = services;
        }

        /// <summary>What face button <paramref name="index"/> buys now, at today's prices; null if nothing.</summary>
        public StallOffer OfferAt(int index)
        {
            if (_services == null || _market == null || index < 0 || index >= _goods.Length || _goods[index] == null)
                return null;
            var goods = _goods[index];
            var definition = _services.Catalog.Find(goods.TypeId);
            return definition != null ? _market.ToSettings().Offer(goods.TypeId, definition.Price, goods.Grade) : null;
        }

        public bool TryPress(FaceButton button, PlayerInteractor player)
        {
            if (!player.IsSlotMenuOpen || !player.Hands.IsEmpty)
                return false;
            var offer = OfferAt((int)button);
            if (offer == null)
                return false;

            var crate = offer.Buy(_services.Days, () => (Ingredient)_services.CreateItem(offer.TypeId));
            player.ReceiveItem(crate, ItemAnchor.position);
            Bought?.Invoke(offer.CratePrice);
            return true;
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_services == null)
                return;
            var empty = player.Hands.IsEmpty;
            if (!player.IsSlotMenuOpen)
            {
                lines.Add(new PromptLine(PromptControl.Triggers, Tr.Get("open"), empty));
                return;
            }

            for (var i = 0; i < 4; i++)
            {
                var offer = OfferAt(i);
                var control = (PromptControl)i;
                if (offer == null)
                {
                    lines.Add(new PromptLine(control, "—", available: false));
                    continue;
                }
                var what = Tr.Get(offer.TypeId);
                var label = offer.Grade == Grade.Premium
                    ? Tr.Format("{what}, premium", ("what", what))
                    : Tr.Format("{what}, budget", ("what", what));
                lines.Add(new PromptLine(control, $"{label} ×{offer.Portions}", empty,
                    iconId: offer.TypeId, iconLabel: Locale.Money(offer.CratePrice, MoneyStyle.Whole)));
            }
        }
    }
}
