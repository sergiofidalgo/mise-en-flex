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

        /// <summary>Starts a street's random draws afresh, so the same street is built the same way every time.</summary>
        public static void StreetSeed(int seed) => _random = new System.Random(seed);

        /// <summary>
        /// One of the city's cars (sedan, hatchback, taxi or van, in a paint of its own), its front
        /// along +Z. Nothing on it is static, so it can drive.
        /// </summary>
        public static Transform StreetCar(Transform parent)
        {
            _random ??= new System.Random(Seed);
            var car = Car(parent, 0f, 0f);
            MakeDynamic(car.gameObject);
            return car;
        }

        /// <summary>
        /// One of the city's low buildings of shops and flats, its front (local +Z) turned
        /// <paramref name="yaw"/> degrees; <paramref name="at"/> is the middle of its footprint.
        /// Without the shops' own lamps: a restaurant's street is lit by its day, not by them.
        /// </summary>
        public static Transform StreetBuilding(Transform parent, Vector3 at, float yaw, float width, float depth, float height, int index, int sideWindows = 0)
        {
            _random ??= new System.Random(Seed);
            var building = RowBuilding(parent, at, yaw, width, depth, height, index, StreetArt.Tags(), StreetArt.Murals(), sideWindows);
            foreach (var light in building.GetComponentsInChildren<Light>(true))
                Object.DestroyImmediate(light.gameObject);
            return building;
        }

        /// <summary>
        /// Paints one of the city's murals (0 the hat, 1 the sunset, 2 the steak) on a side wall of a
        /// <see cref="StreetBuilding"/> of this width and height: the wall on its local +X, or −X.
        /// </summary>
        public static void StreetMural(Transform building, float width, float height, bool plusX, int mural, float size)
        {
            var murals = StreetArt.Murals();
            var side = plusX ? 1f : -1f;
            Decal(building, murals[mural % murals.Length], new Vector3(side * (width / 2f + 0.03f), -height + size / 2f + 1.6f, 0f),
                new Vector2(size, size), side * -90f);
        }

        /// <summary>One of the city's trees, standing in the ground.</summary>
        public static void StreetTree(Transform parent, Vector3 at, float scale)
        {
            Model(parent, "TreeTrunk", CityModel, "CityTreeTrunk", Toon("CityTreeTrunk", new Color(0.32f, 0.22f, 0.15f)), at, 0f, scale: scale);
            Model(parent, "TreeCrown", CityModel, "CityTreeCrown", Toon("CityTreeCrown", new Color(0.2f, 0.36f, 0.26f)), at, 0f, scale: scale);
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
