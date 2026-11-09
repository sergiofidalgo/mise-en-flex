using Game.Data;
using Game.Gameplay.Office;
using Game.Presentation;
using Game.Presentation.Hud;
using Game.Presentation.Office;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The tailor's room (decision 2026-11-09, plan in docs/plans/tailor.md; the user's notes on
    /// ladders, the tailor and a busier room): four step-ladders along the back, each before a
    /// three-way mirror, that any player climbs to dress; the tailor, who potters about the middle
    /// of the room and walks over to measure whoever has just climbed up; and the trade's clutter.
    /// The room is the floor's back-left square (x -17.5 to -9, z 3.5 to 13.5), its door on the east
    /// wall at z 6, into the boss's office; the middle and the way from the door stay clear.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // The ladders' top steps, along the back, each facing the room (south), mirrors behind.
        private static readonly Vector3[] TailorLadders =
        {
            new Vector3(-16.1f, 0f, 11.55f), new Vector3(-14.35f, 0f, 11.55f), new Vector3(-12.15f, 0f, 11.55f), new Vector3(-10.4f, 0f, 11.55f),
        };

        private static readonly Vector3 TailorHome = new Vector3(-13.25f, 0f, 8.4f);
        private const float LadderTop = 0.45f;

        private static void FurnishTailor(Transform room)
        {
            var walnut = Toon("OfficeWalnut", Walnut);
            var brass = Toon("OfficeBrass", Brass);

            // A round rug in the middle, where the tailor potters about.
            Shape(room, PrimitiveType.Cylinder, "Rug", Toon("TailorRug", new Color(0.36f, 0.16f, 0.2f)), TailorHome + new Vector3(0f, 0.006f, 0f), new Vector3(3.4f, 0.006f, 3.4f));

            // Jacket rails along the west wall.
            for (var r = 0; r < 2; r++)
            {
                var rail = new Vector3(-16.9f, 0f, 4.9f + r * 2.3f);
                Model(room, "Rail", OfficeModel, "OfficeRail", brass, rail, 90f, collider: new Vector3(1.7f, 1.6f, 0.5f));
                var jackets = new[] { Cream, Suit, new Color(0.55f, 0.36f, 0.22f), new Color(0.32f, 0.42f, 0.55f), Burgundy, Leather };
                for (var j = 0; j < 5; j++)
                {
                    var c = (j + r * 2) % jackets.Length;
                    Model(room, "Jacket", OfficeModel, "OfficeJacket", Toon($"OfficeJacket{c}", jackets[c]), rail + new Vector3(0f, 1.6f, -0.6f + j * 0.3f), 0f);
                }
            }

            // Shelves of fabric bolts on the west wall, rolled and stacked in every colour.
            var shelf = new Vector3(-17.15f, 0f, 9.8f);
            Block(room, "BoltShelf", walnut, shelf, new Vector3(0.45f, 1.9f, 1.7f), collider: true);
            var bolts = new[] { new Color(0.75f, 0.2f, 0.2f), new Color(0.2f, 0.32f, 0.6f), new Color(0.85f, 0.72f, 0.3f), new Color(0.25f, 0.5f, 0.35f),
                new Color(0.55f, 0.3f, 0.55f), Cream, new Color(0.3f, 0.3f, 0.34f), new Color(0.85f, 0.5f, 0.45f) };
            for (var level = 0; level < 3; level++)
            for (var k = 0; k < 5; k++)
            {
                var c = bolts[(level * 3 + k) % bolts.Length];
                Shape(room, PrimitiveType.Cylinder, "Bolt", Toon($"TailorBolt{ColorUtility.ToHtmlStringRGB(c)}", c),
                    shelf + new Vector3(0.3f, 0.45f + level * 0.55f, -0.62f + k * 0.31f), new Vector3(0.24f, 0.22f, 0.24f), Quaternion.Euler(0f, 0f, 90f));
            }

            // A rack of thread spools on the wall beside the shelf.
            var rack = new Vector3(-17.38f, 1.1f, 11.6f);
            Block(room, "SpoolRack", walnut, rack, new Vector3(0.06f, 0.7f, 0.9f));
            for (var row = 0; row < 3; row++)
            for (var k = 0; k < 5; k++)
            {
                var c = bolts[(row * 2 + k * 3) % bolts.Length];
                Shape(room, PrimitiveType.Cylinder, "Spool", Toon($"TailorBolt{ColorUtility.ToHtmlStringRGB(c)}", c),
                    rack + new Vector3(0.09f, 0.14f + row * 0.21f, -0.36f + k * 0.18f), new Vector3(0.08f, 0.05f, 0.08f));
            }

            // The cutting table: pattern paper, a bolt half unrolled, shears, a pin cushion, chalk.
            var table = Prop(room, "PF_Table", new Vector3(-13.3f, 0f, 4.7f), 0f, keep: "Body");
            if (table != null)
                table.transform.localScale = new Vector3(1.8f, 1f, 1.1f);
            var top = new Vector3(-13.3f, 0.79f, 4.7f);
            Block(room, "PatternPaper", Toon("TailorPaper", new Color(0.93f, 0.9f, 0.8f)), top + new Vector3(-0.35f, 0f, 0.05f), new Vector3(0.7f, 0.01f, 0.5f));
            Shape(room, PrimitiveType.Cylinder, "Bolt", Toon("TailorBoltRed", new Color(0.75f, 0.2f, 0.2f)), top + new Vector3(0.55f, 0.09f, 0.1f), new Vector3(0.18f, 0.32f, 0.18f), Quaternion.Euler(90f, 0f, 0f));
            Block(room, "Unrolled", Toon("TailorBoltRed", new Color(0.75f, 0.2f, 0.2f)), top + new Vector3(0.55f, 0f, -0.25f), new Vector3(0.6f, 0.012f, 0.4f));
            Block(room, "ShearBlade", Toon("OfficeChrome", new Color(0.78f, 0.8f, 0.84f)), top + new Vector3(-0.1f, 0.012f, -0.18f), new Vector3(0.05f, 0.012f, 0.26f));
            Shape(room, PrimitiveType.Cylinder, "ShearHandle", Toon("OfficeInk", Ink), top + new Vector3(-0.1f, 0.02f, -0.36f), new Vector3(0.09f, 0.008f, 0.09f));
            Shape(room, PrimitiveType.Sphere, "PinCushion", Toon("TailorCushion", new Color(0.8f, 0.2f, 0.25f)), top + new Vector3(-0.75f, 0.05f, -0.15f), new Vector3(0.14f, 0.09f, 0.14f));
            Block(room, "Chalk", Toon("TailorChalk", new Color(0.4f, 0.6f, 0.85f)), top + new Vector3(-0.55f, 0f, 0.25f), new Vector3(0.07f, 0.02f, 0.05f));
            Shape(room, PrimitiveType.Cylinder, "TapeRoll", Toon("TailorTape", new Color(0.95f, 0.8f, 0.25f)), top + new Vector3(0.1f, 0.02f, 0.22f), new Vector3(0.12f, 0.02f, 0.12f));

            // A sewing machine at its own table by the east wall, a spool on top.
            var sewing = new Vector3(-10.0f, 0f, 9.3f);
            Block(room, "SewingTable", walnut, sewing, new Vector3(0.6f, 0.75f, 1.0f), collider: true);
            var machine = Toon("TailorMachine", new Color(0.12f, 0.12f, 0.14f));
            var gold = Toon("OfficeBrass", Brass);
            Block(room, "MachineBase", machine, sewing + new Vector3(0f, 0.75f, 0f), new Vector3(0.24f, 0.08f, 0.5f));
            Block(room, "MachinePillar", machine, sewing + new Vector3(0f, 0.83f, 0.18f), new Vector3(0.14f, 0.24f, 0.12f));
            Block(room, "MachineArm", machine, sewing + new Vector3(0f, 1.03f, 0.0f), new Vector3(0.13f, 0.1f, 0.48f));
            Block(room, "MachineHead", machine, sewing + new Vector3(0f, 0.86f, -0.2f), new Vector3(0.11f, 0.2f, 0.1f));
            Block(room, "MachineStripe", gold, sewing + new Vector3(0.067f, 1.05f, 0.0f), new Vector3(0.004f, 0.04f, 0.36f));
            Shape(room, PrimitiveType.Cylinder, "MachineSpool", Toon("TailorBolt" + ColorUtility.ToHtmlStringRGB(bolts[0]), bolts[0]), sewing + new Vector3(0f, 1.17f, 0.12f), new Vector3(0.05f, 0.04f, 0.05f));
            Shape(room, PrimitiveType.Cylinder, "Stool", walnut, sewing + new Vector3(-0.6f, 0.25f, 0f), new Vector3(0.36f, 0.25f, 0.36f));

            // An ironing board with its iron, by the rails.
            var board = new Vector3(-15.4f, 0f, 4.2f);
            Block(room, "IroningBoard", Toon("TailorBoard", new Color(0.82f, 0.85f, 0.88f)), board + new Vector3(0f, 0.86f, 0f), new Vector3(1.2f, 0.03f, 0.36f));
            foreach (var x in new[] { -0.3f, 0.3f })
                Block(room, "IroningLeg", Toon("OfficeChrome", new Color(0.78f, 0.8f, 0.84f)), board + new Vector3(x, 0f, 0f), new Vector3(0.03f, 0.86f, 0.3f));
            Block(room, "Iron", Toon("TailorIron", new Color(0.3f, 0.45f, 0.7f)), board + new Vector3(0.35f, 0.89f, 0f), new Vector3(0.22f, 0.1f, 0.12f));

            // Dress forms: one in a half-made jacket by the sewing machine, one by the table.
            Mannequin(room, new Vector3(-9.75f, 0f, 11.9f));
            Mannequin(room, new Vector3(-11.4f, 0f, 4.2f));

            // A hat stand by the door with hats from the rail, so you see what's on offer coming in.
            var stand = new Vector3(-9.7f, 0f, 4.3f);
            Shape(room, PrimitiveType.Cylinder, "HatStandPole", walnut, stand + new Vector3(0f, 0.9f, 0f), new Vector3(0.06f, 0.9f, 0.06f));
            Shape(room, PrimitiveType.Cylinder, "HatStandFoot", walnut, stand + new Vector3(0f, 0.03f, 0f), new Vector3(0.45f, 0.03f, 0.45f));
            var hats = new[] { ("HatCowboy", new Color(0.55f, 0.36f, 0.2f), 1.62f, 30f), ("HatCaptain", new Color(0.97f, 0.97f, 0.95f), 1.42f, 150f),
                ("HatToque", new Color(0.97f, 0.97f, 0.95f), 1.2f, 260f) };
            foreach (var (mesh, colour, height, turn) in hats)
            {
                // Each hat sits on a hook: the kit's hats are built round the head's centre, 0.71 m
                // above the waist frame, their brims about 0.12 m above that, so they're lowered by
                // 0.83 m and pushed out from the pole.
                var hook = stand + Quaternion.Euler(0f, turn, 0f) * new Vector3(0f, 0f, 0.2f) + Vector3.up * height;
                Model(room, "StandHat", CharacterArt.Model, mesh, Toon($"Tailor{mesh}", colour), hook - Vector3.up * 0.83f, turn);
            }

            PottedPlant(room, new Vector3(-16.9f, 0f, 13.0f), 1f);
        }

        private static void AddTailor(Transform rooms)
        {
            var room = rooms.Find("Tailor");
            var walnut = Toon("OfficeWalnut", Walnut);
            var brass = Toon("OfficeBrass", Brass);
            var velvet = Toon("TailorVelvet", new Color(0.55f, 0.16f, 0.2f));

            // Four step-ladders, any player climbs any: a platform with two steps up to it, a
            // velvet top and a brass rail at the back, before a three-way mirror.
            var ladders = new TailorPlinth[TailorLadders.Length];
            for (var i = 0; i < ladders.Length; i++)
            {
                var at = TailorLadders[i];
                var ladder = new GameObject($"TailorLadder_{i + 1}").transform;
                ladder.SetParent(room, false);
                ladder.position = at;
                Block(ladder, "Platform", walnut, Vector3.zero + new Vector3(0f, 0f, 0.05f), new Vector3(0.72f, LadderTop - 0.03f, 0.55f), collider: true);
                Block(ladder, "Top", velvet, new Vector3(0f, LadderTop - 0.03f, 0.05f), new Vector3(0.68f, 0.03f, 0.5f));
                Block(ladder, "StepHigh", walnut, new Vector3(0f, 0f, -0.33f), new Vector3(0.66f, 0.3f, 0.22f));
                Block(ladder, "StepLow", walnut, new Vector3(0f, 0f, -0.53f), new Vector3(0.66f, 0.15f, 0.2f));
                foreach (var x in new[] { -0.33f, 0.33f })
                    Shape(ladder, PrimitiveType.Cylinder, "RailPost", brass, new Vector3(x, LadderTop + 0.35f, 0.3f), new Vector3(0.04f, 0.35f, 0.04f));
                Shape(ladder, PrimitiveType.Cylinder, "Rail", brass, new Vector3(0f, LadderTop + 0.7f, 0.3f), new Vector3(0.05f, 0.35f, 0.05f), Quaternion.Euler(0f, 0f, 90f));
                Mirror(room, at + new Vector3(0f, 0f, 1.45f), 180f);

                var plinth = new SerializedObject(ladder.gameObject.AddComponent<TailorPlinth>());
                plinth.FindProperty("_foot").vector3Value = new Vector3(0f, 0f, -0.85f);
                plinth.FindProperty("_top").vector3Value = new Vector3(0f, LadderTop, 0.05f);
                plinth.ApplyModifiedPropertiesWithoutUndo();
                ladders[i] = ladder.GetComponent<TailorPlinth>();
            }

            // The tailor, on their own walking frame (the office's breathing idle keeps the person
            // still within it), with a yellow tape for measuring.
            var mover = new GameObject("TailorWalk").transform;
            mover.SetParent(room, false);
            mover.position = TailorHome;
            var tailor = Person(mover, "Tailor", Vector3.zero, 0f, new Color(0.35f, 0.30f, 0.45f), new Color(0.22f, 0.2f, 0.26f), dress: look =>
            {
                look.Skin = CharacterArt.SkinTones[1];
                look.HairStyle = CharacterArt.HairKind.Fringe;
                look.Hair = CharacterArt.GreyHair[1];
                look.Moustache = true;
                look.Glasses = true;
                look.Tie = true;
                look.TieColor = new Color(0.75f, 0.6f, 0.3f);
                look.Garment = CharacterArt.Garment.Vest;
                look.Under = CharacterArt.ShirtColours[3];
                look.Stride = 0.85f;
                look.Waddle = 1f;
            });
            // The tailor moves about and players stand right beside them: no collider to snag on.
            foreach (var collider in tailor.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);
            var tape = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Tape", mover, Toon("TailorTape", new Color(0.95f, 0.8f, 0.25f)), keepCollider: false);
            tape.transform.localScale = new Vector3(0.035f, 0.006f, 0.3f);
            tape.SetActive(false); // shown, and stretched between the hands, only while measuring
            var attendant = new SerializedObject(mover.gameObject.AddComponent<TailorAttendant>());
            var list = attendant.FindProperty("_ladders");
            list.arraySize = ladders.Length;
            for (var i = 0; i < ladders.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = ladders[i];
            attendant.FindProperty("_gait").objectReferenceValue = tailor.GetComponentInChildren<WalkCycleView>();
            attendant.FindProperty("_waist").objectReferenceValue = FindDeep(tailor, "Waist");
            attendant.FindProperty("_leftShoulder").objectReferenceValue = FindDeep(tailor, "Shoulder_L");
            attendant.FindProperty("_rightShoulder").objectReferenceValue = FindDeep(tailor, "Shoulder_R");
            attendant.FindProperty("_tape").objectReferenceValue = tape.transform;
            attendant.FindProperty("_home").vector3Value = TailorHome;
            attendant.ApplyModifiedPropertiesWithoutUndo();

            var hud = new SerializedObject(new GameObject("TailorHud").AddComponent<TailorCardHud>());
            hud.FindProperty("_style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HudStyle>(SettingsFolder + "/HudStyle.asset");
            hud.FindProperty("_coop").objectReferenceValue = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            var plinths = hud.FindProperty("_plinths");
            plinths.arraySize = ladders.Length;
            for (var i = 0; i < ladders.Length; i++)
                plinths.GetArrayElementAtIndex(i).objectReferenceValue = ladders[i];
            hud.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
        }
    }
}
