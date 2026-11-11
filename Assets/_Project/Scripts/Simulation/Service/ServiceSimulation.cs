using System;
using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Items;
using Game.Simulation.Quality;

namespace Game.Simulation.Service
{
    public enum ServicePhase
    {
        Open,
        /// <summary>The door is closed to new customers; those inside can still be served.</summary>
        LastOrders,
        /// <summary>Time is up, but customers are still inside.</summary>
        Closed,
        /// <summary>Time is up and everyone has gone. Nothing changes after this.</summary>
        Finished,
    }

    /// <summary>
    /// A dinner service at the counter (decision 2026-11-02): customers arrive in waves with a
    /// rush, take a free stool at the pass (or wait by the door, or walk away if the queue is
    /// full), are served on the pass in front of them, eat and leave. Plain C#, advanced by
    /// in-game seconds; gameplay moves the customers and reports when they've sat down or left.
    /// </summary>
    public sealed class ServiceSimulation
    {
        private readonly List<Customer> _customers = new List<Customer>();
        private readonly List<Customer> _queue = new List<Customer>();
        private Customer[] _seats;
        private readonly Random _random;
        // Orders roll on their own generator, so changing the menu doesn't move the arrivals.
        private readonly Random _orderRandom;
        private readonly Random _lookRandom;
        private readonly Random _reviewRandom;
        // Group sizes too: the first numbers of seats a table has don't change who comes when.
        private readonly Random _partyRandom;

        // Each table's seats, in the order they were opened. A stool is a table for one.
        private readonly List<int[]> _tables = new List<int[]>();
        private readonly List<int> _tableSizes = new List<int>();
        private readonly List<Review> _reviews = new List<Review>();
        private float _nextArrivalIn;
        private int _nextId = 1;

        /// <param name="patienceMultiplier">The customer patience realism dial: 2 = twice as patient, 0 = never walk out.</param>
        public ServiceSimulation(ServiceSettings settings, int seatCount, float patienceMultiplier = 1f)
        {
            Settings = settings;
            PatienceMultiplier = patienceMultiplier;
            OpenSeats(Enumerable.Repeat(1, seatCount).ToArray());
            _random = new Random(settings.Seed);
            _orderRandom = new Random(unchecked(settings.Seed * 31 + 17));
            _reviewRandom = new Random(unchecked(settings.Seed * 31 + 29));
            _partyRandom = new Random(unchecked(settings.Seed * 31 + 41));
            // Its own dice, so adding looks changed nobody's order, party or review.
            _lookRandom = new Random(unchecked(settings.Seed * 31 + 53));
            _nextArrivalIn = NextGap(0f);
        }

        public ServiceSettings Settings { get; }

        /// <summary>In-game seconds since the service started.</summary>
        public float Elapsed { get; private set; }

        /// <summary>True once the service time is up.</summary>
        public bool IsClosed => Elapsed >= Settings.DurationSeconds;

        public ServicePhase Phase
        {
            get
            {
                if (IsClosed)
                    return EveryoneHasGone() ? ServicePhase.Finished : ServicePhase.Closed;
                return Elapsed >= LastOrdersAt ? ServicePhase.LastOrders : ServicePhase.Open;
            }
        }

        private float LastOrdersAt => Settings.DurationSeconds - Settings.LastOrdersSeconds;

        /// <summary>Every customer this service, including those who have gone.</summary>
        public IReadOnlyList<Customer> Customers => _customers;

        public IReadOnlyList<Customer> Queue => _queue;

        public int SeatCount => _seats.Length;

        /// <summary>Who is sitting at (or walking to) <paramref name="seat"/>, or null.</summary>
        public Customer AtSeat(int seat) => seat >= 0 && seat < _seats.Length ? _seats[seat] : null;

        /// <summary>Customers who left unserved: turned away at a full door, or out of patience.</summary>
        public int LostCovers { get; private set; }

        /// <summary>The part of <see cref="LostCovers"/> who ran out of patience.</summary>
        public int WalkOuts { get; private set; }

        /// <summary>The customer patience realism dial; read every tick, so it can change mid-service.</summary>
        public float PatienceMultiplier { get; set; }

        /// <summary>
        /// Whether new customers first walk in along the street (decision 2026-11-11): they appear
        /// <see cref="CustomerState.Approaching"/>, and only at the door (<see cref="ReachDoor"/>)
        /// are they seated, queued or turned away, and does their patience start. Off, they're at
        /// the door the moment they're due. Gameplay turns it on where there's a street to walk.
        /// </summary>
        public bool GuestsWalkIn { get; set; }

        /// <summary>
        /// The countdown before service (decision 2026-11-11): the doors are shut. The service clock
        /// stands still and nobody is admitted; with <see cref="GuestsWalkIn"/>, the first party walks
        /// up the street on the first tick and waits on the doorstep. <see cref="OpenDoors"/> lets them in.
        /// </summary>
        public bool DoorsHeld { get; private set; }

        /// <summary>Shuts the doors for the countdown. Only before the first customer.</summary>
        public void HoldDoors()
        {
            if (_customers.Count > 0)
                throw new InvalidOperationException("The doors are held before the first customer.");
            DoorsHeld = true;
        }

        /// <summary>The countdown is over: anyone waiting on the doorstep comes in, and the clock starts.</summary>
        public void OpenDoors()
        {
            if (!DoorsHeld)
                return;
            DoorsHeld = false;
            foreach (var customer in _customers.ToArray())
            {
                // Each party once, when its first member is reached; only those all at the door.
                if (customer.State != CustomerState.Approaching || customer.Party[0] != customer)
                    continue;
                if (customer.Party.All(member => member.AtDoor))
                    Admit(customer.Party);
            }
        }

        /// <summary>Average stars of the customers served so far (0–5), or null before the first.</summary>
        public float? AverageStars
        {
            get
            {
                var total = 0f;
                var count = 0;
                foreach (var customer in _customers)
                {
                    if (customer.Satisfaction == null)
                        continue;
                    total += customer.RatedStars ?? 0f;
                    count++;
                }
                return count > 0 ? total / count : (float?)null;
            }
        }

        /// <summary>Money this service: payments and tips, less walk-out penalties. Can go negative.</summary>
        public float Money { get; private set; }

        /// <summary>The tips part of <see cref="Money"/>.</summary>
        public float Tips { get; private set; }

        /// <summary>Money lost to walk-outs so far (already taken off <see cref="Money"/>).</summary>
        public float WalkOutLosses { get; private set; }

        /// <summary>Reviews so far, oldest first. Turned-away customers don't leave one; walk-outs do.</summary>
        public IReadOnlyList<Review> Reviews => _reviews;

        /// <summary>Raised when a customer has finished eating and paid.</summary>
        public event Action<Customer> Paid;

        /// <summary>Raised when a walk-out has cost money.</summary>
        public event Action<Customer> WalkedOut;

        /// <summary>Raised when a customer leaves a review.</summary>
        public event Action<Review> Reviewed;

        /// <summary>
        /// Raised for each customer at the door, after they've been given a seat or a queue spot (or
        /// walked away). With <see cref="GuestsWalkIn"/>, after <see cref="Appeared"/>.
        /// </summary>
        public event Action<Customer> Arrived;

        /// <summary>Raised for each new customer setting off up the street, with <see cref="GuestsWalkIn"/> on.</summary>
        public event Action<Customer> Appeared;

        public void Tick(float seconds)
        {
            if (seconds <= 0f || Phase == ServicePhase.Finished)
                return;
            if (DoorsHeld)
            {
                // The first party sets off at once, to be waiting when the doors open; the next
                // comes a usual gap after opening. Nobody inside yet, so nothing else moves.
                if (GuestsWalkIn && _customers.Count == 0)
                    Arrive();
                return;
            }

            foreach (var customer in _customers)
            {
                if (customer.State == CustomerState.Gone || customer.State == CustomerState.Approaching)
                    continue; // on the street, they aren't in yet: no time inside, no patience spent
                customer.SecondsInRestaurant += seconds;
                if (customer.State == CustomerState.Waiting || customer.State == CustomerState.Queueing)
                    DrainPatience(customer, seconds);
                else if (customer.State == CustomerState.Eating)
                {
                    customer.EatingLeft -= seconds;
                    if (customer.EatingLeft <= 0f)
                    {
                        Settle(customer);
                        customer.State = CustomerState.DoneEating;

                        // They wait for each other, and leave together once the last has finished.
                        if (customer.Party.All(member => member.State == CustomerState.DoneEating))
                            LeaveTogether(customer.Party);
                    }
                }
            }

            // Arrivals, possibly several in one long tick, until last orders.
            var remaining = seconds;
            while (remaining >= _nextArrivalIn && Elapsed + _nextArrivalIn < LastOrdersAt)
            {
                remaining -= _nextArrivalIn;
                Elapsed += _nextArrivalIn;
                Arrive();
                _nextArrivalIn = NextGap(Elapsed);
            }
            _nextArrivalIn -= remaining;
            Elapsed += remaining;
        }

        /// <summary>
        /// Sets what customers order, as the doors open: prices can change right up to then. The
        /// first arrival is timed again for the menu's demand.
        /// </summary>
        public void SetMenu(OrderMenu menu)
        {
            if (_customers.Count > 0)
                throw new InvalidOperationException("The menu is set before the first customer.");
            Settings.Menu = menu;
            _nextArrivalIn = NextGap(Elapsed);
        }

        /// <summary>Gameplay: the customer has reached their stool.</summary>
        public void SitDown(Customer customer)
        {
            if (customer.State == CustomerState.WalkingToSeat)
                customer.State = CustomerState.Waiting;
        }

        /// <summary>
        /// Gameplay: the furniture leaves no way to their seat, so they give up. A lost cover, and the
        /// seat goes to whoever's next. It costs what they'd have paid, not a walk-out's penalty.
        /// </summary>
        public void GiveUp(Customer customer)
        {
            if (customer.State != CustomerState.WalkingToSeat)
                return;
            foreach (var member in customer.Party)
            {
                if (!IsUnserved(member))
                    continue;
                member.WalkedOut = true;
                LostCovers++;
            }
            LeaveTogether(customer.Party);
        }

        /// <summary>
        /// Sets today's seating, before anyone's arrived: how many chairs each table has, stools
        /// being tables for one. Seats are numbered through the tables in this order.
        /// </summary>
        public void OpenSeats(IReadOnlyList<int> tables)
        {
            if (_customers.Count > 0)
                throw new InvalidOperationException("Seats are set before the first customer.");

            _tables.Clear();
            _tableSizes.Clear();
            var next = 0;
            foreach (var chairs in tables)
            {
                _tables.Add(Enumerable.Range(next, chairs).ToArray());
                _tableSizes.Add(chairs);
                next += chairs;
            }
            _seats = new Customer[next];
        }

        /// <summary>
        /// Gameplay: an approaching customer has reached the door. Once the whole party has, they're
        /// seated, queued or turned away, as anyone due at the door is.
        /// </summary>
        public void ReachDoor(Customer customer)
        {
            if (customer.State != CustomerState.Approaching)
                return;
            customer.AtDoor = true;
            if (DoorsHeld)
                return; // waits on the doorstep until OpenDoors
            foreach (var member in customer.Party)
                if (!member.AtDoor)
                    return;
            Admit(customer.Party);
        }

        /// <summary>Gameplay: the customer has walked out of the door.</summary>
        public void Left(Customer customer)
        {
            if (customer.State == CustomerState.Leaving)
                customer.State = CustomerState.Gone;
        }

        /// <summary>
        /// A plate with food was put on the pass in front of <paramref name="seat"/>. If someone is
        /// sitting there waiting, they take it and start eating.
        /// </summary>
        /// <param name="score">The dish's score against their order, kept as their satisfaction.</param>
        public bool Serve(int seat, Plate plate, DishScore score = null)
        {
            var customer = seat >= 0 && seat < _seats.Length ? _seats[seat] : null;
            if (customer == null || customer.State != CustomerState.Waiting || plate == null || plate.Items.Count == 0)
                return false;

            customer.ServedPlate = plate;
            customer.Satisfaction = score;
            customer.PatienceWhenServed = customer.Patience;
            customer.EatingLeft = Settings.EatSeconds;
            customer.State = CustomerState.Eating;
            return true;
        }

        private void Arrive()
        {
            var size = PartyMix.PickSize(_tableSizes, _partyRandom);
            var party = new Customer[size];
            for (var i = 0; i < size; i++)
            {
                party[i] = new Customer(_nextId++) { Order = Settings.Menu?.Pick(_orderRandom), Party = party, ArrivedAt = Elapsed };
                party[i].Look = Settings.CustomerLooks > 0 ? _lookRandom.Next(Settings.CustomerLooks) : -1;
                _customers.Add(party[i]);
            }

            if (GuestsWalkIn)
            {
                // Up the street first; at the door they're seated, queued or turned away (ReachDoor).
                foreach (var member in party)
                    member.State = CustomerState.Approaching;
                foreach (var member in party)
                    Appeared?.Invoke(member);
                return;
            }
            Admit(party);
        }

        // At the door: a free table, a place in the queue, or away again.
        private void Admit(IReadOnlyList<Customer> party)
        {
            var size = party.Count;
            foreach (var member in party)
                member.ArrivedAt = Elapsed;

            if (TrySeat(party))
            {
                // Seated, and on their way.
            }
            else if (Settings.MaxQueue > 0 && (_queue.Count == 0 || _queue.Count + size <= Settings.MaxQueue))
            {
                // A big group can always wait if nobody else is, or it could never come in at all.
                foreach (var member in party)
                {
                    member.State = CustomerState.Queueing;
                    _queue.Add(member);
                }
            }
            else
            {
                foreach (var member in party)
                {
                    member.WalkedOut = true;
                    member.State = CustomerState.Leaving;
                    LostCovers++;
                }
            }

            foreach (var member in party)
                Arrived?.Invoke(member);
        }

        // The smallest free table that fits them: groups don't share, and someone alone doesn't take
        // a big table while a small one's free.
        private bool TrySeat(IReadOnlyList<Customer> party)
        {
            int[] best = null;
            foreach (var table in _tables)
            {
                if (table.Length < party.Count || (best != null && table.Length >= best.Length))
                    continue;
                if (Array.TrueForAll(table, seat => _seats[seat] == null))
                    best = table;
            }
            if (best == null)
                return false;
            for (var i = 0; i < party.Count; i++)
                Seat(party[i], best[i]);
            return true;
        }

        // A table's come free: the longest-waiting group that fits gets it, so a big group at the
        // front doesn't keep someone alone from an empty stool.
        private void SeatFromQueue()
        {
            for (var i = 0; i < _queue.Count;)
            {
                var party = _queue[i].Party;
                if (TrySeat(party))
                {
                    foreach (var member in party)
                        _queue.Remove(member);
                    i = 0;
                }
                else
                {
                    i += party.Count;
                }
            }
        }

        private static bool IsUnserved(Customer customer) =>
            customer.State == CustomerState.Waiting || customer.State == CustomerState.Queueing || customer.State == CustomerState.WalkingToSeat;

        private void Seat(Customer customer, int seat)
        {
            _seats[seat] = customer;
            customer.Seat = seat;
            customer.State = CustomerState.WalkingToSeat;
        }

        // Linear drain: a seated customer goes from full to empty over PatienceSeconds × the dial;
        // queueing drains at QueuePatienceRate of that. Walking to the stool doesn't count. A group
        // shares one meter: it drains once, through the first of them waiting, and if it runs out
        // they all go. Anyone already eating pays for what they've had.
        private void DrainPatience(Customer customer, float seconds)
        {
            if (Settings.PatienceSeconds <= 0f)
                return;
            // A dial of 0 means endless patience, but only while open: after closing time they
            // lose it at the normal rate, so a service can always finish.
            if (PatienceMultiplier <= 0f && !IsClosed)
                return;
            if (customer.Party.First(IsDraining) != customer)
                return;

            var dial = PatienceMultiplier > 0f ? PatienceMultiplier : 1f;
            var rate = customer.State == CustomerState.Queueing ? Settings.QueuePatienceRate : 1f;
            var left = Math.Max(0f, customer.Patience - rate * seconds / (Settings.PatienceSeconds * dial));
            foreach (var member in customer.Party)
            {
                if (IsUnserved(member))
                    member.Patience = left;
            }
            if (left > 0f)
                return;

            foreach (var member in customer.Party)
            {
                if (member.State == CustomerState.Eating)
                {
                    Settle(member);
                    continue;
                }
                if (!IsUnserved(member))
                    continue;

                member.WalkedOut = true;
                member.RanOutOfPatience = true;
                LostCovers++;
                WalkOuts++;
                _queue.Remove(member);

                member.WalkOutPenalty = Billing.WalkOutPenalty(member.Order, Settings.Payment);
                Money -= member.WalkOutPenalty;
                WalkOutLosses += member.WalkOutPenalty;
                WalkedOut?.Invoke(member);

                LeaveReview(member);
            }
            LeaveTogether(customer.Party);
        }

        private static bool IsDraining(Customer customer) =>
            customer.State == CustomerState.Waiting || customer.State == CustomerState.Queueing;

        // Paying and reviewing happen when they finish, as they get up to leave.
        private void Settle(Customer customer)
        {
            var bill = Billing.Calculate(customer.Order, customer.Satisfaction, customer.PatienceWhenServed ?? 1f, Settings.Payment);
            customer.Bill = bill;
            Money += bill.Total;
            Tips += bill.Tip;
            Paid?.Invoke(customer);
            if (customer.Satisfaction != null)
                LeaveReview(customer);
        }

        // Not everyone posts: Review stays null for a guest who keeps their night to themselves.
        private void LeaveReview(Customer customer)
        {
            var review = ReviewWriter.Write(customer, Settings.Reviews, _reviewRandom);
            if (review == null)
                return;
            customer.Review = review;
            _reviews.Add(review);
            Reviewed?.Invoke(review);
        }

        // They all get up, and their table goes to whoever's next.
        private void LeaveTogether(IReadOnlyList<Customer> party)
        {
            foreach (var member in party)
            {
                member.State = CustomerState.Leaving;
                if (member.Seat >= 0 && _seats[member.Seat] == member)
                    _seats[member.Seat] = null;
                _queue.Remove(member);
            }
            SeatFromQueue();
        }

        private bool EveryoneHasGone()
        {
            foreach (var customer in _customers)
            {
                if (customer.State != CustomerState.Gone)
                    return false;
            }
            return true;
        }

        // Gap to the next arrival: the base interval (shorter during the rush) with random jitter.
        private float NextGap(float at)
        {
            var fraction = Settings.DurationSeconds > 0f ? at / Settings.DurationSeconds : 0f;
            // Prices shift how many come (decision 2026-11-10): twice the demand, half the gap.
            var interval = Settings.ArrivalIntervalSeconds / Math.Max(0.01f, Settings.Menu?.Demand ?? 1f);
            if (fraction >= Settings.RushStart && fraction < Settings.RushEnd)
                interval /= Math.Max(0.01f, Settings.RushMultiplier);
            var jitter = 1f + Settings.ArrivalJitter * (float)(_random.NextDouble() * 2.0 - 1.0);
            return Math.Max(1f, interval * jitter);
        }
    }
}
