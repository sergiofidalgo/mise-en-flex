using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Catches empty references on our own components in generated scenes and prefabs,
    /// which otherwise only show up as errors in Play mode.
    /// </summary>
    public class WiringTests
    {
        private const string GameNamespacePrefix = "Game.";
        private const string KitchenScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        // Every restaurant's own kitchen (Phase 6, milestone 3); only the grill's is in git.
        private const string DiningRoomScenePath = "Assets/_Project/Scenes/Kitchen_CasaLucia.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs";

        [TestCase(KitchenScenePath)]
        [TestCase(DiningRoomScenePath)]
        public void KitchenSceneHasNoEmptyReferences(string path)
        {
            if (path != KitchenScenePath && !System.IO.File.Exists(path))
                Assert.Ignore($"{path} isn't built yet: run Tools/Game/Build Greybox Kitchen.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true));

            Assert.That(FindEmptyReferences(components), Is.Empty);
        }

        // Regression: chefs could climb onto low crates. Every station's collider must reach above
        // a chef's head (1.7 m), so the capsule only meets vertical sides.
        [TestCase(KitchenScenePath)]
        [TestCase(DiningRoomScenePath)]
        public void StationCollidersAreTooTallToClimb(string path)
        {
            const float chefHeight = 1.7f;
            if (path != KitchenScenePath && !System.IO.File.Exists(path))
                Assert.Ignore($"{path} isn't built yet: run Tools/Game/Build Greybox Kitchen.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var stations = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Game.Gameplay.Stations.Interactable>(true))
                .Where(station => !(station is Game.Gameplay.Stations.StreetDoor)) // walked through, not around
                .ToArray();

            Assert.That(stations, Is.Not.Empty);
            foreach (var station in stations)
            {
                var top = station.GetComponents<Collider>().Select(c => c.bounds.max.y).DefaultIfEmpty(0f).Max();
                Assert.That(top, Is.GreaterThan(chefHeight), $"{station.name} can be climbed (collider top {top:0.00} m)");
            }
        }

        [Test]
        public void PrefabsHaveNoEmptyReferences()
        {
            var components = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .SelectMany(prefab => prefab.GetComponentsInChildren<MonoBehaviour>(true));

            Assert.That(FindEmptyReferences(components), Is.Empty);
        }

        // Every object-reference field on our components must be filled in. If a field is
        // ever meant to be optional, give it an explicit opt-out here rather than weakening this.
        private static List<string> FindEmptyReferences(IEnumerable<MonoBehaviour> components)
        {
            var problems = new List<string>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    problems.Add("Missing script");
                    continue;
                }

                var type = component.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith(GameNamespacePrefix))
                    continue;

                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
                        problems.Add($"{component.gameObject.name}/{type.Name}.{property.propertyPath}");
                }
            }

            return problems;
        }
    }
}
