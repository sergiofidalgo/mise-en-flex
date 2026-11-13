using Game.Presentation.Office.Ambience;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The modelled things that stand round a restaurant (SM_Outside, from the Blender script; the
    /// user's notes, 2026-11-12: the chimney, the trees, the menu, the branding and the houses
    /// behind didn't convince): houses, trees, the air conditioning's outdoor unit, the menu's A-board, the sign. Each
    /// is put together here from its meshes, one colour a mesh.
    /// </summary>
    public static class Outside
    {
        private const string Model = "SM_Outside";

        public static bool Available => PilotArt.FindMesh(Model, "OutHouseAWalls") != null;

        // Brick, render and painted board; a roof of slate or of tile; shutters and a door to go with them.
        private static readonly (Color Wall, Color Roof, Color Accent)[] Houses =
        {
            (new Color(0.74f, 0.5f, 0.41f), new Color(0.33f, 0.31f, 0.36f), new Color(0.24f, 0.4f, 0.38f)),
            (new Color(0.86f, 0.8f, 0.66f), new Color(0.62f, 0.36f, 0.29f), new Color(0.5f, 0.24f, 0.22f)),
            (new Color(0.62f, 0.69f, 0.74f), new Color(0.36f, 0.33f, 0.34f), new Color(0.9f, 0.88f, 0.82f)),
            (new Color(0.8f, 0.62f, 0.5f), new Color(0.4f, 0.3f, 0.3f), new Color(0.28f, 0.34f, 0.5f)),
        };
        private static readonly Color Cream = new Color(0.94f, 0.91f, 0.84f);
        private static readonly Color DarkWood = new Color(0.24f, 0.18f, 0.15f);
        private static readonly Color Copper = new Color(0.74f, 0.42f, 0.25f);

        /// <summary>A house, its front to the south: a cottage or a taller one, turn about. Returns its window panes.</summary>
        public static Renderer House(Transform parent, Vector3 at, int index)
        {
            var tag = index % 2 == 0 ? "A" : "B";
            var (wall, roof, accent) = Houses[index % Houses.Length];
            var house = Group(parent, "House", at);
            var brick = HouseStyle.RoomMaterial($"StreetHouse{index % Houses.Length}", "BRICK", m =>
            {
                m.SetColor("_BaseColor", wall);
                m.SetColor("_SeamColor", wall * 0.8f);
                m.SetFloat("_Size", 0.16f);
                m.SetFloat("_Length", 0.42f);
                m.SetFloat("_SeamWidth", 0.022f);
                m.SetFloat("_Variation", 0.07f);
            });
            Kit.Part(house, Model, $"OutHouse{tag}Walls", brick);
            Kit.Part(house, Model, $"OutHouse{tag}Roof", Kit.Toon($"StreetRoof_{ColorUtility.ToHtmlStringRGB(roof)}", roof));
            Kit.Part(house, Model, $"OutHouse{tag}Trim", Kit.Toon("StreetHouseTrim", Cream));
            Kit.Part(house, Model, $"OutHouse{tag}Shutters", Kit.Toon($"StreetShutter_{ColorUtility.ToHtmlStringRGB(accent)}", accent));
            Kit.Part(house, Model, $"OutHouse{tag}Door", Kit.Toon($"StreetDoor_{ColorUtility.ToHtmlStringRGB(accent)}", Color.Lerp(accent, DarkWood, 0.35f)));
            Kit.Part(house, Model, $"OutHouse{tag}Chimney", Kit.Toon("StreetChimney", new Color(0.6f, 0.38f, 0.32f)));
            var glass = Kit.Part(house, Model, $"OutHouse{tag}Glass", Kit.Toon("StreetWindowOff", new Color(0.24f, 0.29f, 0.38f)));
            return glass != null ? glass.GetComponent<Renderer>() : null;
        }

        /// <summary>A broad tree or a poplar, its leaves in two tones and stirring in the breeze.</summary>
        public static void Tree(Transform parent, Vector3 at, bool poplar, float scale, float phase)
        {
            var kind = poplar ? "OutPoplar" : "OutTree";
            var tree = Group(parent, poplar ? "Poplar" : "Tree", at);
            tree.localScale = Vector3.one * scale;
            Kit.Part(tree, Model, kind + "Trunk", Kit.Toon("StreetTrunk", new Color(0.4f, 0.28f, 0.2f)));
            var crown = Group(tree, "Crown", Vector3.zero);
            Kit.Part(crown, Model, kind + "Leaves", Kit.Toon(poplar ? "StreetPoplarLeaves" : "StreetTreeLeaves", poplar ? new Color(0.22f, 0.4f, 0.3f) : new Color(0.27f, 0.47f, 0.3f)));
            Kit.Part(crown, Model, kind + "LeavesLight", Kit.Toon(poplar ? "StreetPoplarLight" : "StreetTreeLight", poplar ? new Color(0.32f, 0.52f, 0.34f) : new Color(0.43f, 0.62f, 0.34f)));
            // The crown leans and swells a little, each tree out of step with the next.
            crown.gameObject.AddComponent<Oscillate>().Set(Oscillate.Shape.Wave, new Vector3(0.04f, 0f, 0f), new Vector3(0f, 0f, poplar ? 1.6f : 0.8f),
                new Vector3(0.015f, -0.01f, 0.015f), 4.2f + Mathf.Repeat(phase * 3.7f, 1.6f), 0.2f, Mathf.Repeat(phase, 1f));
            foreach (var part in tree.GetComponentsInChildren<Transform>())
                part.gameObject.isStatic = false;
        }

        // The fan's axle in the unit: the Blender script's OUT_UNIT_FAN, in Unity's axes.
        private static readonly Vector3 UnitFan = new Vector3(0.14f, 0.42f, -0.13f);

        /// <summary>
        /// The air conditioning's outdoor unit, standing on the ground, its front turned
        /// <paramref name="yaw"/> degrees from south. Returns it and its fan, which turns about
        /// its own local z.
        /// </summary>
        public static (Transform Unit, Transform Fan) OutdoorUnit(Transform parent, Vector3 at, float yaw)
        {
            var unit = Group(parent, "OutdoorUnit", at);
            unit.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Kit.Part(unit, Model, "OutUnitBody", Kit.Toon("StreetUnitBody", new Color(0.9f, 0.87f, 0.78f)));
            Kit.Part(unit, Model, "OutUnitGrille", Kit.Toon("StreetUnitGrille", new Color(0.78f, 0.75f, 0.67f)));
            Kit.Part(unit, Model, "OutUnitDark", Kit.Toon("StreetUnitDark", new Color(0.17f, 0.19f, 0.21f)));
            Kit.Part(unit, Model, "OutUnitFeet", Kit.Toon("KitSteel", new Color(0.72f, 0.73f, 0.75f)));
            var fan = Group(unit, "Fan", UnitFan);
            Kit.Part(fan, Model, "OutUnitFan", Kit.Toon("StreetUnitFan", new Color(0.55f, 0.57f, 0.6f)));
            foreach (var part in unit.GetComponentsInChildren<Transform>())
                part.gameObject.isStatic = false; // the fan turns
            return (unit, fan);
        }

        /// <summary>The menu chalked on an A-board, facing the street.</summary>
        public static Transform MenuBoard(Transform parent, Vector3 at)
        {
            var board = Group(parent, "MenuBoard", at);
            Kit.Part(board, Model, "OutMenuFrame", Kit.Toon("KitBenchWood", new Color(0.52f, 0.36f, 0.24f)));
            Kit.Part(board, Model, "OutMenuSlate", Kit.Toon("KitSlate", new Color(0.16f, 0.18f, 0.17f)));
            Kit.Part(board, Model, "OutMenuChalk", Kit.Glow("StreetChalk", new Color(0.96f, 0.95f, 0.9f)));
            return board;
        }

        /// <summary>
        /// The restaurant's sign: a cream board in a copper rim under a cloche, between two dark
        /// posts <paramref name="postsApart"/> metres apart. The board's face is 1.9 m by 1.05 m,
        /// its middle 3.6 m up; the game letters the name on it.
        /// </summary>
        public static void Sign(Transform parent, Vector3 at, float postsApart)
        {
            var sign = Group(parent, "Sign", at);
            Kit.Part(sign, Model, "OutSignPosts", Kit.Toon("StreetSignPosts", DarkWood));
            Kit.Part(sign, Model, "OutSignBoard", Kit.Toon("StreetSignBoard", new Color(0.96f, 0.92f, 0.82f)));
            Kit.Part(sign, Model, "OutSignRim", Kit.Toon("StreetSignCopper", Copper));
            foreach (var side in new[] { -1f, 1f })
            {
                var post = sign.gameObject.AddComponent<BoxCollider>();
                post.center = new Vector3(side * postsApart / 2f, 1.1f, 0f);
                post.size = new Vector3(0.2f, 2.2f, 0.2f);
            }
        }

        private static Transform Group(Transform parent, string name, Vector3 at)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = at;
            return group;
        }
    }

    /// <summary>
    /// The ground's surfaces (the user's note, 2026-11-12: flat colours across the scene): the room
    /// shader's world-space patterns, so brick is brick-sized wherever it lies.
    /// </summary>
    public static class Surfaces
    {
        /// <summary>Small bricks laid in rows, each a shade of its own: the pavement.</summary>
        public static Material BrickPaving(Color brick) => HouseStyle.RoomMaterial("StreetPavement", "PLANKS", m =>
        {
            m.SetColor("_BaseColor", brick);
            m.SetColor("_SeamColor", brick * 0.72f);
            m.SetFloat("_Size", 0.12f);
            m.SetFloat("_Length", 0.26f);
            m.SetFloat("_SeamWidth", 0.014f);
            m.SetFloat("_Variation", 0.13f);
            m.SetFloat("_Grain", 0f);
        });

        /// <summary>Square paving slabs <paramref name="size"/> metres across.</summary>
        public static Material Slabs(string name, Color stone, float size) => HouseStyle.RoomMaterial(name, "TILES", m =>
        {
            m.SetColor("_BaseColor", stone);
            m.SetColor("_SeamColor", stone * 0.78f);
            m.SetFloat("_Size", size);
            m.SetFloat("_SeamWidth", 0.03f);
            m.SetFloat("_Variation", 0.07f);
        });

        /// <summary>Asphalt: mottled, with grit.</summary>
        public static Material Asphalt(string name, Color tar) => HouseStyle.RoomMaterial(name, "STONE", m =>
        {
            m.SetColor("_BaseColor", tar);
            m.SetFloat("_Size", 0.22f);
            m.SetFloat("_Variation", 0.07f);
        });

        /// <summary>Grass: broad lighter and darker patches, flecked.</summary>
        public static Material Grass(Color green) => HouseStyle.RoomMaterial("StreetGrass", "STONE", m =>
        {
            m.SetColor("_BaseColor", green);
            m.SetFloat("_Size", 0.3f);
            m.SetFloat("_Variation", 0.11f);
        });
    }
}
