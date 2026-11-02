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

        /// <summary>Average in-game seconds between arrivals outside the rush.</summary>
        public float ArrivalIntervalSeconds { get; set; } = 25f * 60f;

        /// <summary>±fraction of random jitter on each gap between arrivals.</summary>
        public float ArrivalJitter { get; set; } = 0.35f;

        /// <summary>Start and end of the rush, as fractions of the service.</summary>
        public float RushStart { get; set; } = 0.35f;
        public float RushEnd { get; set; } = 0.6f;

        /// <summary>How many times more often customers arrive during the rush.</summary>
        public float RushMultiplier { get; set; } = 2f;

        /// <summary>In-game seconds a customer spends eating.</summary>
        public float EatSeconds { get; set; } = 15f * 60f;

        /// <summary>Customers who can wait by the door for a seat; more than that walk away.</summary>
        public int MaxQueue { get; set; } = 3;

        /// <summary>Random seed, so a service plays the same way each time while tuning.</summary>
        public int Seed { get; set; } = 1;

        /// <summary>What customers order; null for no orders (any food will do).</summary>
        public OrderMenu Menu { get; set; }
    }
}
