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
        public void PushedIntoFurnitureTheySlideAlongIt()
        {
            var layout = Layout(new Vector2(0f, 0f));
            var touching = new Vector2(-2f, 0.81f); // just north of the row, clear of the station's x
            var beside = new Vector2(0f, 0.81f);

            var slid = layout.Slide(beside, new Vector2(0.2f, -0.2f));
            Assert.That(slid.X, Is.EqualTo(0.2f).Within(0.001f), "the part of the push that fits");
            Assert.That(slid.Y, Is.EqualTo(0.81f).Within(0.001f), "the furniture stops the rest");

            Assert.That(layout.Slide(touching, new Vector2(0.1f, 0f)), Is.EqualTo(touching + new Vector2(0.1f, 0f)));
        }

        [Test]
        public void StraightIntoFurnitureGoesNowhere()
        {
            var layout = Layout(new Vector2(0f, 0f));
            var beside = new Vector2(0f, 0.81f);
            Assert.That(layout.Slide(beside, new Vector2(0f, -0.2f)), Is.EqualTo(beside));
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

        [Test]
        public void ARouteGoesToASideThatCanBeWalkedTo()
        {
            // Its side nearest the middle of the room is a nook sealed off by a wall of counters.
            var counter = new Footprint(new Vector2(0f, 2f));
            var wall = new[] { -4f, -3f, -2f, -1f, 1f, 2f, 3f, 4f }.Select(x => new Vector2(x, 2f)).ToList();
            var layout = new StationLayout(wall.Concat(new[] { new Vector2(0f, 2f) }).Select(c => new Footprint(c)), new Vector2(4.6f, 3.6f), 0.5f, 0.3f, 0.85f);

            var route = layout.FindPathTo(new Vector2(0f, 3.2f), counter);
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Last().Y, Is.GreaterThan(2f), "from the side they can get to");
        }

        [Test]
        public void AStationClosedOffOnEverySideHasNoRoute()
        {
            var wall = new[] { -4f, -3f, -2f, -1f, 0f, 1f, 2f, 3f, 4f }.Select(x => new Footprint(new Vector2(x, 0f)));
            var far = new Footprint(new Vector2(2f, 2.4f));
            var layout = new StationLayout(wall.Concat(new[] { far }), new Vector2(4.6f, 3.6f), 0.5f, 0.3f, 0.85f);

            Assert.That(layout.FindPathTo(new Vector2(0f, -2f), far), Is.Null);
        }

        // Two rooms, with a wall right across and a door in the middle of it.
        private static StationLayout TwoRooms(params Footprint[] stations) => new StationLayout(stations, new FloorPlan(
            new Vector2(-4.6f, -7.6f), new Vector2(4.6f, 3.6f),
            new[] { (new Vector2(-5f, -3.1f), new Vector2(-0.6f, -2.9f)), (new Vector2(0.6f, -3.1f), new Vector2(5f, -2.9f)) },
            0.5f, 0.3f, 0.85f));

        [Test]
        public void AWallIsntFloor()
        {
            var layout = TwoRooms();
            Assert.That(layout.IsFree(new Vector2(-2f, -3f)), Is.False);
            Assert.That(layout.IsFree(new Vector2(-2f, -2.5f)), Is.True);
            Assert.That(layout.IsFree(new Vector2(0f, -3f)), Is.True, "the doorway");
        }

        [Test]
        public void TheRoomDoesntHaveToBeCentred()
        {
            var layout = TwoRooms();
            Assert.That(layout.IsFree(new Vector2(0f, -7f)), Is.True);
            Assert.That(layout.IsFree(new Vector2(0f, -7.8f)), Is.False);
            Assert.That(layout.IsFree(new Vector2(0f, 3.8f)), Is.False);
        }

        [Test]
        public void TheWayBetweenRoomsIsThroughTheDoor()
        {
            var path = TwoRooms().FindPath(new Vector2(-3f, -5f), new Vector2(-3f, 0f));
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Any(point => System.Math.Abs(point.X) < 0.6f), Is.True, "through the door");
        }

        [Test]
        public void ADoorBlockedByFurnitureClosesTheWay()
        {
            var layout = TwoRooms(new Footprint(new Vector2(0f, -2.4f)));
            Assert.That(layout.FindPath(new Vector2(-3f, -5f), new Vector2(-3f, 0f)), Is.Null);
        }

        [Test]
        public void SomeoneStandsTheSameGapFromANarrowStation()
        {
            var narrow = new Footprint(new Vector2(0f, 0f), 0f, new Vector2(0.5f, 0.25f));
            var sides = new StationLayout(new[] { narrow }, Room, 0.5f, 0.3f, 0.85f).FreeSides(narrow);

            Assert.That(sides.Any(p => (p - new Vector2(0f, 0.6f)).Length() < 0.001f), Is.True, "0.35 m from its long edge");
            Assert.That(sides.Any(p => (p - new Vector2(0.85f, 0f)).Length() < 0.001f), Is.True, "0.35 m from its end");
        }
    }
}