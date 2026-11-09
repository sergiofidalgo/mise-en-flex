using Game.Presentation.Office.Ambience;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The street at the foot of the boss's tower, where the opening starts (docs/opening-flow.md;
    /// user's note, 2026-11-09: "start at a street, add clutter, movement, details, graffitis"). A
    /// road along the tower's south side between two pavements: the tower's lobby with its canopy
    /// and its own red carpet to the kerb on one side; across the road a row of low shops and flats
    /// with lit windows, awnings, neon, fire escapes and graffitied shutters and walls. People walk
    /// and wait, cars and a delivery rider pass, a food truck serves a queue, steam rises from the
    /// manholes, pigeons peck. The camera starts down here and climbs to the penthouse 120 m up.
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

        // Kept clear of buildings: the air the opening's camera rises through, from over the street
        // out to where it stops for the menu and looks back at the tower (docs/opening-flow.md).
        private static Rect OpeningCorridor => new Rect(-8f, -140f, 80f, 92f);

        private static readonly Color Asphalt = new Color(0.17f, 0.17f, 0.2f);
        private static readonly Color Paving = new Color(0.42f, 0.38f, 0.37f);
        private static readonly Color Kerb = new Color(0.55f, 0.53f, 0.5f);
        private static readonly Color Iron = new Color(0.16f, 0.15f, 0.17f);
        private static readonly Color Neon = new Color(1f, 0.35f, 0.65f);
        private static readonly Color NeonBlue = new Color(0.35f, 0.85f, 1f);

        private static void BuildStreet(Transform city)
        {
            var street = new GameObject("Street").transform;
            street.SetParent(city, false);
            street.localPosition = Vector3.up * Ground;

            // The ground everywhere below the city, so nothing looks into a void from the street.
            Cube(street, "Ground", Toon("StreetGround", new Color(0.12f, 0.12f, 0.15f)), new Vector3(0f, -0.2f, 0f), new Vector3(500f, 0.2f, 500f), collider: false);
            Cube(street, "Road", Toon("StreetAsphalt", Asphalt), new Vector3(0f, 0f, (NorthKerb + SouthKerb) / 2f), new Vector3(2f * StreetEnd, 0.02f, NorthKerb - SouthKerb));
            var paint = Toon("StreetPaint", new Color(0.95f, 0.85f, 0.45f));
            for (var x = -StreetEnd; x < StreetEnd; x += 6f)
                Cube(street, "LaneDash", paint, new Vector3(x, 0.02f, (NorthKerb + SouthKerb) / 2f), new Vector3(2.6f, 0.01f, 0.18f));
            for (var z = SouthKerb + 0.8f; z < NorthKerb - 0.4f; z += 1.0f)
                Cube(street, "Crossing", Toon("StreetWhite", new Color(0.9f, 0.9f, 0.88f)), new Vector3(-12f, 0.02f, z), new Vector3(3.2f, 0.01f, 0.55f));
            foreach (var (from, to) in new[] { (NorthKerb, TowerFace), (SouthFronts, SouthKerb) })
            {
                var mid = (from + to) / 2f;
                Cube(street, "Pavement", Tiles("StreetPaving", Paving, new Color(0.32f, 0.29f, 0.28f), 1.2f), new Vector3(0f, 0f, mid), new Vector3(2f * StreetEnd, 0.15f, to - from));
            }
            foreach (var z in new[] { NorthKerb, SouthKerb })
                Block(street, "Kerb", Toon("StreetKerb", Kerb), new Vector3(0f, 0f, z), new Vector3(2f * StreetEnd, 0.18f, 0.25f));

            TowerLobby(street);
            StreetRow(street);
            StreetFurniture(street);
            StreetTraffic(street);
            StreetPeople(street);
        }

        // The boss's tower at street level: a lit glass lobby, a brass canopy over the doors, his own
        // red carpet out to the kerb between brass posts, a doorman.
        private static void TowerLobby(Transform street)
        {
            var glass = Unlit("StreetLobbyGlow", new Color(1f, 0.85f, 0.6f));
            Cube(street, "LobbyGlass", glass, new Vector3(0f, 0.15f, TowerFace - 0.05f), new Vector3(16f, 4.2f, 0.05f));
            for (var x = -8f; x <= 8.01f; x += 2f)
                Block(street, "LobbyMullion", Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f)), new Vector3(x, 0.15f, TowerFace - 0.08f), new Vector3(0.12f, 4.2f, 0.12f));
            Block(street, "Canopy", Toon("OfficeBrass", Brass), new Vector3(0f, 4.4f, TowerFace - 1.6f), new Vector3(7f, 0.3f, 3.2f));
            foreach (var x in new[] { -3.2f, 3.2f })
                Block(street, "CanopyPost", Toon("OfficeBrass", Brass), new Vector3(x, 0.15f, TowerFace - 3f), new Vector3(0.12f, 4.25f, 0.12f));
            Cube(street, "LobbyCarpet", Toon("OfficeCarpet", Burgundy), new Vector3(0f, 0.16f, (TowerFace + NorthKerb) / 2f), new Vector3(2.2f, 0.01f, TowerFace - NorthKerb));
            for (var z = TowerFace - 1.2f; z > NorthKerb + 0.5f; z -= 1.5f)
            foreach (var x in new[] { -1.6f, 1.6f })
                Block(street, "Stanchion", Toon("OfficeBrass", Brass), new Vector3(x, 0.15f, z), new Vector3(0.1f, 0.9f, 0.1f));
            Point(street, "CanopyLight", new Vector3(0f, 3.8f, TowerFace - 2f), Incandescent, 9f, 3.5f);
            Person(street, "Doorman", new Vector3(2.4f, 0.15f, TowerFace - 1.2f), 180f, Burgundy, Suit, 1f,
                look => { look.Hat = CharacterArt.HatKind.Cap; look.HatColor = Burgundy; look.Tie = true; look.TieColor = new Color(0.86f, 0.62f, 0.26f); });
        }

        // The row across the road: low shops and flats, their fronts facing the tower.
        private static void StreetRow(Transform street)
        {
            var tags = StreetArt.Tags();
            var murals = StreetArt.Murals();
            var x = -StreetEnd;
            var i = 0;
            while (x < StreetEnd)
            {
                var w = 8f + (float)_random.NextDouble() * 6f;
                var d = 12f;
                var height = 12f + (float)_random.NextDouble() * 10f; // 4–7 storeys: the camera looks over them
                var c = _random.Next(FacadeColors.Length);
                var wall = Toon($"OfficeFacade{c}", Color.Lerp(FacadeColors[c], Dusk, 0.18f)); // as the city's, same material
                var trim = Toon($"OfficeFacadeTrim{c}", Color.Lerp(FacadeColors[c], Cream, 0.55f));
                var building = new GameObject("RowBuilding").transform;
                building.SetParent(street, false);
                building.localPosition = new Vector3(x + w / 2f, height, SouthFronts - d / 2f);
                Model(building, "Body", CityModel, "CityBlock", wall, new Vector3(0f, -height, 0f), 0f, scale3: new Vector3(w, height, d));
                Model(building, "Cornice", CityModel, "CityCornice", trim, Vector3.zero, 0f, scale3: new Vector3(w + 0.4f, 1f, d + 0.4f));
                // Flats above, their windows facing the tower (side 2), framed.
                Windows(building, w, d, Mathf.FloorToInt((height - 4.5f) / 3.2f), trim, side: 2, framed: true);
                var front = new Vector3(0f, -height, d / 2f);

                // The ground floor: a lit shop under an awning, or a shutter covered in tags.
                if (i % 3 != 2)
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
                    // A neon sign over some shops, buzzing on and off now and then.
                    if (i % 2 == 0)
                        NeonSign(building, front + new Vector3(w * 0.2f, 4.3f, 0.15f), i % 4 == 0 ? Neon : NeonBlue);
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
                x += w + 2f; // an alley between buildings
                i++;
            }
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
                StreetLight(street, new Vector3(x, 0.15f, NorthKerb + 0.6f));
                StreetLight(street, new Vector3(x + 6.5f, 0.15f, SouthKerb - 0.6f));
                if (Mathf.Abs(x) > 5f)
                    Tree(street, new Vector3(x + 3f, 0.15f, NorthKerb + 1.4f));
            }
            var red = Toon("StreetHydrant", new Color(0.82f, 0.2f, 0.18f));
            var bags = Toon("StreetBinBag", new Color(0.12f, 0.12f, 0.13f));
            for (var k = 0; k < 7; k++)
            {
                var x = -StreetEnd + 10f + k * 25f + (float)_random.NextDouble() * 6f;
                var z = k % 2 == 0 ? southWalk + 1f : northWalk;
                Model(street, "Bin", "SM_Bin", "BinBody", Toon("StreetBin", new Color(0.25f, 0.42f, 0.32f)), new Vector3(x, 0.15f, z), (float)_random.NextDouble() * 360f);
                Shape(street, PrimitiveType.Sphere, "BinBag", bags, new Vector3(x + 0.7f, 0.45f, z), new Vector3(0.7f, 0.6f, 0.6f));
                Shape(street, PrimitiveType.Cylinder, "Hydrant", red, new Vector3(x + 4f, 0.5f, SouthKerb - 0.5f), new Vector3(0.3f, 0.35f, 0.3f));
                Shape(street, PrimitiveType.Sphere, "HydrantCap", red, new Vector3(x + 4f, 0.88f, SouthKerb - 0.5f), Vector3.one * 0.3f);
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
                if (Mathf.Abs(x) < 5f || Mathf.Abs(x + 50f) < 6f)
                    continue;
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
                PerchedBird(street, new Vector3(-10f + k * 4.3f + (float)_random.NextDouble(), 0.15f, k % 2 == 0 ? southWalk : northWalk - 1f), (float)_random.NextDouble() * 360f);
        }

        // Cars driving both ways, a delivery rider weaving through.
        private static void StreetTraffic(Transform street)
        {
            var lanes = new[] { (-25.9f, 1f), (-22.1f, -1f) };
            for (var k = 0; k < 6; k++)
            {
                var (z, dir) = lanes[k % 2];
                var car = Car(street, z, 0f);
                car.gameObject.AddComponent<LaneMover>().Set(new Vector3(-dir * StreetEnd, 0f, z), new Vector3(dir * StreetEnd, 0f, z), 8f + (float)_random.NextDouble() * 3f, false, 0f, k / 6f);
                MakeDynamic(car.gameObject);
            }
            // The delivery rider on a scooter, a box on the back.
            var rider = new GameObject("DeliveryRider").transform;
            rider.SetParent(street, false);
            var scooter = Toon("StreetScooter", new Color(0.35f, 0.75f, 0.55f));
            Block(rider, "Deck", scooter, new Vector3(0f, 0.25f, 0f), new Vector3(0.35f, 0.12f, 1.3f));
            Block(rider, "Box", Toon("StreetDeliveryBox", new Color(0.95f, 0.45f, 0.25f)), new Vector3(0f, 0.55f, -0.55f), new Vector3(0.6f, 0.55f, 0.55f));
            Block(rider, "Column", scooter, new Vector3(0f, 0.3f, 0.55f), new Vector3(0.1f, 0.85f, 0.1f));
            foreach (var z in new[] { -0.55f, 0.55f })
                Shape(rider, PrimitiveType.Cylinder, "Wheel", Toon("CityTyre", new Color(0.08f, 0.08f, 0.09f)), new Vector3(0f, 0.22f, z), new Vector3(0.42f, 0.06f, 0.42f), Quaternion.Euler(0f, 0f, 90f));
            Person(rider, "Rider", new Vector3(0f, 0.3f, -0.05f), 0f, new Color(0.95f, 0.45f, 0.25f), new Color(0.2f, 0.22f, 0.3f), 0.95f,
                look => { look.Hat = CharacterArt.HatKind.Cap; look.HatColor = new Color(0.95f, 0.45f, 0.25f); });
            rider.gameObject.AddComponent<LaneMover>().Set(new Vector3(StreetEnd, 0.02f, -23f), new Vector3(-StreetEnd, 0.02f, -23f), 6.5f, false, 0f, 0.4f);
            MakeDynamic(rider.gameObject);
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
                new Color(0.18f + (float)_random.NextDouble() * 0.2f, 0.2f, 0.28f), 0.95f + (float)_random.NextDouble() * 0.1f, look =>
                {
                    look.Skin = CharacterArt.SkinTones[_random.Next(CharacterArt.SkinTones.Length)];
                    look.HairStyle = (CharacterArt.HairKind)_random.Next(5);
                    look.Hair = CharacterArt.NaturalHair[_random.Next(CharacterArt.NaturalHair.Length)];
                });

        // A car in the menu city's model, its front along +Z (the model's front is −X).
        private static Transform Car(Transform parent, float z, float x)
        {
            var car = new GameObject("Car").transform;
            car.SetParent(parent, false);
            car.localPosition = new Vector3(x, 0f, z);
            var body = new GameObject("Model").transform;
            body.SetParent(car, false);
            body.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var colours = new[] { new Color(0.62f, 0.2f, 0.18f), new Color(0.22f, 0.36f, 0.48f), new Color(0.78f, 0.7f, 0.58f), new Color(0.3f, 0.42f, 0.32f), new Color(0.85f, 0.75f, 0.3f) };
            var c = _random.Next(colours.Length);
            Model(body, "Body", CityModel, "CityCarBody", Toon($"NightCar{c}", colours[c]), Vector3.zero, 0f);
            Model(body, "Glass", CityModel, "CityCarGlass", Unlit("NightCarGlass", new Color(0.18f, 0.22f, 0.34f)), Vector3.zero, 0f);
            Model(body, "Wheels", CityModel, "CityCarWheels", Toon("CityTyre", new Color(0.08f, 0.08f, 0.09f)), Vector3.zero, 0f);
            Model(body, "Headlights", CityModel, "CityCarHeadlights", Unlit("CityHeadlight", new Color(1f, 0.95f, 0.75f)), Vector3.zero, 0f);
            Model(body, "Taillights", CityModel, "CityCarTaillights", Unlit("CityTaillight", new Color(0.95f, 0.22f, 0.16f)), Vector3.zero, 0f);
            return car;
        }

        // A full-height lamp post with its light.
        private static void StreetLight(Transform parent, Vector3 at)
        {
            Model(parent, "LampPost", CityModel, "CityLampPost", Toon("CityLampIron", new Color(0.12f, 0.16f, 0.14f)), at, 0f);
            Model(parent, "LampGlass", CityModel, "CityLampGlass", Unlit("OfficeLampGlow", new Color(1f, 0.86f, 0.55f)), at, 0f);
            Point(parent, "StreetLampLight", at + Vector3.up * 4.2f, Incandescent, 9f, 3f);
        }

        // A neon sign: a glowing bar with a frame, that buzzes off now and then.
        private static void NeonSign(Transform parent, Vector3 at, Color colour)
        {
            var sign = new GameObject("Neon").transform;
            sign.SetParent(parent, false);
            sign.localPosition = at;
            Block(sign, "Backing", Toon("StreetNeonBacking", Iron), new Vector3(0f, -0.05f, -0.05f), new Vector3(2.2f, 0.9f, 0.08f));
            var glow = new GameObject("Glow").transform;
            glow.SetParent(sign, false);
            var material = Unlit($"StreetNeon{ColorUtility.ToHtmlStringRGB(colour)}", colour);
            Cube(glow, "Tube", material, new Vector3(0f, 0.55f, 0.02f), new Vector3(1.9f, 0.08f, 0.04f));
            Cube(glow, "Tube", material, new Vector3(0f, 0.1f, 0.02f), new Vector3(1.9f, 0.08f, 0.04f));
            Cube(glow, "Tube", material, new Vector3(-0.6f, 0.15f, 0.02f), new Vector3(0.08f, 0.45f, 0.04f));
            Cube(glow, "Tube", material, new Vector3(0.35f, 0.15f, 0.02f), new Vector3(0.5f, 0.35f, 0.04f));
            var light = Point(sign, "NeonLight", new Vector3(0f, 0.3f, 0.8f), colour, 5f, 1.5f);
            light.gameObject.AddComponent<FlickerLight>().Set(light, FlickerLight.Mood.Faulty, glow.gameObject, 7f + (float)_random.NextDouble() * 6f, (float)_random.NextDouble() * 10f);
            MakeDynamic(sign.gameObject);
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
