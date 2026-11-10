using Game.Gameplay.Saving;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;

namespace Game.Gameplay.Debt
{
    /// <summary>
    /// The accountant's side of a loan, on disk: borrowing adds the amount to the campaign's money
    /// and the debt (with the boss's 20%) to its loan, and saves at once, as everything done in the
    /// office does (§15.4).
    /// </summary>
    public static class AccountantBooks
    {
        /// <summary>
        /// The campaign in the slot, or a new one holding <paramref name="startingMoney"/> if nothing's
        /// saved yet. Null if there's a file that can't be read (one from a newer game): it's never
        /// written over.
        /// </summary>
        public static CampaignSave Read(int slot, float startingMoney)
        {
            var file = SaveSlots.File(slot);
            if (!file.Exists)
                return new CampaignSave { Balance = startingMoney };
            return file.Read();
        }

        /// <summary>Borrows and saves. False, changing nothing, if the amount isn't on offer.</summary>
        public static bool Borrow(int slot, CampaignSave campaign, float amount, LoanSettings terms)
        {
            var loan = campaign.ToLoan();
            if (!loan.Borrow(amount, terms))
                return false;
            campaign.Balance += amount;
            campaign.StoreLoan(loan);
            campaign.LastPlayedTicks = System.DateTime.Now.Ticks;
            SaveSlots.File(slot).Write(campaign);
            // From now on the kitchen must load this file, even for a campaign that hasn't played a day.
            CampaignLaunch.Continue(slot);
            return true;
        }
    }
}
