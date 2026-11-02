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
        private const string PrefabFolder = "Assets/_Project/Prefabs";

        [Test]
        public void KitchenSceneHasNoEmptyReferences()
        {
            var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true));

            Assert.That(FindEmptyReferences(components), Is.Empty);
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
