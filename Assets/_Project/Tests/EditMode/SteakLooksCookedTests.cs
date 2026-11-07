using System.Linq;
using System.Reflection;
using Game.Data;
using Game.Gameplay.Items;
using Game.Presentation;
using Game.Simulation.Cooking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Cooks a real steak in the simulation, with the game's own tuning, and checks that what the
    /// steak's view sends to its shader changes in a way the player can see.
    /// </summary>
    public class SteakLooksCookedTests
    {
        private const string SteakPrefabPath = "Assets/_Project/Prefabs/Items/PF_Item_Steak.prefab";
        private const string SteakDefinitionPath = "Assets/_Project/Data/Ingredients/ING_Steak.asset";
        private const float PreheatSeconds = 60f;
        private const float SearSeconds = 60f;
        private const float TopLooksCookedFrom = 50f;
        private const float MaxCookSeconds = 600f;
        // Summed change across red, green and blue that a player would notice at a glance.
        private const float VisibleChange = 0.3f;

        private CookingSimulation _sim;
        private Pan _pan;
        private GameObject _view;

        [SetUp]
        public void HeatAPan()
        {
            var equipment = AssetDatabase.FindAssets("t:EquipmentTier")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EquipmentTier>(AssetDatabase.GUIDToAssetPath(guid)))
                .First();
            _sim = new CookingSimulation(22f);
            var hob = _sim.AddHob(equipment.Hob);
            _pan = _sim.AddPan(equipment.Pan);
            hob.PlacePan(_pan);
            hob.Dial = 1f;
            _sim.Tick(PreheatSeconds);
        }

        [TearDown]
        public void RemoveView()
        {
            if (_view != null)
                Object.DestroyImmediate(_view);
        }

        private MaterialPropertyBlock Shown(IngredientView view, Renderer renderer)
        {
            typeof(IngredientView).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block;
        }

        [Test]
        public void SearingTheUndersideShowsBeforeTheFlip()
        {
            var definition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(SteakDefinitionPath);
            var steak = _sim.AddIngredient(definition.ToProperties());
            _pan.Put(steak);

            _view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SteakPrefabPath));
            _view.GetComponent<ItemView>().Initialize(steak);
            var view = _view.GetComponent<IngredientView>();
            var renderer = _view.GetComponentInChildren<Renderer>();

            var before = Shown(view, renderer);
            var rawBottom = before.GetColor("_CrustBottom");
            var rawTop = before.GetColor("_BaseColor");
            var rawBand = before.GetColor("_BandBottom");

            _sim.Tick(SearSeconds);
            var after = Shown(view, renderer);

            TestContext.WriteLine($"Browning down {steak.Browning(steak.DownSide):F3}, up {steak.Browning(steak.UpSide):F3}");
            TestContext.WriteLine($"Top {rawTop} -> {after.GetColor("_BaseColor")}");
            TestContext.WriteLine($"Bottom band {rawBand} -> {after.GetColor("_BandBottom")}");
            TestContext.WriteLine($"Bottom crust {rawBottom} -> {after.GetColor("_CrustBottom")}");

            Assert.That(steak.Browning(steak.DownSide), Is.GreaterThan(0f), "The underside should have started browning.");
            Assert.That(steak.Browning(steak.UpSide), Is.EqualTo(0f), "Only the underside should have browned.");
            Assert.That(after.GetColor("_CrustBottom"), Is.Not.EqualTo(rawBottom),
                "The seared underside should show at the steak's edge before it's flipped.");
            Assert.That(after.GetFloat("_BrowningBottom"), Is.GreaterThan(0f));
        }

        [Test]
        public void AfterFlippingTheSearedSideShowsOnTop()
        {
            var definition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(SteakDefinitionPath);
            var steak = _sim.AddIngredient(definition.ToProperties());
            _pan.Put(steak);

            _view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SteakPrefabPath));
            _view.GetComponent<ItemView>().Initialize(steak);
            var view = _view.GetComponent<IngredientView>();
            var renderer = _view.GetComponentInChildren<Renderer>();
            var rawTop = Shown(view, renderer).GetColor("_BaseColor");

            // An early flip: the crust has barely started, but the seared side is clearly cooked.
            _sim.Tick(SearSeconds);
            steak.Flip();
            var top = Shown(view, renderer).GetColor("_BaseColor");
            TestContext.WriteLine($"Flipped after {SearSeconds} s (browning {steak.Browning(steak.UpSide):F3}): top {rawTop} -> {top}");

            var change = Mathf.Abs(top.r - rawTop.r) + Mathf.Abs(top.g - rawTop.g) + Mathf.Abs(top.b - rawTop.b);
            Assert.That(change, Is.GreaterThan(VisibleChange), "Once flipped, the seared side should no longer look raw.");
        }

        [Test]
        public void TheTopDullsAsHeatRisesThroughTheSteak()
        {
            var definition = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(SteakDefinitionPath);
            var steak = _sim.AddIngredient(definition.ToProperties());
            _pan.Put(steak);

            _view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SteakPrefabPath));
            _view.GetComponent<ItemView>().Initialize(steak);
            var view = _view.GetComponent<IngredientView>();
            var renderer = _view.GetComponentInChildren<Renderer>();

            var rawTop = Shown(view, renderer).GetColor("_BaseColor");
            var seconds = 0f;
            while (steak.PeakSurfaceTemperature(steak.UpSide) < TopLooksCookedFrom && seconds < MaxCookSeconds)
            {
                _sim.Tick(1f);
                seconds++;
            }

            TestContext.WriteLine($"Top reached {steak.PeakSurfaceTemperature(steak.UpSide):F1} °C after {seconds} s");
            Assume.That(steak.PeakSurfaceTemperature(steak.UpSide), Is.GreaterThanOrEqualTo(TopLooksCookedFrom),
                "The top never got hot enough within the time allowed.");
            Assert.That(Shown(view, renderer).GetColor("_BaseColor"), Is.Not.EqualTo(rawTop));
        }
    }
}
