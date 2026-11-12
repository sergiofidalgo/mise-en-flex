using Game.Simulation.Items;
using Game.Simulation.Quality;

namespace Game.Simulation.Service
{
    public enum CustomerState
    {
        /// <summary>Walking to their seat (gameplay moves them and calls SitDown).</summary>
        WalkingToSeat,
        /// <summary>Waiting by the door for a seat to free up.</summary>
        Queueing,
        /// <summary>Seated, waiting to be served.</summary>
        Waiting,
        Eating,
        /// <summary>Finished and paid, still at the table until their group has finished too.</summary>
        DoneEating,
        /// <summary>Walking out (gameplay moves them and calls Left).</summary>
        Leaving,
        Gone,
        /// <summary>
        /// Walking up the street to the door, not in yet (ServiceSimulation.GuestsWalkIn; gameplay
        /// moves them and calls ReachDoor). Patience hasn't started.
        /// </summary>
        Approaching,
    }

    /// <summary>One customer's visit. States change through <see cref="ServiceSimulation"/>.</summary>
    public sealed class Customer
    {
        internal Customer(int id)
        {
            Id = id;
        }

        public int Id { get; }

        /// <summary>Which of the customer looks they wear, or -1 when there are none; their review card shows it.</summary>
        public int Look { get; internal set; } = -1;

        /// <summary>Service seconds when they arrived at the door, for the guests chart.</summary>
        public float ArrivedAt { get; internal set; }

        /// <summary>While approaching: they've reached the door, and wait there for the rest of their party.</summary>
        public bool AtDoor { get; internal set; }

        public CustomerState State { get; internal set; }

        /// <summary>Who they came with, themselves included. They sit together and leave together.</summary>
        public System.Collections.Generic.IReadOnlyList<Customer> Party { get; internal set; }

        /// <summary>The seat they were given, or -1 while queueing.</summary>
        public int Seat { get; internal set; } = -1;

        /// <summary>In-game seconds since they arrived.</summary>
        public float SecondsInRestaurant { get; internal set; }

        /// <summary>In-game seconds of eating left.</summary>
        public float EatingLeft { get; internal set; }

        /// <summary>What they want; null if the service has no menu.</summary>
        public Order Order { get; internal set; }

        /// <summary>The plate they were served, while eating and after.</summary>
        public Plate ServedPlate { get; internal set; }

        /// <summary>
        /// How happy they were with their dish: its score when served, against their order. Null
        /// until served. Patience and the wait join it in later milestones.
        /// </summary>
        public DishScore Satisfaction { get; internal set; }

        /// <summary>
        /// The stars they give their dish: its own stars, moved by what they expected for the price
        /// (<see cref="Service.Order.Expectation"/>), in half stars from 0 to 5. Null until served.
        /// </summary>
        public float? RatedStars => Satisfaction == null ? (float?)null : Rate(Satisfaction.Overall, Order);

        /// <summary>
        /// The stars they'd give the dish without the bin's smell or an open kitchen (decisions
        /// 2026-11-11): how good the food and the table were. Null until served.
        /// </summary>
        public float? StarsBeforeSurroundings => Satisfaction == null ? (float?)null : Rate(Satisfaction.OverallBeforeSurroundings, Order);

        /// <summary>A 0..1 score as stars, moved by what the guest expected for the price, in half stars.</summary>
        public static float Rate(float overall, Order order)
        {
            var shift = order?.Expectation ?? 0f;
            if (shift == 0f)
                return (float)System.Math.Round(overall * 10f, System.MidpointRounding.AwayFromZero) / 2f; // as DishScore.Stars
            var stars = (float)System.Math.Round((overall * 5f + shift) * 2f, System.MidpointRounding.AwayFromZero) / 2f;
            return System.Math.Clamp(stars, 0f, 5f);
        }

        /// <summary>What they paid when they finished eating, or null if they haven't (or never will).</summary>
        public Bill? Bill { get; internal set; }

        /// <summary>Money the restaurant lost because they walked out; 0 otherwise.</summary>
        public float WalkOutPenalty { get; internal set; }

        /// <summary>Their review, once they've finished or walked out. Null otherwise.</summary>
        public Review Review { get; internal set; }

        /// <summary>True if they left without being served (no room by the door, or out of patience).</summary>
        public bool WalkedOut { get; internal set; }

        /// <summary>True if they walked out because they waited too long.</summary>
        public bool RanOutOfPatience { get; internal set; }

        /// <summary>
        /// Patience left, 1 (just arrived) to 0 (walks out). Drains while queueing and while
        /// seated waiting for food; stops once they're served.
        /// </summary>
        public float Patience { get; internal set; } = 1f;

        /// <summary>
        /// They ordered through the delivery app (decision 2026-11-12): never in the room, their food
        /// went with a courier and their review says so.
        /// </summary>
        public bool OrderedDelivery { get; internal set; }

        /// <summary>Patience left when their food arrived, or null if not served. Feeds tips in milestone 4.</summary>
        public float? PatienceWhenServed { get; internal set; }
    }
}
