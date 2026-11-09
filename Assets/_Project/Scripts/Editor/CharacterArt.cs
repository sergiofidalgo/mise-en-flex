using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Assembles a person from the character kit (concepts v9.2 → v16, 2026-11-09,
    /// <c>SM_Characters</c>): a big head (head and hat about 38% of the height), a barrel body and
    /// short legs that swing. Every person shares one body, built in the old chef's frames (feet
    /// and waist, a shoulder per arm) plus a hip per leg, so <c>ChefArmsView</c>, bending and
    /// <c>WalkCycleView</c> move it; a <see cref="Look"/> picks the hair, hat, face and clothes and
    /// their colours. All parts use the house toon material, so people sit in the kitchen's look.
    /// Sizes must match <c>build_characters()</c> in <c>blender/build_pilot_models.py</c>.
    /// </summary>
    public static class CharacterArt
    {
        public const string Model = "SM_Characters";

        /// <summary>Where the body bends (CHAR_WAIST).</summary>
        public const float WaistHeight = 0.65f;
        /// <summary>Each arm hangs from here, out from the centre and up from the waist (CHAR_SHOULDER).</summary>
        public static readonly Vector2 Shoulder = new Vector2(0.25f, 0.38f);
        /// <summary>Each leg hangs from here, out from the centre and up from the floor (CHAR_HIP).</summary>
        public static readonly Vector2 Hip = new Vector2(0.1f, 0.48f);

        /// <summary>
        /// Where a chef holds what they carry (the player's carry anchor): held up and out in front,
        /// where the 0.48 m arms reach it, so the hands are on it rather than it floating, and it
        /// shows past the toque from more angles (the user's playtest notes). From each shoulder to
        /// its grip, 0.17 m either side of the middle, is about 0.57 m: the arm plus the mitten.
        /// </summary>
        public static readonly Vector3 CarryAnchor = new Vector3(0f, 1.1f, 0.56f);
        /// <summary>Nothing worn reaches further from the centre (CHAR_REACH): the capsule is 0.35 m.</summary>
        public const float Reach = 0.38f;

        /// <summary>
        /// How far the chef bends into storage, in degrees (the user found 20° reads fine). The
        /// head's centre is 0.71 m above the waist, so a bend of θ carries it 0.71·sin θ forward.
        /// Standing at the shelf (centre 0.85 m from the shelf's middle, its top board reaching 0.2 m
        /// out at 1.05 m high), the 0.27 m head clears that board while 0.71·sin θ + 0.27 ≤ 0.65,
        /// so θ ≤ about 32°. The old chef's 40° would put the head through the shelf.
        /// </summary>
        public const float StorageBend = 20f;

        // The old chef's knife sheath (SM_Chef) is reused, moved from its hip down to the barrel's.
        // Moving it moves the knife's belt pose (ChefArmsView) by the same amount.
        public static readonly Vector3 SheathShift = new Vector3(0f, -0.2f, -0.06f);

        public enum HatKind { None, Toque, Cap, Bandana, Headband, Horns, SunHat }
        public enum HairKind { Bald, Fringe, Crop, Bun, Afro }
        public enum NoseKind { Button, Big }
        public enum PropKind { Cane, Bottle, Newspaper, Handbag, Backpack, Satchel }
        /// <summary>
        /// What's worn on top (the user's note, 2026-11-09): a plain shirt, an open jacket over a
        /// shirt, a vest over a shirt (its sleeves the shirt's), a tank top with bare arms, a hoodie.
        /// </summary>
        public enum Garment { Shirt, Jacket, Vest, TankTop, Hoodie }

        /// <summary>Shirts under jackets and vests: white, pale blue, cream, pale pink.</summary>
        public static readonly Color[] ShirtColours =
        {
            new Color(0.94f, 0.93f, 0.9f), new Color(0.75f, 0.83f, 0.92f), new Color(0.9f, 0.86f, 0.75f), new Color(0.93f, 0.8f, 0.8f),
        };

        /// <summary>
        /// What can change from one person to the next within an archetype (concept v9.2: "the
        /// person is rolled"). Everything listed is built on the body; <c>CharacterRoll</c> picks.
        /// </summary>
        public sealed class Variation
        {
            public Color[] Skins = SkinTones;
            public HairKind[] HairStyles = { HairKind.Crop };
            public Color[] Hairs = NaturalHair;
            /// <summary>Shades of the archetype's clothes; never a player colour.</summary>
            public Color[] Tops = System.Array.Empty<Color>();
            /// <summary>Cloth patterns for the top (Game/Toon's _Pattern; 0 is plain, listed to weight it).</summary>
            public int[] Patterns = { 0 };
            public Color[] PatternColours = System.Array.Empty<Color>();
            /// <summary>Pattern repeats per metre.</summary>
            public float PatternScale = 10f;
            public float BigNose = 0.5f;
            public float Glasses;
            public float Sunglasses;
            public float Moustache;
            /// <summary>What they may wear on top; list one twice to make it likelier.</summary>
            public Garment[] Garments = { Garment.Shirt };
            /// <summary>The shirt under a jacket or vest, and a hoodie's strings.</summary>
            public Color[] Unders = ShirtColours;
            /// <summary>The archetype's props, one of which they may carry.</summary>
            public PropKind[] Props = System.Array.Empty<PropKind>();
            public float NoProp = 0.3f;
        }

        /// <summary>Eight skin tones, from very light to deep, each checked under the toon ramp.</summary>
        public static readonly Color[] SkinTones =
        {
            new Color(0.98f, 0.85f, 0.74f), new Color(0.96f, 0.78f, 0.63f), new Color(0.9f, 0.68f, 0.52f),
            new Color(0.82f, 0.58f, 0.42f), new Color(0.68f, 0.45f, 0.3f), new Color(0.55f, 0.36f, 0.24f),
            new Color(0.45f, 0.29f, 0.2f), new Color(0.36f, 0.22f, 0.15f),
        };

        /// <summary>Natural hair colours, darkest a warm brown rather than black (<see cref="DarkHair"/>).</summary>
        public static readonly Color[] NaturalHair =
        {
            new Color(0.2f, 0.13f, 0.09f), new Color(0.3f, 0.19f, 0.12f), new Color(0.45f, 0.28f, 0.15f),
            new Color(0.62f, 0.3f, 0.14f), new Color(0.85f, 0.7f, 0.42f),
        };

        public static readonly Color[] GreyHair =
        {
            new Color(0.85f, 0.85f, 0.83f), new Color(0.62f, 0.6f, 0.58f), new Color(0.45f, 0.42f, 0.4f),
        };

        private static readonly Dictionary<PropKind, (string Mesh, Color Colour, bool Held)[]> PropParts =
            new Dictionary<PropKind, (string, Color, bool)[]>
            {
                [PropKind.Cane] = new[] { ("PropCane", new Color(0.36f, 0.22f, 0.12f), true) },
                [PropKind.Bottle] = new[] { ("PropBottle", new Color(0.92f, 0.94f, 0.95f), true), ("PropBottleCap", new Color(0.25f, 0.45f, 0.75f), true) },
                [PropKind.Newspaper] = new[] { ("PropNewspaper", new Color(0.9f, 0.87f, 0.78f), true) },
                [PropKind.Handbag] = new[] { ("PropHandbag", new Color(0.2f, 0.15f, 0.13f), true) },
                [PropKind.Backpack] = new[] { ("PropBackpack", new Color(0.32f, 0.36f, 0.27f), false) },
                [PropKind.Satchel] = new[] { ("PropSatchel", new Color(0.45f, 0.27f, 0.15f), false) },
            };

        /// <summary>One person's choices. A null colour on a coloured part means "the player's colour".</summary>
        public sealed class Look
        {
            public string Name = "Person";
            public Color Skin = new Color(0.96f, 0.78f, 0.63f);
            public Color Top = new Color(0.97f, 0.97f, 0.95f);
            public Color Bottom = new Color(0.2f, 0.2f, 0.24f);
            public Color Shoes = new Color(0.16f, 0.12f, 0.1f);
            public Color Hair = new Color(0.3f, 0.19f, 0.12f);
            public HairKind HairStyle = HairKind.Crop;
            public HatKind Hat = HatKind.None;
            public Color HatColor = new Color(0.97f, 0.97f, 0.95f);
            /// <summary>The hat's coloured part (toque band, headband, ribbon); null = player colour.</summary>
            public Color? HatAccent;
            public NoseKind Nose = NoseKind.Button;
            public bool Apron, Scarf, Buttons, Dress, Tie, Camera, Glasses, Sunglasses, Moustache;
            /// <summary>Gold-framed aviators instead of glasses or wayfarer sunglasses.</summary>
            public bool Aviators;
            /// <summary>A security earpiece with a big coiled wire (the bodyguards).</summary>
            public bool Earpiece;
            public Garment Garment = Garment.Shirt;
            /// <summary>The shirt under a jacket or vest.</summary>
            public Color Under = ShirtColours[0];
            /// <summary>Apron and neckerchief colour; null = player colour.</summary>
            public Color? Accent;
            public Color TieColor = new Color(0.55f, 0.15f, 0.15f);
            /// <summary>Body width (the concept's build dial). The head stays the same.</summary>
            public float Build = 1f;
            /// <summary>The whole person's size. Customers are a little smaller than chefs (playtest).</summary>
            public float Scale = 1f;

            // How they walk, a character trait like their clothes (the user's note, 2026-11-09).
            /// <summary>Walking speed relative to the usual pace (customers; chefs walk at the player's speed).</summary>
            public float Pace = 1f;
            /// <summary>Metres per two steps.</summary>
            public float Stride = 1f;
            /// <summary>Degrees each leg swings either way.</summary>
            public float LegSwing = 24f;
            /// <summary>Degrees the body rolls onto each foot.</summary>
            public float Waddle = 1.5f;
            /// <summary>How far the body rises each step, in metres.</summary>
            public float Bob = 0.02f;

            /// <summary>Who this can be, for customers; null for a fixed look.</summary>
            public Variation Vary;

            public Look Clone() => (Look)MemberwiseClone();
        }

        public sealed class Rig
        {
            /// <summary>The whole person, under what it was built in; it waddles.</summary>
            public Transform Figure;
            public Transform Waist;
            public Transform LeftShoulder;
            public Transform RightShoulder;
            public Transform LeftHip;
            public Transform RightHip;
            /// <summary>The walk, which ChefArmsView follows for a chef's arm swing.</summary>
            public Game.Presentation.WalkCycleView Gait;
            /// <summary>Rolls the person, for customers; null for a fixed look.</summary>
            public Game.Presentation.CharacterRoll Roll;
            /// <summary>Parts that take the player's colour (<c>PlayerVisual</c>).</summary>
            public readonly List<Renderer> Tinted = new List<Renderer>();
        }

        private static readonly Color Ink = new Color(0.16f, 0.11f, 0.09f);
        private static readonly Color Shine = new Color(1f, 0.98f, 0.94f);

        /// <summary>
        /// The darkest hair: a warm brown just lighter than the ink (#3A2414), not black, so the toon
        /// bands and the ink outline still show its shape on dark skin and against dark floors.
        /// </summary>
        public static readonly Color DarkHair = new Color(0.2f, 0.13f, 0.09f);

        public static bool Available => PilotArt.FindMesh(Model, "CharHead") != null;

        /// <summary>The chef every player starts as: whites, toque with the colour band, apron and neckerchief.</summary>
        public static Look PlayerChef() => new Look
        {
            Name = "Chef",
            HairStyle = HairKind.Crop,
            Hat = HatKind.Toque,
            Apron = true,
            Scarf = true,
            Buttons = true,
        };

        /// <summary>
        /// The customers' archetypes (concept v9.2, section C): clothes, a prop, a hat and a walk tell
        /// them apart: the office worker strides briskly, the retiree ambles with small steps, the
        /// gym-goer bounds, the date sways. Colours stay clear of the four player colours (orange, teal, raspberry, lime).
        /// </summary>
        public static IReadOnlyList<Look> CustomerLooks()
        {
            var looks = new[]
            {
                new Look { Name = "Regular", Top = new Color(0.62f, 0.2f, 0.16f), Bottom = new Color(0.2f, 0.27f, 0.42f),
                    Hat = HatKind.Cap, HatColor = new Color(0.55f, 0.18f, 0.15f), HatAccent = new Color(0.92f, 0.88f, 0.8f),
                    Hair = new Color(0.45f, 0.42f, 0.4f), HairStyle = HairKind.Crop, Moustache = true, Nose = NoseKind.Big,
                    Waddle = 2f,
                    Vary = new Variation { Hairs = GreyHair.Concat(NaturalHair.Take(3)).ToArray(), Moustache = 0.6f, Glasses = 0.2f,
                        Garments = new[] { Garment.Shirt, Garment.Shirt, Garment.Vest },
                        Patterns = new[] { 2, 2, 2, 0 }, PatternScale = 9f, // a flannel's plaid
                        PatternColours = new[] { new Color(0.12f, 0.1f, 0.1f), new Color(0.9f, 0.86f, 0.78f) },
                        Tops = new[] { new Color(0.62f, 0.2f, 0.16f), new Color(0.24f, 0.36f, 0.24f), new Color(0.25f, 0.3f, 0.45f), new Color(0.5f, 0.38f, 0.22f) },
                        Props = new[] { PropKind.Newspaper }, NoProp = 0.5f } },
                new Look { Name = "Office", Skin = new Color(0.55f, 0.36f, 0.24f), Top = new Color(0.27f, 0.29f, 0.34f),
                    Bottom = new Color(0.22f, 0.23f, 0.27f), Hair = CharacterArt.DarkHair, HairStyle = HairKind.Crop,
                    Tie = true, TieColor = new Color(0.2f, 0.32f, 0.6f),
                    Pace = 1.15f, Stride = 1.1f, Waddle = 0.6f,
                    Vary = new Variation { HairStyles = new[] { HairKind.Crop, HairKind.Bun, HairKind.Bald }, Glasses = 0.35f, Moustache = 0.15f,
                        Garments = new[] { Garment.Jacket, Garment.Jacket, Garment.Vest, Garment.Shirt },
                        Patterns = new[] { 0, 0, 4 }, PatternScale = 14f, PatternColours = new[] { new Color(0.7f, 0.7f, 0.72f) },
                        Tops = new[] { new Color(0.27f, 0.29f, 0.34f), new Color(0.2f, 0.24f, 0.38f), new Color(0.45f, 0.46f, 0.48f), new Color(0.36f, 0.28f, 0.22f) },
                        Props = new[] { PropKind.Satchel, PropKind.Backpack }, NoProp = 0.25f } },
                new Look { Name = "DateNight", Skin = new Color(0.98f, 0.83f, 0.7f), Top = new Color(0.5f, 0.12f, 0.2f),
                    Bottom = new Color(0.98f, 0.83f, 0.7f), Dress = true, Hair = new Color(0.85f, 0.7f, 0.42f), HairStyle = HairKind.Bun,
                    Pace = 0.95f, Stride = 0.75f, LegSwing = 16f, Waddle = 2.5f,
                    Vary = new Variation { HairStyles = new[] { HairKind.Bun, HairKind.Crop, HairKind.Afro }, Glasses = 0.1f,
                        Patterns = new[] { 0, 3, 3 }, PatternScale = 10f,
                        PatternColours = new[] { new Color(0.95f, 0.92f, 0.85f), new Color(0.12f, 0.1f, 0.1f) },
                        Tops = new[] { new Color(0.5f, 0.12f, 0.2f), new Color(0.18f, 0.2f, 0.4f), new Color(0.12f, 0.36f, 0.28f), new Color(0.16f, 0.15f, 0.17f) },
                        Props = new[] { PropKind.Handbag }, NoProp = 0.3f } },
                new Look { Name = "Tourist", Skin = new Color(0.86f, 0.62f, 0.45f), Top = new Color(0.88f, 0.9f, 0.95f),
                    Bottom = new Color(0.82f, 0.74f, 0.55f), Hat = HatKind.SunHat, HatColor = new Color(0.9f, 0.82f, 0.6f),
                    HatAccent = new Color(0.2f, 0.27f, 0.5f), Hair = new Color(0.4f, 0.26f, 0.15f), Camera = true,
                    Pace = 0.85f, Stride = 0.9f, Waddle = 2f,
                    Vary = new Variation { Sunglasses = 0.4f,
                        Garments = new[] { Garment.Shirt, Garment.Shirt, Garment.TankTop },
                        Patterns = new[] { 1, 1, 5, 0 }, PatternScale = 10f, // a breton, or gingham
                        PatternColours = new[] { new Color(0.2f, 0.25f, 0.5f), new Color(0.6f, 0.18f, 0.15f) },
                        Tops = new[] { new Color(0.88f, 0.9f, 0.95f), new Color(0.7f, 0.8f, 0.92f), new Color(0.95f, 0.9f, 0.65f) },
                        Props = new[] { PropKind.Backpack }, NoProp = 0.5f } },
                new Look { Name = "Retiree", Skin = new Color(0.94f, 0.76f, 0.64f), Top = new Color(0.7f, 0.6f, 0.45f),
                    Bottom = new Color(0.4f, 0.36f, 0.32f), Hair = new Color(0.85f, 0.85f, 0.83f), HairStyle = HairKind.Fringe,
                    Glasses = true, Nose = NoseKind.Big,
                    Pace = 0.7f, Stride = 0.65f, LegSwing = 14f, Waddle = 2.5f, Bob = 0.01f,
                    Vary = new Variation { HairStyles = new[] { HairKind.Fringe, HairKind.Bald, HairKind.Crop }, Hairs = GreyHair,
                        Skins = SkinTones, Glasses = 0.7f, Moustache = 0.4f, BigNose = 0.7f,
                        Garments = new[] { Garment.Shirt, Garment.Vest, Garment.Jacket },
                        Patterns = new[] { 0, 0, 5 }, PatternScale = 8f,
                        PatternColours = new[] { new Color(0.9f, 0.86f, 0.78f), new Color(0.35f, 0.25f, 0.18f) },
                        Tops = new[] { new Color(0.7f, 0.6f, 0.45f), new Color(0.45f, 0.45f, 0.3f), new Color(0.45f, 0.52f, 0.62f), new Color(0.45f, 0.2f, 0.2f) },
                        Props = new[] { PropKind.Cane }, NoProp = 0.2f } },
                new Look { Name = "Gym", Skin = new Color(0.36f, 0.22f, 0.15f), Top = new Color(0.22f, 0.3f, 0.5f),
                    Bottom = new Color(0.18f, 0.18f, 0.2f), Hat = HatKind.Headband, HatAccent = new Color(0.95f, 0.95f, 0.92f),
                    Hair = CharacterArt.DarkHair, HairStyle = HairKind.Crop, Build = 1.08f,
                    Pace = 1.25f, Stride = 1.25f, LegSwing = 30f, Waddle = 0.8f, Bob = 0.03f,
                    // No afro: it would cover the headband, the gym-goer's trademark.
                    Vary = new Variation { HairStyles = new[] { HairKind.Crop, HairKind.Bun, HairKind.Bald }, BigNose = 0.3f,
                        Garments = new[] { Garment.TankTop, Garment.TankTop, Garment.Hoodie, Garment.Shirt },
                        Patterns = new[] { 0, 1 }, PatternScale = 6f, PatternColours = new[] { new Color(0.95f, 0.95f, 0.92f) },
                        Tops = new[] { new Color(0.22f, 0.3f, 0.5f), new Color(0.25f, 0.25f, 0.28f), new Color(0.55f, 0.15f, 0.15f), new Color(0.4f, 0.25f, 0.55f) },
                        Props = new[] { PropKind.Bottle }, NoProp = 0.2f } },
            };
            // A little smaller than the chefs (the user's playtest: customers felt too big), so the
            // players stay the biggest, clearest people in the room.
            foreach (var look in looks)
                look.Scale = CustomerScale;
            return looks;
        }

        /// <summary>Customers' size relative to the chefs.</summary>
        public const float CustomerScale = 0.9f;

        /// <summary>The dishwasher: a grey T-shirt and a slate apron, nothing in a player's colour.</summary>
        public static Look Dishwasher() => new Look
        {
            Name = "Dishwasher",
            Skin = new Color(0.45f, 0.29f, 0.2f),
            Top = new Color(0.62f, 0.64f, 0.66f),
            Bottom = new Color(0.25f, 0.25f, 0.28f),
            Hair = CharacterArt.DarkHair,
            HairStyle = HairKind.Crop,
            Apron = true,
            Accent = new Color(0.33f, 0.36f, 0.42f),
        };

        /// <summary>
        /// Builds <paramref name="look"/> under <paramref name="visuals"/>, whose origin is the feet.
        /// Returns null, building nothing, if the kit hasn't been exported from Blender.
        /// </summary>
        public static Rig Build(Transform visuals, Look look, bool swingArms = true)
        {
            if (!Available)
                return null;

            var rig = new Rig();
            var bodyScale = new Vector3(look.Build, 1f, look.Build);
            // Everything hangs from one figure, which waddles as it walks (WalkCycleView), so
            // whatever owns `visuals` keeps its own turn and scale.
            rig.Figure = Frame(visuals, "Figure", Vector3.zero, Vector3.one * look.Scale);

            // Lower body: the hips widen with the build; each leg hangs from its hip and swings.
            var lower = Frame(rig.Figure, "Lower", Vector3.zero, bodyScale);
            Part(lower, look.Dress ? "CharDress" : "CharHips", look.Top);
            rig.LeftHip = Leg(rig.Figure, "Hip_L", -Hip.x * look.Build, look);
            rig.RightHip = Leg(rig.Figure, "Hip_R", Hip.x * look.Build, look);
            if (look.Apron)
                Coloured(rig, lower, "CharApron", look.Accent);

            rig.Waist = Frame(rig.Figure, "Waist", new Vector3(0f, WaistHeight, 0f), Vector3.one);
            var body = Frame(rig.Waist, "Body", Vector3.zero, bodyScale);
            Part(body, "CharTorso", look.Top);
            var vary = look.Vary;
            var garments = vary != null ? vary.Garments.Distinct().ToArray() : new[] { look.Garment };
            // Each garment's pieces on the torso; its sleeves are added with the arms below.
            var garmentParts = garments.ToDictionary(g => g, g => GarmentParts(body, look, g));
            if (look.Buttons)
                Part(body, "CharButtons", Ink);
            if (look.Tie)
                Part(body, "CharTie", look.TieColor);
            if (look.Camera)
                Part(body, "CharCamera", Ink);
            if (look.Scarf)
                Coloured(rig, rig.Waist, "CharScarf", look.Accent);

            // The head and everything on it: the same size whatever the build.
            var choices = new List<(string Name, GameObject[][] Options, float None)>();
            GameObject[][] Singles(IEnumerable<GameObject> parts) => parts.Select(p => new[] { p }).ToArray();
            Part(rig.Waist, "CharHead", look.Skin);
            if (vary == null)
                Part(rig.Waist, look.Nose == NoseKind.Big ? "CharNoseBig" : "CharNoseButton", look.Skin);
            else
            {
                // Both noses, one shown (a coin weighted by BigNose: the big one is listed BigNose
                // of the time as the second of two options, so roughly that share).
                var button = Part(rig.Waist, "CharNoseButton", look.Skin).gameObject;
                var big = Part(rig.Waist, "CharNoseBig", look.Skin).gameObject;
                choices.Add(("Nose", Singles(vary.BigNose >= 0.5f ? new[] { button, big, big } : new[] { button, button, big }), 0f));
            }
            Part(rig.Waist, "CharEyes", Ink);
            Part(rig.Waist, "CharEyeShine", Shine);
            Part(rig.Waist, "CharSmile", Ink);
            if (!look.Sunglasses && !look.Aviators)
                Part(rig.Waist, "CharBrows", look.Hair);
            if (look.Aviators)
            {
                Part(rig.Waist, "CharAviators", Ink);
                Part(rig.Waist, "CharAviatorFrames", Gold);
            }
            if (look.Earpiece)
                Part(rig.Waist, "CharEarpiece", new Color(0.12f, 0.12f, 0.14f));
            if (vary == null)
            {
                if (look.Moustache)
                    Part(rig.Waist, "CharMoustache", look.Hair);
                if (look.Glasses)
                    Part(rig.Waist, "CharGlasses", Ink);
                if (look.Sunglasses)
                    Part(rig.Waist, "CharSunglasses", Ink);
                Hair(rig.Waist, look);
            }
            else
            {
                if (vary.Moustache > 0f)
                    choices.Add(("Moustache", Singles(new[] { Part(rig.Waist, "CharMoustache", look.Hair).gameObject }), 1f - vary.Moustache));
                var eyewear = new List<GameObject>();
                if (vary.Glasses > 0f)
                    eyewear.Add(Part(rig.Waist, "CharGlasses", Ink).gameObject);
                if (vary.Sunglasses > 0f)
                    eyewear.Add(Part(rig.Waist, "CharSunglasses", Ink).gameObject);
                if (eyewear.Count > 0)
                    choices.Add(("Eyewear", Singles(eyewear), Mathf.Clamp01(1f - vary.Glasses - vary.Sunglasses)));
                // Each hair style the archetype allows, in its own group, one shown.
                var styles = new List<GameObject>();
                foreach (var style in vary.HairStyles)
                {
                    var group = Frame(rig.Waist, "Hair_" + style, Vector3.zero, Vector3.one);
                    var styled = look.Clone();
                    styled.HairStyle = style;
                    Hair(group, styled);
                    styles.Add(group.gameObject);
                }
                choices.Add(("Hair", Singles(styles), 0f));
            }
            Hat(rig, look);

            // Arms: a short sleeve and a mitten on each shoulder, which turn as ChefArmsView aims them.
            rig.LeftShoulder = Arm(rig.Waist, "Shoulder_L", -Shoulder.x * look.Build, look, garments, garmentParts);
            rig.RightShoulder = Arm(rig.Waist, "Shoulder_R", Shoulder.x * look.Build, look, garments, garmentParts);

            if (vary != null)
            {
                // The archetype's props: held ones in the right hand, worn ones on the body.
                var props = new List<GameObject>();
                foreach (var kind in vary.Props)
                {
                    var group = Frame(PropParts[kind][0].Held ? rig.RightShoulder : body, "Prop_" + kind, Vector3.zero, Vector3.one);
                    foreach (var (mesh, colour, _) in PropParts[kind])
                        Part(group, mesh, colour);
                    props.Add(group.gameObject);
                }
                if (props.Count > 0)
                    choices.Add(("Prop", Singles(props), vary.NoProp));
                // The garment: its torso pieces and both sleeves show together.
                if (garments.Length > 1)
                {
                    var weighted = vary.Garments.Select(g => garmentParts[g].ToArray()).ToArray();
                    choices.Insert(0, ("Garment", weighted, 0f));
                }
                AddRoll(rig, look, choices);
            }

            // The walk: legs, a bob of the upper body and a waddle; the arms too unless a chef's
            // ChefArmsView poses them (it follows this gait).
            rig.Gait = rig.Figure.gameObject.AddComponent<Game.Presentation.WalkCycleView>();
            var gait = new SerializedObject(rig.Gait);
            gait.FindProperty("_leftHip").objectReferenceValue = rig.LeftHip;
            gait.FindProperty("_rightHip").objectReferenceValue = rig.RightHip;
            gait.FindProperty("_body").objectReferenceValue = rig.Waist;
            gait.FindProperty("_leftShoulder").objectReferenceValue = rig.LeftShoulder;
            gait.FindProperty("_rightShoulder").objectReferenceValue = rig.RightShoulder;
            gait.FindProperty("_swingArms").boolValue = swingArms;
            gait.FindProperty("_strideLength").floatValue = look.Stride;
            gait.FindProperty("_legSwing").floatValue = look.LegSwing;
            gait.FindProperty("_waddle").floatValue = look.Waddle;
            gait.FindProperty("_bobHeight").floatValue = look.Bob;
            gait.ApplyModifiedPropertiesWithoutUndo();
            return rig;
        }

        // The roll: the choices built above, and colours for every skin, hair and clothes part.
        private static void AddRoll(Rig rig, Look look, List<(string Name, GameObject[][] Options, float None)> choices)
        {
            var all = rig.Figure.GetComponentsInChildren<Renderer>(includeInactive: true);
            Renderer[] Named(params string[] names) =>
                all.Where(r => names.Contains(r.gameObject.name) || names.Any(n => n.EndsWith("*") && r.gameObject.name.StartsWith(n.TrimEnd('*')))).ToArray();
            var skin = Named("CharHead", "CharNoseButton", "CharNoseBig", "CharHand", "CharArmBare", "CharTankShoulders").ToList();
            if (look.Dress)
                skin.AddRange(Named("CharLeg")); // bare legs under a dress
            var palettes = new List<(string Name, Renderer[] Renderers, Color[] Colours)>
            {
                ("Skin", skin.ToArray(), look.Vary.Skins),
                ("Hair", Named("CharBrows", "CharMoustache", "Hair*"), look.Vary.Hairs),
            };
            if (look.Vary.Tops.Length > 0)
                palettes.Add(("Top", Named(TopParts), look.Vary.Tops));
            palettes.Add(("Under", Named("CharShirtV", "CharVestV", "CharArmUnder", "CharHoodStrings"), look.Vary.Unders));

            var roll = new SerializedObject(rig.Figure.gameObject.AddComponent<Game.Presentation.CharacterRoll>());
            roll.FindProperty("_archetype").stringValue = look.Name;
            var choiceList = roll.FindProperty("_choices");
            choiceList.arraySize = choices.Count;
            for (var i = 0; i < choices.Count; i++)
            {
                var element = choiceList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Name").stringValue = choices[i].Name;
                element.FindPropertyRelative("NoneChance").floatValue = choices[i].None;
                var options = element.FindPropertyRelative("Options");
                options.arraySize = choices[i].Options.Length;
                for (var j = 0; j < choices[i].Options.Length; j++)
                {
                    var parts = options.GetArrayElementAtIndex(j).FindPropertyRelative("Parts");
                    parts.arraySize = choices[i].Options[j].Length;
                    for (var k = 0; k < choices[i].Options[j].Length; k++)
                        parts.GetArrayElementAtIndex(k).objectReferenceValue = choices[i].Options[j][k];
                }
            }
            var paletteList = roll.FindProperty("_palettes");
            paletteList.arraySize = palettes.Count;
            for (var i = 0; i < palettes.Count; i++)
            {
                var element = paletteList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Name").stringValue = palettes[i].Name;
                var renderers = element.FindPropertyRelative("Renderers");
                renderers.arraySize = palettes[i].Renderers.Length;
                for (var j = 0; j < palettes[i].Renderers.Length; j++)
                    renderers.GetArrayElementAtIndex(j).objectReferenceValue = palettes[i].Renderers[j];
                var colours = element.FindPropertyRelative("Colours");
                colours.arraySize = palettes[i].Colours.Length;
                for (var j = 0; j < palettes[i].Colours.Length; j++)
                    colours.GetArrayElementAtIndex(j).colorValue = palettes[i].Colours[j];
            }
            // The clothes' pattern, on the same parts as their shade.
            var patternList = roll.FindProperty("_patterns");
            patternList.arraySize = 1;
            var patterned = patternList.GetArrayElementAtIndex(0);
            var clothes = Named(TopParts);
            var patternRenderers = patterned.FindPropertyRelative("Renderers");
            patternRenderers.arraySize = clothes.Length;
            for (var j = 0; j < clothes.Length; j++)
                patternRenderers.GetArrayElementAtIndex(j).objectReferenceValue = clothes[j];
            var kinds = patterned.FindPropertyRelative("Kinds");
            kinds.arraySize = look.Vary.Patterns.Length;
            for (var j = 0; j < look.Vary.Patterns.Length; j++)
                kinds.GetArrayElementAtIndex(j).intValue = look.Vary.Patterns[j];
            var patternColours = patterned.FindPropertyRelative("Colours");
            patternColours.arraySize = look.Vary.PatternColours.Length;
            for (var j = 0; j < look.Vary.PatternColours.Length; j++)
                patternColours.GetArrayElementAtIndex(j).colorValue = look.Vary.PatternColours[j];
            patterned.FindPropertyRelative("Scale").floatValue = look.Vary.PatternScale;
            roll.ApplyModifiedPropertiesWithoutUndo();
            rig.Roll = rig.Figure.GetComponent<Game.Presentation.CharacterRoll>();
        }

        private static Transform Leg(Transform figure, string name, float x, Look look)
        {
            var hip = Frame(figure, name, new Vector3(x, Hip.y, 0f), Vector3.one);
            Part(hip, "CharLeg", look.Bottom);
            Part(hip, "CharShoe", look.Shoes);
            return hip;
        }

        private static void Hair(Transform waist, Look look)
        {
            var hatted = look.Hat != HatKind.None && look.Hat != HatKind.Headband && look.Hat != HatKind.Horns;
            switch (look.HairStyle)
            {
                case HairKind.Bald:
                    return;
                case HairKind.Fringe:
                    Part(waist, "HairFringe", look.Hair);
                    return;
            }
            // Under a hat only the back shows: what's on top would poke through (the under-hat rule).
            Part(waist, "HairBack", look.Hair);
            if (hatted)
                return;
            Part(waist, look.HairStyle == HairKind.Afro ? "HairAfro" : "HairCrop", look.Hair);
            if (look.HairStyle == HairKind.Bun)
                Part(waist, "HairBun", look.Hair);
        }

        private static void Hat(Rig rig, Look look)
        {
            switch (look.Hat)
            {
                case HatKind.Toque:
                    Part(rig.Waist, "HatToque", look.HatColor);
                    Coloured(rig, rig.Waist, "HatToqueBand", look.HatAccent);
                    break;
                case HatKind.Cap:
                    Part(rig.Waist, "HatCap", look.HatColor);
                    Coloured(rig, rig.Waist, "HatCapPeak", look.HatAccent);
                    break;
                case HatKind.Bandana:
                    Coloured(rig, rig.Waist, "HatBandana", look.HatAccent);
                    break;
                case HatKind.Headband:
                    Coloured(rig, rig.Waist, "HatHeadband", look.HatAccent);
                    break;
                case HatKind.Horns:
                    Part(rig.Waist, "HatHorns", look.HatColor);
                    Coloured(rig, rig.Waist, "HatHeadband", look.HatAccent);
                    break;
                case HatKind.SunHat:
                    Part(rig.Waist, "HatSunHat", look.HatColor);
                    Coloured(rig, rig.Waist, "HatSunHatBand", look.HatAccent);
                    break;
            }
        }

        // The parts in the top's colour and pattern, whatever's worn.
        private static readonly string[] TopParts = { "CharTorso", "CharHips", "CharDress", "CharArm", "CharLapels", "CharHood", "CharHoodiePocket" };
        private static readonly Color Gold = new Color(0.85f, 0.68f, 0.3f);

        // What a garment adds on the torso (a jacket's open front, a vest's V and buttons...).
        private static List<GameObject> GarmentParts(Transform body, Look look, Garment garment)
        {
            var parts = new List<Renderer>();
            switch (garment)
            {
                case Garment.Jacket:
                    parts.Add(Part(body, "CharShirtV", look.Under));
                    parts.Add(Part(body, "CharLapels", look.Top));
                    break;
                case Garment.Vest:
                    parts.Add(Part(body, "CharVestV", look.Under));
                    parts.Add(Part(body, "CharVestButtons", Ink));
                    break;
                case Garment.Hoodie:
                    parts.Add(Part(body, "CharHood", look.Top));
                    parts.Add(Part(body, "CharHoodiePocket", look.Top));
                    parts.Add(Part(body, "CharHoodStrings", look.Under));
                    break;
                case Garment.TankTop:
                    parts.Add(Part(body, "CharTankShoulders", look.Skin));
                    break;
            }
            return parts.Where(r => r != null).Select(r => r.gameObject).ToList();
        }

        // A shoulder with its hand, and the sleeve each garment needs: in the top's colour, the
        // shirt's (under a vest), or a bare arm (a tank top). Each sleeve joins its garment's parts.
        private static Transform Arm(Transform waist, string name, float x, Look look, Garment[] garments,
            Dictionary<Garment, List<GameObject>> garmentParts)
        {
            var shoulder = Frame(waist, name, new Vector3(x, Shoulder.y, 0f), Vector3.one);
            var sleeves = new Dictionary<string, GameObject>();
            foreach (var garment in garments)
            {
                var (key, mesh, colour) = garment switch
                {
                    Garment.Vest => ("CharArmUnder", "CharArm", look.Under),
                    Garment.TankTop => ("CharArmBare", "CharArmBare", look.Skin),
                    _ => ("CharArm", "CharArm", look.Top),
                };
                if (!sleeves.TryGetValue(key, out var sleeve))
                {
                    sleeve = Part(shoulder, mesh, colour, name: key)?.gameObject;
                    sleeves[key] = sleeve;
                }
                if (sleeve != null)
                    garmentParts[garment].Add(sleeve);
            }
            Part(shoulder, "CharHand", look.Skin);
            return shoulder;
        }

        private static Transform Frame(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.localPosition = position;
            frame.localScale = scale;
            return frame;
        }

        // A part in a fixed colour, or the player's colour (white, tinted in play) when none is given.
        private static void Coloured(Rig rig, Transform parent, string mesh, Color? color)
        {
            var renderer = Part(parent, mesh, color ?? Color.white, color.HasValue ? null : "ChefAccent");
            if (!color.HasValue && renderer != null)
                rig.Tinted.Add(renderer);
        }

        private static Renderer Part(Transform parent, string mesh, Color color, string material = null, string name = null)
        {
            var shared = PilotArt.FindMesh(Model, mesh);
            if (shared == null)
                return null;
            var part = new GameObject(name ?? mesh);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = shared;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material != null
                ? StyleTest.EnsureToonMaterial(material, color)
                : Toon(color);
            return renderer;
        }

        /// <summary>The house toon material in a colour, shared by every part of that colour.</summary>
        public static Material Toon(Color color) =>
            StyleTest.EnsureToonMaterial("Char_" + ColorUtility.ToHtmlStringRGB(color), color);
    }
}
