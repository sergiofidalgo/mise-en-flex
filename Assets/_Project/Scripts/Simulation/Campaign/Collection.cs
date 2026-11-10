using System;

namespace Game.Simulation.Campaign
{
    /// <summary>
    /// The boss's collection, worked out as Review starts on a restaurant's collection day (every
    /// 5 finished days there): that restaurant's cut plus one instalment of the campaign's loan
    /// (decision 2026-11-10).
    /// <para>
    /// Partial payment (our call, 2026-11-10): he takes whatever is in the pocket, and the rest
    /// stays as money below zero, owed to him, as debt already was (decision 2026-11-08). So there
    /// is one number to read, the balance, and no separate arrears to track. The collection is
    /// paid in full only if the balance is at zero or above afterwards: debt left over from
    /// earlier (stock bought on credit, an earlier shortfall) has to be cleared too, or it's
    /// missed again. Paying part still helps, because it's less to clear next time.
    /// </para>
    /// </summary>
    public readonly struct Collection
    {
        public Collection(float cut, float instalment, float balanceBefore)
        {
            Cut = cut;
            Instalment = instalment;
            BalanceBefore = balanceBefore;
        }

        /// <summary>That restaurant's cut (its fee).</summary>
        public float Cut { get; }

        /// <summary>The loan instalment; 0 with no loan.</summary>
        public float Instalment { get; }

        public float Due => Cut + Instalment;

        public float BalanceBefore { get; }

        public float BalanceAfter => BalanceBefore - Due;

        /// <summary>What actually changed hands: what was in the pocket, up to what was due.</summary>
        public float Paid => Math.Min(Due, Math.Max(0f, BalanceBefore));

        /// <summary>What's still owed to him after it: the money below zero.</summary>
        public float Shortfall => Math.Max(0f, -BalanceAfter);

        /// <summary>Everything owed is cleared, earlier debt included: resets the escalation.</summary>
        public bool PaidInFull => BalanceAfter >= 0f;

        /// <summary>
        /// Works out the collection without taking it: <paramref name="loan"/> is left as it is, for a
        /// forecast (the dashboard's "next collection", the call when it's near).
        /// </summary>
        public static Collection Preview(float balance, float cut, LoanAccount loan) =>
            new Collection(cut, loan?.NextInstalment ?? 0f, balance);

        /// <summary>Works out the collection and takes the instalment off the loan.</summary>
        public static Collection Take(float balance, float cut, LoanAccount loan) =>
            new Collection(cut, loan?.TakeInstalment() ?? 0f, balance);
    }
}
