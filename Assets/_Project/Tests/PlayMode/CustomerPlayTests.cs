using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Simulation.Days;
using Game.Simulation.Service;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>The real scene, running: customers' bodies in the dining room.</summary>
    public class CustomerPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private CustomerDirector _customers;
        private PlayerInput _player;

        private IEnumerator OpenForService()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _customers = Object.FindFirstObjectByType<CustomerDirector>();

            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;

            var days = _root.Services.Days;
            days.Advance(); // to Prep
            days.Advance(); // to Service
            yield return null;
        }

        // Arrivals come on the game clock; bring the next one on now.
        private Customer NextCustomer()
        {
            var service = _root.Services.Service;
            var count = service.Customers.Count;
            while (service.Customers.Count == count)
                service.Tick(1f);
            return service.Customers.Last();
        }

        private IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            var until = Time.time + seconds;
            while (!condition() && Time.time < until)
                yield return null;
            Assert.That(condition(), Is.True, "in time");
        }

        [UnityTest]
        public IEnumerator ACustomerWalksInFromTheStreetAndSits()
        {
            yield return OpenForService();
            var customer = NextCustomer();
            yield return null;

            var agent = _customers.Agents[customer];
            Assert.That(agent.transform.position.z, Is.LessThan(-6f), "at the street door");

            yield return WaitFor(() => customer.State == CustomerState.Waiting, 15f);
            Assert.That(agent.transform.position.z, Is.GreaterThan(-3.6f), "on a stool, at the hatch");
        }

        [UnityTest]
        public IEnumerator AChefCanPushACustomerAndTheyComeBack()
        {
            yield return OpenForService();
            var customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Waiting, 15f);
            var agent = _customers.Agents[customer];
            var seat = agent.transform.position;

            // Standing right up against them, pressing south.
            _player.GetComponent<CharacterController>().enabled = false;
            _player.transform.position = seat + new Vector3(0f, 0f, 0.2f);
            _player.GetComponent<CharacterController>().enabled = true;
            yield return null;
            yield return null;

            var apart = new Vector2(_player.transform.position.x - agent.transform.position.x, _player.transform.position.z - agent.transform.position.z).magnitude;
            Assert.That(apart, Is.GreaterThan(0.5f), "bodies don't overlap");

            _player.GetComponent<CharacterController>().enabled = false;
            _player.transform.position = new Vector3(-3f, _player.transform.position.y, 0f);
            _player.GetComponent<CharacterController>().enabled = true;
            yield return WaitFor(() => (agent.transform.position - seat).magnitude < 0.1f, 5f);
        }

        [UnityTest]
        public IEnumerator EveryoneGetsOutAgain()
        {
            yield return OpenForService();
            var customer = NextCustomer();
            yield return WaitFor(() => customer.State == CustomerState.Waiting, 15f);
            var service = _root.Services.Service;

            // Run their patience out: they walk back out through the door, and are gone.
            while (customer.State == CustomerState.Waiting)
                service.Tick(1f);
            yield return WaitFor(() => customer.State == CustomerState.Gone, 15f);
        }
    }
}
