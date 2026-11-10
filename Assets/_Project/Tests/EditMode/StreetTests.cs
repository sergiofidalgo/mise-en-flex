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
    /// <summary>The way out to the street's stalls (Phase 5, milestone 3).</summary>
    public class StreetTests
    {
        private static FloorPlan Building() => new FloorPlan(
            new Vector2(-3.6f, -6.6f), new Vector2(3.6f, 1.6f), new (Vector2, Vector2)[0], 0.5f, 0.3f, 0.85f);

        [Test]
        public void TheDoorwaySplitsTheWallAroundTheDoor()
        {
            var pieces = DoorwayGap.Split(-4f, 4f, 2f, 1.2f);
            Assert.That(pieces, Has.Count.EqualTo(2));
            Assert.That(pieces[0].From, Is.EqualTo(-4f));
            Assert.That(pieces[0].To, Is.EqualTo(1.4f).Within(1e-5f));
            Assert.That(pieces[1].From, Is.EqualTo(2.6f).Within(1e-5f));
            Assert.That(pieces[1].To, Is.EqualTo(4f));
        }

        [Test]
        public void ADoorByACornerLeavesNoSliver()
        {
            var pieces = DoorwayGap.Split(-4f, 4f, 3.42f, 1.2f, shortest: 0.05f);
            Assert.That(pieces, Has.Count.EqualTo(1));
            Assert.That(pieces[0].To, Is.EqualTo(2.82f).Within(1e-5f));
        }

        [Test]
        public void ADoorElsewhereLeavesTheWallWhole()
        {
            var pieces = DoorwayGap.Split(-7f, 2f, 10f, 1.2f);
            Assert.That(pieces, Has.Count.EqualTo(1));
            Assert.That(pieces[0], Is.EqualTo((-7f, 2f)));
        }

        [Test]
        public void ThePavementIsOutdoorsAndTheDoorwayIn()
        {
            var building = Building();
            Assert.That(building.IsIndoors(new Vector2(0f, -3f)), Is.True);
            Assert.That(building.IsIndoors(new Vector2(2f, -6.8f)), Is.True, "standing in the doorway");
            Assert.That(building.IsIndoors(new Vector2(2f, -7.6f)), Is.False, "on the pavement");
            Assert.That(building.IsIndoors(new Vector2(-8f, -5f)), Is.False, "at the stalls");
        }
    
        [Test]
        public void TheStallsStandOutsideWithRoomToBuyAtTheirFront()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Kitchen_Greybox.unity", OpenSceneMode.Additive);
            try
            {
                var stalls = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StallStation>(true)).ToArray();
                Assert.That(stalls.Select(s => s.name), Is.EquivalentTo(new[] { "Stall_Butcher", "Stall_Greengrocer" }));
                var building = Building();
                Physics.SyncTransforms();
                foreach (var stall in stalls)
                {
                    var at = stall.transform.position;
                    Assert.That(building.IsIndoors(new Vector2(at.x, at.z)), Is.False, $"{stall.name} is out on the street");

                    // Where a chef stands to buy: in front of the counter, on open ground.
                    var front = at + stall.transform.forward * 0.9f + UnityEngine.Vector3.up;
                    Assert.That(Physics.Raycast(front, UnityEngine.Vector3.down, 2f), Is.True, $"ground in front of {stall.name}");
                    Assert.That(Physics.CheckSphere(front, 0.3f), Is.False, $"nothing in the way in front of {stall.name}");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
