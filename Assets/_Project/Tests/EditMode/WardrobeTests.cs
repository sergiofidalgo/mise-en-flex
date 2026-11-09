using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The tailor's rules (decision 2026-11-09): a look per player slot that saves and comes back,
    /// damaged or old files repaired to the default chef, and crazy items that can be looked at but
    /// not worn until earned.
    /// </summary>
    public class WardrobeTests
    {
        private static readonly string[] NothingEarned = System.Array.Empty<string>();

        [Test]
        public void EveryModuleStartsWithAnOpenDefaultAndUniqueIds()
        {
            foreach (var module in Wardrobe.Modules)
            {
                var items = Wardrobe.Options(module);
                Assert.That(items[0].Crazy, Is.False, $"{module}'s default must be wearable");
                Assert.That(items.Select(i => i.Id).Distinct().Count(), Is.EqualTo(items.Count), $"{module} ids repeat");
            }
            Assert.That(Wardrobe.Options(WardrobeModule.Skin).Count, Is.EqualTo(Wardrobe.SkinToneCount));
        }

        [Test]
        public void TheDefaultLookIsTheDefaultOfEveryModule()
        {
            var look = new ChefLook();
            foreach (var module in Wardrobe.Modules)
                Assert.That(look.Get(module), Is.EqualTo(Wardrobe.Default(module).Id), module.ToString());
        }

        [Test]
        public void AProfileSavesAndLoadsEachSlotsLook()
        {
            var profile = new ChefProfile();
            var dressed = new ChefLook { Hat = "cap", Face = "afro", Skin = "skin6", Accessory = "aviators", Outfit = "wok" };
            profile.SetLook(1, dressed);

            var read = JsonUtility.FromJson<ChefProfile>(JsonUtility.ToJson(profile));
            Assert.That(read.Upgrade(), Is.True);
            Assert.That(read.Look(1).SameAs(dressed), Is.True);
            Assert.That(read.Look(0).SameAs(new ChefLook()), Is.True, "a slot that never dressed is the default chef");
            Assert.That(read.Looks.Count, Is.EqualTo(ChefProfile.Slots));
        }

        [Test]
        public void ADamagedOrOldLookIsRepairedToTheDefault()
        {
            var profile = JsonUtility.FromJson<ChefProfile>(
                "{\"Version\":1,\"Looks\":[{\"Hat\":\"sombrero\",\"Face\":\"afro\",\"Skin\":\"\",\"Accessory\":\"monocle\",\"Outfit\":\"suit\"}]}");
            Assert.That(profile.Upgrade(), Is.True);
            var look = profile.Look(0);
            Assert.That(look.Hat, Is.EqualTo("toque"), "an item no longer on the rail");
            Assert.That(look.Face, Is.EqualTo("afro"), "a good item is kept");
            Assert.That(look.Skin, Is.EqualTo("skin1"));
            Assert.That(look.Accessory, Is.EqualTo("none"));
            Assert.That(look.Outfit, Is.EqualTo("whites"), "a crazy item that hasn't been earned");
        }

        [Test]
        public void AnEarnedCrazyItemIsKept()
        {
            var profile = new ChefProfile { Unlocked = new List<string> { "horns" } };
            profile.SetLook(0, new ChefLook { Hat = "horns" });
            Assert.That(profile.Upgrade(), Is.True);
            Assert.That(profile.Look(0).Hat, Is.EqualTo("horns"));
        }

        [Test]
        public void AProfileFromANewerGameIsRefused()
        {
            var profile = new ChefProfile { Version = ChefProfile.CurrentVersion + 1 };
            Assert.That(profile.Upgrade(), Is.False);
        }

        [Test]
        public void BrowsingWrapsRoundModulesAndItems()
        {
            var session = new TailorSession(new ChefLook(), NothingEarned);
            Assert.That(session.Module, Is.EqualTo(WardrobeModule.Hat));
            session.StepModule(-1);
            Assert.That(session.Module, Is.EqualTo(WardrobeModule.Outfit));
            session.StepModule(1);

            var hats = Wardrobe.Options(WardrobeModule.Hat);
            session.StepItem(-1);
            Assert.That(session.Preview.Hat, Is.EqualTo(hats[hats.Count - 1].Id));
            session.StepItem(1);
            Assert.That(session.Preview.Hat, Is.EqualTo(hats[0].Id));
        }

        [Test]
        public void AnOpenItemIsWornAndSaves()
        {
            var session = new TailorSession(new ChefLook(), NothingEarned);
            Assert.That(session.StepItem(1), Is.True, "the cap is open");
            Assert.That(session.Worn.Hat, Is.EqualTo("cap"));
            Assert.That(session.Preview.Hat, Is.EqualTo("cap"));
        }

        [Test]
        public void ALockedItemShowsButIsNotWorn()
        {
            var session = new TailorSession(new ChefLook { Hat = "headband" }, NothingEarned);
            Assert.That(session.StepItem(1), Is.False, "the horns are locked");
            Assert.That(session.Preview.Hat, Is.EqualTo("horns"));
            Assert.That(session.ItemLocked, Is.True);
            Assert.That(session.Worn.Hat, Is.EqualTo("headband"), "still wearing the last open hat");

            session.Leave();
            Assert.That(session.Preview.Hat, Is.EqualTo("headband"), "leaving drops the locked preview");
        }

        [Test]
        public void ThePlayerCarriesEveryItemOnTheRail()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PF_Player.prefab");
            if (player == null)
                Assert.Ignore("Build the greybox kitchen first.");
            var wardrobe = player.GetComponentInChildren<Game.Presentation.ChefWardrobe>(true);
            Assert.That(wardrobe, Is.Not.Null, "the player has no wardrobe");
            var so = new SerializedObject(wardrobe);
            foreach (var (module, field) in new[] { (WardrobeModule.Hat, "_hats"), (WardrobeModule.Face, "_faces"),
                (WardrobeModule.Skin, "_skins"), (WardrobeModule.Accessory, "_accessories"), (WardrobeModule.Outfit, "_outfits") })
            {
                var list = so.FindProperty(field);
                var ids = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue).ToList();
                foreach (var item in Wardrobe.Options(module))
                    Assert.That(ids, Does.Contain(item.Id), $"{module} {item.Id} isn't built on the player");
            }
        }

        [Test]
        public void ALockedItemIsWornOnceEarned()
        {
            var session = new TailorSession(new ChefLook { Hat = "headband" }, new[] { "horns" });
            Assert.That(session.StepItem(1), Is.True);
            Assert.That(session.Worn.Hat, Is.EqualTo("horns"));
        }
    }
}
