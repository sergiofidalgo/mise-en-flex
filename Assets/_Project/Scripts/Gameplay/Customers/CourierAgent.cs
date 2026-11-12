using Game.Simulation.Service;

namespace Game.Gameplay.Customers
{
    /// <summary>A delivery courier on foot. Moved by the <see cref="CourierDirector"/>.</summary>
    public sealed class CourierAgent : WalkingAgent
    {
        public DeliveryOrder Order { get; private set; }

        public void Initialize(DeliveryOrder order) => Order = order;
    }
}
