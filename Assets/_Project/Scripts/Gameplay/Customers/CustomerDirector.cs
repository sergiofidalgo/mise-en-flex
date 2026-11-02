using System.Collections.Generic;
using Game.Simulation.Service;
using UnityEngine;

namespace Game.Gameplay.Customers
{
    /// <summary>
    /// Puts the service's customers in the scene: a body for each arrival, walking from the door
    /// to their stool (or a queue spot by the door), sitting facing the pass, and walking back out.
    /// Reports to the simulation when they've sat down and when they've left.
    /// </summary>
    public sealed class CustomerDirector : MonoBehaviour
    {
        // Within this distance a customer has reached where they were going.
        private const float ArriveDistance = 0.05f;

        [SerializeField] private KitchenCompositionRoot _root;
        [SerializeField] private CustomerAgent _customerPrefab;
        [SerializeField] private Transform _door;

        [Tooltip("Where each stool's customer stands, in seat order (seat i faces pass counter i).")]
        [SerializeField] private Transform[] _seats = System.Array.Empty<Transform>();

        [Tooltip("Spots by the door where customers wait for a stool, nearest the door first.")]
        [SerializeField] private Transform[] _queueSpots = System.Array.Empty<Transform>();

        private readonly Dictionary<Customer, CustomerAgent> _agents = new Dictionary<Customer, CustomerAgent>();
        private readonly List<Customer> _gone = new List<Customer>();
        private ServiceSimulation _service;

        private void Start()
        {
            _service = _root.Services.Service;
            _service.Arrived += HandleArrived;
        }

        private void OnDestroy()
        {
            if (_service != null)
                _service.Arrived -= HandleArrived;
        }

        private void HandleArrived(Customer customer)
        {
            var agent = Instantiate(_customerPrefab, _door.position, _door.rotation);
            agent.name = $"Customer_{customer.Id}";
            agent.Initialize(customer);
            _agents[customer] = agent;
        }

        private void Update()
        {
            if (_service == null)
                return;

            var step = _root.Services.ServiceConfig.WalkSpeed * Time.deltaTime;
            _gone.Clear();
            foreach (var pair in _agents)
            {
                var customer = pair.Key;
                var agent = pair.Value;
                switch (customer.State)
                {
                    case CustomerState.WalkingToSeat:
                        if (agent.WalkTowards(SeatSpot(customer.Seat), step, ArriveDistance))
                            _service.SitDown(customer);
                        break;
                    case CustomerState.Queueing:
                        agent.WalkTowards(QueueSpot(customer), step, ArriveDistance);
                        break;
                    case CustomerState.Waiting:
                    case CustomerState.Eating:
                        // Sit facing the pass.
                        agent.WalkTowards(SeatSpot(customer.Seat), step, ArriveDistance);
                        agent.Face(Vector3.forward);
                        break;
                    case CustomerState.Leaving:
                        if (agent.WalkTowards(_door.position, step, ArriveDistance))
                            _service.Left(customer);
                        break;
                    case CustomerState.Gone:
                        _gone.Add(customer);
                        break;
                }
            }

            foreach (var customer in _gone)
            {
                Destroy(_agents[customer].gameObject);
                _agents.Remove(customer);
            }
        }

        private Vector3 SeatSpot(int seat) =>
            seat >= 0 && seat < _seats.Length ? _seats[seat].position : _door.position;

        private Vector3 QueueSpot(Customer customer)
        {
            var place = 0;
            foreach (var queued in _service.Queue)
            {
                if (queued == customer)
                    break;
                place++;
            }
            return _queueSpots.Length > 0 ? _queueSpots[Mathf.Min(place, _queueSpots.Length - 1)].position : _door.position;
        }
    }
}
