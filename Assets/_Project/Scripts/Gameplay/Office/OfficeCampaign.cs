using System;
using System.Collections.Generic;
using Game.Data;
using Game.Gameplay.Saving;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using UnityEngine;

namespace Game.Gameplay.Office
{
    /// <summary>
    /// The campaign as the office sees it (Phase 6, milestone 2): what's owned on the city miniature,
    /// the money, and saving at once after anything done here (game-design §15.4). Loaded when first
    /// asked for, since the campaign is chosen on the street menu after the scene has opened. A new
    /// campaign starts owning nothing, with the boss's starting loan (decision 2026-11-10), and
    /// isn't written until something is bought.
    /// </summary>
    public sealed class OfficeCampaign : MonoBehaviour
    {
        [SerializeField] private MiniatureConfig _config;

        private CampaignSave _campaign;
        private List<RestaurantSettings> _lots;
        private int _loadedSlot = -1;

        /// <summary>Raised after a purchase, for feedback and the miniature's pins.</summary>
        public event Action<RestaurantSave> Bought;

        /// <summary>Raised after the money changes here: a purchase or a loan (the money card).</summary>
        public event Action BalanceChanged;

        public MiniatureConfig Config => _config;

        /// <summary>Every lot on the miniature, in the config's order.</summary>
        public IReadOnlyList<RestaurantSettings> Lots => _lots ??= _config != null ? _config.LotSettings() : new List<RestaurantSettings>();

        /// <summary>The chosen campaign, read from its slot or started fresh.</summary>
        public CampaignSave Campaign
        {
            get
            {
                if (!CampaignLaunch.Decided)
                    CampaignLaunch.ChooseForDirectPlay(); // the office opened straight from the Editor
                if (_campaign == null || _loadedSlot != CampaignLaunch.Slot)
                {
                    _loadedSlot = CampaignLaunch.Slot;
                    _campaign = CampaignLaunch.WantsSave ? SaveSlots.File(_loadedSlot).Read() : null;
                    _campaign ??= RestaurantPurchase.NewCampaign(_config != null ? _config.StartingLoan : 0f);
                }
                return _campaign;
            }
        }

        /// <summary>The restaurant made from that lot, or null while it's on sale.</summary>
        public RestaurantSave Owned(int lot) => lot >= 0 && lot < Lots.Count ? Campaign.Find(Lots[lot].Id) : null;

        /// <summary>Buys a lot under that name and style, and saves.</summary>
        public RestaurantSave Buy(int lot, string name, string style)
        {
            var record = RestaurantPurchase.Buy(Campaign, Lots[lot], name, style);
            Save();
            Bought?.Invoke(record);
            BalanceChanged?.Invoke();
            return record;
        }

        /// <summary>
        /// Borrows at the accountant's desk and saves (decision 2026-11-10). From then on the slot
        /// holds this campaign, even before a restaurant is bought.
        /// </summary>
        public bool Lend(float amount, LoanSettings terms)
        {
            if (!Game.Gameplay.Debt.AccountantBooks.Borrow(Campaign, amount, terms))
                return false;
            Save();
            BalanceChanged?.Invoke();
            return true;
        }

        /// <summary>Makes an owned lot the restaurant played next, and saves. False if it isn't owned.</summary>
        public bool Choose(int lot)
        {
            if (lot < 0 || lot >= Lots.Count || !RestaurantPurchase.Choose(Campaign, Lots[lot].Id))
                return false;
            Save();
            return true;
        }

        // Anything done in the office saves at once. From now on the slot holds this campaign, so
        // the kitchen continues it rather than starting a new one.
        private void Save()
        {
            var campaign = Campaign;
            campaign.LastPlayedTicks = DateTime.Now.Ticks;
            try
            {
                SaveSlots.File(_loadedSlot).Write(campaign);
                CampaignLaunch.Continue(_loadedSlot);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Couldn't save the campaign: {exception.Message}");
            }
        }
    }
}
