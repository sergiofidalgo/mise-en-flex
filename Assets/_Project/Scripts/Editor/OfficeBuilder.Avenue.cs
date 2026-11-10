using Game.Presentation.Office.Ambience;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The avenue's mouth, opposite the boss's tower (the user's diagram, 2026-11-09; cut back on
    /// 2026-11-10, when the opening stopped running up the avenue: "no long run up the road"). The
    /// camera starts here and pushes straight across the closed street to the tower's entrance, so
    /// only the mouth is ever on screen: the road between its two corner buildings, the way into
    /// the night market. Also the pieces the street's people share: blade signs, window neon,
    /// people posing and photographing, phones and flashes.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // The avenue runs along z, centred on x = 0, the tower's middle.
        private const float AvenueRoad = 5f;     // half the road's width
        private const float AvenueFronts = 10f;  // where the shopfronts stand, either side
        private const float AvenueDepth = 14f;   // how deep its buildings are
        private const float AvenueEnd = -66f;    // behind the camera's start: nothing further is ever seen
        private static float AvenueTop => SouthFronts - 14f; // its buildings start behind the street's row

        // Kept clear of the city's other buildings: the avenue's mouth and its two corner buildings.
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
            Cube(avenue, "Road", Toon("StreetAsphalt", Asphalt), new Vector3(0f, 0f, middle), new Vector3(2f * AvenueRoad, 0.02f, length));
            var paving = Tiles("StreetPaving", Paving, new Color(0.32f, 0.29f, 0.28f), 1.2f);
            foreach (var side in new[] { -1f, 1f })
            {
                Cube(avenue, "Sidewalk", paving, new Vector3(side * (AvenueRoad + AvenueFronts) / 2f, 0f, (SouthFronts + AvenueEnd) / 2f), new Vector3(AvenueFronts - AvenueRoad, 0.15f, SouthFronts - AvenueEnd));
                Block(avenue, "Kerb", Toon("StreetKerb", Kerb), new Vector3(side * AvenueRoad, 0f, middle), new Vector3(0.25f, 0.18f, length));
            }
            // One building each side behind the street's corner ones, so no gap opens beside the camera's start.
            var tags = StreetArt.Tags();
            var murals = StreetArt.Murals();
            var i = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                var w = AvenueTop - AvenueEnd;
                var height = 20f + (float)_random.NextDouble() * 8f;
                var building = RowBuilding(avenue, new Vector3(side * (AvenueFronts + AvenueDepth / 2f), 0f, AvenueTop - w / 2f), side < 0f ? 90f : -90f, w, AvenueDepth, height, i, tags, murals);
                BladeSign(building, new Vector3(0f, -height, AvenueDepth / 2f), w, i);
                i += 3;
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

        // Someone posing: with the phone held up at arm's length (its flash popping), or with a
        // hand raised beside their face.
        private static CharacterArt.Rig Poser(Transform parent, string name, Vector3 at, float facing, bool phone)
        {
            var rig = Figure(parent, name, at, facing);
            if (rig == null)
                return null;
            if (phone)
            {
                rig.RightShoulder.localRotation = Quaternion.Euler(-125f, 0f, -15f);
                Flash(rig.RightShoulder, Phone(rig.RightShoulder), 3f + (float)_random.NextDouble() * 3f);
            }
            else
            {
                rig.LeftShoulder.localRotation = Quaternion.Euler(-150f, 0f, 20f);
            }
            Breathe(rig.Figure != null ? rig.Figure.gameObject : parent.gameObject);
            return rig;
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
                Flash(rig.RightShoulder, Phone(rig.RightShoulder), 2.5f + (float)_random.NextDouble() * 2.5f);
                return;
            }
            // The camera between the hands, its lens along the arms.
            var hold = new GameObject("CameraHold").transform;
            hold.SetParent(rig.RightShoulder, false);
            hold.localPosition = new Vector3(0.09f, -CharacterArt.Reach - 0.05f, 0.02f);
            hold.localRotation = Quaternion.Euler(90f, 0f, 0f); // the arm points along its −y: the lens follows it
            Kit.Camera(hold, Vector3.zero);
            Flash(rig.RightShoulder, hold, 2f + (float)_random.NextDouble() * 2f, new Vector3(0f, 0.07f, 0.02f));
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

        // A phone in the right hand (the kit's model), held upright along the arm, its lit screen
        // back towards the holder.
        private static Transform Phone(Transform shoulder) =>
            Kit.Phone(shoulder, new Vector3(0f, -CharacterArt.Reach - 0.06f, 0.03f), Quaternion.Euler(90f, 0f, 0f));

        // A flash at `lens`: a white spark and a burst of light that washes whatever is near
        // (the user's note, 2026-11-10: far more flashes, the game is about social media).
        private static void Flash(Transform owner, Transform lens, float period, Vector3? at = null)
        {
            var where = at ?? new Vector3(0f, 0.06f, 0.02f);
            var spark = Shape(lens, PrimitiveType.Sphere, "Spark", Unlit("StreetFlash", Color.white), where, Vector3.one * 0.3f);
            var light = Point(lens, "FlashLight", where + new Vector3(0f, 0f, 0.3f), new Color(0.92f, 0.96f, 1f), 9f, 9f);
            lens.gameObject.AddComponent<CameraFlash>().Set(light, spark, period, (float)_random.NextDouble() * 50f);
            MakeDynamic(owner.gameObject);
        }
    }
}
