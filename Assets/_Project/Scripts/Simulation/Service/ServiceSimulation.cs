using System;
using System.Collections.Generic;
using Game.Simulation.Items;

namespace Game.Simulation.Service
{
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
        private readonly Customer[] _seats;
        private readonly Random _random;
        // Orders roll on their own generator, so changing the menu doesn't move the arrivals.
        private readonly Random _orderRandom;
        private float _nextArrivalIn;
        private int _nextId = 1;

        public ServiceSimulation(ServiceSettings settings, int seatCount)
        {
            Settings = settings;
            _seats = new Customer[seatCount];
            _random = new Random(settings.Seed);
            _orderRandom = new Random(unchecked(settings.Seed * 31 + 17));
            _nextArrivalIn = NextGap(0f);
        }

        public ServiceSettings Settings { get; }

        /// <summary>In-game seconds since the service started.</summary>
        public float Elapsed { get; private set; }

        /// <summary>True once the service time is up; nobody new arrives after that.</summary>
        public bool IsClosed => Elapsed >= Settings.DurationSeconds;

        /// <summary>Every customer this service, including those who have gone.</summary>
        public IReadOnlyList<Customer> Customers => _customers;

        public IReadOnlyList<Customer> Queue => _queue;

        public int SeatCount => _seats.Length;

        /// <summary>Who is sitting at (or walking to) <paramref name="seat"/>, or null.</summary>
        public Customer AtSeat(int seat) => _seats[seat];

        public int LostCovers { get; private set; }

        /// <summary>Raised for each new customer, after they've been given a seat or a queue spot (or walked away).</summary>
        public event Action<Customer> Arrived;

        public void Tick(float seconds)
        {
            if (seconds <= 0f)
                return;

            foreach (var customer in _customers)
            {
                if (customer.State == CustomerState.Gone)
                    continue;
                customer.SecondsInRestaurant += seconds;
                if (customer.State == CustomerState.Eating)
                {
                    customer.EatingLeft -= seconds;
                    if (customer.EatingLeft <= 0f)
                        StartLeaving(customer);
                }
            }

            // Arrivals: possibly several in one long tick.
            var remaining = seconds;
            while (!IsClosed && remaining >= _nextArrivalIn)
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
        public bool Serve(int seat, Plate plate)
        {
            var customer = seat >= 0 && seat < _seats.Length ? _seats[seat] : null;
            if (customer == null || customer.State != CustomerState.Waiting || plate == null || plate.Items.Count == 0)
                return false;

            customer.ServedPlate = plate;
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
