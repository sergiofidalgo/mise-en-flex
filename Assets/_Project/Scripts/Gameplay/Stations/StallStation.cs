using System;
using System.Collections.Generic;
using Game.Data;
using Game.Simulation.Cooking;
using System.Linq;
using Game.Simulation.Items;
using Game.Simulation.Market;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A market stall on the street outside (Phase 5, milestone 3; decision 2026-11-10). It works like
    /// storage: holding RT opens its menu, and a face button buys that stock. It goes straight into the
    /// restaurant's storage: food that keeps cold to a fridge first, the rest to a shelf first, each
    /// spilling over into the other when full. Only what fits is bought, paid at once as ingredients,
    /// even into debt. Open in Prep and Service, like everything chefs work at; going out during
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

        // Premium prices in the menu.
        private static readonly Color PremiumGold = new Color(0.98f, 0.78f, 0.3f);

        [SerializeField] private MarketConfig _market;

        [Tooltip("Up to four, one per face button, in FaceButton order: South (A), East (B), West (X), North (Y).")]
        [SerializeField] private Goods[] _goods = Array.Empty<Goods>();

        private KitchenServices _services;

        /// <summary>Raised when stock is bought, with what it cost.</summary>
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
            if (!player.IsSlotMenuOpen)
                return false;
            var offer = OfferAt((int)button);
            if (offer == null)
                return false;

            var storage = StorageInOrder(offer.TypeId);
            var stocked = offer.BuyInto(_services.Days, storage.Select(s => s.Unit),
                unit => (Ingredient)_services.CreateItem(offer.TypeId, unit.Temperature));
            if (stocked.Count == 0)
                return false; // no room anywhere: the stall shakes "can't"

            foreach (var (unit, count) in stocked)
                storage.First(s => s.Unit == unit).NotifyStocked(count);
            Bought?.Invoke(offer.PriceFor(stocked.Sum(s => s.Count)));
            return true;
        }

        // The restaurant's fridges and shelves, in the order this food fills them.
        private List<StorageStation> StorageInOrder(string typeId)
        {
            var keepsCold = _services.Catalog.Find(typeId)?.KeepCold ?? false;
            return StorageOrder.For(_services.Stations.All.OfType<StorageStation>().Where(s => s.Unit != null), s => s.IsRefrigerated, keepsCold);
        }

        public void DescribeActions(PlayerInteractor player, List<PromptLine> lines)
        {
            if (_services == null)
                return;
            if (!player.IsSlotMenuOpen)
            {
                lines.Add(new PromptLine(PromptControl.Triggers, Tr.Get("open"), player.Hands.IsEmpty));
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
                // As many as storage has room for: none, and it's greyed out.
                var fits = offer.PortionsThatFit(StorageInOrder(offer.TypeId).Select(s => s.Unit));
                // The menu shows the icon and this: premium's price is in gold, so the two grades read
                // apart (the HUD font has no star). A proper premium badge is for the art pass.
                var price = Locale.Money(offer.PriceFor(fits), MoneyStyle.Whole);
                lines.Add(new PromptLine(control, $"{label} ×{fits}", fits > 0,
                    iconId: offer.TypeId, iconLabel: offer.Grade == Grade.Premium ? $"<color=#{ColorUtility.ToHtmlStringRGB(PremiumGold)}>{price}</color>" : price));
            }
        }
    }
}
