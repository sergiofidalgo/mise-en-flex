using Game.Simulation.Cooking;
using System;
using System.Collections.Generic;
using Game.Simulation.Service;

namespace Game.Simulation.Days
{
    /// <summary>Where the day is. Close has no phase of its own: it's summarised in the Review (decision 2026-11-03).</summary>
    public enum DayPhase
    {
        Plan,
        Prep,
        Service,
        Review,
    }

    /// <summary>What closing up found (Close is summarised: decision 2026-11-03).</summary>
    public readonly struct CloseReport
    {
        public CloseReport(int spoiled, int leftOut)
        {
            Spoiled = spoiled;
            LeftOut = leftOut;
        }

        /// <summary>Stored food too old to keep, thrown out.</summary>
        public int Spoiled { get; }

        /// <summary>Food not in storage overnight, still where it was and aged at room temperature.</summary>
        public int LeftOut { get; }
    }

    /// <summary>
    /// The restaurant's days: Plan → Prep → Service → Review, then the next day's Plan. Money
    /// carries from day to day. Saved as Review starts.
    /// </summary>
    public sealed class DayCycle
    {
        public DayCycle(float startingBalance = 0f, FeeSettings fee = null)
        {
            Balance = startingBalance;
            Fee = fee ?? new FeeSettings();
        }

        /// <summary>Carries on from a save: the morning after the day that was played (decision 2026-11-08).</summary>
        /// <param name="daysTowardsFee">Finished days since the last fee.</param>
        /// <param name="period">Money in and out since the last fee.</param>
        public static DayCycle Continuing(int dayPlayed, float balance, FeeSettings fee = null, int daysTowardsFee = 0, Ledger period = null, IEnumerable<DayRecord> history = null)
        {
            var days = new DayCycle(balance, fee) { Day = Math.Max(1, dayPlayed + 1), DaysTowardsFee = daysTowardsFee };
            if (period != null && daysTowardsFee > 0)
                days.Period.Add(period);
            if (history != null)
                days._history.AddRange(history);
            return days;
        }

        private readonly List<DayRecord> _history = new List<DayRecord>();

        public FeeSettings Fee { get; }

        /// <summary>Money in and out today.</summary>
        public Ledger Today { get; } = new Ledger();

        /// <summary>Money in and out since the last fee, today included.</summary>
        public Ledger Period { get; } = new Ledger();

        /// <summary>Finished days since the last fee fell due.</summary>
        public int DaysTowardsFee { get; private set; }

        /// <summary>Finished days before the next fee falls due (it's collected as that day's Review starts).</summary>
        public int DaysUntilFee => Math.Max(1, Fee.EveryDays - DaysTowardsFee);

        /// <summary>True on the Review of a day whose fee has just been collected.</summary>
        public bool FeeCollectedToday { get; private set; }

        /// <summary>What's owed to the boss: money below zero (decision 2026-11-08).</summary>
        public float OwedToBoss => Math.Max(0f, -Balance);

        /// <summary>Every finished day, oldest first.</summary>
        public IReadOnlyList<DayRecord> History => _history;

        public int Day { get; private set; } = 1;

        public DayPhase Phase { get; private set; } = DayPhase.Plan;

        /// <summary>Money across all days so far. Can go negative.</summary>
        public float Balance { get; private set; }

        /// <summary>How the latest service went, for the Review. Null until the first one ends.</summary>
        public ServiceSummary LastService { get; private set; }

        /// <summary>Wages paid for the latest day.</summary>
        public float LastWages { get; private set; }

        /// <summary>Energy paid for the latest day.</summary>
        public EnergyBill LastEnergy { get; private set; }

        /// <summary>What the latest closing up found.</summary>
        public CloseReport LastClose { get; private set; }

        /// <summary>Raised whenever the phase changes.</summary>
        public event Action<DayPhase> PhaseChanged;

        /// <summary>Plan → Prep → Service, or Review → the next day's Plan. A service ends with <see cref="FinishService"/>.</summary>
        public void Advance()
        {
            switch (Phase)
            {
                case DayPhase.Plan:
                    SetPhase(DayPhase.Prep);
                    break;
                case DayPhase.Prep:
                    SetPhase(DayPhase.Service);
                    break;
                case DayPhase.Review:
                    Day++;
                    Today.Clear();
                    if (FeeCollectedToday)
                        Period.Clear();
                    FeeCollectedToday = false;
                    SetPhase(DayPhase.Plan);
                    break;
                default:
                    throw new InvalidOperationException("A service ends with FinishService.");
            }
        }

        /// <summary>The service has finished: banks its money, pays the wages and energy, and moves on to the Review.</summary>
        public void FinishService(ServiceSummary summary, float wages = 0f, CloseReport close = default, EnergyBill energy = default)
        {
            if (Phase != DayPhase.Service)
                throw new InvalidOperationException($"No service to finish during {Phase}.");
            LastService = summary;
            LastWages = wages;
            LastEnergy = energy;
            LastClose = close;
            Book(LedgerLine.Payments, summary.Payments);
            Book(LedgerLine.Tips, summary.Tips);
            Book(LedgerLine.WalkOuts, summary.WalkOutLosses);
            Book(LedgerLine.Wages, wages);
            Book(LedgerLine.Energy, energy.Total);

            // The fee is collected as Review starts, once enough days are finished (decision 2026-11-08).
            DaysTowardsFee++;
            if (DaysTowardsFee >= Fee.EveryDays)
            {
                Book(LedgerLine.Fee, Fee.Amount);
                DaysTowardsFee = 0;
                FeeCollectedToday = true;
            }

            _history.Add(new DayRecord
            {
                Day = Day,
                Served = summary.Served,
                WalkOuts = summary.WalkOuts,
                TurnedAway = summary.TurnedAway,
                Rating = summary.Rating ?? -1f,
                In = Today.In,
                Out = Today.Out,
                Balance = Balance,
            });
            SetPhase(DayPhase.Review);
        }

        /// <summary>Pays for something, if there's enough. Unlike wages, a purchase can't cause debt.</summary>
        /// <returns>False if there isn't enough; nothing is taken.</returns>
        public bool TrySpend(float amount)
        {
            if (amount > Balance)
                return false;
            Book(LedgerLine.Purchases, amount);
            return true;
        }

        /// <summary>Money from outside a service, such as selling furniture.</summary>
        public void Receive(float amount) => Book(LedgerLine.Sales, amount);

        /// <summary>A bill that's paid whatever the balance, such as a crate from a street stall: it can go into debt.</summary>
        public void Charge(LedgerLine line, float amount) => Book(line, amount);

        // Every movement of money goes through here, so the books always add up to the balance.
        private void Book(LedgerLine line, float amount)
        {
            if (amount == 0f)
                return;
            Today.Add(line, amount);
            Period.Add(line, amount);
            Balance += Ledger.IsIncome(line) ? amount : -amount;
        }

        private void SetPhase(DayPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }

    /// <summary>Who has said they're ready to move on (decision 2026-11-03: phases end when all players ready up).</summary>
    public sealed class ReadyCheck
    {
        private readonly HashSet<int> _ready = new HashSet<int>();

        public int ReadyCount => _ready.Count;

        public bool IsReady(int player) => _ready.Contains(player);

        /// <summary>Marks them ready, or takes it back if they already were.</summary>
        public void Toggle(int player)
        {
            if (!_ready.Remove(player))
                _ready.Add(player);
        }

        public void Clear() => _ready.Clear();

        public bool IsEveryoneReady(int playerCount) => playerCount > 0 && _ready.Count >= playerCount;

        /// <summary>
        /// Whether everyone here is ready: only these players count, so one whose controller is
        /// lost doesn't hold the day up (decision 2026-11-09). Nobody here is never ready.
        /// </summary>
        public bool IsEveryoneReady(IReadOnlyCollection<int> present)
        {
            if (present.Count == 0)
                return false;
            foreach (var player in present)
                if (!_ready.Contains(player))
                    return false;
            return true;
        }
    }
}
