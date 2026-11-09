using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Checks the v9.2 character kit (concept 2026-11-09, SM_Characters) keeps the promises that
    /// gameplay and readability rest on: head and hat are about 45% of the height, so identity
    /// reads from above; and nothing a person wears reaches past the body capsule by more than a
    /// little, so hair, hats and arms don't show through walls and counters in any layout.
    /// </summary>
    public class CharacterProportionTests
    {
        private const string ModelPath = "Assets/_Project/Art/Models/SM_Characters.fbx";

        // Must match CharacterArt and build_characters() in blender/build_pilot_models.py.
        private const float WaistHeight = 0.45f;
        private const float ShoulderOut = 0.29f;
        private const float Reach = 0.38f;
        private const float Tolerance = 0.01f;

        // Everything worn on the body or head; arms hang from the shoulders and are checked apart.
        private static readonly string[] Worn =
        {
            "CharLegs", "CharShoes", "CharHips", "CharDress", "CharApron", "CharTorso", "CharScarf", "CharHead",
            "HairBack", "HairCrop", "HairAfro", "HairBun", "HairFringe",
            "HatToque", "HatToqueBand", "HatCap", "HatCapPeak", "HatBandana", "HatHeadband", "HatHorns",
            "HatSunHat", "HatSunHatBand",
        };

        private static Mesh Part(string name)
        {
            if (AssetDatabase.LoadMainAssetAtPath(ModelPath) == null)
                Assert.Ignore($"{ModelPath} hasn't been exported from Blender yet.");
            var mesh = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().FirstOrDefault(m => m.name == name);
            Assert.That(mesh, Is.Not.Null, $"{ModelPath} has no mesh called {name}");
            return mesh;
        }

        // The furthest any vertex is from the vertical axis through the feet.
        private static float RadiusFromCentre(Mesh mesh) =>
            mesh.vertices.Max(v => new Vector2(v.x, v.z).magnitude);

        [Test]
        public void HeadAndToqueAreAboutHalfTheHeight()
        {
            // Upper-body parts are modelled from the waist.
            var headBottom = WaistHeight + Part("CharHead").bounds.min.y;
            var top = WaistHeight + Part("HatToque").bounds.max.y;
            var share = (top - headBottom) / top;
            Assert.That(share, Is.InRange(0.42f, 0.52f), $"head + toque is {share:P0} of {top:F2} m");
        }

        [Test]
        public void NothingWornReachesPastTheLimit()
        {
            foreach (var name in Worn)
                Assert.That(RadiusFromCentre(Part(name)), Is.LessThanOrEqualTo(Reach + Tolerance), name);
        }

        [Test]
        public void HangingArmsStayWithinTheLimit()
        {
            // Arms are modelled from the shoulder, hanging straight down.
            foreach (var name in new[] { "CharArm", "CharHand" })
            {
                var sideways = Part(name).vertices.Max(v => Mathf.Abs(v.x));
                Assert.That(ShoulderOut + sideways, Is.LessThanOrEqualTo(Reach + Tolerance), name);
            }
        }

        [Test]
        public void HatsSitOnTheHead()
        {
            // These hats' lowest point is above the eyes, so none covers them. (A cap and a bandana
            // come lower, at the back and over the brows.)
            var headCentre = Part("CharHead").bounds.center.y;
            foreach (var name in new[] { "HatToque", "HatToqueBand", "HatHeadband", "HatHorns", "HatSunHat" })
                Assert.That(Part(name).bounds.min.y, Is.GreaterThan(headCentre - 0.02f), name);
        }
    }
}
