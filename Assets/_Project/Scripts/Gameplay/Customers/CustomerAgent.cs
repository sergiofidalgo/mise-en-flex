using Game.Simulation.Service;

namespace Game.Gameplay.Customers
{
    /// <summary>A customer's body in the scene. Moved by the <see cref="CustomerDirector"/>.</summary>
    public sealed class CustomerAgent : WalkingAgent
    {
        public Customer Customer { get; private set; }

        public void Initialize(Customer customer) => Customer = customer;
    }
}
