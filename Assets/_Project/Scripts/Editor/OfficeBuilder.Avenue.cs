using Game.Presentation.Office.Ambience;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The avenue up to the boss's tower (the user's diagram, 2026-11-09): a straight road running
    /// south from the tower's street, sidewalks and shops on both sides, the tower standing at its
    /// end. The opening glides up it at street level and stops in the middle of the road for the
    /// menu, the tower straight ahead. It hustles (user's note, 2026-11-09): blade signs with
    /// made-up logos and neon words sticking out over the sidewalks, "OPEN" in the shop windows,
    /// a food market on a little square, string lights across the road, people queuing, posing
    /// for selfies and photographers' flashes outside a restaurant, traffic both ways, steam
    /// from the manholes. No real brands anywhere.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // The avenue runs along z, centred on x = 0, the tower's middle.
        private const float AvenueRoad = 5f;     // half the road's width
        private const float AvenueFronts = 10f;  // where the shopfronts stand, either side
        private const float AvenueDepth = 14f;   // how deep its buildings are
        private const float AvenueEnd = -350f;   // its far end, well behind where the opening starts
        private static float AvenueTop => SouthFronts - 14f; // its buildings start behind the street's row
        // The little square on the east side where the food market sets up.
        private const float MarketNorth = -236f;
        private const float MarketSouth = -264f;
        // Where the selfies happen: a restaurant on the west side, just before the menu stop.
        private const float SelfieZ = -228f;

        // Kept clear of the city's other buildings: the avenue and its two rows.
        private static Rect AvenueBounds => new Rect(-AvenueFronts - AvenueDepth - 2f, AvenueEnd - 5f,
            2f * (AvenueFronts + AvenueDepth + 2f), SouthKerb - (AvenueEnd - 5f));

        private static bool InAvenueMouth(float x, float margin = 0f) => Mathf.Abs(x) < AvenueFronts + margin;

        private static readonly (string Word, bool Vertical)[] NeonWords =
        {
            ("WE'RE OPEN", false), ("BAR", true), ("NOODLES", false), ("LIVE MUSIC", false), ("TACOS", false),
            ("HOTEL", true), ("KARAOKE", false), ("24H", false), ("DINER", false), ("PIZZA", true), ("COCKTAILS", false), ("CAFE", true),
        };
        private static readonly (SignArt.Icon Icon, string Word)[] Logos =
        {
            (SignArt.Icon.Bowl, "RAMEN"), (SignArt.Icon.Cocktail, "BAR"), (SignArt.Icon.Slice, "PIZZA"),
            (SignArt.Icon.Fish, "SUSHI"), (SignArt.Icon.Burger, "BURGERS"), (SignArt.Icon.Cup, "CAFE"),
        };
        private static readonly Color[] NeonColours =
        {
            new Color(1f, 0.35f, 0.65f), new Color(0.35f, 0.85f, 1f), new Color(1f, 0.75f, 0.3f), new Color(0.55f, 1f, 0.5f), new Color(0.8f, 0.5f, 1f),
        };
        private static readonly Color[] BoxColours =
        {
            new Color(0.82f, 0.25f, 0.2f), new Color(0.2f, 0.45f, 0.75f), new Color(0.95f, 0.65f, 0.2f), new Color(0.25f, 0.6f, 0.45f), new Color(0.55f, 0.3f, 0.6f),
        };

        private static void BuildAvenue(Transform street)
        {
            var avenue = new GameObject("Avenue").transform;
            avenue.SetParent(street, false);
            var length = SouthKerb - AvenueEnd;
            var middle = (SouthKerb + AvenueEnd) / 2f;

            // The road, its centre line, a crossing at the mouth; sidewalks and kerbs either side.
            Cube(avenue, "Road", Toon("StreetAsphalt", Asphalt), new Vector3(0f, 0f, middle), new Vector3(2f * AvenueRoad, 0.02f, length));
            var paint = Toon("StreetPaint", new Color(0.95f, 0.85f, 0.45f));
            for (var z = SouthKerb - 12f; z > AvenueEnd; z -= 6f)
                Cube(avenue, "LaneDash", paint, new Vector3(0f, 0.02f, z), new Vector3(0.18f, 0.01f, 2.6f));
            for (var x = -AvenueRoad + 0.8f; x < AvenueRoad - 0.4f; x += 1.0f)
                Cube(avenue, "Crossing", Toon("StreetWhite", new Color(0.9f, 0.9f, 0.88f)), new Vector3(x, 0.02f, SouthFronts - 3f), new Vector3(0.55f, 0.01f, 3.2f));
            var paving = Tiles("StreetPaving", Paving, new Color(0.32f, 0.29f, 0.28f), 1.2f);
            var walkLength = SouthFronts - AvenueEnd;
            foreach (var side in new[] { -1f, 1f })
            {
                Cube(avenue, "Sidewalk", paving, new Vector3(side * (AvenueRoad + AvenueFronts) / 2f, 0f, (SouthFronts + AvenueEnd) / 2f), new Vector3(AvenueFronts - AvenueRoad, 0.15f, walkLength));
                Block(avenue, "Kerb", Toon("StreetKerb", Kerb), new Vector3(side * AvenueRoad, 0f, middle), new Vector3(0.25f, 0.18f, length));
            }

            AvenueRows(avenue);
            Market(avenue);
            SelfieSpot(avenue, new Vector3(-AvenueFronts + 1.2f, 0.15f, SelfieZ), 90f, photographer: true);
            AvenueMoments(avenue);
            AvenueFurniture(avenue);
            AvenueTraffic(avenue);
            AvenuePeople(avenue);
        }

        // Shops both sides, all the way down: each with a blade sign over the sidewalk, some with
        // "OPEN" lit in the window.
        private static void AvenueRows(Transform avenue)
        {
            var tags = StreetArt.Tags();
            var murals = StreetArt.Murals();
            var i = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                if (side > 0f)
                    i += 5; // the east side's signs start further along the lists, so neighbours across the road differ
                var z = AvenueTop;
                var yaw = side < 0f ? 90f : -90f; // fronts towards the road
                while (z > AvenueEnd + 4f)
                {
                    var w = 9f + (float)_random.NextDouble() * 6f;
                    // The market's square on the east side, with one building at its back.
                    if (side > 0f && z - w < MarketNorth && z > MarketSouth)
                    {
                        z = MarketSouth - 1f;
                        continue;
                    }
                    if (side < 0f && Mathf.Abs(z - w / 2f - SelfieZ) < w / 2f && i % 3 == 2)
                        i++; // the selfie restaurant is a lit shop under an awning, not a shutter
                    var height = 12f + (float)_random.NextDouble() * 22f;
                    var at = new Vector3(side * (AvenueFronts + AvenueDepth / 2f), 0f, z - w / 2f);
                    var building = RowBuilding(avenue, at, yaw, w, AvenueDepth, height, i, tags, murals);
                    var front = new Vector3(0f, -height, AvenueDepth / 2f);
                    BladeSign(building, front, w, i);
                    if (i % 3 != 2 && _random.NextDouble() < 0.6)
                        WindowNeon(building, front + new Vector3(-w * 0.25f, 1.9f, 0.08f), i);
                    z -= w + (_random.NextDouble() < 0.5 ? 2f : 0f);
                    i++;
                }
            }
        }

        // A sign sticking out over the sidewalk, read by someone walking or driving up the avenue:
        // a neon word on a dark blade, or a lit box with a logo. Faces both ways along the avenue.
        private static void BladeSign(Transform building, Vector3 front, float w, int i)
        {
            var sign = new GameObject("BladeSign").transform;
            sign.SetParent(building, false);
            var neon = i % 2 == 0;
            var (word, vertical) = NeonWords[(i / 2) % NeonWords.Length];
            Material face;
            Color glow;
            Vector2 size;
            if (neon)
            {
                glow = NeonColours[(i / 2) % NeonColours.Length];
                face = SignArt.Neon(word, glow, vertical);
                // Sized to the lettering: short words bigger, long ones in a slimmer line.
                var aspect = face.mainTexture.width / (float)face.mainTexture.height;
                size = vertical ? new Vector2(0.9f, 0.9f / aspect) : new Vector2((word.Length <= 5 ? 0.75f : 0.5f) * aspect, word.Length <= 5 ? 0.75f : 0.5f);
                size += new Vector2(0.2f, 0.2f); // the blade round the tubes
            }
            else
            {
                var (icon, logo) = Logos[(i / 2) % Logos.Length];
                glow = BoxColours[(i / 2 + 1) % BoxColours.Length];
                face = SignArt.LightBox(icon, logo, glow);
                size = new Vector2(1.4f, 1.75f);
            }
            var y = vertical ? Mathf.Max(4.6f, size.y + 3.6f) : 4.6f + (float)_random.NextDouble() * 1.4f;
            sign.localPosition = front + new Vector3(w * 0.32f, y, 0.25f + size.x / 2f);
            var bracket = Toon("StreetIron", Iron);
            Block(sign, "Bracket", bracket, new Vector3(0f, size.y / 2f + 0.02f, -size.x / 2f - 0.1f), new Vector3(0.06f, 0.06f, size.x + 0.2f));
            Block(sign, "Blade", Toon("StreetNeonBacking", Iron), new Vector3(0f, -size.y / 2f, 0f), new Vector3(0.1f, size.y, size.x));
            // One face towards each end of the avenue (sign art culls its back, so neither reads mirrored).
            var faces = new GameObject("Faces").transform;
            faces.SetParent(sign, false);
            foreach (var turn in new[] { -90f, 90f })
            {
                var quad = Shape(faces, PrimitiveType.Quad, "Face", face, new Vector3(turn < 0f ? 0.06f : -0.06f, 0f, 0f), new Vector3(size.x - (neon ? 0.2f : 0f), size.y - (neon ? 0.2f : 0f), 1f), Quaternion.Euler(0f, turn, 0f));
                if (quad != null)
                    quad.isStatic = false;
            }
            var light = Point(sign, "SignGlow", new Vector3(0f, 0f, 0f), glow, 5f, 1.4f);
            // Some neon buzzes off now and then; light boxes stay on.
            if (neon && _random.NextDouble() < 0.35)
            {
                light.gameObject.AddComponent<FlickerLight>().Set(light, FlickerLight.Mood.Faulty, faces.gameObject, 6f + (float)_random.NextDouble() * 8f, (float)_random.NextDouble() * 10f);
                MakeDynamic(sign.gameObject);
            }
        }

        // "OPEN" in neon on the inside of a shop window.
        private static void WindowNeon(Transform building, Vector3 at, int i)
        {
            var word = i % 4 == 0 ? "WE'RE OPEN" : "OPEN";
            var colour = i % 3 == 0 ? new Color(1f, 0.3f, 0.35f) : NeonColours[i % NeonColours.Length];
            var material = SignArt.Neon(word, colour);
            var height = 0.45f;
            Shape(building, PrimitiveType.Quad, "OpenSign", material, at, new Vector3(height * material.mainTexture.width / material.mainTexture.height, height, 1f), Quaternion.Euler(0f, 180f, 0f));
        }

        // The food market on its square: stalls under striped canopies, crates of produce, bulbs
        // strung over them, stallholders and shoppers. A building closes the square at the back.
        private static void Market(Transform avenue)
        {
            var square = new GameObject("Market").transform;
            square.SetParent(avenue, false);
            var back = AvenueFronts + AvenueDepth;
            Cube(square, "Square", Tiles("StreetSquare", new Color(0.45f, 0.4f, 0.36f), new Color(0.36f, 0.32f, 0.3f), 1.6f),
                new Vector3((AvenueFronts + back) / 2f, 0f, (MarketNorth + MarketSouth) / 2f), new Vector3(back - AvenueFronts, 0.15f, MarketNorth - MarketSouth));
            RowBuilding(square, new Vector3(back + AvenueDepth / 2f, 0f, (MarketNorth + MarketSouth) / 2f), -90f, MarketNorth - MarketSouth, AvenueDepth, 16f, 1, StreetArt.Tags(), StreetArt.Murals());
            var produce = new[]
            {
                new Color(0.85f, 0.2f, 0.18f), new Color(0.95f, 0.6f, 0.15f), new Color(0.45f, 0.75f, 0.3f), new Color(0.95f, 0.85f, 0.3f), new Color(0.55f, 0.25f, 0.5f),
            };
            var bulb = Unlit("OfficeBulb", Bulb);
            var k = 0;
            foreach (var x in new[] { AvenueFronts + 3.5f, AvenueFronts + 9.5f })
            for (var z = MarketNorth - 4.5f; z > MarketSouth + 3f; z -= 6.5f)
            {
                var stall = new GameObject("Stall").transform;
                stall.SetParent(square, false);
                stall.localPosition = new Vector3(x, 0.15f, z);
                stall.localRotation = Quaternion.Euler(0f, -90f, 0f); // its counter towards the road
                var wood = Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f));
                foreach (var (px, pz) in new[] { (-1.3f, -0.9f), (1.3f, -0.9f), (-1.3f, 0.9f), (1.3f, 0.9f) })
                    Block(stall, "Post", wood, new Vector3(px, 0f, pz), new Vector3(0.08f, 2.4f, 0.08f));
                // The canopy in stripes of its colour and cream, sloping to the front.
                var stripe = Toon($"CityAwning{k % 4}", AwningStripes[k % AwningStripes.Length]);
                var cream = Toon("CityAwningCream", new Color(0.93f, 0.88f, 0.78f));
                for (var s = 0; s < 6; s++)
                {
                    var piece = Block(stall, "Canopy", s % 2 == 0 ? stripe : cream, new Vector3(-1.5f + s * 0.5f + 0.25f, 2.4f, 0f), new Vector3(0.5f, 0.06f, 2.3f));
                    if (piece != null)
                        piece.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                }
                Block(stall, "Counter", wood, new Vector3(0f, 0f, 0.7f), new Vector3(2.6f, 0.95f, 0.6f));
                // Crates of produce along the counter.
                for (var c = 0; c < 4; c++)
                {
                    var crate = new Vector3(-0.95f + c * 0.63f, 0.95f, 0.7f);
                    Block(stall, "Crate", Toon("StreetCrate", new Color(0.7f, 0.55f, 0.35f)), crate, new Vector3(0.55f, 0.18f, 0.45f));
                    var fruit = Toon($"StreetProduce{(k + c) % produce.Length}", produce[(k + c) % produce.Length]);
                    for (var f = 0; f < 4; f++)
                        Shape(stall, PrimitiveType.Sphere, "Produce", fruit, crate + new Vector3(-0.14f + (f % 2) * 0.28f, 0.22f, -0.09f + (f / 2) * 0.18f), Vector3.one * 0.17f);
                }
                for (var b = 0; b < 5; b++)
                    TwinklingBulb(stall, bulb, new Vector3(-1.2f + b * 0.6f, 2.25f - Mathf.Sin(b / 4f * Mathf.PI) * 0.12f, 1.05f), 6f + (float)_random.NextDouble() * 4f, 0.96f);
                Point(stall, "StallLight", new Vector3(0f, 2.1f, 0.6f), Incandescent, 5f, 1.8f);
                Passerby(stall, "Stallholder", new Vector3(0.3f, 0f, -0.2f), 0f);
                if (k % 2 == 0)
                    Passerby(stall, "Shopper", new Vector3(-0.4f, 0f, 1.6f), 180f);
                k++;
            }
            // Bulbs strung across the square.
            for (var z = MarketNorth - 2f; z > MarketSouth; z -= 9f)
            for (var b = 0; b < 16; b++)
            {
                var t = b / 15f;
                TwinklingBulb(square, bulb, new Vector3(Mathf.Lerp(AvenueFronts, back, t), 4.2f - Mathf.Sin(t * Mathf.PI) * 0.6f, z), 7f + (float)_random.NextDouble() * 4f, 0.95f);
            }
        }

        // A restaurant's moment out front: two friends posing, one holding the phone up, the flash
        // going off now and then; with a photographer too, a red carpet and a rope for the queue.
        private static void SelfieSpot(Transform avenue, Vector3 at, float facing, bool photographer)
        {
            var spot = new GameObject("SelfieSpot").transform;
            spot.SetParent(avenue, false);
            spot.localPosition = at;
            spot.localRotation = Quaternion.Euler(0f, facing, 0f); // local +Z towards the road
            if (photographer)
            {
                Cube(spot, "Carpet", Toon("OfficeCarpet", Burgundy), new Vector3(0f, 0.01f, 0.8f), new Vector3(3.4f, 0.01f, 2.2f));
                for (var s = 0; s < 4; s++)
                    Block(spot, "Stanchion", Toon("OfficeBrass", Brass), new Vector3(2.2f + s * 1.2f, 0f, 0.4f), new Vector3(0.1f, 0.9f, 0.1f));
                Block(spot, "Rope", Toon("OfficeCarpet", Burgundy), new Vector3(3.9f, 0.78f, 0.4f), new Vector3(3.6f, 0.05f, 0.05f));
                for (var q = 0; q < 4; q++)
                    Passerby(spot, "Queue", new Vector3(2.6f + q * 1.0f, 0f, -0.1f), 90f + (q % 2 == 0 ? 20f : -15f));
                Point(spot, "DoorLight", new Vector3(0f, 3.2f, 0.4f), Incandescent, 7f, 2.6f);
            }
            // The one with the phone, arm up and out, and a friend leaning in.
            Poser(spot, "SelfieTaker", new Vector3(-0.35f, 0f, 0.4f), 0f, phone: true);
            Poser(spot, "Friend", new Vector3(0.35f, 0f, 0.35f), -10f, phone: false);
            if (photographer)
                Photographer(spot, new Vector3(0.1f, 0f, 3.1f), 180f);
        }

        // Someone posing: with the phone held up at arm's length (its flash popping), or with a
        // hand raised beside their face.
        private static void Poser(Transform parent, string name, Vector3 at, float facing, bool phone)
        {
            var rig = Figure(parent, name, at, facing);
            if (rig == null)
                return;
            if (phone)
            {
                rig.RightShoulder.localRotation = Quaternion.Euler(-125f, 0f, -15f);
                Flash(rig.RightShoulder, Phone(rig.RightShoulder), 5f + (float)_random.NextDouble() * 4f);
            }
            else
            {
                rig.LeftShoulder.localRotation = Quaternion.Euler(-150f, 0f, 20f);
            }
            Breathe(rig.Figure != null ? rig.Figure.gameObject : parent.gameObject);
        }

        // A photographer, both hands up holding the camera (or a phone) to their eye, flashing.
        private static void Photographer(Transform parent, Vector3 at, float facing, bool phone = false)
        {
            var rig = Figure(parent, "Photographer", at, facing);
            if (rig == null)
                return;
            rig.RightShoulder.localRotation = Quaternion.Euler(-80f, 0f, -20f);
            rig.LeftShoulder.localRotation = Quaternion.Euler(-80f, 0f, 20f);
            if (phone)
            {
                Flash(rig.RightShoulder, Phone(rig.RightShoulder), 4f + (float)_random.NextDouble() * 3f);
                return;
            }
            var hand = new GameObject("Camera").transform;
            hand.SetParent(rig.RightShoulder, false);
            hand.localPosition = new Vector3(0.08f, -CharacterArt.Reach, 0.05f);
            Block(hand, "Body", Toon("OfficeInk", Ink), new Vector3(0f, -0.06f, 0f), new Vector3(0.16f, 0.11f, 0.08f));
            Block(hand, "Lens", Toon("OfficeInk", Ink), new Vector3(0f, -0.05f, 0.06f), new Vector3(0.07f, 0.07f, 0.08f));
            Flash(rig.RightShoulder, hand, 3.5f + (float)_random.NextDouble() * 2f);
        }

        // All the way along the avenue (user's note, 2026-11-09: "the whole street should be
        // hustling, not just a portion of it"), a moment every so often, alternating sides:
        // a selfie, a portrait, friends chatting, a queue at a door, a tourist photographing the tower.
        private static void AvenueMoments(Transform avenue)
        {
            var k = 0;
            for (var z = SouthFronts - 18f; z > AvenueEnd + 10f; z -= 24f, k++)
            {
                var side = k % 2 == 0 ? 1f : -1f;
                if (side > 0f && z < MarketNorth + 3f && z > MarketSouth - 3f)
                    continue; // the market has its own crowd
                if (side < 0f && Mathf.Abs(z - SelfieZ) < 12f)
                    continue; // and the restaurant its photographer
                var at = new Vector3(side * (AvenueFronts - 1.1f), 0.15f, z);
                var facing = side < 0f ? 90f : -90f; // towards the road
                var moment = new GameObject("Moment").transform;
                moment.SetParent(avenue, false);
                moment.localPosition = at;
                moment.localRotation = Quaternion.Euler(0f, facing, 0f); // local +Z towards the road
                switch (k % 5)
                {
                    case 0: // a selfie: two friends, one holding the phone up
                        Poser(moment, "SelfieTaker", new Vector3(-0.35f, 0f, 0f), 0f, phone: true);
                        Poser(moment, "Friend", new Vector3(0.35f, 0f, -0.05f), -10f, phone: false);
                        break;
                    case 1: // a portrait: one posing, a friend taking it on a phone
                        Poser(moment, "Sitter", Vector3.zero, 0f, phone: false);
                        Photographer(moment, new Vector3(0.3f, 0f, 2.1f), 180f, phone: true);
                        break;
                    case 2: // friends chatting in a ring, one talking with their hands
                        for (var f = 0; f < 3; f++)
                        {
                            var angle = f * 120f * Mathf.Deg2Rad;
                            var rig = Figure(moment, "Chatter", new Vector3(Mathf.Sin(angle) * 0.6f, 0f, Mathf.Cos(angle) * 0.6f + 0.3f), f * 120f + 180f);
                            if (rig != null && f == 0)
                                Wobble(rig.RightShoulder.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(-55f, 0f, 0f), 2.6f, 0.45f);
                            if (rig != null)
                                Breathe(rig.Figure != null ? rig.Figure.gameObject : moment.gameObject);
                        }
                        break;
                    case 3: // a queue at a door, behind a rope, under the door's light
                        for (var s = 0; s < 3; s++)
                            Block(moment, "Stanchion", Toon("OfficeBrass", Brass), new Vector3(1.2f + s * 1.2f, 0f, 0.5f), new Vector3(0.1f, 0.9f, 0.1f));
                        Block(moment, "Rope", Toon("OfficeCarpet", Burgundy), new Vector3(2.4f, 0.78f, 0.5f), new Vector3(2.4f, 0.05f, 0.05f));
                        for (var q = 0; q < 4; q++)
                            Passerby(moment, "Queue", new Vector3(0.2f + q * 0.9f, 0f, 0f), 90f + (q % 2 == 0 ? 15f : -10f));
                        Point(moment, "DoorLight", new Vector3(-0.6f, 3.0f, -0.4f), Incandescent, 6f, 2.2f);
                        break;
                    default: // a tourist photographing the tower up the avenue, flash and all
                        Photographer(moment, new Vector3(0f, 0f, 0.4f), side < 0f ? -90f : 90f);
                        Passerby(moment, "Companion", new Vector3(0.7f, 0f, 0.2f), side < 0f ? -70f : 70f);
                        break;
                }
            }
        }

        private static CharacterArt.Rig Figure(Transform parent, string name, Vector3 at, float facing)
        {
            var person = new GameObject(name).transform;
            person.SetParent(parent, false);
            person.localPosition = at;
            person.localRotation = Quaternion.Euler(0f, facing, 0f);
            var look = new CharacterArt.Look
            {
                Name = name,
                Top = CasualTops[_random.Next(CasualTops.Length)],
                Bottom = new Color(0.2f + (float)_random.NextDouble() * 0.2f, 0.22f, 0.3f),
                Skin = CharacterArt.SkinTones[_random.Next(CharacterArt.SkinTones.Length)],
                HairStyle = (CharacterArt.HairKind)_random.Next(5),
                Hair = CharacterArt.NaturalHair[_random.Next(CharacterArt.NaturalHair.Length)],
            };
            var rig = CharacterArt.Build(person, look, swingArms: false);
            if (rig == null)
                Object.DestroyImmediate(person.gameObject);
            return rig;
        }

        // A phone in the right hand, its screen lit.
        private static Transform Phone(Transform shoulder)
        {
            var hand = new GameObject("Phone").transform;
            hand.SetParent(shoulder, false);
            hand.localPosition = new Vector3(0f, -CharacterArt.Reach, 0.06f);
            Block(hand, "Case", Toon("OfficeInk", Ink), new Vector3(0f, -0.07f, 0f), new Vector3(0.08f, 0.15f, 0.015f));
            Cube(hand, "Screen", Unlit("StreetPhoneScreen", new Color(0.7f, 0.85f, 1f)), new Vector3(0f, -0.065f, -0.009f), new Vector3(0.065f, 0.13f, 0.002f));
            return hand;
        }

        // A flash at `lens`: a white spark and a burst of light now and then.
        private static void Flash(Transform owner, Transform lens, float period)
        {
            var spark = Shape(lens, PrimitiveType.Sphere, "Spark", Unlit("StreetFlash", Color.white), new Vector3(0f, -0.04f, 0.06f), Vector3.one * 0.3f);
            var light = Point(lens, "FlashLight", new Vector3(0f, 0f, 0.3f), new Color(0.92f, 0.96f, 1f), 7f, 7f);
            lens.gameObject.AddComponent<CameraFlash>().Set(light, spark, period, (float)_random.NextDouble() * 50f);
            MakeDynamic(owner.gameObject);
        }

        // Lamps, a few trees, bins, hydrants, chalk boards, parked cars, steam from manholes,
        // pigeons, and string lights across the road.
        private static void AvenueFurniture(Transform avenue)
        {
            var walk = (AvenueRoad + AvenueFronts) / 2f;
            for (var z = SouthFronts - 10f; z > AvenueEnd; z -= 18f)
            {
                StreetLight(avenue, new Vector3(-AvenueRoad - 0.6f, 0.15f, z));
                StreetLight(avenue, new Vector3(AvenueRoad + 0.6f, 0.15f, z - 9f));
            }
            for (var z = SouthFronts - 30f; z > AvenueEnd; z -= 40f)
            {
                var side = ((int)(-z / 40f)) % 2 == 0 ? -1f : 1f;
                if (side > 0f && z < MarketNorth + 4f && z > MarketSouth - 4f)
                    continue;
                if (Mathf.Abs(z - SelfieZ) > 8f)
                    Tree(avenue, new Vector3(side * (AvenueRoad + 1.4f), 0.15f, z));
            }
            var red = Toon("StreetHydrant", new Color(0.82f, 0.2f, 0.18f));
            var bags = Toon("StreetBinBag", new Color(0.12f, 0.12f, 0.13f));
            var board = Toon("StreetChalkboard", new Color(0.16f, 0.18f, 0.17f));
            var frame = Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f));
            for (var k = 0; k < 16; k++)
            {
                var side = k % 2 == 0 ? -1f : 1f;
                var z = SouthFronts - 20f - k * 19f - (float)_random.NextDouble() * 6f;
                if (Mathf.Abs(z - SelfieZ) < 6f)
                    continue;
                var nearFront = side * (AvenueFronts - 0.7f);
                Model(avenue, "Bin", "SM_Bin", "BinBody", Toon("StreetBin", new Color(0.25f, 0.42f, 0.32f)), new Vector3(side * (AvenueRoad + 0.9f), 0.15f, z), (float)_random.NextDouble() * 360f);
                if (k % 3 == 0)
                    Shape(avenue, PrimitiveType.Sphere, "BinBag", bags, new Vector3(nearFront, 0.45f, z + 3f), new Vector3(0.7f, 0.6f, 0.6f));
                Shape(avenue, PrimitiveType.Cylinder, "Hydrant", red, new Vector3(side * (AvenueRoad + 0.5f), 0.5f, z - 6f), new Vector3(0.3f, 0.35f, 0.3f));
                Shape(avenue, PrimitiveType.Sphere, "HydrantCap", red, new Vector3(side * (AvenueRoad + 0.5f), 0.88f, z - 6f), Vector3.one * 0.3f);
                // A chalk board outside a shop, leaning in two halves.
                var chalk = new GameObject("ChalkBoard").transform;
                chalk.SetParent(avenue, false);
                chalk.localPosition = new Vector3(side * (AvenueFronts - 1.6f), 0.15f, z + 8f);
                chalk.localRotation = Quaternion.Euler(0f, 90f, 0f);
                foreach (var lean in new[] { -12f, 12f })
                {
                    var half = new GameObject("Half").transform;
                    half.SetParent(chalk, false);
                    half.localRotation = Quaternion.Euler(lean, 0f, 0f);
                    Block(half, "Frame", frame, Vector3.zero, new Vector3(0.6f, 0.95f, 0.04f));
                    Block(half, "Board", board, new Vector3(0f, 0.08f, lean < 0f ? -0.025f : 0.025f), new Vector3(0.5f, 0.8f, 0.01f));
                }
            }

            // Parked cars along both kerbs, out of the way of the selfies and the camera's road.
            for (var k = 0; k < 10; k++)
            {
                var side = k % 2 == 0 ? -1f : 1f;
                var z = SouthFronts - 25f - k * 29f - (float)_random.NextDouble() * 5f;
                if (Mathf.Abs(z - SelfieZ) < 10f)
                    continue;
                var car = Car(avenue, z, side * (AvenueRoad - 0.95f));
                car.localRotation = Quaternion.Euler(0f, side < 0f ? 180f : 0f, 0f);
            }

            // Steam from the manholes, which the camera glides through.
            foreach (var z in new[] { -280f, -244f, -180f, -110f })
            {
                Shape(avenue, PrimitiveType.Cylinder, "Manhole", Toon("StreetManhole", new Color(0.12f, 0.11f, 0.12f)), new Vector3(1.6f, 0.025f, z), new Vector3(0.9f, 0.01f, 0.9f));
                Steam(avenue, new Vector3(1.6f, 0.05f, z));
            }
            for (var k = 0; k < 10; k++)
                PerchedBird(avenue, new Vector3((k % 2 == 0 ? -1f : 1f) * (walk + (float)_random.NextDouble()), 0.15f, -200f - k * 9f), (float)_random.NextDouble() * 360f);

            // String lights across the road from front to front, sagging, every so often.
            var bulb = Unlit("OfficeBulb", Bulb);
            for (var z = SouthFronts - 30f; z > AvenueEnd + 20f; z -= 34f)
            for (var b = 0; b < 22; b++)
            {
                var t = b / 21f;
                TwinklingBulb(avenue, bulb, new Vector3(Mathf.Lerp(-AvenueFronts, AvenueFronts, t), 7.5f - Mathf.Sin(t * Mathf.PI) * 1.1f, z), 6f + (float)_random.NextDouble() * 5f, 0.94f);
            }
        }

        // Traffic both ways, each lane at one speed so no car drives through another; the camera
        // keeps to the middle of the road, clear of both lanes.
        private static void AvenueTraffic(Transform avenue)
        {
            var lanes = new[] { (-2.2f, -1f, 8.5f), (2.2f, 1f, 9.5f) };
            for (var k = 0; k < 10; k++)
            {
                var (x, dir, speed) = lanes[k % 2];
                var car = Car(avenue, 0f, x, driving: true);
                var from = new Vector3(x, 0f, dir > 0f ? AvenueEnd : SouthFronts - 6f);
                var to = new Vector3(x, 0f, dir > 0f ? SouthFronts - 6f : AvenueEnd);
                car.gameObject.AddComponent<LaneMover>().Set(from, to, speed, false, 0f, (k / 2) / 5f + (k % 2) * 0.1f);
                MakeDynamic(car.gameObject);
            }
        }

        // People up and down both sidewalks, some stopping to look in a window.
        private static void AvenuePeople(Transform avenue)
        {
            // Near the kerb, so they pass in front of the moments by the shopfronts, not through them.
            for (var k = 0; k < 56; k++)
            {
                var side = k % 2 == 0 ? -1f : 1f;
                var x = side * (AvenueRoad + 1.2f + (float)_random.NextDouble() * 0.9f);
                var start = AvenueEnd + 30f + (float)_random.NextDouble() * (SouthFronts - AvenueEnd - 50f);
                var length = 14f + (float)_random.NextDouble() * 24f;
                var walker = Passerby(avenue, "Walker", new Vector3(x, 0.15f, start), 0f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(x, 0.15f, start), new Vector3(x, 0.15f, start + length),
                    1.1f + (float)_random.NextDouble() * 0.5f, true, 0.5f + (float)_random.NextDouble() * 3f, (float)_random.NextDouble());
                MakeDynamic(walker.gameObject);
            }
        }
    }
}
