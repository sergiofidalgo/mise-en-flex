using System;
using System.Collections.Generic;
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
        private readonly Random _reviewRandom;
        private readonly List<Review> _reviews = new List<Review>();
        private float _nextArrivalIn;
        private int _nextId = 1;

        /// <param name="patienceMultiplier">The customer patience realism dial: 2 = twice as patient, 0 = never walk out.</param>
        public ServiceSimulation(ServiceSettings settings, int seatCount, float patienceMultiplier = 1f)
        {
            Settings = settings;
            PatienceMultiplier = patienceMultiplier;
            _seats = new Customer[seatCount];
            _random = new Random(settings.Seed);
            _orderRandom = new Random(unchecked(settings.Seed * 31 + 17));
            _reviewRandom = new Random(unchecked(settings.Seed * 31 + 29));
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
        public Customer AtSeat(int seat) => _seats[seat];

        /// <summary>Customers who left unserved: turned away at a full door, or out of patience.</summary>
        public int LostCovers { get; private set; }

        /// <summary>The part of <see cref="LostCovers"/> who ran out of patience.</summary>
        public int WalkOuts { get; private set; }

        /// <summary>The customer patience realism dial; read every tick, so it can change mid-service.</summary>
        public float PatienceMultiplier { get; set; }

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
                    total += customer.Satisfaction.Stars;
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

        /// <summary>Raised for each new customer, after they've been given a seat or a queue spot (or walked away).</summary>
        public event Action<Customer> Arrived;

        public void Tick(float seconds)
        {
            if (seconds <= 0f || Phase == ServicePhase.Finished)
                return;

            foreach (var customer in _customers)
            {
                if (customer.State == CustomerState.Gone)
                    continue;
                customer.SecondsInRestaurant += seconds;
                if (customer.State == CustomerState.Waiting || customer.State == CustomerState.Queueing)
                    DrainPatience(customer, seconds);
                else if (customer.State == CustomerState.Eating)
                {
                    customer.EatingLeft -= seconds;
                    if (customer.EatingLeft <= 0f)
                    {
                        Settle(customer);
                        StartLeaving(customer);
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
            customer.WalkedOut = true;
            LostCovers++;
            StartLeaving(customer);
        }

        /// <summary>Sets how many seats today's service has, before anyone's arrived: tables bought count.</summary>
        public void OpenSeats(int count)
        {
            if (_customers.Count > 0)
                throw new InvalidOperationException("Seats are set before the first customer.");
            _seats = new Customer[count];
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
            var customer = new Customer(_nextId++) { Order = Settings.Menu?.Pick(_orderRandom) };
            _customers.Add(customer);

            var seat = Array.IndexOf(_seats, null);
            if (seat >= 0)
            {
                Seat(customer, seat);
            }
            else if (_queue.Count < Settings.MaxQueue)
            {
                customer.State = CustomerState.Queueing;
                _queue.Add(customer);
            }
            else
            {
                customer.WalkedOut = true;
                customer.State = CustomerState.Leaving;
                LostCovers++;
            }

            Arrived?.Invoke(customer);
        }

        private void Seat(Customer customer, int seat)
        {
            _seats[seat] = customer;
            customer.Seat = seat;
            customer.State = CustomerState.WalkingToSeat;
        }

        // Linear drain: a seated customer goes from full to empty over PatienceSeconds × the dial;
        // queueing drains at QueuePatienceRate of that. Walking to the stool doesn't count.
        private void DrainPatience(Customer customer, float seconds)
        {
            if (Settings.PatienceSeconds <= 0f)
                return;
            // A dial of 0 means endless patience, but only while open: after closing time they
            // lose it at the normal rate, so a service can always finish.
            if (PatienceMultiplier <= 0f && !IsClosed)
                return;
            var dial = PatienceMultiplier > 0f ? PatienceMultiplier : 1f;
            var rate = customer.State == CustomerState.Queueing ? Settings.QueuePatienceRate : 1f;
            customer.Patience = Math.Max(0f, customer.Patience - rate * seconds / (Settings.PatienceSeconds * dial));
            if (customer.Patience > 0f)
                return;

            customer.WalkedOut = true;
            customer.RanOutOfPatience = true;
            LostCovers++;
            WalkOuts++;
            _queue.Remove(customer);

            customer.WalkOutPenalty = Billing.WalkOutPenalty(customer.Order, Settings.Payment);
            Money -= customer.WalkOutPenalty;
            WalkOutLosses += customer.WalkOutPenalty;
            WalkedOut?.Invoke(customer);

            LeaveReview(customer);
            StartLeaving(customer);
        }

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

        private void LeaveReview(Customer customer)
        {
            var review = ReviewWriter.Write(customer, Settings.Reviews, _reviewRandom);
            customer.Review = review;
            _reviews.Add(review);
            Reviewed?.Invoke(review);
        }

        private void StartLeaving(Customer customer)
        {
            customer.State = CustomerState.Leaving;
            if (customer.Seat >= 0 && _seats[customer.Seat] == customer)
            {
                var seat = customer.Seat;
                _seats[seat] = null;
                // The longest-waiting customer by the door takes the free stool.
                if (_queue.Count > 0)
                {
                    var next = _queue[0];
                    _queue.RemoveAt(0);
                    Seat(next, seat);
                }
            }
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
            var interval = Settings.ArrivalIntervalSeconds;
            if (fraction >= Settings.RushStart && fraction < Settings.RushEnd)
                interval /= Math.Max(0.01f, Settings.RushMultiplier);
            var jitter = 1f + Settings.ArrivalJitter * (float)(_random.NextDouble() * 2.0 - 1.0);
            return Math.Max(1f, interval * jitter);
        }
    }
}
