using System;
using System.Collections.Generic;

namespace Game.Simulation.Days
{
    /// <summary>Where money came from or went, as the Books tab lists it.</summary>
    public enum LedgerLine
    {
        Payments,
        Tips,
        Sales,
        Ingredients,
        WalkOuts,
        Wages,
        Energy,
        Purchases,
        Fee,
    }

    /// <summary>Money in and out over some stretch of time (a day, or the days since the last fee).</summary>
    public sealed class Ledger
    {
        private static readonly int LineCount = Enum.GetValues(typeof(LedgerLine)).Length;

        private readonly float[] _amounts = new float[LineCount];

        /// <summary>Money in on these lines, out on the rest.</summary>
        public static bool IsIncome(LedgerLine line) => line == LedgerLine.Payments || line == LedgerLine.Tips || line == LedgerLine.Sales;

        public float Of(LedgerLine line) => _amounts[(int)line];

        public float In
        {
            get
            {
                var total = 0f;
                for (var i = 0; i < LineCount; i++)
                    if (IsIncome((LedgerLine)i))
                        total += _amounts[i];
                return total;
            }
        }

        public float Out
        {
            get
            {
                var total = 0f;
                for (var i = 0; i < LineCount; i++)
                    if (!IsIncome((LedgerLine)i))
                        total += _amounts[i];
                return total;
            }
        }

        public float Net => In - Out;

        /// <summary>Adds to a line. Amounts are positive whichever way the money went.</summary>
        public void Add(LedgerLine line, float amount) => _amounts[(int)line] += amount;

        public void Add(Ledger other)
        {
            for (var i = 0; i < LineCount; i++)
                _amounts[i] += other._amounts[i];
        }

        public void Clear() => Array.Clear(_amounts, 0, LineCount);

        /// <summary>For a save: one amount per line, in <see cref="LedgerLine"/> order.</summary>
        public float[] ToArray() => (float[])_amounts.Clone();

        public static Ledger FromArray(IReadOnlyList<float> amounts)
        {
            var ledger = new Ledger();
            if (amounts != null)
                for (var i = 0; i < Math.Min(amounts.Count, LineCount); i++)
                    ledger._amounts[i] = amounts[i];
            return ledger;
        }
    }

    /// <summary>The fee to the boss (decision 2026-11-08: every 5 finished days).</summary>
    public sealed class FeeSettings
    {
        public float Amount { get; set; }

        public int EveryDays { get; set; } = 5;
    }

    /// <summary>How one finished day went, for the restaurant's progress in Books.</summary>
    [Serializable]
    public sealed class DayRecord
    {
        public int Day;
        public int Served;
        public int WalkOuts;
        public int TurnedAway;

        /// <summary>That day's average stars; negative if nobody left a review.</summary>
        public float Rating = -1f;

        public float In;
        public float Out;

        /// <summary>Money at the end of the day, after the fee if one fell due.</summary>
        public float Balance;
    }
}
