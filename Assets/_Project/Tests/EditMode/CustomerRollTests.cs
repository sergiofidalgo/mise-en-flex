using System.Linq;
using Game.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// A customer's hat is rolled like the rest of them (the user's call, 2026-11-10: the modelled
    /// hats on customers). Checks the built prefab keeps the under-hat rule whatever comes up:
    /// under a hat that covers the head the top of the hair is hidden, or it would poke through,
    /// and on a bare head it shows; nobody wears two hats; and every archetype's hats do come up.
    /// </summary>
    public class CustomerRollTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/PF_Customer.prefab";
        // The groups CharacterArt builds for hats that cover the top of the head (CharacterArt.Covers).
        private static readonly string[] Covering = { "Hat_Cap", "Hat_SunHat", "Hat_Cowboy", "Hat_Straw", "Hat_Captain", "Hat_Plumed" };

        [Test]
        public void NoHairShowsThroughARolledHat()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            var rolls = prefab.GetComponentsInChildren<CharacterRoll>(true);
            if (rolls.Length == 0)
                Assert.Ignore("The customer prefab was built without the character kit.");
            var customer = Object.Instantiate(prefab);
            try
            {
                foreach (var roll in customer.GetComponentsInChildren<CharacterRoll>(true))
                {
                    var archetype = roll.transform.parent != null ? roll.transform.parent.name : roll.name;
                    var parts = roll.GetComponentsInChildren<Transform>(true);
                    var hats = parts.Where(t => t.name.StartsWith("Hat_")).ToArray();
                    var tops = parts.Where(t => t.name == "Top" && t.parent.name.StartsWith("Hair_")).ToArray();
                    Assert.That(hats, Is.Not.Empty, $"{archetype} has no hats to roll");
                    Assert.That(tops, Is.Not.Empty, $"{archetype} has no hair that hides under a hat");

                    var random = new System.Random(7);
                    var seen = new System.Collections.Generic.HashSet<string>();
                    var bare = 0;
                    for (var i = 0; i < 300; i++)
                    {
                        roll.Roll(random);
                        var worn = hats.Where(h => h.gameObject.activeSelf).Select(h => h.name).ToArray();
                        Assert.That(worn.Length, Is.LessThanOrEqualTo(1), $"{archetype} wears {string.Join(" and ", worn)}");
                        if (worn.Length == 0)
                            bare++;
                        else
                            seen.Add(worn[0]);
                        var covered = worn.Any(Covering.Contains);
                        foreach (var top in tops)
                            Assert.That(top.gameObject.activeSelf, Is.EqualTo(!covered),
                                $"{archetype}: the top of {top.parent.name} under {(worn.Length > 0 ? worn[0] : "no hat")}");
                    }
                    Assert.That(seen.Count, Is.EqualTo(hats.Length), $"{archetype}: every hat comes up ({string.Join(", ", seen)})");
                    Assert.That(bare, Is.GreaterThan(0), $"{archetype}: some go bare-headed");
                }
            }
            finally
            {
                Object.DestroyImmediate(customer);
            }
        }
    }
}
