using System.Linq;
using Game.Gameplay.Items;
using Game.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Checks where code meets art: the food shader compiles and is driven correctly, and the
    /// imported models have the scale, origin and facing the Blender pipeline promises.
    /// </summary>
    public class ArtPilotAssetTests
    {
        private const string ModelsFolder = "Assets/_Project/Art/Models";
        private const float Tolerance = 0.005f;

        private static Mesh Model(string file, string meshName)
        {
            var path = $"{ModelsFolder}/{file}.fbx";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                Assert.Ignore($"{path} hasn't been exported from Blender yet.");

            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == meshName);
            Assert.That(mesh, Is.Not.Null, $"{path} has no mesh called {meshName}");
            return mesh;
        }

        [Test]
        public void FoodShaderCompilesWithoutErrors()
        {
            var shader = Shader.Find("Game/Food/Layered");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
                string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
        }

        [Test]
        public void LayeredMeshShowsDonenessFromCoreTemperature()
        {
            var shader = Shader.Find("Game/Food/Layered");
            var material = new Material(shader);
            var go = new GameObject("Steak");
            try
            {
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var view = go.AddComponent<IngredientView>();
                var so = new SerializedObject(view);
                so.FindProperty("_itemView").objectReferenceValue = go.AddComponent<ItemView>();
                so.FindProperty("_surface").objectReferenceValue = renderer;
                so.FindProperty("_layeredShader").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(view.ShowsDoneness, Is.True);

                var block = new MaterialPropertyBlock();

                view.ShowState(0.3f, 120f, 50f, 120f);
                renderer.GetPropertyBlock(block);
                var rareCore = block.GetColor("_BandCore");
                var rareSurface = block.GetColor("_BaseColor");
                var rareTop = block.GetColor("_BandTop");

                view.ShowState(0.9f, 120f, 72f, 120f);
                renderer.GetPropertyBlock(block);

                Assert.That(block.GetColor("_BandCore"), Is.Not.EqualTo(rareCore), "A hotter core should change its band.");
                Assert.That(block.GetColor("_BaseColor"), Is.Not.EqualTo(rareSurface), "More browning should change the crust.");
                Assert.That(block.GetColor("_BandTop"), Is.EqualTo(rareTop), "The top band follows its own temperature.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SteakIsScaledInMetresWithItsOriginAtTheBase()
        {
            var bounds = Model("SM_Steak", "SM_Steak").bounds;
            Assert.That(bounds.size.x, Is.EqualTo(0.28f).Within(0.03f));
            Assert.That(bounds.size.y, Is.EqualTo(0.05f).Within(Tolerance));
            Assert.That(bounds.size.z, Is.EqualTo(0.22f).Within(0.03f));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void SteakFitsInThePan()
        {
            var steak = Model("SM_Steak", "SM_Steak").bounds;
            var pan = Model("SM_Pan", "Body").bounds;
            Assert.That(Mathf.Max(steak.size.x, steak.size.z), Is.LessThan(pan.size.x));
        }

        [Test]
        public void PanFitsTheGreyboxSizeWithTheHandleTowardsTheCamera()
        {
            var body = Model("SM_Pan", "Body").bounds;
            var handle = Model("SM_Pan", "Handle").bounds;
            Assert.That(body.size.x, Is.EqualTo(0.44f).Within(Tolerance));
            Assert.That(body.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(handle.center.z, Is.LessThan(0f), "The handle should point south, towards the camera.");
        }

        [Test]
        public void HobFillsItsStationAndTheBurnerStaysBelowThePan()
        {
            var body = Model("SM_Hob", "Body").bounds;
            var burner = Model("SM_Hob", "Burner").bounds;
            Assert.That(body.size.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(body.size.y, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(body.size.z, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(body.min.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(burner.max.y, Is.LessThanOrEqualTo(0.91f), "The pan rests at 0.91 m.");
        }
    }
}
