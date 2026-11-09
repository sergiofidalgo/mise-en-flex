using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Checks the character kit (concepts v9.2 → v16, 2026-11-09, SM_Characters) keeps the
    /// promises that gameplay and readability rest on: head and hat are about 38% of the height
    /// (v16), big enough to carry identity from above; the hands reach what's carried, so it
    /// doesn't float; the legs stand on the floor; and nothing a person wears reaches past the
    /// body capsule by more than a little, so hair, hats, legs and arms don't show through walls
    /// and counters in any layout.
    /// </summary>
    public class CharacterProportionTests
    {
        private const string ModelPath = "Assets/_Project/Art/Models/SM_Characters.fbx";

        // Must match CharacterArt and build_characters() in blender/build_pilot_models.py.
        private const float WaistHeight = 0.65f;
        private const float ShoulderOut = 0.25f;
        private const float ShoulderUp = 0.38f;
        private static readonly Vector2 Hip = new Vector2(0.1f, 0.48f);
        // The player's carry anchor, and how far either side of it each hand grips (ChefArmsView).
        private static readonly Vector3 CarryAnchor = new Vector3(0f, 1.1f, 0.56f);
        private const float GripWidth = 0.17f;
        private const float Reach = 0.38f;
        private const float Tolerance = 0.01f;

        // Everything worn on the body or head; arms hang from the shoulders and are checked apart.
        private static readonly string[] Worn =
        {
            "CharHips", "CharDress", "CharApron", "CharTorso", "CharScarf", "CharHead",
            "HairBack", "HairCrop", "HairAfro", "HairBun", "HairFringe",
            "HatToque", "HatToqueBand", "HatCap", "HatCapPeak", "HatBandana", "HatHeadband", "HatHorns",
            "HatSunHat", "HatSunHatBand",
            "PropBackpack", "PropSatchel",
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
        public void HeadAndToqueAreAboutAThirdOfTheHeight()
        {
            // Upper-body parts are modelled from the waist.
            var headBottom = WaistHeight + Part("CharHead").bounds.min.y;
            var top = WaistHeight + Part("HatToque").bounds.max.y;
            var share = (top - headBottom) / top;
            Assert.That(share, Is.InRange(0.35f, 0.42f), $"head + toque is {share:P0} of {top:F2} m");
        }

        [Test]
        public void NothingWornReachesPastTheLimit()
        {
            foreach (var name in Worn)
                Assert.That(RadiusFromCentre(Part(name)), Is.LessThanOrEqualTo(Reach + Tolerance), name);
        }

        [Test]
        public void LegsStandOnTheFloorWithinTheLimit()
        {
            // Legs are modelled from the hip, hanging straight down.
            Assert.That(Hip.y + Part("CharShoe").bounds.min.y, Is.EqualTo(0f).Within(0.005f), "The shoe's sole is the floor.");
            foreach (var name in new[] { "CharLeg", "CharShoe" })
            {
                var furthest = Part(name).vertices.Max(v => new Vector2(Hip.x + Mathf.Abs(v.x), v.z).magnitude);
                Assert.That(furthest, Is.LessThanOrEqualTo(Reach + Tolerance), name);
            }
        }

        [Test]
        public void HandsReachWhatsCarried()
        {
            // The user's playtest: carried things floated in front of the chef. From each shoulder to
            // its grip on the carried thing must be within the arm and mitten's reach.
            var reach = -Part("CharHand").bounds.min.y;
            var shoulder = new Vector3(ShoulderOut, WaistHeight + ShoulderUp, 0f);
            var grip = CarryAnchor + Vector3.right * GripWidth;
            Assert.That(Vector3.Distance(shoulder, grip), Is.LessThanOrEqualTo(reach + 0.02f));
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
