using Game.Presentation.Office.Ambience;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The street at the foot of the boss's tower, where the opening starts (docs/opening-flow.md;
    /// user's note, 2026-11-09: "start at a street, add clutter, movement, details, graffitis"). A
    /// road along the tower's south side between two pavements: the tower's lobby with its canopy
    /// and its own red carpet to the kerb on one side; across the road a row of low shops and flats
    /// with lit windows, awnings, neon, fire escapes and graffitied shutters and walls. In front of
    /// the tower the street is closed to traffic for a night market (the user's notes, 2026-11-10:
    /// no vehicle near the lens, a street in full swing); beyond the barriers cars park, a food
    /// truck serves a queue, steam rises from the manholes, pigeons peck. The camera starts down
    /// here and climbs to the penthouse 120 m up.
    /// </summary>
    public static partial class OfficeBuilder
    {
        private static float Ground => -CityDepth;
        // The road runs along x; its kerbs and pavements at these z.
        private const float TowerFace = -13.8f;
        private const float NorthKerb = -20f;
        private const float SouthKerb = -28f;
        private const float SouthFronts = -32f;
        private const float StreetEnd = 90f;

        // Kept clear of the city's other buildings: the road, both pavements and the row across it.
        private static Rect StreetBounds => new Rect(-StreetEnd - 5f, SouthFronts - 18f, 2f * StreetEnd + 10f, TowerFace - (SouthFronts - 18f) - 0.5f);


        private static readonly Color Asphalt = new Color(0.17f, 0.17f, 0.2f);
        private static readonly Color Paving = new Color(0.42f, 0.38f, 0.37f);
        private static readonly Color Kerb = new Color(0.55f, 0.53f, 0.5f);
        private static readonly Color Iron = new Color(0.16f, 0.15f, 0.17f);

        private static void BuildStreet(Transform city)
        {
            var street = new GameObject("Street").transform;
            street.SetParent(city, false);
            street.localPosition = Vector3.up * Ground;

            // The ground everywhere below the city, so nothing looks into a void from the street.
            Cube(street, "Ground", Toon("StreetGround", new Color(0.12f, 0.12f, 0.15f)), new Vector3(0f, -0.2f, -120f), new Vector3(700f, 0.2f, 760f), collider: false);
            Cube(street, "Road", Toon("StreetAsphalt", Asphalt), new Vector3(0f, 0f, (NorthKerb + SouthKerb) / 2f), new Vector3(2f * StreetEnd, 0.02f, NorthKerb - SouthKerb));
            var paint = Toon("StreetPaint", new Color(0.95f, 0.85f, 0.45f));
            for (var x = -StreetEnd; x < StreetEnd; x += 6f)
                Cube(street, "LaneDash", paint, new Vector3(x, 0.02f, (NorthKerb + SouthKerb) / 2f), new Vector3(2.6f, 0.01f, 0.18f));
            for (var z = SouthKerb + 0.8f; z < NorthKerb - 0.4f; z += 1.0f)
                Cube(street, "Crossing", Toon("StreetWhite", new Color(0.9f, 0.9f, 0.88f)), new Vector3(0f, 0.02f, z), new Vector3(3.2f, 0.01f, 0.55f)); // from the tower's carpet to the avenue
            var paving = Tiles("StreetPaving", Paving, new Color(0.32f, 0.29f, 0.28f), 1.2f);
            Cube(street, "Pavement", paving, new Vector3(0f, 0f, (NorthKerb + TowerFace) / 2f), new Vector3(2f * StreetEnd, 0.15f, TowerFace - NorthKerb));
            Block(street, "Kerb", Toon("StreetKerb", Kerb), new Vector3(0f, 0f, NorthKerb), new Vector3(2f * StreetEnd, 0.18f, 0.25f));
            // The south pavement and kerb stop either side of the avenue's road.
            foreach (var sign in new[] { -1f, 1f })
            {
                var length = StreetEnd - AvenueRoad;
                var x = sign * (AvenueRoad + length / 2f);
                Cube(street, "Pavement", paving, new Vector3(x, 0f, (SouthFronts + SouthKerb) / 2f), new Vector3(length, 0.15f, SouthKerb - SouthFronts));
                Block(street, "Kerb", Toon("StreetKerb", Kerb), new Vector3(x, 0f, SouthKerb), new Vector3(length, 0.18f, 0.25f));
            }

            TowerEntrance(street); // OfficeBuilder.Entrance.cs: the restaurant, the queue, the chef
            StreetRow(street);
            StreetFurniture(street);
            StreetMarket(street);  // OfficeBuilder.Entrance.cs: the night market on the closed street
            StreetPeople(street);
            BuildAvenue(street); // OfficeBuilder.Avenue.cs: the avenue the opening glides up
        }

        // In front of the tower's entrance, where the restaurant's crowd stands.
        private static bool InFrontOfTower(float x) => Mathf.Abs(x) < 19f;

        // Half the stretch of the tower's street closed to traffic for the night market, between barriers.
        private const float StreetClosed = 24f;

        // The row across the road: low shops and flats, their fronts facing the tower, either side
        // of the avenue's mouth (OfficeBuilder.Avenue.cs). Each side starts flush with the mouth, so
        // the two corner buildings' side walls line the avenue: the market's restaurants have their
        // fronts on them (OfficeBuilder.Entrance.cs).
        private static void StreetRow(Transform street)
        {
            var tags = StreetArt.Tags();
            var murals = StreetArt.Murals();
            var i = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                var x = AvenueFronts; // how far out from the avenue's middle this building starts
                while (x < StreetEnd)
                {
                    var w = 8f + (float)_random.NextDouble() * 6f;
                    const float d = 12f;
                    var height = 12f + (float)_random.NextDouble() * 10f; // 4–7 storeys: the camera looks over them
                    // The two at the avenue's mouth have windows down the side that faces it too.
                    var corner = Mathf.Approximately(x, AvenueFronts);
                    RowBuilding(street, new Vector3(side * (x + w / 2f), 0f, SouthFronts - d / 2f), 0f, w, d, corner ? Mathf.Max(height, 17f) : height, i, tags, murals, corner ? (side < 0f ? 1 : -1) : 0);
                    x += w + 2f; // an alley between buildings
                    i++;
                }
            }
        }

        // A low building of shops and flats, its front (local +Z) turned `yaw` degrees: a lit shop
        // under an awning (or a tagged shutter), flats above with framed windows, neon, fire escapes,
        // a mural on some side walls. `at` is the middle of its footprint, on the ground.
        private static Transform RowBuilding(Transform parent, Vector3 at, float yaw, float w, float d, float height, int i, Material[] tags, Material[] murals, int sideWindows = 0)
        {
            var c = _random.Next(FacadeColors.Length);
            var wall = Toon($"OfficeFacade{c}", Color.Lerp(FacadeColors[c], Dusk, 0.18f)); // as the city's, same material
            var trim = Toon($"OfficeFacadeTrim{c}", Color.Lerp(FacadeColors[c], Cream, 0.55f));
            var building = new GameObject("RowBuilding").transform;
            building.SetParent(parent, false);
            building.localPosition = at + Vector3.up * height;
            building.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Model(building, "Body", CityModel, "CityBlock", wall, new Vector3(0f, -height, 0f), 0f, scale3: new Vector3(w, height, d));
            Model(building, "Cornice", CityModel, "CityCornice", trim, Vector3.zero, 0f, scale3: new Vector3(w + 0.4f, 1f, d + 0.4f));
            // Flats above, their windows facing the street (side 2), framed.
            Windows(building, w, d, Mathf.FloorToInt((height - 4.5f) / 3.2f), trim, side: 2, framed: true);
            if (sideWindows != 0) // above the restaurant that fronts on this side (OfficeBuilder.Entrance.cs)
                Windows(building, w, d, Mathf.FloorToInt((height - 6.5f) / 3.2f), trim, side: sideWindows, framed: true);
            var front = new Vector3(0f, -height, d / 2f);

            // The ground floor: a lit shop under an awning, or a shutter covered in tags.
            if (i % 3 != 2)
            {
                LitShop(building, front, w, i);
            }
            else
            {
                Block(building, "Shutter", Toon("StreetShutter", new Color(0.5f, 0.5f, 0.52f)), front + new Vector3(0f, 0.15f, 0.05f), new Vector3(w - 1.4f, 3f, 0.08f));
                for (var y = 0.4f; y < 3.1f; y += 0.25f)
                    Block(building, "ShutterSlat", Toon("StreetShutterLine", new Color(0.38f, 0.38f, 0.4f)), front + new Vector3(0f, y, 0.1f), new Vector3(w - 1.4f, 0.03f, 0.02f));
                Decal(building, tags[_random.Next(tags.Length)], front + new Vector3(0f, 1.7f, 0.12f), new Vector2(Mathf.Min(w - 2f, 5f), 2f), 180f);
            }
            // Tags higher up and on side walls; a mural on the side of every fourth building.
            if (_random.NextDouble() < 0.5)
                Decal(building, tags[_random.Next(tags.Length)], front + new Vector3(-w * 0.25f, 3.9f, 0.04f), new Vector2(2.4f, 1f), 180f);
            if (i % 4 == 1)
                Decal(building, murals[(i / 4) % murals.Length], new Vector3(w / 2f + 0.03f, -height + 4.5f, d / 2f - 4f), new Vector2(6f, 6f), -90f);
            // A fire escape zigzagging up some fronts.
            if (i % 3 == 1)
                FireEscape(building, front + new Vector3(w * 0.22f, 0f, 0f), height);
            return building;
        }

        // A lit shopfront across `w` metres of wall at `front` (local, facing +z): glowing windows in
        // modelled frames under a striped awning, light spilling out, the place's name in neon on
        // every other one.
        private static void LitShop(Transform building, Vector3 front, float w, int i)
        {
            Cube(building, "ShopGlow", Unlit($"StreetShop{i % 4}", ShopGlow(i)), front + new Vector3(0f, 0.6f, 0.03f), new Vector3(w - 1.6f, 2.4f, 0.02f));
            for (var a = -w / 2f + 1.5f; a < w / 2f - 1f; a += 3.1f)
                Model(building, "ShopFrame", CityModel, "CityShopFrame", Toon("CityShopTrim", new Color(0.34f, 0.21f, 0.14f)), front + new Vector3(a + 1.2f, 1.8f, 0f), 180f);
            var stripe = Toon($"CityAwning{i % 4}", AwningStripes[i % AwningStripes.Length]);
            var cream = Toon("CityAwningCream", new Color(0.93f, 0.88f, 0.78f));
            for (var a = -w / 2f + 1f; a < w / 2f - 0.5f; a += 1f)
            {
                Model(building, "Awning", CityModel, "CityAwningA", stripe, front + new Vector3(a, 3.3f, 0f), 180f);
                Model(building, "Awning", CityModel, "CityAwningB", cream, front + new Vector3(a, 3.3f, 0f), 180f);
            }
            Point(building, "ShopSpill", front + new Vector3(0f, 2.2f, 2f), ShopGlow(i), 6f, 1.6f);
            // The place's name in neon over some shops, buzzing on and off now and then.
            if (i % 2 == 0)
                WallNeon(building, front + new Vector3(-w * 0.14f, 4.4f, 0f), w * 0.62f, i); // left of any blade sign
        }

        private static readonly Color[] AwningStripes =
        {
            new Color(0.78f, 0.22f, 0.18f), new Color(0.2f, 0.48f, 0.36f), new Color(0.22f, 0.32f, 0.58f), new Color(0.9f, 0.6f, 0.2f),
        };

        private static Color ShopGlow(int i) => (i % 4) switch
        {
            0 => new Color(1f, 0.82f, 0.5f),
            1 => new Color(1f, 0.92f, 0.75f),
            2 => new Color(0.9f, 0.95f, 1f),
            _ => new Color(1f, 0.7f, 0.45f),
        };

        // Lamps, trees, bins, hydrants, newspaper boxes, a bus stop, a café's tables, parked cars,
        // a food truck with its queue, steam from the manholes, pigeons.
        private static void StreetFurniture(Transform street)
        {
            var northWalk = (TowerFace + NorthKerb) / 2f;
            var southWalk = (SouthFronts + SouthKerb) / 2f;
            for (var x = -StreetEnd + 6f; x < StreetEnd; x += 13f)
            {
                if (x < RestaurantDoorX - 2f || x > RestaurantEast) // none in front of the restaurant's windows: it's seen from across the street
                    StreetLight(street, new Vector3(x, 0.15f, NorthKerb + 0.6f));
                if (!InAvenueMouth(x + 6.5f))
                    StreetLight(street, new Vector3(x + 6.5f, 0.15f, SouthKerb - 0.6f));
                if (!InFrontOfTower(x + 3f))
                    Tree(street, new Vector3(x + 3f, 0.15f, NorthKerb + 1.4f));
            }
            var bags = Toon("StreetBinBag", new Color(0.12f, 0.12f, 0.13f));
            for (var k = 0; k < 7; k++)
            {
                var x = -StreetEnd + 10f + k * 25f + (float)_random.NextDouble() * 6f;
                var z = k % 2 == 0 ? southWalk + 1f : northWalk;
                if (InAvenueMouth(x, 5f) || InFrontOfTower(x - 3f) || InFrontOfTower(x + 0.7f))
                    continue;
                Model(street, "Bin", "SM_Bin", "BinBody", Toon("StreetBin", new Color(0.25f, 0.42f, 0.32f)), new Vector3(x, 0.15f, z), (float)_random.NextDouble() * 360f);
                Shape(street, PrimitiveType.Sphere, "BinBag", bags, new Vector3(x + 0.7f, 0.45f, z), new Vector3(0.7f, 0.6f, 0.6f));
                Kit.Hydrant(street, new Vector3(x + 4f, 0.15f, SouthKerb - 0.5f));
                Block(street, "NewsBox", Toon(k % 2 == 0 ? "StreetNewsBlue" : "StreetNewsRed", k % 2 == 0 ? new Color(0.25f, 0.4f, 0.75f) : new Color(0.85f, 0.3f, 0.25f)),
                    new Vector3(x - 3f, 0.15f, NorthKerb + 0.7f), new Vector3(0.5f, 1.0f, 0.45f));
                Block(street, "Crate", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), new Vector3(x + 2f, 0.15f, SouthFronts + 0.6f), new Vector3(0.6f, 0.5f, 0.6f));
            }

            // A bus stop: a glass shelter, a bench, a lit advert, people waiting.
            var stop = new Vector3(28f, 0.15f, SouthKerb - 1.6f);
            Block(street, "ShelterRoof", Toon("StreetShelter", new Color(0.3f, 0.32f, 0.36f)), stop + Vector3.up * 2.5f, new Vector3(4f, 0.12f, 1.6f));
            Cube(street, "ShelterBack", PilotArt.EnsureGlassMaterial("OfficeWindow", new Color(0.62f, 0.76f, 0.86f, 0.16f)), stop + new Vector3(0f, 0f, -0.75f), new Vector3(4f, 2.5f, 0.04f));
            Cube(street, "ShelterAdvert", Unlit("StreetAdvert", new Color(0.95f, 0.75f, 0.55f)), stop + new Vector3(1.6f, 0.4f, -0.72f), new Vector3(0.9f, 1.8f, 0.02f));
            Block(street, "Bench", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), stop + new Vector3(-0.5f, 0f, -0.4f), new Vector3(2.2f, 0.45f, 0.45f));
            Point(street, "ShelterLight", stop + new Vector3(0f, 2.2f, 0f), new Color(0.9f, 0.95f, 1f), 5f, 1.6f);
            for (var k = 0; k < 2; k++)
                Passerby(street, "Waiting", stop + new Vector3(-1.2f + k * 1.1f, 0f, 0.3f), 0f);

            // A café's tables out on the pavement, under string lights.
            for (var k = 0; k < 2; k++)
                Prop(street, "PF_Table", new Vector3(-30f + k * 2.6f, 0.15f, southWalk), 0f);
            var bulb = Unlit("OfficeBulb", Bulb);
            for (var k = 0; k < 10; k++)
                TwinklingBulb(street, bulb, new Vector3(-33f + k * 0.85f, 3.0f - Mathf.Sin(k / 9f * Mathf.PI) * 0.3f, SouthFronts + 0.4f), 5f + (float)_random.NextDouble() * 3f, 0.92f);
            Point(street, "CafeGlow", new Vector3(-29f, 2.5f, southWalk), Incandescent, 6f, 2f);
            Passerby(street, "CafeGuest", new Vector3(-30.6f, 0.15f, southWalk + 0.6f), 160f);

            // A food truck at the kerb with a queue: the game's own trade, out on the street.
            var truck = new GameObject("FoodTruck").transform;
            truck.SetParent(street, false);
            truck.localPosition = new Vector3(-50f, 0.15f, SouthKerb + 1.3f);
            Block(truck, "Body", Toon("StreetTruck", new Color(0.95f, 0.78f, 0.3f)), Vector3.zero, new Vector3(5.5f, 2.6f, 2.2f));
            Block(truck, "Cab", Toon("StreetTruck", new Color(0.95f, 0.78f, 0.3f)), new Vector3(3.3f, 0f, 0f), new Vector3(1.5f, 1.8f, 2.1f));
            Cube(truck, "Hatch", Unlit("StreetTruckHatch", new Color(1f, 0.88f, 0.6f)), new Vector3(-0.3f, 1.1f, -1.12f), new Vector3(3f, 1.1f, 0.02f));
            Block(truck, "HatchFlap", Toon("StreetTruckFlap", new Color(0.85f, 0.3f, 0.25f)), new Vector3(-0.3f, 2.25f, -1.45f), new Vector3(3.2f, 0.06f, 0.7f));
            foreach (var x in new[] { -1.8f, 1.8f })
                Shape(truck, PrimitiveType.Cylinder, "Wheel", Toon("CityTyre", new Color(0.08f, 0.08f, 0.09f)), new Vector3(x, 0.38f, -1.12f), new Vector3(0.76f, 0.1f, 0.76f), Quaternion.Euler(90f, 0f, 0f));
            Point(truck, "HatchLight", new Vector3(-0.3f, 1.8f, -2f), Incandescent, 6f, 2.4f);
            for (var k = 0; k < 3; k++)
                Passerby(street, "Queue", new Vector3(-51.2f + k * 1.1f, 0.15f, SouthKerb - 1.1f - k * 0.3f), 0f);

            // Parked cars along both kerbs.
            for (var k = 0; k < 8; k++)
            {
                var x = -StreetEnd + 15f + k * 21f + (float)_random.NextDouble() * 5f;
                if (Mathf.Abs(x) < StreetClosed + 3f || Mathf.Abs(x + 50f) < 6f)
                    continue; // the street is closed to cars in front of the tower
                var car = Car(street, k % 2 == 0 ? NorthKerb - 1.1f : SouthKerb + 1.1f, x);
                car.localRotation = Quaternion.Euler(0f, k % 2 == 0 ? 90f : -90f, 0f);
            }

            // Steam from manholes in the road, pigeons pecking on the pavements.
            foreach (var x in new[] { -20f, 15f, 47f })
            {
                Shape(street, PrimitiveType.Cylinder, "Manhole", Toon("StreetManhole", new Color(0.12f, 0.11f, 0.12f)), new Vector3(x, 0.025f, -24f), new Vector3(0.9f, 0.01f, 0.9f));
                Steam(street, new Vector3(x, 0.05f, -24f));
            }
            for (var k = 0; k < 7; k++)
            {
                var x = -10f + k * 4.3f + (float)_random.NextDouble();
                PerchedBird(street, new Vector3(x, 0.15f, k % 2 == 0 && !InAvenueMouth(x) ? southWalk : northWalk - 1f), (float)_random.NextDouble() * 360f);
            }
        }

        // People walking up and down both pavements, some pausing.
        private static void StreetPeople(Transform street)
        {
            var northWalk = (TowerFace + NorthKerb) / 2f - 1.2f;
            var southWalk = (SouthFronts + SouthKerb) / 2f + 0.4f;
            for (var k = 0; k < 14; k++)
            {
                var z = (k % 2 == 0 ? northWalk : southWalk) + ((float)_random.NextDouble() - 0.5f) * 1.2f;
                var start = -60f + (float)_random.NextDouble() * 100f;
                var length = 12f + (float)_random.NextDouble() * 20f;
                // The tower's front has its own crowd (OfficeBuilder.Entrance.cs): walkers keep clear of it.
                if (k % 2 == 0 && start < 18f && start + length > -18f)
                    start = start < 0f ? -50f - (float)_random.NextDouble() * 10f : 19f + (float)_random.NextDouble() * 20f;
                var walker = Passerby(street, "Walker", new Vector3(start, 0.15f, z), 90f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(start, 0.15f, z), new Vector3(start + length, 0.15f, z),
                    1.1f + (float)_random.NextDouble() * 0.5f, true, 0.5f + (float)_random.NextDouble() * 3f, (float)_random.NextDouble());
                MakeDynamic(walker.gameObject);
            }
        }

        // ---- Pieces ------------------------------------------------------------------------------

        // Someone in the street, in the players' character kit, dressed at random.
        private static Transform Passerby(Transform parent, string name, Vector3 at, float facing) =>
            Person(parent, name, at, facing, CasualTops[_random.Next(CasualTops.Length)],
                new Color(0.18f + (float)_random.NextDouble() * 0.2f, 0.2f, 0.28f), 0.95f + (float)_random.NextDouble() * 0.1f, look => Dress(look, 0.42f));

        // Someone at work on one of the tower's floors: the same, with only the odd cap or pair of
        // headphones for a hat.
        private static Transform Staffer(Transform parent, string name, Vector3 at, float facing) =>
            Person(parent, name, at, facing, CasualTops[_random.Next(CasualTops.Length)],
                new Color(0.18f + (float)_random.NextDouble() * 0.2f, 0.2f, 0.28f), 0.95f + (float)_random.NextDouble() * 0.1f, look => Dress(look, 0.12f, OfficeHats));

        // Set while the tower's floors are built (OfficeBuilder.Tower.cs): its people dress for work.
        private static bool _indoors;
        // Set while a group that all wear the same hat is built.
        private static CharacterArt.HatKind? _hatOverride;

        // The hats a night out brings (the user's note, 2026-11-10: the hats we modelled, to tell the
        // crowd's people apart): listed more than once to be likelier.
        private static readonly CharacterArt.HatKind[] StreetHats =
        {
            CharacterArt.HatKind.Cap, CharacterArt.HatKind.Cap, CharacterArt.HatKind.Cap, CharacterArt.HatKind.Headphones, CharacterArt.HatKind.Headphones,
            CharacterArt.HatKind.Cowboy, CharacterArt.HatKind.Straw, CharacterArt.HatKind.Captain, CharacterArt.HatKind.Plumed, CharacterArt.HatKind.Party,
            CharacterArt.HatKind.Propeller, CharacterArt.HatKind.SunHat, CharacterArt.HatKind.Bandana, CharacterArt.HatKind.Headband,
        };
        private static readonly CharacterArt.HatKind[] OfficeHats = { CharacterArt.HatKind.Headphones, CharacterArt.HatKind.Headphones, CharacterArt.HatKind.Cap };
        private static readonly Color[] HatColours =
        {
            new Color(0.2f, 0.22f, 0.3f), new Color(0.62f, 0.2f, 0.18f), new Color(0.9f, 0.86f, 0.76f), new Color(0.24f, 0.4f, 0.34f),
            new Color(0.5f, 0.34f, 0.2f), new Color(0.3f, 0.22f, 0.36f), new Color(0.86f, 0.62f, 0.22f),
        };
        private static readonly Color[] HatAccents =
        {
            new Color(0.95f, 0.78f, 0.25f), new Color(0.9f, 0.3f, 0.32f), new Color(0.3f, 0.62f, 0.85f), new Color(0.95f, 0.93f, 0.88f),
            new Color(0.4f, 0.75f, 0.45f), new Color(0.95f, 0.5f, 0.7f), new Color(0.16f, 0.15f, 0.18f),
        };
        private static readonly CharacterArt.HairKind[] StreetHair =
        {
            CharacterArt.HairKind.Crop, CharacterArt.HairKind.Crop, CharacterArt.HairKind.Crop, CharacterArt.HairKind.Bob, CharacterArt.HairKind.Bob,
            CharacterArt.HairKind.Bob, CharacterArt.HairKind.Bun, CharacterArt.HairKind.Bun, CharacterArt.HairKind.Afro, CharacterArt.HairKind.Afro,
            CharacterArt.HairKind.Fringe, CharacterArt.HairKind.Bald,
        };
        private static readonly CharacterArt.Garment[] StreetGarments =
        {
            CharacterArt.Garment.Shirt, CharacterArt.Garment.Shirt, CharacterArt.Garment.Jacket, CharacterArt.Garment.Jacket,
            CharacterArt.Garment.Hoodie, CharacterArt.Garment.Hoodie, CharacterArt.Garment.Vest, CharacterArt.Garment.TankTop,
        };

        // One of the crowd, told apart at a glance: skin, hair, what they wear on top, and for
        // `hats` of them a hat, with the odd pair of glasses, scarf or moustache.
        private static void Dress(CharacterArt.Look look, float hats, CharacterArt.HatKind[] from = null)
        {
            if (_indoors) // at work on one of the tower's floors, whoever builds them
                (hats, from) = (Mathf.Min(hats, 0.12f), OfficeHats);
            look.Skin = CharacterArt.SkinTones[_random.Next(CharacterArt.SkinTones.Length)];
            look.HairStyle = StreetHair[_random.Next(StreetHair.Length)];
            look.Hair = _random.NextDouble() < 0.12
                ? CharacterArt.GreyHair[_random.Next(CharacterArt.GreyHair.Length)]
                : CharacterArt.NaturalHair[_random.Next(CharacterArt.NaturalHair.Length)];
            look.Garment = StreetGarments[_random.Next(StreetGarments.Length)];
            look.Under = CharacterArt.ShirtColours[_random.Next(CharacterArt.ShirtColours.Length)];
            look.Nose = _random.NextDouble() < 0.4 ? CharacterArt.NoseKind.Big : CharacterArt.NoseKind.Button;
            look.Build = 0.94f + (float)_random.NextDouble() * 0.16f;
            look.HatColor = HatColours[_random.Next(HatColours.Length)];
            look.HatAccent = HatAccents[_random.Next(HatAccents.Length)]; // never the player's colour: nobody tints these
            look.Accent = HatAccents[_random.Next(HatAccents.Length)];
            if (_random.NextDouble() < hats)
            {
                from ??= StreetHats;
                look.Hat = from[_random.Next(from.Length)];
            }
            if (_hatOverride.HasValue) // everyone at a party, say
                look.Hat = _hatOverride.Value;
            var extra = _random.NextDouble();
            look.Glasses = extra < 0.16;
            look.Sunglasses = extra > 0.95;
            look.Scarf = _random.NextDouble() < 0.14;
            look.Moustache = _random.NextDouble() < 0.1;
        }

        // The street's cars (SM_Cars, from the Blender script): sedans, hatchbacks, taxis and vans,
        // each in its paint with dark trim, chrome hubs and lit lamps. Front along +Z (the models'
        // front is −X). A driving car also throws its headlights on the road ahead.
        private static readonly string[] CarKinds = { "Sedan", "Sedan", "Hatch", "Hatch", "Taxi", "Taxi", "Van" };
        private static readonly Color[] CarPaints =
        {
            new Color(0.62f, 0.2f, 0.18f), new Color(0.22f, 0.36f, 0.48f), new Color(0.78f, 0.7f, 0.58f), new Color(0.3f, 0.42f, 0.32f),
            new Color(0.85f, 0.85f, 0.82f), new Color(0.2f, 0.2f, 0.24f), new Color(0.45f, 0.3f, 0.5f),
        };
        private static readonly Color TaxiYellow = new Color(0.98f, 0.78f, 0.2f);

        private static Transform Car(Transform parent, float z, float x, bool driving = false)
        {
            var car = new GameObject("Car").transform;
            car.SetParent(parent, false);
            car.localPosition = new Vector3(x, 0f, z);
            var body = new GameObject("Model").transform;
            body.SetParent(car, false);
            body.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var kind = CarKinds[_random.Next(CarKinds.Length)];
            if (PilotArt.FindMesh(CarsModel, $"Car{kind}Body") == null)
            {
                // Without the new models: the old rounded city car.
                var c = _random.Next(CarPaints.Length);
                Model(body, "Body", CityModel, "CityCarBody", Toon($"NightCar{c}", CarPaints[c]), Vector3.zero, 0f);
                Model(body, "Glass", CityModel, "CityCarGlass", Unlit("NightCarGlass", new Color(0.18f, 0.22f, 0.34f)), Vector3.zero, 0f);
                Model(body, "Wheels", CityModel, "CityCarWheels", Toon("CityTyre", new Color(0.08f, 0.08f, 0.09f)), Vector3.zero, 0f);
                Model(body, "Headlights", CityModel, "CityCarHeadlights", Unlit("CityHeadlight", new Color(1f, 0.95f, 0.75f)), Vector3.zero, 0f);
                Model(body, "Taillights", CityModel, "CityCarTaillights", Unlit("CityTaillight", new Color(0.95f, 0.22f, 0.16f)), Vector3.zero, 0f);
                return car;
            }
            var paintIndex = _random.Next(CarPaints.Length);
            var paint = kind == "Taxi" ? Toon("NightCarTaxi", TaxiYellow) : Toon($"NightCar{paintIndex}", CarPaints[paintIndex]);
            Model(body, "Body", CarsModel, $"Car{kind}Body", paint, Vector3.zero, 0f);
            Model(body, "Glass", CarsModel, $"Car{kind}Glass", Unlit("NightCarGlass", new Color(0.18f, 0.22f, 0.34f)), Vector3.zero, 0f);
            Model(body, "Trim", CarsModel, $"Car{kind}Trim", Toon("NightCarTrim", new Color(0.13f, 0.13f, 0.15f)), Vector3.zero, 0f);
            Model(body, "Tyres", CarsModel, $"Car{kind}Tyres", Toon("CityTyre", new Color(0.08f, 0.08f, 0.09f)), Vector3.zero, 0f);
            Model(body, "Hubs", CarsModel, $"Car{kind}Hubs", Toon("NightCarHub", new Color(0.72f, 0.72f, 0.74f)), Vector3.zero, 0f);
            Model(body, "Headlights", CarsModel, $"Car{kind}Headlights", Unlit("CityHeadlight", new Color(1f, 0.95f, 0.75f)), Vector3.zero, 0f);
            Model(body, "Taillights", CarsModel, $"Car{kind}Taillights", Unlit("CityTaillight", new Color(0.95f, 0.22f, 0.16f)), Vector3.zero, 0f);
            if (kind == "Taxi")
                Model(body, "Sign", CarsModel, "CarTaxiSign", Unlit("NightTaxiSign", new Color(1f, 0.92f, 0.6f)), Vector3.zero, 0f);
            if (driving)
                Spot(car, "Headlights", new Vector3(0f, 0.75f, 2.4f), new Vector3(-76f, 0f, 0f), new Color(1f, 0.93f, 0.75f), 16f, 60f, 3.5f);
            return car;
        }

        // A full-height lamp post with its light.
        private static void StreetLight(Transform parent, Vector3 at)
        {
            Model(parent, "LampPost", CityModel, "CityLampPost", Toon("CityLampIron", new Color(0.12f, 0.16f, 0.14f)), at, 0f);
            Model(parent, "LampGlass", CityModel, "CityLampGlass", Unlit("OfficeLampGlow", new Color(1f, 0.86f, 0.55f)), at, 0f);
            Kit.Halo(parent, at + Vector3.up * 4.3f, 2.2f, new Color(1f, 0.86f, 0.55f), 0.55f);
            Point(parent, "StreetLampLight", at + Vector3.up * 4.2f, Incandescent, 9f, 3f);
        }

        // Made-up places for the wall neon: never a real brand.
        private static readonly string[] ShopNames =
        {
            "MAMA ROSA", "LUCKY NOODLE", "GOLDEN WOK", "TACO LOCO", "STAR DINER", "THE CELLAR", "CORNER CAFE", "SAKURA", "LA PALOMA", "BURGER BARN",
        };

        // Neon on a shop's wall above its awning (user's note, 2026-11-09: "wall neon lighting better
        // treated"): the place's name in bent tube on a dark raceway held off the wall by standoffs,
        // sometimes an icon beside it, its colour washing the wall round it. Some buzz off now and
        // then. `at` is the middle of the space on the wall (local, front facing +Z); `room` its width.
        private static void WallNeon(Transform building, Vector3 at, float room, int i)
        {
            var sign = new GameObject("WallNeon").transform;
            sign.SetParent(building, false);
            sign.localPosition = at;
            var colour = NeonColours[(i / 2 + 2) % NeonColours.Length];
            var word = SignArt.Neon(ShopNames[(i / 2) % ShopNames.Length], colour);
            var withIcon = i % 3 != 1;
            var height = 0.75f;
            var width = height * word.mainTexture.width / word.mainTexture.height;
            var iconSize = withIcon ? 1.3f : 0f;
            // Long names shrink to fit the wall.
            var fit = Mathf.Min(1f, (room - iconSize) / Mathf.Max(width, 0.1f));
            width *= fit;
            height *= fit;
            var total = width + iconSize;
            var glow = new GameObject("Glow").transform;
            glow.SetParent(sign, false);
            var raceway = Toon("StreetNeonBacking", Iron);
            var left = -total / 2f;
            Block(sign, "Raceway", raceway, new Vector3(left + iconSize + width / 2f, -0.08f, 0.06f), new Vector3(width * 0.9f, 0.16f, 0.1f));
            foreach (var x in new[] { -0.4f, 0.4f })
                Block(sign, "Standoff", raceway, new Vector3(left + iconSize + width / 2f + x * width, -0.04f, 0.02f), new Vector3(0.05f, 0.08f, 0.1f));
            Shape(glow, PrimitiveType.Quad, "Name", word, new Vector3(left + iconSize + width / 2f, 0f, 0.13f), new Vector3(width, height, 1f), Quaternion.Euler(0f, 180f, 0f));
            if (withIcon)
            {
                var icon = (SignArt.Icon)((i / 2) % 6);
                Shape(glow, PrimitiveType.Quad, "Icon", SignArt.NeonIcon(icon, NeonColours[(i / 2 + 4) % NeonColours.Length]),
                    new Vector3(left + iconSize / 2f, 0.1f, 0.13f), new Vector3(iconSize, iconSize, 1f), Quaternion.Euler(0f, 180f, 0f));
            }
            // The wash: a light close to the wall, so the brick round the sign takes its colour.
            var light = Point(sign, "NeonWash", new Vector3(0f, 0f, 0.7f), colour, 4.5f, 2.2f);
            if (_random.NextDouble() < 0.3)
            {
                light.gameObject.AddComponent<FlickerLight>().Set(light, FlickerLight.Mood.Faulty, glow.gameObject, 7f + (float)_random.NextDouble() * 6f, (float)_random.NextDouble() * 10f);
                MakeDynamic(sign.gameObject);
            }
        }

        // Fire escapes: a landing at each floor with a rail and a ladder up to the next.
        private static void FireEscape(Transform building, Vector3 front, float height)
        {
            var iron = Toon("StreetIron", Iron);
            for (var y = 4.2f; y < height - 1.5f; y += 3.2f)
            {
                Block(building, "Landing", iron, front + new Vector3(0f, y, 0.55f), new Vector3(2.6f, 0.06f, 1.1f));
                Block(building, "Rail", iron, front + new Vector3(0f, y + 0.9f, 1.08f), new Vector3(2.6f, 0.05f, 0.05f));
                for (var x = -1.25f; x <= 1.26f; x += 0.5f)
                    Block(building, "Baluster", iron, front + new Vector3(x, y, 1.08f), new Vector3(0.03f, 0.9f, 0.03f));
                var ladder = Block(building, "Ladder", iron, front + new Vector3(0.5f, y, 0.6f), new Vector3(0.5f, 3.6f, 0.06f));
                if (ladder != null)
                    ladder.transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
            }
        }

        // A painted decal flat on a wall, facing `facing` degrees about Y (0 faces −Z, 180 faces +Z, −90 faces +X).
        private static void Decal(Transform parent, Material material, Vector3 at, Vector2 size, float facing)
        {
            var decal = GreyboxAssets.CreatePrimitive(PrimitiveType.Quad, "Graffiti", parent, material, keepCollider: false);
            decal.transform.localPosition = at;
            decal.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            decal.transform.localScale = new Vector3(size.x, size.y, 1f);
            decal.isStatic = true;
        }
    }
}
