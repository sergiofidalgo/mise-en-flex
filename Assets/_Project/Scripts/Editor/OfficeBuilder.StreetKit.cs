using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The pieces of the city's life that the restaurants' streets use too (the user's request,
    /// 2026-11-11: the street outside the kitchen should have the city's details), so a pigeon or
    /// a puff of steam is the same thing in both places.
    /// </summary>
    public static partial class OfficeBuilder
    {
        /// <summary>A pecking pigeon that can fly off: one of the birds a <c>TerraceBirdsView</c> moves.</summary>
        public static Transform StreetPigeon(Transform parent, Vector3 at, float facing)
        {
            _random ??= new System.Random(Seed);
            return PerchedBird(parent, at, facing, canFly: true);
        }

        /// <summary>Soft puffs rising from <paramref name="parent"/>, not emitting until something sets their rate.</summary>
        public static ParticleSystem StreetPuffs(Transform parent, string name, float lifetime, float speed, float minSize, float maxSize, Color colour)
        {
            var puffs = Puffs(parent, name, 0f, lifetime, speed, minSize, maxSize);
            var main = puffs.main;
            main.startColor = colour;
            return puffs;
        }
    }
}
