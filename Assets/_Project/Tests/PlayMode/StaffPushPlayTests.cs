using System.Collections;
using System.Linq;
using Game.Gameplay;
using Game.Gameplay.Staff;
using Game.Simulation.Days;
using Game.Simulation.Staff;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>The real scene, running: staff bodies in a kitchen, not just the rules on their own.</summary>
    public class StaffPushPlayTests
    {
        private const string SceneName = "Kitchen_Greybox";
        // Open floor between the island and the pass, with room to turn a station; the back-right
        // gap is the tray rack's since 2026-11-07.
        private const float OpenFloorX = -0.4f;
        private const float OpenFloorZ = -2.0f;

        private KitchenCompositionRoot _root;
        private StaffDirector _director;
        private PlayerInput _player;

        private IEnumerator OpenKitchenWithChef()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            yield return null;
            _root = Object.FindFirstObjectByType<KitchenCompositionRoot>();
            _director = Object.FindFirstObjectByType<StaffDirector>();

            var keyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.GetDevice<Mouse>() ?? InputSystem.AddDevice<Mouse>();
            _player = Object.FindFirstObjectByType<PlayerInputManager>().JoinPlayer(controlScheme: "Keyboard&Mouse", pairWithDevices: new InputDevice[] { keyboard, mouse });
            yield return null;
            yield return null;
        }

        private IEnumerator HireAndOpen(StaffRole role)
        {
            var services = _root.Services;
            var candidate = services.StaffConfig.CandidatesFor(1, 2).First(c => c.Role == role);
            services.Staff.Hire(candidate, services.Days.Balance);
            services.Days.Advance(); // to Prep: they arrive
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnIdleCleanerCanBePushedLikeAnIdleChef()
        {
            yield return OpenKitchenWithChef();
            yield return HireAndOpen(StaffRole.Dishwasher);

            var agent = _director.Agents.Single();
            var start = agent.transform.position;
            Assert.That(((Dishwasher)agent.Job).Task, Is.EqualTo(DishwasherTask.Idle));

            // Put the chef right up against them.
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.position = start + Vector3.left * 0.3f;
            controller.enabled = true;
            Physics.SyncTransforms();

            for (var i = 0; i < 10; i++)
                yield return null;

            var moved = (agent.transform.position - start).magnitude;
            var apart = (agent.transform.position - _player.transform.position).magnitude;
            Debug.Log($"[PushPlay] idle dishwasher moved {moved:0.000}, now {apart:0.000} from the chef");
            Assert.That(apart, Is.GreaterThan(0.6f), "they're not overlapping any more");
            Assert.That(moved, Is.GreaterThan(0.05f), "the cleaner gave way too");
        }

        [UnityTest]
        public IEnumerator AHelperArrivesWithRoomToBePushedEveryWay()
        {
            yield return OpenKitchenWithChef();
            yield return HireAndOpen(StaffRole.PrepAssistant);

            var agent = _director.Agents.Single();
            foreach (var push in new[]
            {
                new System.Numerics.Vector2(0.3f, 0f), new System.Numerics.Vector2(-0.3f, 0f),
                new System.Numerics.Vector2(0f, 0.3f), new System.Numerics.Vector2(0f, -0.3f),
            })
            {
                var moved = agent.Shove(push);
                Assert.That(moved.Length(), Is.GreaterThan(0.25f), $"pushed by {push}");
                agent.Shove(-moved);
            }
        }

        [UnityTest]
        public IEnumerator AStationCanBeMovedAndItsContentsAndChefFollowTheRules()
        {
            yield return OpenKitchenWithChef();
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            // A counter with nothing on it: one under a board moves only once the board is off.
            var counter = Object.FindObjectsByType<Game.Gameplay.Stations.CounterStation>(FindObjectsSortMode.None).First(c => c.IsClear);
            var start = counter.transform.position;

            var cursor = editor.Enter(_player);
            Assert.That(_player.GetComponent<Game.Gameplay.Stations.PlayerInteractor>().IsManaging);
            cursor.Position = new System.Numerics.Vector2(start.x, start.z);
            Assert.That(editor.PickUp(cursor), Is.True);

            // Somewhere empty in the open floor, between the island and the pass.
            editor.MoveCursor(cursor, new System.Numerics.Vector2(OpenFloorX - start.x, OpenFloorZ - start.z));
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());
            yield return null;

            Assert.That(counter.transform.position.x, Is.EqualTo(OpenFloorX).Within(0.01f));
            Assert.That(counter.transform.position.y, Is.EqualTo(start.y).Within(0.01f), "back on the floor");

            editor.Leave(_player);
            Assert.That(_player.GetComponent<Game.Gameplay.Stations.PlayerInteractor>().IsManaging, Is.False);
        }

        [UnityTest]
        public IEnumerator AStationCanBeTurnedAndGoBackExactlyWhereItWas()
        {
            yield return OpenKitchenWithChef();
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var hob = Object.FindObjectsByType<Game.Gameplay.Stations.HobStation>(FindObjectsSortMode.None).First();
            var start = hob.transform.position;

            var cursor = editor.Enter(_player);
            cursor.Position = new System.Numerics.Vector2(start.x, start.z);
            editor.PickUp(cursor);
            // Open floor between the island and the pass, with room to turn.
            var away = new System.Numerics.Vector2(OpenFloorX - start.x, OpenFloorZ - start.z);
            editor.MoveCursor(cursor, away);
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());

            editor.PickUp(cursor);
            editor.Rotate(cursor, 45f);
            yield return new WaitForSeconds(0.5f); // it eases round
            Assert.That(Mathf.DeltaAngle(hob.transform.eulerAngles.y, 45f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());

            editor.PickUp(cursor);
            editor.Rotate(cursor, -45f);
            editor.MoveCursor(cursor, new System.Numerics.Vector2(0.05f - away.X, -away.Y)); // nearly home: it snaps into line
            Assert.That(editor.Place(cursor), Is.True, cursor.Result.ToString());
            Assert.That(Mathf.DeltaAngle(hob.transform.eulerAngles.y, 0f), Is.EqualTo(0f).Within(0.01f), "placed mid-turn, it lands at its new angle");
            yield return null;

            Assert.That((hob.transform.position - start).magnitude, Is.LessThan(0.2f));
        }

        [UnityTest]
        public IEnumerator CancellingPutsItBack()
        {
            yield return OpenKitchenWithChef();
            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            var counter = Object.FindObjectsByType<Game.Gameplay.Stations.CounterStation>(FindObjectsSortMode.None).First();
            var start = counter.transform.position;

            var cursor = editor.Enter(_player);
            cursor.Position = new System.Numerics.Vector2(start.x, start.z);
            editor.PickUp(cursor);
            editor.MoveCursor(cursor, new System.Numerics.Vector2(1f, 0f));
            editor.Cancel(cursor);
            yield return null;

            Assert.That((counter.transform.position - start).magnitude, Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator ShovedStaffStayWhereTheyWerePushed()
        {
            yield return OpenKitchenWithChef();
            yield return HireAndOpen(StaffRole.PrepAssistant);

            var agent = _director.Agents.Single();
            var shoved = agent.Shove(new System.Numerics.Vector2(-0.5f, 0f));
            Assert.That(shoved.Length(), Is.GreaterThan(0.4f), "open floor around where they arrive");
            var after = agent.transform.position;

            for (var i = 0; i < 20; i++)
                yield return null;
            Assert.That((agent.transform.position - after).magnitude, Is.LessThan(0.01f), "nothing drags them back");
        }
    }
}
