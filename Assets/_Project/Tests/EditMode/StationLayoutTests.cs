using System.Linq;
using System.Numerics;
using Game.Gameplay.Stations;
using Game.Simulation.Movement;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;

namespace Game.Tests.EditMode
{
    public class StationLayoutTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";

        private static readonly Vector2 Room = new Vector2(4.6f, 3.6f);

        private static StationLayout Layout(params Vector2[] stations) => new StationLayout(stations, Room, 0.5f, 0.3f, 0.85f);

        private static void AssertWalkable(StationLayout layout, Vector2 from, Vector2 to)
        {
            var path = layout.FindPath(from, to);
            Assert.That(path, Is.Not.Null, $"no way from {from} to {to}");
            Assert.That(path.Last(), Is.EqualTo(to));
            var here = from;
            foreach (var point in path)
            {
                Assert.That(layout.IsClear(here, point), $"{here} to {point} hits furniture");
                here = point;
            }
        }

        [Test]
        public void AnOpenRoomIsAStraightLine()
        {
            var path = Layout().FindPath(new Vector2(-2f, 0f), new Vector2(2f, 0f));
            Assert.That(path, Has.Count.EqualTo(1));
        }

        [Test]
        public void AStationInTheWayIsWalkedAround()
        {
            var layout = Layout(Vector2.Zero);
            var from = new Vector2(-2f, 0f);
            var to = new Vector2(2f, 0f);

            Assert.That(layout.IsClear(from, to), Is.False);
            Assert.That(layout.FindPath(from, to).Count, Is.GreaterThan(1));
            AssertWalkable(layout, from, to);
        }

        [Test]
        public void ARowOfCountersIsWalkedAroundTheEnd()
        {
            var row = Enumerable.Range(-3, 7).Select(x => new Vector2(x, 0f)).ToArray();
            AssertWalkable(Layout(row), new Vector2(0f, -2f), new Vector2(0f, 2f));
        }

        [Test]
        public void AWallOfCountersWithNoGapHasNoWay()
        {
            var wall = Enumerable.Range(-5, 11).Select(x => new Vector2(x, 0f)).ToArray();
            Assert.That(Layout(wall).FindPath(new Vector2(0f, -2f), new Vector2(0f, 2f)), Is.Null);
        }

        [Test]
        public void TheyStandOnTheFreeSideNearestTheMiddle()
        {
            // Against the north wall: the side facing the room.
            var facingRoom = Layout(new Vector2(0f, 3.4f)).StandingSpot(new Vector2(0f, 3.4f)).Value;
            Assert.That((facingRoom - new Vector2(0f, 2.55f)).Length(), Is.LessThan(0.001f));

            // With a neighbour on the room side, another side.
            var spot = Layout(new Vector2(0f, 2f), new Vector2(0f, 1f)).StandingSpot(new Vector2(0f, 2f));
            Assert.That(spot, Is.Not.EqualTo(new Vector2(0f, 1.15f)));
            Assert.That(spot, Is.Not.Null);
        }

        // The real kitchen: every station can be reached from every other, around the furniture.
        [Test]
        public void EveryStationInTheKitchenCanBeReached()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var stations = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Interactable>(true))
                    .ToArray();
                var points = stations.Select(s => new Vector2(s.transform.position.x, s.transform.position.z)).ToArray();
                Assert.That(points, Is.Not.Empty);

                var layout = new StationLayout(points, Room, 0.5f, 0.3f, 0.85f);
                var spots = points.Select(p => layout.StandingSpot(p)).ToArray();
                for (var i = 0; i < stations.Length; i++)
                    Assert.That(spots[i], Is.Not.Null, $"nowhere to stand at {stations[i].name}");

                var spawn = GameObject.Find("StaffSpawn");
                Assert.That(spawn, Is.Not.Null);
                var from = new Vector2(spawn.transform.position.x, spawn.transform.position.z);
                Assert.That(layout.IsFree(from), "staff arrive somewhere free");

                for (var i = 0; i < stations.Length; i++)
                    AssertWalkable(layout, from, spots[i].Value);
                AssertWalkable(layout, spots[0].Value, spots[spots.Length - 1].Value);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
