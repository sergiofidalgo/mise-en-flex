using System.Collections.Generic;

namespace Game.Simulation.Days
{
    /// <summary>
    /// Tonight's money as a receipt (the user's call, 2026-11-10): what came in, what went out, and
    /// the profit or loss. The boss's collection and loan instalment aren't tonight's running costs,
    /// so they stay off it: the boss card shows them.
    /// </summary>
    public sealed class NightReceipt
    {
        private static readonly LedgerLine[] InOrder = { LedgerLine.Payments, LedgerLine.Sales, LedgerLine.Tips, LedgerLine.Repossessed };
        private static readonly LedgerLine[] OutOrder = { LedgerLine.Ingredients, LedgerLine.Wages, LedgerLine.Energy, LedgerLine.Purchases, LedgerLine.WalkOuts };

        private NightReceipt(List<(LedgerLine, float)> ins, List<(LedgerLine, float)> outs)
        {
            In = ins;
            Out = outs;
            var profit = 0f;
            foreach (var (_, amount) in ins)
                profit += amount;
            foreach (var (_, amount) in outs)
                profit -= amount;
            Profit = profit;
        }

        /// <summary>Money in, in a fixed order; meals always listed, the rest only when not zero.</summary>
        public IReadOnlyList<(LedgerLine Line, float Amount)> In { get; }

        /// <summary>Money out, in a fixed order, only lines that aren't zero.</summary>
        public IReadOnlyList<(LedgerLine Line, float Amount)> Out { get; }

        /// <summary>In less out: negative is a loss.</summary>
        public float Profit { get; }

        public static NightReceipt From(Ledger ledger)
        {
            var ins = new List<(LedgerLine, float)>();
            foreach (var line in InOrder)
                if (line == LedgerLine.Payments || ledger.Of(line) != 0f)
                    ins.Add((line, ledger.Of(line)));
            var outs = new List<(LedgerLine, float)>();
            foreach (var line in OutOrder)
                if (ledger.Of(line) != 0f)
                    outs.Add((line, ledger.Of(line)));
            return new NightReceipt(ins, outs);
        }
    }
}
