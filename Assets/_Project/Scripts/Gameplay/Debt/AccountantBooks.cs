using Game.Simulation.Campaign;
using Game.Simulation.Saving;

namespace Game.Gameplay.Debt
{
    /// <summary>
    /// The accountant's side of a loan: borrowing adds the amount to the campaign's money and the debt
    /// (with the boss's 20%) to its loan. It changes the campaign it's given and nothing else: the
    /// office's one copy of the campaign (<see cref="Office.OfficeCampaign"/>) saves it, so the
    /// accountant and the miniature never write over each other's changes.
    /// </summary>
    public static class AccountantBooks
    {
        /// <summary>Borrows into the campaign. False, changing nothing, if the amount isn't on offer.</summary>
        public static bool Borrow(CampaignSave campaign, float amount, LoanSettings terms)
        {
            var loan = campaign.ToLoan();
            if (!loan.Borrow(amount, terms))
                return false;
            campaign.Balance += amount;
            campaign.StoreLoan(loan);
            return true;
        }
    }
}
