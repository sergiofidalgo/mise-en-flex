namespace Game.Simulation.Service
{
    /// <summary>
    /// Tuning for a service, in in-game seconds (60x: one real second is one in-game minute).
    /// Filled from the ServiceConfig asset.
    /// </summary>
    public sealed class ServiceSettings
    {
        /// <summary>How long the service runs, in-game seconds (5 h ≈ 5 real minutes).</summary>
        public float DurationSeconds { get; set; } = 5f * 3600f;

        /// <summary>In-game seconds before the end when the door closes to new customers.</summary>
        public float LastOrdersSeconds { get; set; } = 30f * 60f;

        /// <summary>Average in-game seconds between arrivals outside the rush.</summary>
        public float ArrivalIntervalSeconds { get; set; } = 25f * 60f;

        /// <summary>±fraction of random jitter on each gap between arrivals.</summary>
        public float ArrivalJitter { get; set; } = 0.35f;

        /// <summary>Start and end of the rush, as fractions of the service.</summary>
        public float RushStart { get; set; } = 0.35f;
        public float RushEnd { get; set; } = 0.6f;

        /// <summary>How many times more often customers arrive during the rush.</summary>
        public float RushMultiplier { get; set; } = 2f;

        /// <summary>
        /// The clock the service runs on: when it opens and how long an hour is (the user's call,
        /// 2026-11-11: an hour passes in 30 real seconds). The default is the old clock, 18:00 and
        /// one in-game second to the second.
        /// </summary>
        public ServiceClock Clock { get; set; } = new ServiceClock();

        /// <summary>
        /// The hours the rushes peak at, on <see cref="Clock"/> (opening times, 2026-11-11: 14:00 and
        /// 21:00). Empty or null: the old single rush from <see cref="RushStart"/> to <see cref="RushEnd"/>.
        /// </summary>
        public float[] RushHours { get; set; }

        /// <summary>A rush lasts this many clock hours either side of its peak.</summary>
        public float RushHalfWidthHours { get; set; } = 1f;

        /// <summary>The average gap to the next arrival at <paramref name="at"/> in-game seconds into the service.</summary>
        public float ArrivalInterval(float at)
        {
            // Prices shift how many come (decision 2026-11-10): twice the demand, half the gap.
            var interval = ArrivalIntervalSeconds / System.Math.Max(0.01f, Menu?.Demand ?? 1f);
            if (InRush(at))
                interval /= System.Math.Max(0.01f, RushMultiplier);
            return interval;
        }

        /// <summary>Whether <paramref name="at"/> in-game seconds into the service falls in a rush.</summary>
        public bool InRush(float at)
        {
            if (RushHours != null && RushHours.Length > 0)
            {
                var hour = (Clock ?? new ServiceClock()).HourAt(at);
                foreach (var peak in RushHours)
                {
                    if (hour >= peak - RushHalfWidthHours && hour < peak + RushHalfWidthHours)
                        return true;
                }
                return false;
            }
            var fraction = DurationSeconds > 0f ? at / DurationSeconds : 0f;
            return fraction >= RushStart && fraction < RushEnd;
        }

        /// <summary>In-game seconds a customer spends eating.</summary>
        public float EatSeconds { get; set; } = 15f * 60f;

        /// <summary>
        /// In-game seconds a seated customer waits for their food before walking out, at a
        /// patience dial of 1. Queueing by the door drains it more slowly.
        /// </summary>
        public float PatienceSeconds { get; set; } = 35f * 60f;

        /// <summary>How fast patience drains while queueing for a stool, relative to seated (0..1).</summary>
        public float QueuePatienceRate { get; set; } = 0.5f;

        /// <summary>Customers who can wait by the door for a seat; more than that walk away.</summary>
        public int MaxQueue { get; set; } = 3;

        /// <summary>
        /// How many looks customers come in (the customer prefab's 3D looks); each customer gets one,
        /// so the review card can show the person who came (user's call, 2026-11-11). 0 for none.
        /// </summary>
        public int CustomerLooks { get; set; }

        /// <summary>Random seed, so a service plays the same way each time while tuning.</summary>
        public int Seed { get; set; } = 1;

        /// <summary>What customers order; null for no orders (any food will do).</summary>
        public OrderMenu Menu { get; set; }

        public PaymentSettings Payment { get; set; } = new PaymentSettings();

        public ReviewSettings Reviews { get; set; } = new ReviewSettings();

        /// <summary>Delivery orders (decision 2026-11-12). They only come once a delivery counter gives them a place.</summary>
        public DeliverySettings Delivery { get; set; } = new DeliverySettings();
    }
}
