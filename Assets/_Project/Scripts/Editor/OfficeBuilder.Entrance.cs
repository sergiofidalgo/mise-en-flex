using Game.Data;
using Game.Presentation;
using Game.Presentation.Hud;
using Game.Presentation.Office;
using Game.Presentation.Office.Ambience;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The boss's tower at street level, where the opening stops for the menu (the user's notes,
    /// 2026-11-10). The east half of the ground floor is the boss's own restaurant, MISE EN FLEX:
    /// packed, seen through clear glass, under its neon name and a giant screen showing its forkcast
    /// feed. Outside: a queue behind a rope, phones up, a red carpet with photographers, selfies,
    /// an influencer under a ring light, delivery riders waiting, flashes everywhere, and hearts,
    /// like counts and review bubbles popping over the phones: the game is about social media and
    /// reviews. Either side, two more places to eat in the tower's podium, and a night market on
    /// the street, which is closed to traffic. The west door is the tower's own, with its doorman;
    /// at the kerb waits player 1's chef, who walks in to the lift when a campaign is chosen.
    /// Furniture is the modelled kit (Kit.cs).
    /// </summary>
    public static partial class OfficeBuilder
    {
        // The two doors in the lobby's glass: the tower's (west) and the restaurant's (east).
        private const float TowerDoorX = -5f;
        private const float RestaurantDoorX = 5f;
        private const float DoorHalfWidth = 0.86f;
        private const float LobbyHalf = 8f;

        private static readonly Color[] StallCanopies =
        {
            new Color(0.78f, 0.22f, 0.18f), new Color(0.2f, 0.48f, 0.36f), new Color(0.22f, 0.32f, 0.58f), new Color(0.9f, 0.6f, 0.2f),
        };
        private static readonly Color[][] StallProduce =
        {
            new[] { new Color(0.85f, 0.2f, 0.18f), new Color(0.95f, 0.6f, 0.15f), new Color(0.45f, 0.75f, 0.3f) },
            new[] { new Color(0.95f, 0.85f, 0.3f), new Color(0.55f, 0.25f, 0.5f), new Color(0.9f, 0.4f, 0.2f) },
            new[] { new Color(0.86f, 0.5f, 0.45f), new Color(0.75f, 0.3f, 0.3f) },
            new[] { new Color(0.6f, 0.8f, 0.4f), new Color(0.9f, 0.3f, 0.25f), new Color(0.95f, 0.75f, 0.4f) },
        };

        private static int _popCount;

        private static void TowerEntrance(Transform street)
        {
            _popCount = 0;
            var entrance = new GameObject("TowerEntrance").transform;
            entrance.SetParent(street, false);
            Lobby(entrance);
            Restaurant(entrance);
            Marquee(entrance);
            RedCarpet(entrance);
            Queue(entrance);
            SidePlaces(entrance);
            EntranceMoments(entrance);
            WaitingChef(street);
        }

        // The lobby's front: clear glass between bronze mullions, the two doors standing open.
        private static void Lobby(Transform entrance)
        {
            var glass = PilotArt.EnsureGlassMaterial("OfficeTowerPane", new Color(0.55f, 0.7f, 0.85f, 0.12f));
            var brass = Toon("OfficeBrass", Brass);
            var bronze = Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f));
            const float height = 4.2f;
            const float doorHeight = 2.8f;
            var z = TowerFace - 0.05f;
            // Glass in pieces round the two door openings, and over them.
            var edges = new[] { -LobbyHalf, TowerDoorX - DoorHalfWidth, TowerDoorX + DoorHalfWidth, RestaurantDoorX - DoorHalfWidth, RestaurantDoorX + DoorHalfWidth, LobbyHalf };
            for (var i = 0; i < edges.Length - 1; i++)
            {
                var door = i % 2 == 1;
                var from = door ? doorHeight : 0f;
                Cube(entrance, door ? "Transom" : "LobbyGlass", glass, new Vector3((edges[i] + edges[i + 1]) / 2f, 0.15f + from, z), new Vector3(edges[i + 1] - edges[i], height - from, 0.04f));
            }
            for (var x = -LobbyHalf; x <= LobbyHalf + 0.01f; x += 2f)
                if (Mathf.Abs(x - TowerDoorX) > DoorHalfWidth + 0.2f && Mathf.Abs(x - RestaurantDoorX) > DoorHalfWidth + 0.2f)
                    Block(entrance, "LobbyMullion", bronze, new Vector3(x, 0.15f, z - 0.03f), new Vector3(0.12f, height, 0.12f));
            foreach (var doorX in new[] { TowerDoorX, RestaurantDoorX })
            {
                Kit.OpenDoor(entrance, new Vector3(doorX, 0.15f, z), 180f, glass);
                foreach (var side in new[] { -1f, 1f })
                    Kit.Planter(entrance, new Vector3(doorX + side * 1.9f, 0.15f, TowerFace - 0.55f), 0f);
            }
            // The tower's door: a brass canopy, a lamp, the doorman.
            Block(entrance, "TowerCanopy", brass, new Vector3(TowerDoorX, 3.15f, TowerFace - 0.9f), new Vector3(2.8f, 0.2f, 1.8f));
            Point(entrance, "TowerDoorLight", new Vector3(TowerDoorX, 2.7f, TowerFace - 1.2f), Incandescent, 6f, 1.4f);
            Person(entrance, "Doorman", new Vector3(TowerDoorX - 1.4f, 0.15f, TowerFace - 1.3f), 180f, Burgundy, Suit, 1f,
                look => { look.Hat = CharacterArt.HatKind.Cap; look.HatColor = Burgundy; look.Tie = true; look.TieColor = new Color(0.86f, 0.62f, 0.26f); });
        }

        // The boss's restaurant behind the glass: warm wood, a lit bar with its bottles along the
        // back, pendant lamps over high tables, every table taken, a waiter. The lift's glass shaft
        // stands in the middle of the room.
        private static void Restaurant(Transform entrance)
        {
            var room = new GameObject("Restaurant").transform;
            room.SetParent(entrance, false);
            var front = TowerFace + 0.1f;
            var back = TowerFace + RoomDepth;
            const float ceiling = 4.3f;
            var walnut = Toon("OfficeWalnut", Walnut);
            var brass = Toon("OfficeBrass", Brass);
            Cube(room, "Floor", Planks("RestaurantFloor", new Color(0.42f, 0.27f, 0.18f), new Color(0.3f, 0.19f, 0.13f), 0.18f, 1.6f), new Vector3(0f, 0.15f, (front + back) / 2f), new Vector3(2f * LobbyHalf + 0.4f, 0.02f, back - front));
            Cube(room, "Ceiling", Toon("RestaurantCeiling", new Color(0.32f, 0.2f, 0.15f)), new Vector3(0f, ceiling, (front + back) / 2f), new Vector3(2f * LobbyHalf + 0.4f, 0.1f, back - front));
            Block(room, "BackWall", walnut, new Vector3(0f, 0.15f, back), new Vector3(2f * LobbyHalf + 0.4f, ceiling - 0.15f, 0.15f));
            foreach (var x in new[] { -LobbyHalf - 0.2f, LobbyHalf + 0.2f })
                Block(room, "SideWall", walnut, new Vector3(x, 0.15f, (front + back) / 2f), new Vector3(0.15f, ceiling - 0.15f, back - front));

            // The bar along the back: a glowing bottle wall, shelves of bottles, the counter.
            var glow = Unlit("RestaurantBottleGlow", new Color(1f, 0.72f, 0.4f));
            Color[] bottles = { new Color(0.3f, 0.6f, 0.35f), new Color(0.75f, 0.45f, 0.2f), new Color(0.85f, 0.85f, 0.9f), new Color(0.55f, 0.2f, 0.25f) };
            foreach (var side in new[] { -1f, 1f })
            {
                var centre = side * 5.2f;
                Cube(room, "BottleWall", glow, new Vector3(centre, 1.2f, back - 0.1f), new Vector3(5f, 2.4f, 0.02f));
                for (var shelf = 0; shelf < 3; shelf++)
                {
                    var y = 1.35f + shelf * 0.75f;
                    Block(room, "Shelf", brass, new Vector3(centre, y - 0.04f, back - 0.25f), new Vector3(5f, 0.04f, 0.3f));
                    for (var b = 0; b < 12; b++)
                        Kit.Bottle(room, new Vector3(centre - 2.3f + b * 0.42f, y, back - 0.25f), bottles[(b + shelf) % 4], 1f + ((b * 7 + shelf) % 3) * 0.12f);
                }
                Block(room, "BarCounter", walnut, new Vector3(centre, 0.15f, back - 1.1f), new Vector3(5f, 1.05f, 0.6f));
                Block(room, "BarTop", brass, new Vector3(centre, 1.2f, back - 1.1f), new Vector3(5.1f, 0.05f, 0.7f));
                Kit.CoffeeMachine(room, new Vector3(centre + side * 1.9f, 1.25f, back - 1.1f), 180f);
            }

            // High tables with their diners, away from the chef's way to the lift (west of it) and
            // the lift's shaft.
            foreach (var (x, z) in new[] { (-7f, front + 4.6f), (-4.6f, front + 5.2f), (3.6f, front + 1.5f), (6.6f, front + 2.2f), (3.8f, front + 4.6f), (6.6f, front + 5f) })
            {
                Kit.BistroTable(room, new Vector3(x, 0.15f, z), high: true);
                Kit.Pendant(room, new Vector3(x, 2.9f, z), new Color(0.75f, 0.25f, 0.2f));
                Kit.Bottle(room, new Vector3(x + 0.12f, 1.24f, z + 0.08f), bottles[0]);
                Passerby(room, "Diner", new Vector3(x - 0.62f, 0.15f, z), 90f);
                Passerby(room, "Diner", new Vector3(x + 0.62f, 0.15f, z), -90f);
            }
            Passerby(room, "Waiter", new Vector3(3.8f, 0.15f, front + 3.1f), -30f);
            foreach (var x in new[] { -5.5f, 0f, 5.5f })
                Point(room, "RestaurantLight", new Vector3(x, 3.4f, front + 3.4f), Incandescent, 8f, 1.8f);
        }

        // Over the restaurant's door: its name in big neon, five stars, and above them a giant screen
        // with its forkcast feed (FeedScreen): the rating, the hearts ticking up, the reviews.
        private static void Marquee(Transform entrance)
        {
            var pink = new Color(1f, 0.38f, 0.62f);
            var name = SignArt.Neon("MISE EN FLEX", pink);
            const float height = 0.95f;
            var width = height * name.mainTexture.width / name.mainTexture.height;
            Block(entrance, "MarqueeRaceway", Toon("StreetNeonBacking", Iron), new Vector3(RestaurantDoorX, 5.62f, TowerFace - 0.5f), new Vector3(width * 0.92f, 0.85f, 0.12f));
            Shape(entrance, PrimitiveType.Quad, "MarqueeName", name, new Vector3(RestaurantDoorX, 6.05f, TowerFace - 0.6f), new Vector3(width, height, 1f));
            var star = SignArt.SocialIcon(SignArt.Social.Star);
            for (var s = 0; s < 5; s++)
                Shape(entrance, PrimitiveType.Quad, "MarqueeStar", star, new Vector3(RestaurantDoorX - 1.5f + s * 0.75f, 5.18f, TowerFace - 0.62f), Vector3.one * 0.62f);
            Point(entrance, "MarqueeWash", new Vector3(RestaurantDoorX, 5.8f, TowerFace - 1.6f), pink, 8f, 2f);

            // The screen, 5.76 m by 3.24 m, in a dark frame.
            const float scale = 0.6f;
            const float bottom = 6.9f;
            // Out in front of the tower's stone piers, which stand 0.35 m proud of its glass.
            var wall = TowerFace - 0.42f;
            Kit.Part(entrance, Kit.Street, "ScreenBezel", Toon("KitIron", Iron), new Vector3(RestaurantDoorX, bottom, wall), 180f, scale);
            var screen = new GameObject("FeedScreen").transform;
            screen.SetParent(entrance, false);
            screen.localPosition = new Vector3(RestaurantDoorX, bottom + scale * (0.28f + 2.7f), wall - scale * 0.12f);
            screen.localScale = Vector3.one * (scale * 9.6f / FeedScreen.Width);
            var feed = new SerializedObject(screen.gameObject.AddComponent<FeedScreen>());
            feed.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HudStyle>(SettingsFolder + "/HudStyle.asset");
            feed.FindProperty("_logo").objectReferenceValue = MenuArt.Sprite("TEX_Menu_ForkcastLogo");
            feed.ApplyModifiedPropertiesWithoutUndo();
            Point(entrance, "ScreenGlow", new Vector3(RestaurantDoorX, bottom + 1.6f, TowerFace - 2.5f), new Color(0.95f, 0.85f, 0.9f), 9f, 1.6f);
        }

        // From the restaurant's door to the kerb: a red carpet between brass posts and ropes, a
        // canopy with bulbs round its edge, a couple posing and photographers either side.
        private static void RedCarpet(Transform entrance)
        {
            Cube(entrance, "RedCarpet", Toon("OfficeCarpet", Burgundy), new Vector3(RestaurantDoorX, 0.16f, (TowerFace + NorthKerb) / 2f), new Vector3(2.2f, 0.01f, TowerFace - NorthKerb));
            Kit.Canopy(entrance, new Vector3(RestaurantDoorX, 0.15f, TowerFace - 0.3f), 180f, Burgundy);
            Point(entrance, "CanopyLight", new Vector3(RestaurantDoorX, 2.9f, TowerFace - 1.8f), Incandescent, 7f, 2f);
            foreach (var side in new[] { -1.4f, 1.4f })
                Kit.Stanchions(entrance, new Vector3(RestaurantDoorX + side, 0.15f, TowerFace - 3.7f), new Vector3(RestaurantDoorX + side, 0.15f, NorthKerb + 0.5f), Burgundy);
            Kit.MenuStand(entrance, new Vector3(RestaurantDoorX - 2.3f, 0.15f, TowerFace - 1.5f), 160f);

            // The couple on the carpet, posing for the photographers.
            var couple = new GameObject("Posing").transform;
            couple.SetParent(entrance, false);
            couple.localPosition = new Vector3(RestaurantDoorX, 0.15f, TowerFace - 4.3f);
            couple.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Poser(couple, "Star", new Vector3(-0.3f, 0f, 0f), 0f, phone: false);
            Poser(couple, "Partner", new Vector3(0.35f, 0f, 0.05f), -10f, phone: false);
            // Photographers outside the ropes, cameras up, flashing.
            foreach (var (x, z, facing) in new[] { (RestaurantDoorX - 2.4f, NorthKerb + 0.9f, 40f), (RestaurantDoorX + 2.5f, NorthKerb + 1.0f, -40f), (RestaurantDoorX + 0.2f, NorthKerb - 1.3f, 0f) })
                Photographer(entrance, new Vector3(x, z > NorthKerb ? 0.15f : 0.02f, z), facing);
        }

        // The queue for the restaurant along the front, behind a rope: most of them on their phones,
        // filming, taking selfies, the flash going, hearts and reviews rising.
        private static void Queue(Transform entrance)
        {
            var ropeZ = TowerFace - 1.9f;
            var start = RestaurantDoorX + 2.2f;
            const float end = 16.2f;
            Kit.Stanchions(entrance, new Vector3(start, 0.15f, ropeZ), new Vector3(end, 0.15f, ropeZ), Burgundy);
            var k = 0;
            for (var x = start + 0.3f; x < end; x += 0.85f, k++)
            {
                var at = new Vector3(x, 0.15f, TowerFace - 1.0f + ((k % 3) - 1) * 0.15f);
                if (k % 3 == 1)
                    Passerby(entrance, "Queue", at, -90f + (k % 2 == 0 ? 15f : -20f));
                else
                    PhoneUp(entrance, at, k % 3 == 0 ? 180f + (k % 2 == 0 ? 20f : -15f) : -90f, selfie: k % 3 == 0);
            }
            // Two at a time going in and coming out, so the queue is for somewhere.
            for (var w = 0; w < 2; w++)
            {
                var walker = Passerby(entrance, "GoingIn", new Vector3(RestaurantDoorX, 0.15f, TowerFace - 2.6f), 0f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(RestaurantDoorX + (w == 0 ? -0.3f : 0.3f), 0.15f, TowerFace - 2.6f),
                    new Vector3(RestaurantDoorX + (w == 0 ? -0.5f : 0.6f), 0.15f, TowerFace + 2.4f), 1.1f, true, 3f + w, w * 0.5f);
                MakeDynamic(walker.gameObject);
            }
        }

        // Two more places to eat in the tower's podium, either side of the lobby (the user's note,
        // 2026-11-10: "other restaurants on the sides"): lit fronts under awnings, their names in
        // neon, a logo box; outside the west one, a terrace of high tables.
        private static void SidePlaces(Transform entrance)
        {
            var k = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                var place = new GameObject(side < 0f ? "PlaceWest" : "PlaceEast").transform;
                place.SetParent(entrance, false);
                place.localPosition = new Vector3(side * 13.1f, 0.15f, TowerFace - 0.32f);
                place.localRotation = Quaternion.Euler(0f, 180f, 0f); // its front (local +z) towards the street
                const float w = 9f;
                LitShop(place, Vector3.zero, w, k * 2);
                var (icon, word) = Logos[side < 0f ? 0 : 2];
                Shape(place, PrimitiveType.Quad, "LogoBox", SignArt.LightBox(icon, word, BoxColours[side < 0f ? 0 : 2]), new Vector3(w * 0.36f, 2.1f, 0.12f), new Vector3(1.2f, 1.5f, 1f), Quaternion.Euler(0f, 180f, 0f));
                WindowNeon(place, new Vector3(-w * 0.25f, 1.9f, 0.08f), k * 4);
                k++;
            }
            // The west place's terrace: high tables under a string of bulbs, people standing with a drink.
            var bulb = Unlit("OfficeBulb", Bulb);
            for (var t = 0; t < 3; t++)
            {
                var at = new Vector3(-16f + t * 2.5f, 0.15f, TowerFace - 2.4f);
                Kit.BistroTable(entrance, at, high: true);
                Kit.Bottle(entrance, at + new Vector3(0.1f, 1.09f, 0.05f), new Color(0.3f, 0.6f, 0.35f));
                Passerby(entrance, "TerraceGuest", at + new Vector3(-0.6f, 0f, 0.1f), 90f);
                if (t != 1)
                    Passerby(entrance, "TerraceGuest", at + new Vector3(0.6f, 0f, -0.1f), -90f);
                else
                    PhoneUp(entrance, at + new Vector3(0.65f, 0f, -0.1f), -90f, selfie: false); // photographing the food
            }
            for (var b = 0; b < 14; b++)
                TwinklingBulb(entrance, bulb, new Vector3(-17.2f + b * 0.6f, 3.6f - Mathf.Sin(b / 13f * Mathf.PI) * 0.35f, TowerFace - 3.3f), 6f + (float)_random.NextDouble() * 4f, 0.95f);
        }

        // More of it along the front: selfies by the glass, an influencer filming under a ring
        // light, delivery riders waiting for orders by their scooters.
        private static void EntranceMoments(Transform entrance)
        {
            PhoneUp(entrance, new Vector3(-1.4f, 0.15f, TowerFace - 1.6f), 180f, selfie: true);
            PhoneUp(entrance, new Vector3(-0.6f, 0.15f, TowerFace - 1.7f), 170f, selfie: false);
            PhoneUp(entrance, new Vector3(1.9f, 0.15f, TowerFace - 2.8f), 20f, selfie: false); // photographing the marquee

            // The influencer: filming themselves with the tower behind, under a ring light on a stand.
            var at = new Vector3(-8.4f, 0.15f, TowerFace - 4.4f);
            Passerby(entrance, "Influencer", at, 180f);
            var light = Kit.RingLight(entrance, at + new Vector3(0f, 0f, -1.3f), 0f);
            Point(light, "RingGlow", new Vector3(0f, 1.6f, 0.4f), new Color(1f, 0.97f, 0.92f), 3.5f, 2.6f);
            HeartStream(light, new Vector3(0f, 1.95f, 0f), 2.6f);

            // Delivery riders by the kerb, bags on their backs, scooters parked.
            var orange = new Color(0.95f, 0.45f, 0.25f);
            var teal = new Color(0.25f, 0.7f, 0.65f);
            for (var r = 0; r < 2; r++)
            {
                Kit.Scooter(entrance, new Vector3(1.0f + r * 1.5f, 0.02f, NorthKerb - 0.75f), 80f + r * 15f, r == 0 ? new Color(0.35f, 0.75f, 0.55f) : new Color(0.85f, 0.3f, 0.25f), r == 0 ? orange : teal);
                var rider = Figure(entrance, "Rider", new Vector3(1.3f + r * 1.3f, 0.15f, NorthKerb + 0.9f + r * 0.4f), r == 0 ? 30f : -150f);
                if (rider != null)
                    Kit.DeliveryBag(rider.Waist, new Vector3(0f, 0.02f, -0.33f), 180f, r == 0 ? orange : teal);
            }
        }

        // The night market on the closed street (the user's notes, 2026-11-10: use the market stalls;
        // no vehicle near the lens, people taking selfies instead): stalls either side of the avenue's
        // mouth, where the camera starts, and along the tower's street; barriers at both ends; bulbs
        // strung overhead; people shopping, photographing the food and each other.
        private static void StreetMarket(Transform street)
        {
            var market = new GameObject("NightMarket").transform;
            market.SetParent(street, false);
            var k = 0;
            // Either side of the avenue's mouth, their counters towards the middle where the camera comes through.
            foreach (var z in new[] { -43.2f, -39.2f, -35.2f })
            foreach (var side in new[] { -1f, 1f })
                MarketStall(market, new Vector3(side * 3.7f, 0.02f, z), side < 0f ? 90f : -90f, k++);
            // Along the tower's street, their counters towards the avenue.
            foreach (var x in new[] { -18f, -14.2f, -10.4f, 13.8f, 17.6f })
                MarketStall(market, new Vector3(x, 0.02f, NorthKerb - 1.5f), 180f, k++);

            // Closed to traffic: barriers across the street at both ends.
            foreach (var x in new[] { -StreetClosed, StreetClosed })
            foreach (var z in new[] { NorthKerb - 1.4f, (NorthKerb + SouthKerb) / 2f, SouthKerb + 1.4f })
                Kit.Barrier(market, new Vector3(x, 0.02f, z), 90f);

            // Bulbs strung across the avenue's mouth, and from the row's roofs to the tower's podium.
            var bulb = Unlit("OfficeBulb", Bulb);
            foreach (var z in new[] { -45f, -41f, -37f, -33f })
            for (var b = 0; b < 18; b++)
            {
                var t = b / 17f;
                TwinklingBulb(market, bulb, new Vector3(Mathf.Lerp(-AvenueFronts, AvenueFronts, t), 6.2f - Mathf.Sin(t * Mathf.PI) * 1.1f, z), 6f + (float)_random.NextDouble() * 5f, 0.95f);
            }
            foreach (var x in new[] { -15f, -9.5f, 10.5f, 16f })
            for (var b = 0; b < 16; b++)
            {
                var t = b / 15f;
                TwinklingBulb(market, bulb, new Vector3(x, Mathf.Lerp(6.4f, 5.3f, t) - Mathf.Sin(t * Mathf.PI) * 0.9f, Mathf.Lerp(SouthFronts, TowerFace - 0.4f, t)), 6f + (float)_random.NextDouble() * 5f, 0.95f);
            }

            // Selfies where the camera passes, the tower behind them (they face it coming), and in the road.
            SelfiePair(market, new Vector3(-1.9f, 0.02f, -33.2f), 195f);
            SelfiePair(market, new Vector3(2.7f, 0.02f, -31.4f), 170f);
            SelfiePair(market, new Vector3(-3.4f, 0.02f, -22.6f), 170f);
            // A portrait: one posing with the marquee behind, a friend taking it.
            var portrait = new GameObject("Portrait").transform;
            portrait.SetParent(market, false);
            portrait.localPosition = new Vector3(8.6f, 0.02f, -22.2f);
            Poser(portrait, "Sitter", Vector3.zero, 180f, phone: false);
            Photographer(portrait, new Vector3(0.2f, 0f, -2.2f), 0f, phone: true);

            // People strolling the market: along the street either side of the camera's view, and a few right across it at the far kerb.
            for (var w = 0; w < 12; w++)
            {
                var east = w % 2 == 0;
                var from = east ? 10f + (float)_random.NextDouble() * 4f : -21f + (float)_random.NextDouble() * 6f;
                var length = 7f + (float)_random.NextDouble() * 5f;
                var z = w < 9 ? -25.6f + (float)_random.NextDouble() * 1.6f : NorthKerb - 0.6f - (float)_random.NextDouble() * 0.5f;
                if (w >= 9)
                {
                    from = -9f;
                    length = 18f;
                }
                var walker = Passerby(market, "Stroller", new Vector3(from, 0.02f, z), 90f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(from, 0.02f, z), new Vector3(from + length, 0.02f, z),
                    0.9f + (float)_random.NextDouble() * 0.4f, true, 1f + (float)_random.NextDouble() * 3f, (float)_random.NextDouble());
                MakeDynamic(walker.gameObject);
            }
        }

        // One stall of the market: the kit's stall, its holder behind the counter, a light, and someone
        // buying or photographing the produce.
        private static void MarketStall(Transform market, Vector3 at, float yaw, int k)
        {
            var produce = StallProduce[k % StallProduce.Length];
            var stall = Kit.Stall(market, at, yaw, StallCanopies[k % StallCanopies.Length], produce, slabs: k % 4 == 2);
            Point(stall, "StallLight", new Vector3(0f, 1.9f, 0.3f), Incandescent, 5f, 1.7f);
            Passerby(stall, "Stallholder", new Vector3(0.25f, 0f, -0.85f), 0f);
            if (k % 3 == 0)
                PhoneUp(stall, new Vector3(-0.45f, 0f, 1.25f), 180f, selfie: false); // a photo of the food first
            else
                Passerby(stall, "Shopper", new Vector3(-0.45f, 0f, 1.2f), 180f + (k % 2 == 0 ? 15f : -10f));
            if (k % 2 == 1)
                Passerby(stall, "Shopper", new Vector3(0.55f, 0f, 1.35f), 170f);
        }

        // Two friends and a phone at arm's length.
        private static void SelfiePair(Transform parent, Vector3 at, float facing)
        {
            var pair = new GameObject("Selfie").transform;
            pair.SetParent(parent, false);
            pair.localPosition = at;
            pair.localRotation = Quaternion.Euler(0f, facing, 0f);
            PhoneUp(pair, new Vector3(-0.32f, 0f, 0f), 0f, selfie: true);
            Poser(pair, "Friend", new Vector3(0.36f, 0f, -0.05f), -8f, phone: false);
        }

        // Someone holding their phone up, at arm's length for a selfie or out in front to film,
        // its flash going, and something popping over it: hearts, a like count, a review.
        private static void PhoneUp(Transform parent, Vector3 at, float facing, bool selfie)
        {
            var rig = Figure(parent, selfie ? "Selfie" : "Filming", at, facing);
            if (rig == null)
                return;
            rig.RightShoulder.localRotation = selfie ? Quaternion.Euler(-125f, 0f, -15f) : Quaternion.Euler(-95f, 0f, -10f);
            if (!selfie)
                rig.LeftShoulder.localRotation = Quaternion.Euler(-85f, 0f, 15f);
            var phone = Phone(rig.RightShoulder);
            Flash(rig.RightShoulder, phone, 1.8f + (float)_random.NextDouble() * 2.2f);
            Pops(phone, new Vector3(0f, 0.2f, 0f));
            Breathe(rig.Figure != null ? rig.Figure.gameObject : parent.gameObject);
        }

        // ---- Social pops (the user's choice, 2026-11-10: all of them) -------------------------------

        // What rises over a phone, by turns: a stream of hearts, like and rating pills, review bubbles.
        private static void Pops(Transform owner, Vector3 at)
        {
            var period = 3.5f + (float)_random.NextDouble() * 3.5f;
            switch (_popCount++ % 3)
            {
                case 0:
                    HeartStream(owner, at, period);
                    break;
                case 1:
                    PopCards(owner, at, period, LikePill(_popCount), RatingPill(_popCount));
                    break;
                default:
                    PopCards(owner, at, period + 2f, ReviewBubble(_popCount), ReviewBubble(_popCount + 3));
                    break;
            }
        }

        // Hearts floating up in a stream, like the reactions on a live video: big, outlined, a burst
        // of them every few seconds.
        private static void HeartStream(Transform owner, Vector3 at, float period)
        {
            var holder = new GameObject("Hearts");
            holder.transform.SetParent(owner, false);
            holder.transform.localPosition = at;
            holder.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // the cone opens upwards
            var hearts = holder.AddComponent<ParticleSystem>();
            var main = hearts.main;
            main.duration = period;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.34f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = hearts.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst((float)_random.NextDouble() * Mathf.Max(0.1f, period - 1.6f), 1, 2, 6, 0.22f) });
            var shape = hearts.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.06f;
            var size = hearts.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.14f, 1.15f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.75f)));
            var fade = hearts.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var noise = hearts.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.7f;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SignArt.SocialIcon(SignArt.Social.Heart, particle: true);
            renderer.sortingOrder = 13;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MakeDynamic(holder);
        }

        // One of `cards` at a time rises over the phone, facing the camera (SocialPop).
        private static void PopCards(Transform owner, Vector3 at, float period, params GameObject[] cards)
        {
            var holder = new GameObject("SocialPop").transform;
            holder.SetParent(owner, false);
            holder.localPosition = at;
            foreach (var card in cards)
                card.transform.SetParent(holder, false);
            // Sized in the world, whatever the owner's scale.
            var scale = owner.lossyScale.x > 1e-4f ? 1f / owner.lossyScale.x : 1f;
            holder.gameObject.AddComponent<SocialPop>().Set(cards, period, 2.4f, 1.0f * scale, scale, (float)_random.NextDouble() * 50f);
            MakeDynamic(holder.gameObject);
        }

        private static readonly string[] LikeCounts = { "12.4k", "2.1k", "986", "34k", "7.7k" };
        private static readonly string[] Ratings = { "4.8", "5.0", "4.9", "4.6" };
        private static readonly (string Handle, Color Avatar)[] Handles =
        {
            ("@ines.eats", new Color(0.45f, 0.75f, 0.5f)), ("@forkyeah", new Color(0.95f, 0.6f, 0.3f)), ("@crumb.daily", new Color(0.5f, 0.6f, 0.95f)),
            ("@table4two", new Color(0.9f, 0.45f, 0.6f)), ("@saucy.sam", new Color(0.7f, 0.5f, 0.9f)), ("@nomnom.nat", new Color(0.95f, 0.8f, 0.3f)),
        };

        // A pink pill with a heart and a count, like the menu's own.
        private static GameObject LikePill(int k) =>
            Pill("LikePill", new Color(1f, 0.3f, 0.5f), SignArt.Social.Heart, LikeCounts[k % LikeCounts.Length], Color.white);

        // A dark pill with a gold star and a rating.
        private static GameObject RatingPill(int k) =>
            Pill("RatingPill", new Color(0.2f, 0.14f, 0.16f), SignArt.Social.Star, Ratings[k % Ratings.Length], new Color(1f, 0.86f, 0.4f));

        private static GameObject Pill(string name, Color back, SignArt.Social icon, string text, Color ink)
        {
            var pill = new GameObject(name).transform;
            Sign(pill, "Back", SignArt.Pill(back), Vector3.zero, new Vector2(1.05f, 0.35f), 10);
            Sign(pill, "Icon", SignArt.SocialIcon(icon), new Vector3(-0.32f, 0f, -0.005f), new Vector2(0.3f, 0.3f), 11);
            Label(pill, text, new Vector3(0.13f, 0f, -0.01f), 2.3f, ink, 12);
            return pill.gameObject;
        }

        // A review card: an avatar, a handle, five stars.
        private static GameObject ReviewBubble(int k)
        {
            var (handle, avatar) = Handles[k % Handles.Length];
            var bubble = new GameObject("ReviewBubble").transform;
            Sign(bubble, "Card", SignArt.ReviewCard(), Vector3.zero, new Vector2(1.5f, 0.75f), 10);
            Sign(bubble, "Avatar", SignArt.Disc(avatar), new Vector3(-0.5f, 0.1f, -0.005f), new Vector2(0.32f, 0.32f), 11);
            Label(bubble, handle, new Vector3(0.16f, 0.2f, -0.01f), 1.55f, new Color(0.16f, 0.09f, 0.12f), 12);
            var star = SignArt.SocialIcon(SignArt.Social.Star);
            for (var s = 0; s < 5; s++)
                Sign(bubble, "Star", star, new Vector3(-0.2f + s * 0.18f, -0.03f, -0.005f), new Vector2(0.19f, 0.19f), 11);
            return bubble.gameObject;
        }

        // A flat sign piece, drawn in order (they're all see-through, so depth can't sort them).
        private static void Sign(Transform parent, string name, Material material, Vector3 at, Vector2 size, int order)
        {
            var quad = GreyboxAssets.CreatePrimitive(PrimitiveType.Quad, name, parent, material, keepCollider: false);
            quad.transform.localPosition = at;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<Renderer>().sortingOrder = order;
            quad.isStatic = false;
        }

        private static void Label(Transform parent, string text, Vector3 at, float size, Color colour, int order)
        {
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = at;
            label.font = MenuArt.Font("Inter-Bold");
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            label.GetComponent<MeshRenderer>().sortingOrder = order;
            label.ForceMeshUpdate();
        }

        // Player 1's chef, waiting at the kerb by the tower's door; OpeningChef walks them in to the
        // lift. Built with the whole tailor's rail, so they wear player 1's look.
        private static void WaitingChef(Transform street)
        {
            var cab = GameObject.Find("City/BossTower/TowerLift/Cab")?.transform;
            var chef = new GameObject("OpeningChef").transform;
            chef.SetParent(street, false);
            var waiting = new Vector3(TowerDoorX - 1.2f, 0.15f, NorthKerb + 1.0f);
            chef.localPosition = waiting;
            chef.localRotation = Quaternion.Euler(0f, 20f, 0f);
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(chef, false);
            var rig = CharacterArt.BuildChef(visuals, swingArms: true);
            if (rig == null || cab == null)
            {
                Object.DestroyImmediate(chef.gameObject);
                return;
            }
            MakeDynamic(chef.gameObject);
            // In through the tower's door, round the west side of the lift's shaft to its doors
            // at the back, and in.
            var cabCentre = cab.position;
            var path = new[]
            {
                street.TransformPoint(waiting),
                street.TransformPoint(new Vector3(TowerDoorX, 0.15f, TowerFace - 2.4f)),
                street.TransformPoint(new Vector3(TowerDoorX, 0.15f, TowerFace + 0.9f)),
                street.TransformPoint(new Vector3(-ShaftHalfWidth - 0.7f, 0.15f, ShaftNorth + 0.6f)),
                street.TransformPoint(new Vector3(0f, 0.15f, ShaftNorth + 0.6f)),
                new Vector3(cabCentre.x, street.TransformPoint(new Vector3(0f, 0.15f, 0f)).y, cabCentre.z),
            };
            var doors = new System.Collections.Generic.List<Transform>();
            foreach (Transform part in cab)
                if (part.name == "Door")
                    doors.Add(part);
            var so = new SerializedObject(chef.gameObject.AddComponent<OpeningChef>());
            var list = so.FindProperty("_path");
            list.arraySize = path.Length;
            for (var i = 0; i < path.Length; i++)
                list.GetArrayElementAtIndex(i).vector3Value = path[i];
            so.FindProperty("_cab").objectReferenceValue = cab;
            var doorList = so.FindProperty("_doors");
            doorList.arraySize = doors.Count;
            for (var i = 0; i < doors.Count; i++)
                doorList.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
            so.FindProperty("_wardrobe").objectReferenceValue = rig.Figure.GetComponent<ChefWardrobe>();
            var tinted = so.FindProperty("_tinted");
            tinted.arraySize = rig.Tinted.Count;
            for (var i = 0; i < rig.Tinted.Count; i++)
                tinted.GetArrayElementAtIndex(i).objectReferenceValue = rig.Tinted[i];
            so.FindProperty("_coop").objectReferenceValue = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            so.ApplyModifiedPropertiesWithoutUndo();

            // The lift carries the chef up, and lets them go once the camera has landed.
            var lift = Object.FindFirstObjectByType<OpeningElevator>();
            if (lift != null)
            {
                var liftSo = new SerializedObject(lift);
                liftSo.FindProperty("_rider").objectReferenceValue = chef.gameObject;
                liftSo.FindProperty("_lead").floatValue = -1.1f; // the cab level with the camera, the chef in the middle of the frame
                liftSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
