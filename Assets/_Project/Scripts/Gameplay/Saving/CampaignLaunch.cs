namespace Game.Gameplay.Saving
{
    /// <summary>
    /// What the main menu chose (a slot, and whether to continue it), kept across the scene load.
    /// A deliberate small static, like the earlier rejoin exception: it's the only thing that has to
    /// outlive the scene.
    /// </summary>
    public static class CampaignLaunch
    {
        /// <summary>True once the menu (or a direct start) has chosen.</summary>
        public static bool Decided { get; private set; }

        /// <summary>True if the kitchen should load the slot's campaign as it starts.</summary>
        public static bool WantsSave { get; private set; }

        /// <summary>Which of the three slots this campaign is saved in.</summary>
        public static int Slot { get; private set; }

        public static void Continue(int slot)
        {
            Decided = true;
            WantsSave = true;
            Slot = slot;
        }

        /// <summary>A new campaign in this slot; anything there is deleted first.</summary>
        public static void StartNew(int slot = 0)
        {
            SaveSlots.Delete(slot);
            Decided = true;
            WantsSave = false;
            Slot = slot;
        }

        /// <summary>
        /// The kitchen opened without the menu (in the Editor): the last played slot carries on, or a
        /// new campaign starts in a free one.
        /// </summary>
        public static void ChooseForDirectPlay()
        {
            var saves = SaveSlots.ReadAll();
            var last = SaveSlots.LastPlayed(saves);
            if (last >= 0)
            {
                Continue(last);
                return;
            }
            var free = SaveSlots.FirstFree(saves);
            StartNew(free >= 0 ? free : 0);
        }

        /// <summary>The grill's kitchen: every restaurant without one of its own opens it.</summary>
        public const string FirstKitchen = "Kitchen_Greybox";

        /// <summary>
        /// The kitchen scene this campaign opens (Phase 6, milestone 3): its current restaurant's own,
        /// or <paramref name="fallback"/> (the grill's) for one without a kitchen yet, or no campaign.
        /// </summary>
        public static string KitchenScene(Game.Simulation.Saving.CampaignSave campaign, Game.Data.MiniatureConfig restaurants, string fallback = FirstKitchen) =>
            Game.Simulation.Campaign.KitchenScenes.For(campaign, restaurants != null ? restaurants.LotSettings() : null,
                string.IsNullOrEmpty(fallback) ? FirstKitchen : fallback);

        /// <summary>The kitchen scene the campaign in this slot opens, read from its file.</summary>
        public static string KitchenScene(int slot, Game.Data.MiniatureConfig restaurants, string fallback = FirstKitchen)
        {
            Game.Simulation.Saving.CampaignSave campaign = null;
            try
            {
                var file = SaveSlots.File(slot);
                campaign = file.Exists ? file.Read() : null;
            }
            catch (System.Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Couldn't read slot {slot} to choose its kitchen: {exception.Message}");
            }
            return KitchenScene(campaign, restaurants, fallback);
        }

        /// <summary>Back to a fresh launch: also as play mode starts, in case statics survive between runs.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Decided = false;
            WantsSave = false;
            Slot = 0;
        }
    }
}
