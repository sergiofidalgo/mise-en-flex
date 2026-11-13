using System.Collections;
using Game.Gameplay;
using Game.Gameplay.Customers;
using Game.Gameplay.Stations;
using Game.Gameplay.Street;
using Game.Simulation.Days;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// The street's mishaps in the real kitchen's scene (2026-11-12): a chef standing in a car's
    /// way is thrown onto the pavement, loses the food they carried and gets up again; a chef who
    /// walks off the end of the street is carried back to the doorstep.
    /// </summary>
    public class StreetMishapsPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";

        private KitchenCompositionRoot _root;
        private PlayerInteractor _chef;
        private Gamepad _pad;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [TearDown]
        public void Release()
        {
            if (_pad != null)
                InputSystem.RemoveDevice(_pad);
            _pad = null;
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }

        private IEnumerator OpenKitchenInPrep()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _pad = InputSystem.AddDevice<Gamepad>("StreetTestPad");
            var player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Gamepad", pairWithDevices: _pad);
            yield return null;
            yield return null;
            _chef = player.GetComponent<PlayerInteractor>();
            if (_root.Services.Days.Phase == DayPhase.Plan)
                _root.Services.Days.Advance();
            Assume.That(_root.Services.Days.Phase, Is.EqualTo(DayPhase.Prep));
        }

        private void StandAt(Vector3 point) =>
            _chef.GetComponent<PlayerMovement>().Teleport(new System.Numerics.Vector2(point.x, point.z));

        [UnityTest]
        public IEnumerator ACarThrowsAChefInItsWayOntoThePavementAndTheirFoodIsLost()
        {
            yield return OpenKitchenInPrep();
            var hits = Object.FindFirstObjectByType<HitAndRun>();
            Assert.That(hits, Is.Not.Null, "the street has its traffic's rule");
            var struck = false;
            var landed = false;
            var recovered = false;
            hits.Struck += (_, _) => struck = true;
            hits.Landed += _ => landed = true;
            hits.Recovered += _ => recovered = true;

            // A steak in their hands, and a car that has got going, well inside the scene.
            _chef.ReceiveItem(_root.Services.CreateItem("Steak"), _chef.transform.position);
            RoadVehicle car = null;
            var until = Time.time + 20f;
            while (car == null && Time.time < until)
            {
                foreach (var candidate in Object.FindObjectsByType<RoadVehicle>(FindObjectsSortMode.None))
                    if (candidate.Speed > 2f && Mathf.Abs(candidate.transform.position.x) < 6f)
                        car = candidate;
                yield return null;
            }
            Assert.That(car, Is.Not.Null, "a car driving past the restaurant");

            // Stood three metres ahead of it, in its lane.
            StandAt(car.transform.position + car.transform.forward * 3f);
            until = Time.time + 3f;
            while (!struck && Time.time < until)
                yield return null;
            Assert.That(struck, Is.True, "hit");
            Assert.That(ChefHold.IsFree(_chef), Is.False, "out of the player's hands while they fly");
            Assert.That(_chef.Hands.IsEmpty, Is.True, "the steak is gone");

            yield return new WaitForSeconds(0.45f);
            Assert.That(_chef.transform.position.y, Is.GreaterThan(1f), "up in the air");

            until = Time.time + 6f;
            while (!recovered && Time.time < until)
                yield return null;
            Assert.That(landed, Is.True);
            Assert.That(recovered, Is.True, "up again");
            Assert.That(ChefHold.IsFree(_chef), Is.True, "the player's again");
            Assert.That(_chef.IsBusy, Is.False);
            yield return null;
            // On a pavement, not in the road: clear of both lanes.
            var z = _chef.transform.position.z;
            var lanes = car.transform.position.z;
            Assert.That(Mathf.Abs(z - lanes), Is.GreaterThan(1.4f), "off the road");
            Assert.That(Vector3.Angle(_chef.transform.up, Vector3.up), Is.LessThan(1f), "standing");
        }

        [UnityTest]
        public IEnumerator AChefWhoWalksOffTheStreetIsCarriedBackToTheDoorstep()
        {
            yield return OpenKitchenInPrep();
            var leaving = Object.FindFirstObjectByType<SceneLeave>();
            Assert.That(leaving, Is.Not.Null);
            var left = false;
            var returned = false;
            leaving.Left += _ => left = true;
            leaving.Returned += _ => returned = true;
            var door = Object.FindFirstObjectByType<CustomerDirector>().Door;

            // Off the west end of the pavement, by the wall's side of it (clear of the road).
            _chef.ReceiveItem(_root.Services.CreateItem("Steak"), _chef.transform.position);
            StandAt(new Vector3(door.position.x - 22f, 0f, door.position.z - 1.5f));
            yield return null;
            yield return null;
            Assert.That(left, Is.True, "noticed");
            Assert.That(leaving.IsAway(_chef), Is.True);
            Assert.That(ChefHold.IsFree(_chef), Is.False);

            var until = Time.time + 20f;
            while (!returned && Time.time < until)
                yield return null;
            Assert.That(returned, Is.True, "set down again");
            yield return null;
            Assert.That(ChefHold.IsFree(_chef), Is.True, "the player's again");
            var fromDoor = _chef.transform.position - door.position;
            fromDoor.y = 0f;
            Assert.That(fromDoor.magnitude, Is.LessThan(1.6f), "on the doorstep");
            Assert.That(Vector3.Dot(fromDoor, door.forward), Is.LessThan(0f), "outside it");
            Assert.That(_chef.Hands.IsEmpty, Is.False, "what they carried came back with them");
            Assert.That(Vector3.Angle(_chef.transform.up, Vector3.up), Is.LessThan(1f), "standing");
        }
    }
}
