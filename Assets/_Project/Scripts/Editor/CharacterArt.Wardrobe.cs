using System.Collections.Generic;
using System.Linq;
using Game.Presentation;
using Game.Simulation.Characters;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The player's chef with the whole tailor's rail on it (decision 2026-11-09): every hat, face,
    /// accessory and outfit in <see cref="Wardrobe"/> is built and hidden, and <see cref="ChefWardrobe"/>
    /// shows the slot's look. The player colour stays on the neckerchief, the apron and each hat's
    /// band, peak, gems or cone.
    /// </summary>
    public static partial class CharacterArt
    {
        private sealed class OutfitDef
        {
            public Garment Garment = Garment.Shirt;
            public Color Top;
            // Literal colours here: statics in the other CharacterArt file may not be set up yet
            // (C# doesn't order static initialisers across the files of a partial class).
            public Color Under = new Color(0.94f, 0.93f, 0.9f);
            public bool Buttons, Tie, Camera;
            public int Pattern;
            public Color PatternColour;
        }

        // What each outfit on the rail is made of; ids match Wardrobe's.
        private static readonly Dictionary<string, OutfitDef> Outfits = new Dictionary<string, OutfitDef>
        {
            ["whites"] = new OutfitDef { Top = new Color(0.97f, 0.97f, 0.95f), Buttons = true },
            ["linecook"] = new OutfitDef { Top = new Color(0.3f, 0.32f, 0.36f) },
            ["wok"] = new OutfitDef { Garment = Garment.TankTop, Top = new Color(0.92f, 0.9f, 0.86f) },
            ["pastry"] = new OutfitDef { Top = new Color(0.96f, 0.87f, 0.87f), Buttons = true },
            ["suit"] = new OutfitDef { Garment = Garment.Jacket, Top = new Color(0.14f, 0.15f, 0.18f), Tie = true },
            ["tourist"] = new OutfitDef { Top = new Color(0.88f, 0.9f, 0.95f), Camera = true, Pattern = 1, PatternColour = new Color(0.2f, 0.25f, 0.5f) },
        };

        // Each face: its hair and colour, nose and facial hair; ids match Wardrobe's.
        private static readonly Dictionary<string, (HairKind Hair, Color Colour, NoseKind Nose, bool Moustache)> Faces =
            new Dictionary<string, (HairKind, Color, NoseKind, bool)>
            {
                ["crop"] = (HairKind.Crop, new Color(0.3f, 0.19f, 0.12f), NoseKind.Button, false),
                ["moustache"] = (HairKind.Crop, new Color(0.2f, 0.13f, 0.09f), NoseKind.Big, true),
                ["bun"] = (HairKind.Bun, new Color(0.85f, 0.7f, 0.42f), NoseKind.Button, false),
                ["afro"] = (HairKind.Afro, new Color(0.2f, 0.13f, 0.09f), NoseKind.Button, false),
                ["bald"] = (HairKind.Bald, new Color(0.45f, 0.42f, 0.4f), NoseKind.Big, false),
                ["fringe"] = (HairKind.Fringe, new Color(0.85f, 0.85f, 0.83f), NoseKind.Big, true),
                ["ginger"] = (HairKind.Crop, new Color(0.62f, 0.3f, 0.14f), NoseKind.Button, false),
                ["darkbun"] = (HairKind.Bun, new Color(0.2f, 0.13f, 0.09f), NoseKind.Big, false),
            };

        // A point on the head as authored in Blender (x, y, height above the head's centre), where
        // it ends up in the waist's frame: scaled with the head (0.87) about its centre (0.71 m above
        // the waist), and Blender's x and y are the game's -x and -z.
        private static Vector3 HeadPoint(Vector3 blender) =>
            new Vector3(-blender.x * 0.87f, 0.71f + blender.z * 0.87f, -blender.y * 0.87f);

        // A pivot that sways (HatSway) for a loose part authored about it (`swing` in Blender).
        private static Transform Sway(Transform parent, string name, Vector3 blenderPivot, float stiffness, float damping,
            float drag, float inertia, float breeze, float maxAngle)
        {
            var pivot = Frame(parent, name, HeadPoint(blenderPivot), Vector3.one);
            pivot.gameObject.AddComponent<HatSway>().Set(stiffness, damping, drag, inertia, breeze, maxAngle);
            return pivot;
        }

        // Notes floating up and away from an ear (FxMusicNote particles): they rise, drift outwards and
        // wobble, and shrink away at the top rather than fading, as the toon look has no see-through.
        private static Renderer MusicNotes(Transform parent, Vector3 at, float side)
        {
            var emitter = new GameObject("MusicNotes");
            emitter.transform.SetParent(parent, false);
            emitter.transform.localPosition = at;
            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 1.9f); // big enough to read from the game camera
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; // they're left behind as the chef walks
            main.maxParticles = 8;
            main.playOnAwake = true;
            var emission = particles.emission;
            emission.rateOverTime = 1.8f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // straight up
            var drift = particles.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.Local;
            drift.x = new ParticleSystem.MinMaxCurve(-side * 0.08f, -side * 0.16f); // out from the ear
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.7f;
            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = PilotArt.FindMesh(Model, "FxMusicNote");
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = StyleTest.EnsureToonMaterial("ChefAccent", Color.white);
            return renderer;
        }

        // Where the propeller's blades turn, in the waist's frame: the top of the stem, 0.52 m above
        // the authored head's centre (0.71 m), scaled with the head (0.87); Blender's y is the game's -z.
        private static readonly Vector3 PropellerHub = new Vector3(0f, 0.71f + 0.52f * 0.87f, -0.02f * 0.87f);

        // The skin tones in Wardrobe's order: skin1 (the default chef's), skin0, then skin2 on.
        private static Color SkinFor(string id) => SkinTones[int.Parse(id.Substring(4))];

        /// <summary>Builds the player's chef with every option on the tailor's rail, under <paramref name="visuals"/>.</summary>
        public static Rig BuildChef(Transform visuals, bool swingArms)
        {
            if (!Available)
                return null;
            var basics = PlayerChef();
            var rig = new Rig();
            rig.Figure = Frame(visuals, "Figure", Vector3.zero, Vector3.one);
            var lower = Frame(rig.Figure, "Lower", Vector3.zero, Vector3.one);
            rig.LeftHip = Leg(rig.Figure, "Hip_L", -Hip.x, basics);
            rig.RightHip = Leg(rig.Figure, "Hip_R", Hip.x, basics);
            rig.Tinted.Add(Part(lower, "CharApron", Color.white, "ChefAccent"));

            rig.Waist = Frame(rig.Figure, "Waist", new Vector3(0f, WaistHeight, 0f), Vector3.one);
            var body = Frame(rig.Waist, "Body", Vector3.zero, Vector3.one);
            rig.Tinted.Add(Part(rig.Waist, "CharScarf", Color.white, "ChefAccent"));
            rig.LeftShoulder = Frame(rig.Waist, "Shoulder_L", new Vector3(-Shoulder.x, Shoulder.y, 0f), Vector3.one);
            rig.RightShoulder = Frame(rig.Waist, "Shoulder_R", new Vector3(Shoulder.x, Shoulder.y, 0f), Vector3.one);

            // Outfits: hips, torso, what the garment adds, and both sleeves, together.
            var outfits = new List<ChefWardrobe.Entry>();
            foreach (var item in Wardrobe.Options(WardrobeModule.Outfit))
            {
                var def = Outfits[item.Id];
                var look = basics.Clone();
                look.Top = def.Top;
                look.Under = def.Under;
                var parts = new List<Renderer> { Part(lower, "CharHips", def.Top), Part(body, "CharTorso", def.Top) };
                parts.AddRange(GarmentParts(body, look, def.Garment).Select(p => p.GetComponent<Renderer>()));
                if (def.Buttons)
                    parts.Add(Part(body, "CharButtons", Ink));
                if (def.Tie)
                    parts.Add(Part(body, "CharTie", new Color(0.8f, 0.2f, 0.18f)));
                if (def.Camera)
                    parts.Add(Part(body, "CharCamera", Ink));
                foreach (var shoulder in new[] { rig.LeftShoulder, rig.RightShoulder })
                    parts.Add(def.Garment == Garment.TankTop
                        ? Part(shoulder, "CharArmBare", basics.Skin)
                        : Part(shoulder, "CharArm", def.Top));
                if (def.Pattern > 0)
                {
                    var printed = Patterned(def.Top, def.Pattern, def.PatternColour, 10f);
                    foreach (var part in parts.Where(p => p != null && (p.name == "CharTorso" || p.name == "CharHips" || p.name == "CharArm")))
                        part.sharedMaterial = printed;
                }
                outfits.Add(new ChefWardrobe.Entry { Id = item.Id, Parts = Objects(parts) });
            }
            foreach (var shoulder in new[] { rig.LeftShoulder, rig.RightShoulder })
                Part(shoulder, "CharHand", basics.Skin);

            // The face's common features, then each face: nose, brows, facial hair and the hair at
            // the back always; the top of the hair only under no covering hat.
            Part(rig.Waist, "CharHead", basics.Skin);
            Part(rig.Waist, "CharEyes", Ink);
            Part(rig.Waist, "CharEyeShine", Shine);
            Part(rig.Waist, "CharSmile", Ink);
            var faces = new List<ChefWardrobe.Entry>();
            foreach (var item in Wardrobe.Options(WardrobeModule.Face))
            {
                var (hair, colour, nose, moustache) = Faces[item.Id];
                var always = Frame(rig.Waist, "Face_" + item.Id, Vector3.zero, Vector3.one);
                var bare = Frame(rig.Waist, "FaceTop_" + item.Id, Vector3.zero, Vector3.one);
                Part(always, nose == NoseKind.Big ? "CharNoseBig" : "CharNoseButton", basics.Skin);
                Part(always, "CharBrows", colour);
                if (moustache)
                    Part(always, "CharMoustache", colour);
                switch (hair)
                {
                    case HairKind.Fringe:
                        Part(always, "HairFringe", colour);
                        break;
                    case HairKind.Bald:
                        break;
                    default:
                        Part(always, "HairBack", colour);
                        Part(bare, hair == HairKind.Afro ? "HairAfro" : "HairCrop", colour);
                        if (hair == HairKind.Bun)
                            Part(bare, "HairBun", colour);
                        break;
                }
                faces.Add(new ChefWardrobe.Entry { Id = item.Id, Parts = new[] { always.gameObject }, Bare = new[] { bare.gameObject } });
            }

            // Hats, each with its part in the player's colour.
            var hats = new List<ChefWardrobe.Entry>();
            foreach (var item in Wardrobe.Options(WardrobeModule.Hat))
            {
                var group = Frame(rig.Waist, "Hat_" + item.Id, Vector3.zero, Vector3.one);
                var covers = true;
                switch (item.Id)
                {
                    case "toque":
                        Part(group, "HatToque", basics.HatColor);
                        rig.Tinted.Add(Part(group, "HatToqueBand", Color.white, "ChefAccent"));
                        break;
                    case "cap":
                        Part(group, "HatCap", basics.HatColor);
                        rig.Tinted.Add(Part(group, "HatCapPeak", Color.white, "ChefAccent"));
                        break;
                    case "bandana":
                        // White polka dots on the player's colour, so it reads as a bandana, not a cap.
                        var bandana = Part(group, "HatBandana", Color.white, "ChefAccent");
                        bandana.sharedMaterial = PlayerPrint(3, Color.white, 16f);
                        rig.Tinted.Add(bandana);
                        // Its tails flutter as the chef moves (the user's note, 2026-11-09).
                        rig.Tinted.Add(Part(Sway(group, "BandanaTails", new Vector3(0f, 0.31f, 0.04f), 90f, 6f, 12f, 1.5f, 6f, 45f),
                            "HatBandanaTails", Color.white, "ChefAccent"));
                        break;
                    case "headband":
                        rig.Tinted.Add(Part(group, "HatHeadband", Color.white, "ChefAccent"));
                        covers = false;
                        break;
                    case "horns":
                        Part(group, "HatHorns", new Color(0.72f, 0.14f, 0.12f));
                        rig.Tinted.Add(Part(group, "HatHeadband", Color.white, "ChefAccent"));
                        covers = false;
                        break;
                    case "crown":
                        Part(group, "HatCrown", Gold);
                        rig.Tinted.Add(Part(group, "HatCrownGems", Color.white, "ChefAccent"));
                        break;
                    case "partyhat":
                        // The cone wobbles on its elastic, the pompom with it.
                        var cone = Sway(group, "PartyCone", new Vector3(0f, 0.02f, 0.27f), 110f, 5f, 4f, 1.4f, 2f, 25f);
                        rig.Tinted.Add(Part(cone, "HatPartyCone", Color.white, "ChefAccent"));
                        Part(cone, "HatPartyPompom", Shine);
                        Part(group, "HatPartyStrap", Shine);
                        break;
                    case "captain":
                        Part(group, "HatCaptain", new Color(0.97f, 0.97f, 0.95f));
                        Part(group, "HatCaptainPeak", new Color(0.1f, 0.1f, 0.12f));
                        Part(group, "HatCaptainBraid", Gold);
                        rig.Tinted.Add(Part(group, "HatCaptainBand", Color.white, "ChefAccent"));
                        break;
                    case "propeller":
                        // Four panels, then the peak in the player's colour and the spinning blades.
                        foreach (var (panel, colour) in new[] { ("A", new Color(0.85f, 0.18f, 0.16f)), ("B", new Color(0.2f, 0.32f, 0.7f)),
                            ("C", new Color(0.95f, 0.78f, 0.2f)), ("D", new Color(0.18f, 0.5f, 0.3f)) })
                            Part(group, "HatPropellerPanel" + panel, colour);
                        rig.Tinted.Add(Part(group, "HatPropellerPeak", Color.white, "ChefAccent"));
                        Part(group, "HatPropellerStem", new Color(0.95f, 0.5f, 0.2f));
                        var hub = Frame(group, "PropellerHub", PropellerHub, Vector3.one);
                        Part(hub, "HatPropellerBlades", new Color(0.98f, 0.55f, 0.15f));
                        hub.gameObject.AddComponent<PropellerSpin>();
                        break;
                    case "headphones":
                        Part(group, "HatHeadphonesBand", new Color(0.15f, 0.15f, 0.17f));
                        rig.Tinted.Add(Part(group, "HatHeadphonesCups", Color.white, "ChefAccent"));
                        Part(group, "HatHeadphonesPads", new Color(0.15f, 0.15f, 0.17f));
                        // Music notes float up from each ear in the player's colour (the user's idea).
                        foreach (var side in new[] { -1f, 1f })
                            rig.Tinted.Add(MusicNotes(group, HeadPoint(new Vector3(side * 0.42f, 0.03f, 0.05f)), side));
                        covers = false;
                        break;
                    case "cowboy":
                        Part(group, "HatCowboy", new Color(0.55f, 0.36f, 0.2f));
                        rig.Tinted.Add(Part(group, "HatCowboyBand", Color.white, "ChefAccent"));
                        break;
                    case "headdress":
                        // The user's call (2026-11-09, decisions.md): a beaded browband in the player's
                        // colour, rosettes, and a halo of feathers tipped back, each swaying.
                        rig.Tinted.Add(Part(group, "HatHeaddressBand", Color.white, "ChefAccent"));
                        Part(group, "HatHeaddressBeads", new Color(0.95f, 0.94f, 0.9f));
                        Part(group, "HatHeaddressRosettes", new Color(0.8f, 0.25f, 0.2f));
                        // The war bonnet's halo (the user's reference): the feathers stand round the whole band
                        // on a cone whose axis is tipped back, so the front ones rise over the brow, the side ones
                        // fan out and back, and the back ones sweep down behind the head.
                        const int feathers = 16;
                        const float axisTilt = 55f * Mathf.Deg2Rad; // the cone's axis, back from straight up
                        const float spread = 55f * Mathf.Deg2Rad;   // each feather, out from that axis
                        var axis = new Vector3(0f, Mathf.Cos(axisTilt), -Mathf.Sin(axisTilt)); // the game's back is -z
                        for (var f = 0; f < feathers; f++)
                        {
                            // Blender's angle from +x, starting just off the front (-90°).
                            var angle = (-90f + 360f * (f + 0.5f) / feathers) * Mathf.Deg2Rad;
                            var foot = new Vector3(0.29f * Mathf.Cos(angle), 0.29f * Mathf.Sin(angle), 0.22f);
                            var outward = new Vector3(-Mathf.Cos(angle), 0f, -Mathf.Sin(angle)); // Blender x, y → the game's -x, -z
                            var across = (outward - axis * Vector3.Dot(outward, axis)).normalized;
                            var along = axis * Mathf.Cos(spread) + across * Mathf.Sin(spread);
                            var face = across * Mathf.Cos(spread) - axis * Mathf.Sin(spread); // the vane faces out of the cone
                            var feather = Sway(group, "Feather", foot, 45f + f % 3 * 8f, 4f, 6f, 1.2f, 6f, 30f);
                            feather.localRotation = Quaternion.LookRotation(face, along);
                            Part(feather, "HatHeaddressFeather", new Color(0.96f, 0.95f, 0.9f));
                            Part(feather, "HatHeaddressFeatherTip", new Color(0.16f, 0.13f, 0.12f));
                        }
                        covers = false;
                        break;
                    case "plumed":
                        Part(group, "HatPlumed", new Color(0.3f, 0.22f, 0.32f));
                        rig.Tinted.Add(Part(group, "HatPlumedBand", Color.white, "ChefAccent"));
                        // The feather sways with every step and drifts in the breeze.
                        Part(Sway(group, "Feather", new Vector3(0.2f, 0.1f, 0.27f), 55f, 4.5f, 9f, 1.6f, 7f, 40f),
                            "HatPlumedFeather", new Color(0.96f, 0.95f, 0.92f));
                        break;
                    case "straw":
                        Part(group, "HatStraw", new Color(0.82f, 0.68f, 0.42f));
                        Part(group, "HatStrawRibs", new Color(0.62f, 0.48f, 0.28f));
                        rig.Tinted.Add(Part(group, "HatStrawStrap", Color.white, "ChefAccent"));
                        break;
                }
                hats.Add(new ChefWardrobe.Entry { Id = item.Id, Parts = new[] { group.gameObject }, Covers = covers });
            }

            // Accessories.
            var accessories = new List<ChefWardrobe.Entry>();
            foreach (var item in Wardrobe.Options(WardrobeModule.Accessory))
            {
                var group = Frame(rig.Waist, "Accessory_" + item.Id, Vector3.zero, Vector3.one);
                switch (item.Id)
                {
                    case "glasses": Part(group, "CharGlasses", Ink); break;
                    case "sunglasses": Part(group, "CharSunglasses", Ink); break;
                    case "aviators":
                        Part(group, "CharAviators", Ink);
                        Part(group, "CharAviatorFrames", Gold);
                        break;
                }
                accessories.Add(new ChefWardrobe.Entry { Id = item.Id, Parts = new[] { group.gameObject } });
            }

            var skins = Wardrobe.Options(WardrobeModule.Skin)
                .Select(item => new ChefWardrobe.Entry { Id = item.Id, Colour = SkinFor(item.Id) }).ToList();
            var skinNames = new[] { "CharHead", "CharNoseButton", "CharNoseBig", "CharHand", "CharArmBare", "CharTankShoulders" };
            var skinParts = rig.Figure.GetComponentsInChildren<Renderer>(includeInactive: true)
                .Where(r => skinNames.Contains(r.gameObject.name)).ToArray();
            rig.Tinted.RemoveAll(r => r == null);

            var wardrobe = new SerializedObject(rig.Figure.gameObject.AddComponent<ChefWardrobe>());
            WriteEntries(wardrobe.FindProperty("_hats"), hats);
            WriteEntries(wardrobe.FindProperty("_faces"), faces);
            WriteEntries(wardrobe.FindProperty("_skins"), skins);
            WriteEntries(wardrobe.FindProperty("_accessories"), accessories);
            WriteEntries(wardrobe.FindProperty("_outfits"), outfits);
            var skin = wardrobe.FindProperty("_skin");
            skin.arraySize = skinParts.Length;
            for (var i = 0; i < skinParts.Length; i++)
                skin.GetArrayElementAtIndex(i).objectReferenceValue = skinParts[i];
            wardrobe.ApplyModifiedPropertiesWithoutUndo();

            // The default chef shows in the prefab: everything else starts hidden.
            rig.Figure.GetComponent<ChefWardrobe>().Show(new ChefLook());

            AddGait(rig, basics, swingArms);
            return rig;
        }

        private static GameObject[] Objects(IEnumerable<Renderer> parts) =>
            parts.Where(p => p != null).Select(p => p.gameObject).ToArray();

        private static void WriteEntries(SerializedProperty list, List<ChefWardrobe.Entry> entries)
        {
            list.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = entries[i].Id;
                element.FindPropertyRelative("Covers").boolValue = entries[i].Covers;
                element.FindPropertyRelative("Colour").colorValue = entries[i].Colour;
                WriteObjects(element.FindPropertyRelative("Parts"), entries[i].Parts);
                WriteObjects(element.FindPropertyRelative("Bare"), entries[i].Bare);
            }
        }

        private static void WriteObjects(SerializedProperty list, GameObject[] objects)
        {
            objects ??= System.Array.Empty<GameObject>();
            list.arraySize = objects.Length;
            for (var i = 0; i < objects.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }

        /// <summary>
        /// A white toon material printed with a pattern, for a part tinted in the player's colour:
        /// the tint (a property block on _BaseColor) colours the cloth, and the print stays.
        /// </summary>
        private static Material PlayerPrint(int pattern, Color patternColour, float scale) =>
            Patterned(Color.white, pattern, patternColour, scale);

        /// <summary>The house toon material printed with a cloth pattern (Game/Toon's _Pattern).</summary>
        public static Material Patterned(Color colour, int pattern, Color patternColour, float scale)
        {
            var material = StyleTest.EnsureToonMaterial(
                $"Char_{ColorUtility.ToHtmlStringRGB(colour)}_P{pattern}{ColorUtility.ToHtmlStringRGB(patternColour)}", colour);
            material.SetFloat("_Pattern", pattern);
            material.SetColor("_PatternColor", patternColour);
            material.SetFloat("_PatternScale", scale);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
