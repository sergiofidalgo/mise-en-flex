using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Simulation.Cooking;
using Game.Simulation.Items;
using Game.Simulation.Market;
using UnityEngine;

namespace Game.Gameplay.Stations
{
    /// <summary>
    /// A market stall on the street outside (Phase 5, milestone 3; decisions 2026-11-10 and
    /// 2026-11-11). It works like storage: holding RT opens its menu, and each press of a face button
    /// buys one portion of that ingredient, brand new and fully fresh, at its price. It goes straight
    /// into the restaurant's storage: food that keeps cold to a fridge first, the rest to a shelf
    /// first, each spilling over into the other when full. With no room anywhere nothing is bought.
    /// Paid at once as ingredients, even into debt. Open in Prep and Service, like everything chefs
    /// work at; going out during service costs the time the walk takes.
    /// </summary>
    public sealed class StallStation : Interactable, IFaceButtonTarget, IStationPrompt, ISlotMenuStation
    {
        [Tooltip("Ingredient type ids, up to four, one per face button, in FaceButton order: South (A), East (B), West (X), North (Y).")]
        [SerializeField] private string[] _goods = Array.Empty<string>();

        private KitchenServices _services;

        /// <summary>Raised when a portion is bought, with what it cost.</summary>
        public event Action<float> Bought;

        public override IItemContainer Container => null;

        public bool PromptAsDiamond(PlayerInteractor player) => player.IsSlotMenuOpen;

        public override void Bind(KitchenServices services)
        {
            _services = services;
        }

        /// <summary>What face button <paramref name="index"/> buys, at its price; null if nothing.</summary>
        public StallOffer OfferAt(int index)
        {
            if (_services == null || index < 0 || index >= _goods.Length || string.IsNullOrEmpty(_goods[index]))
                return null;
            var definition = _services.Catalog.Find(_goods[index]);
            return definition != null ? new StallOffer(definition.TypeId, definition.Price) : null;
        }

        public bool TryPress(FaceButton button, PlayerInteractor player)
        {
            if (!player.IsSlotMenuOpen)
                return false;
            var offer = OfferAt((int)button);
            if (offer == null)
                return false;

            var storage = StorageInOrder(offer.TypeId);
            var unit = offer.BuyInto(_services.Days, storage.Select(s => s.Unit),
                into => (Ingredient)_services.CreateItem(offer.TypeId, into.Temperature));
            if (unit == null)
                return false; // no room anywhere: the stall shakes "can't"

            storage.First(s => s.Unit == unit).NotifyStocked(1);
            Bought?.Invoke(offer.Price);
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
                // The icon and the price of one; greyed out when storage has no room for it.
                var fits = offer.Fits(StorageInOrder(offer.TypeId).Select(s => s.Unit));
                lines.Add(new PromptLine(control, Tr.Get(offer.TypeId), fits,
                    iconId: offer.TypeId, iconLabel: Locale.Money(offer.Price)));
            }
        }
    }
}
