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
        /// <summary>Walking out (gameplay moves them and calls Left).</summary>
        Leaving,
        Gone,
    }

    /// <summary>One customer's visit. States change through <see cref="ServiceSimulation"/>.</summary>
    public sealed class Customer
    {
        internal Customer(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public CustomerState State { get; internal set; }

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

        /// <summary>True if they left without being served (no seat, or later, out of patience).</summary>
        public bool WalkedOut { get; internal set; }
    }
}
