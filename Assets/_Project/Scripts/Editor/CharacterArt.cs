using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Assembles a person from the v9.2 character kit (concept 2026-11-09, <c>SM_Characters</c>):
    /// big head, barrel body, stubby legs. Every person shares one body, built in the same two
    /// frames as the old chef (feet and waist, plus a shoulder per arm), so <c>ChefArmsView</c> and
    /// bending keep working; a <see cref="Look"/> picks the hair, hat, face and clothes and their
    /// colours. All parts use the house toon material, so people sit in the kitchen's look.
    /// Sizes must match <c>build_characters()</c> in <c>blender/build_pilot_models.py</c>.
    /// </summary>
    public static class CharacterArt
    {
        public const string Model = "SM_Characters";

        /// <summary>Where the body bends (CHAR_WAIST).</summary>
        public const float WaistHeight = 0.45f;
        /// <summary>Each arm hangs from here, out from the centre and up from the waist (CHAR_SHOULDER).</summary>
        public static readonly Vector2 Shoulder = new Vector2(0.29f, 0.43f);
        /// <summary>Nothing worn reaches further from the centre (CHAR_REACH): the capsule is 0.35 m.</summary>
        public const float Reach = 0.38f;

        /// <summary>
        /// How far the chef bends into storage, in degrees. The head's centre is 0.88 m above the
        /// waist, so a bend of θ carries it 0.88·sin θ forward. Standing at the shelf (centre 0.85 m
        /// from the shelf's middle, its top board reaching 0.2 m out at 1.05 m high), the 0.31 m head
        /// clears that board only while 0.88·sin θ + 0.31 ≤ 0.65, so θ ≤ about 22°. The old chef's
        /// 40° would put the head through the shelf.
        /// </summary>
        public const float StorageBend = 20f;

        // The old chef's knife sheath (SM_Chef) is reused, moved from its hip down to the barrel's.
        // Moving it moves the knife's belt pose (ChefArmsView) by the same amount.
        public static readonly Vector3 SheathShift = new Vector3(0.04f, -0.32f, -0.06f);

        public enum HatKind { None, Toque, Cap, Bandana, Headband, Horns, SunHat }
        public enum HairKind { Bald, Fringe, Crop, Bun, Afro }
        public enum NoseKind { Button, Big }

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
            /// <summary>Apron and neckerchief colour; null = player colour.</summary>
            public Color? Accent;
            public Color TieColor = new Color(0.55f, 0.15f, 0.15f);
            /// <summary>Body width (the concept's build dial). The head stays the same.</summary>
            public float Build = 1f;
        }

        public sealed class Rig
        {
            public Transform Waist;
            public Transform LeftShoulder;
            public Transform RightShoulder;
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
        /// The customers' archetypes (concept v9.2, section C): clothes, a prop and a hat tell them
        /// apart. Colours stay clear of the four player colours (orange, teal, raspberry, lime).
        /// </summary>
        public static IReadOnlyList<Look> CustomerLooks() => new[]
        {
            new Look { Name = "Regular", Top = new Color(0.62f, 0.2f, 0.16f), Bottom = new Color(0.2f, 0.27f, 0.42f),
                Hat = HatKind.Cap, HatColor = new Color(0.55f, 0.18f, 0.15f), HatAccent = new Color(0.92f, 0.88f, 0.8f),
                Hair = new Color(0.45f, 0.42f, 0.4f), HairStyle = HairKind.Crop, Moustache = true, Nose = NoseKind.Big },
            new Look { Name = "Office", Skin = new Color(0.55f, 0.36f, 0.24f), Top = new Color(0.27f, 0.29f, 0.34f),
                Bottom = new Color(0.22f, 0.23f, 0.27f), Hair = CharacterArt.DarkHair, HairStyle = HairKind.Crop,
                Tie = true, TieColor = new Color(0.2f, 0.32f, 0.6f) },
            new Look { Name = "DateNight", Skin = new Color(0.98f, 0.83f, 0.7f), Top = new Color(0.5f, 0.12f, 0.2f),
                Bottom = new Color(0.98f, 0.83f, 0.7f), Dress = true, Hair = new Color(0.85f, 0.7f, 0.42f), HairStyle = HairKind.Bun },
            new Look { Name = "Tourist", Skin = new Color(0.86f, 0.62f, 0.45f), Top = new Color(0.88f, 0.9f, 0.95f),
                Bottom = new Color(0.82f, 0.74f, 0.55f), Hat = HatKind.SunHat, HatColor = new Color(0.9f, 0.82f, 0.6f),
                HatAccent = new Color(0.2f, 0.27f, 0.5f), Hair = new Color(0.4f, 0.26f, 0.15f), Camera = true },
            new Look { Name = "Retiree", Skin = new Color(0.94f, 0.76f, 0.64f), Top = new Color(0.7f, 0.6f, 0.45f),
                Bottom = new Color(0.4f, 0.36f, 0.32f), Hair = new Color(0.85f, 0.85f, 0.83f), HairStyle = HairKind.Fringe,
                Glasses = true, Nose = NoseKind.Big },
            new Look { Name = "Gym", Skin = new Color(0.36f, 0.22f, 0.15f), Top = new Color(0.22f, 0.3f, 0.5f),
                Bottom = new Color(0.18f, 0.18f, 0.2f), Hat = HatKind.Headband, HatAccent = new Color(0.95f, 0.95f, 0.92f),
                Hair = CharacterArt.DarkHair, HairStyle = HairKind.Afro, Build = 1.08f },
        };

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
        public static Rig Build(Transform visuals, Look look)
        {
            if (!Available)
                return null;

            var rig = new Rig();
            var bodyScale = new Vector3(look.Build, 1f, look.Build);

            // Lower body: the legs and hips widen with the build, the shoes don't.
            var lower = Frame(visuals, "Lower", Vector3.zero, bodyScale);
            Part(lower, "CharLegs", look.Bottom);
            Part(visuals, "CharShoes", look.Shoes);
            Part(lower, look.Dress ? "CharDress" : "CharHips", look.Top);
            if (look.Apron)
                Coloured(rig, lower, "CharApron", look.Accent);

            rig.Waist = Frame(visuals, "Waist", new Vector3(0f, WaistHeight, 0f), Vector3.one);
            var body = Frame(rig.Waist, "Body", Vector3.zero, bodyScale);
            Part(body, "CharTorso", look.Top);
            if (look.Buttons)
                Part(body, "CharButtons", Ink);
            if (look.Tie)
                Part(body, "CharTie", look.TieColor);
            if (look.Camera)
                Part(body, "CharCamera", Ink);
            if (look.Scarf)
                Coloured(rig, rig.Waist, "CharScarf", look.Accent);

            // The head and everything on it: the same size whatever the build.
            Part(rig.Waist, "CharHead", look.Skin);
            Part(rig.Waist, look.Nose == NoseKind.Big ? "CharNoseBig" : "CharNoseButton", look.Skin);
            Part(rig.Waist, "CharEyes", Ink);
            Part(rig.Waist, "CharEyeShine", Shine);
            Part(rig.Waist, "CharSmile", Ink);
            if (!look.Sunglasses)
                Part(rig.Waist, "CharBrows", look.Hair);
            if (look.Moustache)
                Part(rig.Waist, "CharMoustache", look.Hair);
            if (look.Glasses)
                Part(rig.Waist, "CharGlasses", Ink);
            if (look.Sunglasses)
                Part(rig.Waist, "CharSunglasses", Ink);
            Hair(rig.Waist, look);
            Hat(rig, look);

            // Arms: a short sleeve and a mitten on each shoulder, which turn as ChefArmsView aims them.
            rig.LeftShoulder = Arm(rig.Waist, "Shoulder_L", -Shoulder.x * look.Build, look);
            rig.RightShoulder = Arm(rig.Waist, "Shoulder_R", Shoulder.x * look.Build, look);
            return rig;
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

        private static Transform Arm(Transform waist, string name, float x, Look look)
        {
            var shoulder = Frame(waist, name, new Vector3(x, Shoulder.y, 0f), Vector3.one);
            Part(shoulder, "CharArm", look.Top);
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

        private static Renderer Part(Transform parent, string mesh, Color color, string material = null)
        {
            var shared = PilotArt.FindMesh(Model, mesh);
            if (shared == null)
                return null;
            var part = new GameObject(mesh);
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
