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
            // And without the city's own tags and murals on its shutters and fronts: here the only
            // graffiti is what the street asks for (the user's note, 2026-11-12: only by the parking lot).
            foreach (var child in building.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name == "Graffiti")
                    Object.DestroyImmediate(child.gameObject);
            // Its walls in brick or stone blocks (the user's note, 2026-11-12: "buildings could also
            // use some texture"), in the colour the city gave it. A material of the street's own, so
            // the night city's flat walls stay as they are.
            var body = building.Find("Body");
            if (body != null && body.TryGetComponent(out Renderer walls))
            {
                var colour = walls.sharedMaterial.GetColor("_BaseColor");
                var stone = colour.b > colour.r; // the cool colours in larger blocks
                walls.sharedMaterial = HouseStyle.RoomMaterial($"StreetBlock_{ColorUtility.ToHtmlStringRGB(colour)}", "BRICK", m =>
                {
                    m.SetColor("_BaseColor", colour);
                    m.SetColor("_SeamColor", colour * (stone ? 0.84f : 0.76f));
                    m.SetFloat("_Size", stone ? 0.45f : 0.2f);
                    m.SetFloat("_Length", stone ? 1.1f : 0.52f);
                    m.SetFloat("_SeamWidth", stone ? 0.04f : 0.028f);
                    m.SetFloat("_Variation", 0.07f);
                });
            }
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

        /// <summary>
        /// Puts a few old graffiti tags low on the same side wall, under and beside
        /// its mural (the user's note, 2026-11-12: "perhaps some graffiti on the building?"): each
        /// a different tag, at head height or below, along the wall's <paramref name="depth"/>.
        /// </summary>
        public static void StreetTags(Transform building, float width, float depth, float height, bool plusX, int first)
        {
            var tags = StreetArt.OldTags(); // years old: bleached, flaking, sooty
            var side = plusX ? 1f : -1f;
            // Along the wall (as a share of its depth), how high its middle is, and how wide it's sprayed.
            var spots = new[] { (-0.34f, 1.35f, 2.6f), (0.03f, 0.85f, 2.1f), (0.33f, 1.5f, 2.9f), (-0.12f, 0.6f, 1.5f) };
            for (var k = 0; k < spots.Length; k++)
            {
                var (along, up, across) = spots[k];
                Decal(building, tags[(first + k) % tags.Length], new Vector3(side * (width / 2f + 0.035f + k * 0.002f), -height + up, along * depth),
                    new Vector2(across, across * 0.4f), side * -90f);
            }
        }

        /// <summary>
        /// A manhole cover in the road with steam rising from it, as the city's streets have (the
        /// user's reference, 2026-11-12: steam coming out of the road, as in New York).
        /// </summary>
        public static void StreetManhole(Transform parent, Vector3 at)
        {
            _random ??= new System.Random(Seed);
            Shape(parent, PrimitiveType.Cylinder, "Manhole", Toon("StreetManhole", new Color(0.12f, 0.11f, 0.12f)), at + Vector3.up * 0.012f, new Vector3(0.9f, 0.01f, 0.9f));
            Shape(parent, PrimitiveType.Cylinder, "ManholeRim", Toon("StreetManholeRim", new Color(0.3f, 0.3f, 0.32f)), at + Vector3.up * 0.008f, new Vector3(1.06f, 0.008f, 1.06f));
            Steam(parent, at + Vector3.up * 0.04f);
        }

        /// <summary>One of the city's trees, standing in the ground.</summary>
        public static void StreetTree(Transform parent, Vector3 at, float scale)
        {
            Model(parent, "TreeTrunk", CityModel, "CityTreeTrunk", Toon("CityTreeTrunk", new Color(0.32f, 0.22f, 0.15f)), at, 0f, scale: scale);
            Model(parent, "TreeCrown", CityModel, "CityTreeCrown", Toon("CityTreeCrown", new Color(0.2f, 0.36f, 0.26f)), at, 0f, scale: scale);
        }

    }
}
