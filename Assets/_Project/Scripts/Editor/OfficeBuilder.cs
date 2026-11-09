using System.Collections.Generic;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Office;
using Game.Presentation.Office;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor
{
    /// <summary>
    /// Builds the boss's floor (spec: docs/office-scene.md; decisions 2026-11-09): nine rooms on the
    /// penthouse of his tower, a terrace with a helipad behind, and the night city below. Players join
    /// through the one big elevator in reception. Only the space and the elevator work; the rooms'
    /// jobs (miniature, loans, tailor, test kitchen) come in Phase 6.
    ///
    /// It's built from the kitchen's own pieces wherever they exist, so the two read as one game: the
    /// room shader's planks, tiles and panelled walls; copies of the kitchen's counters, hob, fridge,
    /// shelf, tables and stools (visual only, without their station scripts); people made of the
    /// chef's model; the night city's trees, lamps and towers. What the kitchen has no model for
    /// (sofas, desks, office chairs, plants, the helicopter) comes from SM_Office, made by the same
    /// Blender script. Seeded, so every rebuild is the same. Menu: Tools/Game/Build Office. Needs
    /// PF_Player and the furniture prefabs (Build Greybox Kitchen).
    /// </summary>
    public static partial class OfficeBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Office_Greybox.unity";
        private const string SettingsFolder = "Assets/_Project/Data/Settings";
        private const string PrefabsFolder = "Assets/_Project/Prefabs";
        private const string SfxFolder = "Assets/_Project/Audio/SFX";
        private const string OfficeModel = "SM_Office";
        private const string CityModel = "SM_City";
        private const string ChefModel = "SM_Chef";
        private const int Seed = 2611_09;

        // The floor (user's notes, 2026-11-09): three columns and three rows. The middle column, the
        // reception, the guarded doors and the boss's office, is the grand one at 18 m; the side
        // rooms (lounge, test kitchen, tailor, accountant, hall of fame, miniature) need less and
        // are 8.5 m. The back row is deepest. The camera looks north, so the front row (−Z) is
        // nearest. The terrace runs along the back.
        private static readonly float[] ColumnEdges = { -17.5f, -9f, 9f, 17.5f };
        private static readonly float[] RowEdges = { -13.5f, -4.5f, 3.5f, 13.5f };
        private const float TerraceNorth = 19.5f;
        private static float West => ColumnEdges[0];
        private static float East => ColumnEdges[3];
        private static float South => RowEdges[0];
        private static float North => RowEdges[3];

        // Inner walls as in the kitchen, 1.0 m so the camera sees into every room, but panelled in
        // walnut with a brass cap rather than the kitchen's cream plaster. Outer walls are glass.
        private const float WallHeight = 1f;
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 2f;
        private const float SillHeight = 0.5f;
        private const float OuterWallHeight = 2.8f;
        private const float MullionSpacing = 2.2f;
        // Fewer mullions along the front, which the camera looks through into the rooms.
        private const float FrontMullionSpacing = 4.4f;
        private const float FrontWallHeight = 1.2f;
        private const float PictureHeight = 0.78f; // the kitchen's, over the panelling

        // The elevator car, against the front wall in reception, its doors facing north.
        private const float CarHalfWidth = 1.9f;
        private const float CarDoorZ = -11f;
        private const float CarWallHeight = 2.2f;
        private const float DoorWallHeight = 2.5f;
        private const float DoorPanelWidth = 1.5f;
        private const float DoorPanelHeight = 2.0f;

        // The chef model's frame (GreyboxKitchenBuilder): people are made of it.
        private const float ChefWaist = 0.85f;
        private const float ChefShoulderHeight = 0.38f;
        private const float ChefShoulderWidth = 0.27f;

        // Palette: the kitchen's warm woods and cream, dressed up; burgundy rather than the "Bad"
        // red for the carpet, and floors kept mid-dark so a white chef pops (spec §5).
        private static readonly Color Ink = new Color32(0x3A, 0x24, 0x14, 0xFF);
        private static readonly Color Walnut = new Color(0.30f, 0.19f, 0.12f);
        private static readonly Color WoodMid = new Color32(0x8B, 0x5A, 0x33, 0xFF);
        private static readonly Color Cream = new Color32(0xEA, 0xDF, 0xC6, 0xFF);
        private static readonly Color Brass = new Color(0.86f, 0.62f, 0.26f);
        private static readonly Color Burgundy = new Color(0.45f, 0.12f, 0.15f);
        private static readonly Color Leather = new Color(0.22f, 0.38f, 0.26f);
        private static readonly Color Suit = new Color(0.14f, 0.15f, 0.18f);
        private static readonly Color Skin = new Color(0.96f, 0.78f, 0.63f);
        private static readonly Color Face = new Color(0.16f, 0.11f, 0.09f);
        private static readonly Color Plant = new Color(0.26f, 0.48f, 0.28f);
        private static readonly Color Terracotta = new Color(0.62f, 0.36f, 0.24f);
        private static readonly Color WindowLit = new Color(1f, 0.80f, 0.45f);
        private static readonly Color WindowDark = new Color(0.12f, 0.13f, 0.22f);
        private static readonly Color LampWarm = new Color(1f, 0.85f, 0.66f);
        private static readonly Color Velvet = new Color(0.55f, 0.18f, 0.16f);

        // The city at dusk (user's note, 2026-11-09): warm facades in the game's palette, faded with
        // distance into a violet haze rather than a navy night.
        private static readonly Color Dusk = new Color(0.36f, 0.31f, 0.42f);
        private const float FogStart = 70f;
        private const float FogEnd = 230f;
        private const float CityDepth = 120f;
        private static readonly Color[] FacadeColors =
        {
            new Color(0.58f, 0.32f, 0.25f), // brick
            new Color(0.70f, 0.58f, 0.44f), // sandstone
            new Color(0.80f, 0.73f, 0.61f), // cream
            new Color(0.46f, 0.52f, 0.43f), // sage
            new Color(0.40f, 0.45f, 0.55f), // slate
            new Color(0.66f, 0.42f, 0.30f), // terracotta
        };
        private static readonly Color[] UmbrellaColors =
        {
            new Color(0.80f, 0.30f, 0.24f), new Color(0.18f, 0.40f, 0.32f), new Color(0.90f, 0.70f, 0.30f),
        };

        private static readonly Color[] TowerColors =
        {
            new Color(0.22f, 0.24f, 0.38f), new Color(0.30f, 0.25f, 0.36f), new Color(0.25f, 0.31f, 0.36f), new Color(0.20f, 0.21f, 0.32f),
        };

        private static System.Random _random;

        [MenuItem("Tools/Game/Build Office")]
        public static void Build()
        {
            // The scene first: opening one unloads assets nothing references yet (see the kitchen builder).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _random = new System.Random(Seed);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + "/PF_Player.prefab");
            if (playerPrefab == null)
                throw new System.InvalidOperationException("No PF_Player: run Tools/Game/Build Greybox Kitchen first.");
            if (PilotArt.FindMesh(OfficeModel, "OfficeBlock") == null)
                throw new System.InvalidOperationException("No SM_Office: run blender/build_pilot_models.py -- --only SM_Office.fbx.");
            var coop = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            var elevatorConfig = GreyboxAssets.EnsureAsset<ElevatorConfig>(SettingsFolder + "/ElevatorConfig.asset");

            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            HouseStyle.LightScene(sun); // the kitchen's light, so everything looks as it does there

            var floor = new GameObject("Floor").transform;
            BuildShell(floor);
            BuildWalls(floor);
            var rooms = new GameObject("Rooms").transform;
            BuildReception(rooms);
            BuildGuardedDoors(rooms);
            BuildBossOffice(rooms);
            BuildTailor(rooms);
            BuildTestKitchen(rooms);
            BuildBackRoom(rooms);
            BuildHallOfFame(rooms);
            BuildLounge(rooms);
            BuildAccountant(rooms);
            BuildTerrace(rooms);
            BuildCity();
            AddLife(rooms); // OfficeBuilder.Life.cs: birds, rooftop life, windows, people's idles
            LightRooms(rooms); // OfficeBuilder.Lighting.cs: each room's lamps
            AddTailor(rooms); // OfficeBuilder.Tailor.cs: the plinths dress their players

            var players = new GameObject("Players");
            var manager = players.AddComponent<PlayerInputManager>();
            manager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenJoinActionIsTriggered;
            manager.joinAction = new InputActionProperty(GreyboxKitchenBuilder.LoadActionReference("Gameplay/Join"));
            manager.playerPrefab = playerPrefab;

            var elevator = BuildElevator(floor, manager, elevatorConfig, out var spawnPoints);
            var join = new SerializedObject(players.AddComponent<PlayerJoinController>());
            join.FindProperty("_settings").objectReferenceValue = coop;
            var points = join.FindProperty("_spawnPoints");
            points.arraySize = spawnPoints.Length;
            for (var i = 0; i < spawnPoints.Length; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            join.ApplyModifiedPropertiesWithoutUndo();
            BuildElevatorView(elevator);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 45f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Dusk; // the haze, so the city fades into the sky
            HouseStyle.SmoothEdges(camera);
            camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<OfficeCamera>();
            var framing = new SerializedObject(follow);
            framing.FindProperty("_floorMin").vector2Value = new Vector2(West, South);
            framing.FindProperty("_floorMax").vector2Value = new Vector2(East, TerraceNorth);
            framing.ApplyModifiedPropertiesWithoutUndo();
            camera.transform.SetPositionAndRotation(follow.Wanted(), Quaternion.Euler(55f, 0f, 0f));
            NightLighting(sun, camera); // the moon, the night haze, lamps on (OfficeBuilder.Lighting.cs)
            AddOpening(camera, follow, manager, coop); // OfficeBuilder.Opening.cs: street, menu, climb, join

            GreyboxAssets.EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OfficeBuilder] Built {ScenePath}.");
        }

        // ---- The floor, walls and tower ----------------------------------------------------------

        private static Vector2 RoomCentre(int column, int row) =>
            new Vector2((ColumnEdges[column] + ColumnEdges[column + 1]) / 2f, (RowEdges[row] + RowEdges[row + 1]) / 2f);

        private static Vector2 RoomSize(int column, int row) =>
            new Vector2(ColumnEdges[column + 1] - ColumnEdges[column], RowEdges[row + 1] - RowEdges[row]);

        // The roof slab everyone walks on, and each room's floor on it in the room shader.
        private static void BuildShell(Transform floor)
        {
            Cube(floor, "RoofSlab", Toon("OfficeSlab", Walnut), new Vector3(0f, -0.4f, (South + TerraceNorth) / 2f),
                new Vector3(East - West + 0.8f, 0.4f, TerraceNorth - South + 0.8f), collider: true);

            RoomFloor(floor, "Floor_Tailor", 0, 2, Planks("OfficeHoneyPlanks", new Color(0.62f, 0.42f, 0.26f), new Color(0.34f, 0.21f, 0.13f), 0.16f, 1.2f));
            RoomFloor(floor, "Floor_BossOffice", 1, 2, Planks("OfficeWalnutPlanks", new Color(0.36f, 0.23f, 0.15f), new Color(0.2f, 0.12f, 0.08f), 0.2f, 1.8f));
            RoomFloor(floor, "Floor_BackRoom", 2, 2, Planks("OfficeOakPlanks", new Color(0.55f, 0.37f, 0.23f), new Color(0.3f, 0.19f, 0.12f), 0.18f, 1.4f));
            RoomFloor(floor, "Floor_TestKitchen", 0, 1, HouseStyle.KitchenFloor());
            RoomFloor(floor, "Floor_GuardedDoors", 1, 1, Tiles("OfficeAnteroomStone", new Color(0.36f, 0.30f, 0.26f), new Color(0.24f, 0.19f, 0.16f), 0.8f));
            RoomFloor(floor, "Floor_HallOfFame", 2, 1, Planks("OfficeHallPlanks", new Color(0.47f, 0.31f, 0.2f), new Color(0.26f, 0.16f, 0.1f), 0.18f, 1.6f));
            RoomFloor(floor, "Floor_Lounge", 0, 0, Tiles("OfficeLoungeCarpet", new Color(0.24f, 0.34f, 0.34f), new Color(0.2f, 0.29f, 0.29f), 1.5f, 0.004f));
            RoomFloor(floor, "Floor_Reception", 1, 0, Tiles("OfficeLobbyStone", new Color(0.58f, 0.48f, 0.38f), new Color(0.40f, 0.32f, 0.25f), 1.0f));
            RoomFloor(floor, "Floor_Accountant", 2, 0, Tiles("OfficeAccountCarpet", new Color(0.40f, 0.38f, 0.26f), new Color(0.35f, 0.33f, 0.22f), 1.5f, 0.004f));

            // The carpet: from the elevator's doors straight through the guarded doors to his rug.
            var length = 8.6f - CarDoorZ;
            var centre = (CarDoorZ + 8.6f) / 2f;
            Cube(floor, "Carpet", Toon("OfficeCarpet", Burgundy), new Vector3(0f, 0f, centre), new Vector3(2.2f, 0.016f, length));
            foreach (var x in new[] { -1.14f, 1.14f })
                Cube(floor, "CarpetEdge", Toon("OfficeBrass", Brass), new Vector3(x, 0f, centre), new Vector3(0.08f, 0.018f, length));

            Cube(floor, "Floor_Terrace", Planks("OfficeDecking", new Color(0.45f, 0.38f, 0.32f), new Color(0.27f, 0.22f, 0.19f), 0.14f, 2.4f),
                new Vector3(0f, 0f, (North + TerraceNorth) / 2f), new Vector3(East - West, 0.006f, TerraceNorth - North));
        }

        private static void RoomFloor(Transform parent, string name, int column, int row, Material material)
        {
            var centre = RoomCentre(column, row);
            var size = RoomSize(column, row);
            Cube(parent, name, material, new Vector3(centre.x, 0f, centre.y), new Vector3(size.x, 0.006f, size.y));
        }

        private static void BuildWalls(Transform floor)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(floor, false);
            var none = new float[0];
            float[] xs = ColumnEdges, zs = RowEdges;

            // Outside (user's note, 2026-11-09): a penthouse is walled in glass. Tall windows on a
            // panelled sill on the sides and back, so the city shows through. The front is cut low
            // (the art bible's dollhouse cut): the camera looks over it, and a tall frame there would
            // run across the front rooms. The terrace is reached only from the boss's office, by a
            // door either side of his desk.
            Glass(walls, "Glass_South", new Vector3(West, 0f, South), new Vector3(East, 0f, South), none, FrontMullionSpacing, FrontWallHeight);
            Glass(walls, "Glass_West", new Vector3(West, 0f, South), new Vector3(West, 0f, North), none, MullionSpacing);
            Glass(walls, "Glass_East", new Vector3(East, 0f, South), new Vector3(East, 0f, North), none, MullionSpacing);
            Glass(walls, "Glass_North", new Vector3(West, 0f, North), new Vector3(East, 0f, North), new[] { -6f, 6f }, MullionSpacing);

            // Who reaches what (user's call, 2026-11-09): the tailor and the miniature open only
            // from the boss's office, which only the guarded doors reach. The other six rooms all
            // connect: lounge, reception and accountant across the front; lounge to test kitchen
            // and accountant to hall of fame; test kitchen and hall of fame to the guarded doors.
            // The serving hatch from the test kitchen to the bodyguards is a gap filled by a counter.
            WallAlongZ(walls, "Wall_LoungeReception", xs[1], zs[0], zs[1], new[] { -7f });
            WallAlongZ(walls, "Wall_KitchenGuards", xs[1], zs[1], zs[2], new[] { -1.8f }, hatchAt: 1.6f);
            WallAlongZ(walls, "Wall_TailorBoss", xs[1], zs[2], zs[3], new[] { 6f });
            WallAlongZ(walls, "Wall_ReceptionAccountant", xs[2], zs[0], zs[1], new[] { -7f });
            WallAlongZ(walls, "Wall_GuardsHall", xs[2], zs[1], zs[2], new[] { -0.5f });
            WallAlongZ(walls, "Wall_BossBackRoom", xs[2], zs[2], zs[3], new[] { 6f });

            var west = (xs[0] + xs[1]) / 2f;
            var east = (xs[2] + xs[3]) / 2f;
            WallAlongX(walls, "Wall_LoungeKitchen", zs[1], xs[0], xs[1], new[] { west });
            WallAlongX(walls, "Wall_ReceptionGuards", zs[1], xs[1], xs[2], new[] { 0f }, 3f);
            WallAlongX(walls, "Wall_AccountantHall", zs[1], xs[2], xs[3], new[] { east });
            WallAlongX(walls, "Wall_KitchenTailor", zs[2], xs[0], xs[1], none);
            WallAlongX(walls, "Wall_GuardsBoss", zs[2], xs[1], xs[2], new[] { 0f }, 2.4f);
            WallAlongX(walls, "Wall_HallBackRoom", zs[2], xs[2], xs[3], none);

            Railing(walls, "Railing_North", new Vector3(West, 0f, TerraceNorth), new Vector3(East, 0f, TerraceNorth));
            Railing(walls, "Railing_West", new Vector3(West, 0f, North), new Vector3(West, 0f, TerraceNorth));
            Railing(walls, "Railing_East", new Vector3(East, 0f, North), new Vector3(East, 0f, TerraceNorth));
        }

        private static void WallAlongX(Transform parent, string name, float z, float x0, float x1, float[] doors, float doorWidth = DoorWidth) =>
            Wall(parent, name, x0, x1, doors, doorWidth, null, (a, b) => (new Vector3((a + b) / 2f, 0f, z), new Vector3(b - a, 0f, WallThickness)));

        private static void WallAlongZ(Transform parent, string name, float x, float z0, float z1, float[] doors, float doorWidth = DoorWidth, float? hatchAt = null) =>
            Wall(parent, name, z0, z1, doors, doorWidth, hatchAt, (a, b) => (new Vector3(x, 0f, (a + b) / 2f), new Vector3(WallThickness, 0f, b - a)));

        // A wall from `from` to `to` with door gaps; `place` turns a run into a centre and size.
        private static void Wall(Transform parent, string name, float from, float to, float[] doors, float doorWidth, float? hatchAt,
            System.Func<float, float, (Vector3 Centre, Vector3 Size)> place)
        {
            var wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            var gaps = new List<(float From, float To)>();
            foreach (var door in doors)
                gaps.Add((door - doorWidth / 2f, door + doorWidth / 2f));
            const float hatchWidth = 1f;
            if (hatchAt.HasValue)
                gaps.Add((hatchAt.Value - hatchWidth / 2f, hatchAt.Value + hatchWidth / 2f));
            gaps.Sort((a, b) => a.From.CompareTo(b.From));

            var start = from;
            foreach (var gap in gaps)
            {
                WallRun(wall, place(start, gap.From));
                start = gap.To;
            }
            WallRun(wall, place(start, to));

            if (hatchAt.HasValue)
            {
                // The serving hatch: a kitchen counter set in the wall, its top at counter height.
                var (centre, _) = place(hatchAt.Value - hatchWidth / 2f, hatchAt.Value + hatchWidth / 2f);
                var hatch = Prop(wall, "PF_Counter", centre, 90f);
                if (hatch != null)
                    hatch.name = "ServingHatch";
            }
        }

        private static void WallRun(Transform wall, (Vector3 Centre, Vector3 Size) run)
        {
            if (Mathf.Max(run.Size.x, run.Size.z) < 0.05f)
                return;
            Cube(wall, "Wall", OfficeWall(), run.Centre, new Vector3(run.Size.x, WallHeight, run.Size.z), collider: true);
        }

        // An outer wall: a panelled sill, glass up to a walnut head, and walnut mullions, with a gap
        // at each door (positions along the wall's own axis).
        private static void Glass(Transform parent, string name, Vector3 from, Vector3 to, float[] doors, float mullionSpacing, float height = OuterWallHeight)
        {
            var wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            var glass = PilotArt.EnsureGlassMaterial("OfficeWindow", new Color(0.62f, 0.76f, 0.86f, 0.16f));
            var frame = Toon("OfficeWalnut", Walnut);
            var alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
            float a0 = alongX ? from.x : from.z, a1 = alongX ? to.x : to.z;
            Vector3 At(float a) => alongX ? new Vector3(a, 0f, from.z) : new Vector3(from.x, 0f, a);
            Vector3 Size(float length, float height, float thickness) => alongX ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length);
            void Run(float a, float b)
            {
                if (b - a < 0.05f)
                    return;
                var centre = At((a + b) / 2f);
                Cube(wall, "Sill", OfficeWall(), centre, Size(b - a, SillHeight, WallThickness), collider: true);
                Cube(wall, "Glass", glass, centre + Vector3.up * SillHeight, Size(b - a, height - SillHeight - 0.12f, 0.04f), collider: true);
                Block(wall, "Head", frame, centre + Vector3.up * (height - 0.12f), Size(b - a + 0.1f, 0.12f, WallThickness + 0.04f));
                Block(wall, "SillCap", Toon("OfficeBrass", Brass), centre + Vector3.up * SillHeight, Size(b - a, 0.04f, WallThickness + 0.06f));
                var panes = Mathf.Max(1, Mathf.RoundToInt((b - a) / mullionSpacing));
                for (var i = 0; i <= panes; i++)
                    Block(wall, "Mullion", frame, At(Mathf.Lerp(a, b, i / (float)panes)) + Vector3.up * SillHeight,
                        Size(0.1f, height - SillHeight - 0.12f, WallThickness));
            }
            var start = a0;
            foreach (var door in doors)
            {
                Run(start, door - DoorWidth / 2f);
                start = door + DoorWidth / 2f;
            }
            Run(start, a1);
        }

        private static void Railing(Transform parent, string name, Vector3 from, Vector3 to)
        {
            var railing = new GameObject(name).transform;
            railing.SetParent(parent, false);
            var glass = PilotArt.EnsureGlassMaterial("OfficeWindow", new Color(0.62f, 0.76f, 0.86f, 0.18f));
            var length = Vector3.Distance(from, to);
            var alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
            var centre = (from + to) / 2f;
            Cube(railing, "Glass", glass, centre, alongX ? new Vector3(length, 1f, 0.05f) : new Vector3(0.05f, 1f, length), collider: true);
            Block(railing, "Rail", Toon("OfficeBrass", Brass), centre + Vector3.up * 1f, alongX ? new Vector3(length, 0.07f, 0.1f) : new Vector3(0.1f, 0.07f, length));
            var posts = Mathf.CeilToInt(length / 1.8f);
            for (var i = 0; i <= posts; i++)
                Block(railing, "Post", Toon("OfficeBrass", Brass), Vector3.Lerp(from, to, i / (float)posts), new Vector3(0.09f, 1f, 0.09f));
        }

        // ---- The elevator -------------------------------------------------------------------------

        private static OfficeElevator BuildElevator(Transform floor, PlayerInputManager manager, ElevatorConfig config, out Transform[] spawnPoints)
        {
            var root = new GameObject("Elevator").transform;
            root.SetParent(floor, false);
            var brass = Toon("OfficeBrass", Brass);
            var frame = Toon("OfficeWalnut", Walnut);
            var depth = CarDoorZ - South;

            // The car: a brass-trimmed floor, walnut side walls and the door wall; the front is the
            // building's own low wall, so the camera sees in.
            Cube(root, "CarFloor", Tiles("OfficeLiftFloor", new Color(0.32f, 0.25f, 0.22f), new Color(0.6f, 0.45f, 0.24f), 0.6f, 0.02f),
                new Vector3(0f, 0.004f, South + depth / 2f), new Vector3(CarHalfWidth * 2f, 0.01f, depth));
            foreach (var side in new[] { -1f, 1f })
            {
                Block(root, side < 0 ? "CarWall_W" : "CarWall_E", frame, new Vector3(side * (CarHalfWidth + 0.1f), 0f, South + depth / 2f - 0.15f),
                    new Vector3(0.2f, CarWallHeight, depth - 0.3f), collider: true);
                Block(root, "CarWallTrim", brass, new Vector3(side * (CarHalfWidth + 0.1f), CarWallHeight, South + depth / 2f - 0.15f), new Vector3(0.26f, 0.06f, depth - 0.3f));
            }

            // The door wall: pockets either side the panels slide into, and a lintel with the dial.
            var pocket = CarHalfWidth + 1.5f;
            foreach (var side in new[] { -1f, 1f })
            {
                var centreX = side * (DoorPanelWidth + (pocket - DoorPanelWidth) / 2f);
                Block(root, side < 0 ? "Pocket_W" : "Pocket_E", frame, new Vector3(centreX, 0f, CarDoorZ), new Vector3(pocket - DoorPanelWidth, DoorWallHeight, 0.3f), collider: true);
                Block(root, "Jamb", brass, new Vector3(side * DoorPanelWidth, 0f, CarDoorZ - 0.16f), new Vector3(0.1f, DoorPanelHeight, 0.05f));
            }
            Block(root, "Lintel", frame, new Vector3(0f, DoorPanelHeight, CarDoorZ), new Vector3(DoorPanelWidth * 2f, DoorWallHeight - DoorPanelHeight, 0.3f), collider: true);
            Block(root, "LintelTrim", brass, new Vector3(0f, DoorWallHeight, CarDoorZ), new Vector3(pocket * 2f, 0.08f, 0.36f));

            var doorMaterial = Toon("OfficeLiftDoor", new Color(0.80f, 0.60f, 0.32f));
            var left = Block(root, "Door_L", doorMaterial, new Vector3(-DoorPanelWidth / 2f, 0f, CarDoorZ), new Vector3(DoorPanelWidth, DoorPanelHeight, 0.08f), collider: true);
            var right = Block(root, "Door_R", doorMaterial, new Vector3(DoorPanelWidth / 2f, 0f, CarDoorZ), new Vector3(DoorPanelWidth, DoorPanelHeight, 0.08f), collider: true);
            left.isStatic = right.isStatic = false;

            var spawns = new GameObject("SpawnPoints").transform;
            spawns.SetParent(root, false);
            var spawnAt = new[] { new Vector3(-0.8f, 0f, CarDoorZ - 0.9f), new Vector3(0.8f, 0f, CarDoorZ - 0.9f), new Vector3(-0.8f, 0f, CarDoorZ - 1.8f), new Vector3(0.8f, 0f, CarDoorZ - 1.8f) };
            spawnPoints = new Transform[spawnAt.Length];
            for (var i = 0; i < spawnAt.Length; i++)
            {
                spawnPoints[i] = new GameObject($"Spawn_P{i + 1}").transform;
                spawnPoints[i].SetParent(spawns, false);
                spawnPoints[i].localPosition = spawnAt[i];
            }

            // The car and its doorway, as a trigger: a chef there holds the doors open.
            var car = new GameObject("CarZone").AddComponent<BoxCollider>();
            car.transform.SetParent(root, false);
            car.isTrigger = true;
            car.center = new Vector3(0f, 1f, (South + CarDoorZ + 0.7f) / 2f);
            car.size = new Vector3(CarHalfWidth * 2f, 2f, CarDoorZ + 0.7f - South);

            var elevator = root.gameObject.AddComponent<OfficeElevator>();
            var so = new SerializedObject(elevator);
            so.FindProperty("_players").objectReferenceValue = manager;
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_leftDoor").objectReferenceValue = left.transform;
            so.FindProperty("_rightDoor").objectReferenceValue = right.transform;
            so.FindProperty("_car").objectReferenceValue = car;
            so.ApplyModifiedPropertiesWithoutUndo();
            return elevator;
        }

        // The dial above the doors on the car's side (the side the camera sees), the arrival lamp
        // and its light, and the bell.
        private static void BuildElevatorView(OfficeElevator elevator)
        {
            var root = elevator.transform;
            var face = CarDoorZ - 0.16f;
            var dialHeight = DoorPanelHeight + (DoorWallHeight - DoorPanelHeight) / 2f;
            Shape(root, PrimitiveType.Cylinder, "Dial", Toon("OfficeDialFace", Cream), new Vector3(0f, dialHeight, face),
                new Vector3(0.42f, 0.02f, 0.42f), Quaternion.Euler(90f, 0f, 0f));
            Shape(root, PrimitiveType.Cylinder, "DialRim", Toon("OfficeBrass", Brass), new Vector3(0f, dialHeight, face + 0.01f),
                new Vector3(0.5f, 0.015f, 0.5f), Quaternion.Euler(90f, 0f, 0f));
            var needle = new GameObject("Needle").transform;
            needle.SetParent(root, false);
            needle.localPosition = new Vector3(0f, dialHeight, face - 0.03f);
            Cube(needle, "Hand", Toon("OfficeInk", Ink), new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.18f, 0.01f)).isStatic = false;

            var lamp = new GameObject("ArrivedLamp");
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0.55f, dialHeight, face - 0.02f); // beside the dial
            Shape(lamp.transform, PrimitiveType.Cube, "Bulb", Unlit("OfficeLiftLamp", new Color(1f, 0.72f, 0.3f)), Vector3.zero,
                new Vector3(0.3f, 0.08f, 0.03f)).isStatic = false;
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lamp.transform, false);
            light.transform.localPosition = new Vector3(-0.55f, -0.4f, -0.9f);
            light.type = LightType.Point;
            light.color = LampWarm;
            light.range = 4.5f;
            light.intensity = 2f;
            lamp.SetActive(false);

            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f; // heard the same wherever the camera is: a join must never be missed

            var view = new SerializedObject(root.gameObject.AddComponent<ElevatorView>());
            view.FindProperty("_elevator").objectReferenceValue = elevator;
            view.FindProperty("_audio").objectReferenceValue = audio;
            view.FindProperty("_needle").objectReferenceValue = needle;
            view.FindProperty("_arrivedLamp").objectReferenceValue = lamp;
            SetClips(view.FindProperty("_bell._clips"), "SFX_Lift_Ding_01");
            SetClips(view.FindProperty("_doors._clips"), "SFX_Lift_Doors_01");
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetClips(SerializedProperty clips, params string[] names)
        {
            clips.arraySize = names.Length;
            for (var i = 0; i < names.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{names[i]}.wav");
                if (clip == null)
                    Debug.LogWarning($"[OfficeBuilder] No {names[i]}.wav: run tools/synth_sfx.py.");
                clips.GetArrayElementAtIndex(i).objectReferenceValue = clip;
            }
        }

        // ---- Rooms --------------------------------------------------------------------------------

        private static Transform Room(Transform parent, string name)
        {
            var room = new GameObject(name).transform;
            room.SetParent(parent, false);
            return room;
        }

        // Reception, 18 × 9 m: the elevator, the desk on the right, somewhere to wait on the left.
        private static void BuildReception(Transform rooms)
        {
            var room = Room(rooms, "Reception");
            for (var i = 0; i < 3; i++)
                Prop(room, "PF_Counter", new Vector3(4.5f + i, 0f, -7.6f), 0f);
            Shape(room, PrimitiveType.Sphere, "DeskBell", Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f)), new Vector3(5.0f, 0.98f, -7.75f), Vector3.one * 0.14f);
            Block(room, "Ledger", Toon("OfficeLedger", Burgundy), new Vector3(5.9f, 0.92f, -7.7f), new Vector3(0.42f, 0.06f, 0.3f));
            Person(room, "Receptionist", new Vector3(5.5f, 0f, -6.7f), 180f, new Color(0.5f, 0.2f, 0.3f), Suit, dress: look =>
            {
                look.Skin = CharacterArt.SkinTones[5];
                look.HairStyle = CharacterArt.HairKind.Bun;
                look.Hair = CharacterArt.DarkHair;
                look.Glasses = true;
                look.Garment = CharacterArt.Garment.Jacket;
            });

            // The waiting area: a sofa and armchairs round a low table, under a floor lamp.
            Sofa(room, new Vector3(-5.6f, 0f, -6.0f), 180f, Velvet);
            Armchair(room, new Vector3(-7.6f, 0f, -8.6f), 90f, Velvet);
            Armchair(room, new Vector3(-3.6f, 0f, -8.6f), -90f, Velvet);
            Block(room, "CoffeeTable", Toon("OfficeWalnut", Walnut), new Vector3(-5.6f, 0f, -8.4f), new Vector3(1.6f, 0.42f, 0.9f), collider: true);
            Block(room, "Magazines", Toon("OfficeLedger", Burgundy), new Vector3(-5.3f, 0.42f, -8.4f), new Vector3(0.34f, 0.03f, 0.25f));
            FloorLamp(room, new Vector3(-8.4f, 0f, -5.2f));
            foreach (var at in new[] { new Vector3(8.3f, 0f, -5.2f), new Vector3(-8.3f, 0f, -12.8f), new Vector3(8.3f, 0f, -12.8f), new Vector3(-4.0f, 0f, -12.9f), new Vector3(4.0f, 0f, -12.9f), new Vector3(-2.0f, 0f, -5.2f), new Vector3(2.0f, 0f, -5.2f) })
                PottedPlant(room, at, 1f);
            HangPicture(room, new Vector3(-9f, PictureHeight, -10.6f), Vector3.right, new Color(0.56f, 0.66f, 0.5f));
            HangPicture(room, new Vector3(9f, PictureHeight, -10.6f), Vector3.left, new Color(0.5f, 0.62f, 0.72f));
        }

        // The guarded doors, 18 × 8 m: the bodyguards' post in front of the boss's door, with seats
        // for whoever is kept waiting.
        private static void BuildGuardedDoors(Transform rooms)
        {
            var room = Room(rooms, "GuardedDoors");
            Bodyguard(room, "Bodyguard_L", new Vector3(-1.9f, 0f, 2.6f));
            Bodyguard(room, "Bodyguard_R", new Vector3(1.9f, 0f, 2.6f));
            foreach (var x in new[] { -1.5f, 1.5f })
            {
                Block(room, "Column", Toon("OfficeColumn", Cream), new Vector3(x, 0f, 3.15f), new Vector3(0.5f, 2.3f, 0.5f), collider: true);
                Block(room, "ColumnCap", Toon("OfficeBrass", Brass), new Vector3(x, 2.3f, 3.15f), new Vector3(0.62f, 0.12f, 0.62f));
            }
            Sofa(room, new Vector3(-5.2f, 0f, -0.4f), 90f, Velvet);
            Sofa(room, new Vector3(5.2f, 0f, -0.4f), -90f, Velvet);
            foreach (var at in new[] { new Vector3(-8.3f, 0f, -3.8f), new Vector3(8.3f, 0f, -3.8f), new Vector3(-8.3f, 0f, 2.9f), new Vector3(8.3f, 0f, 2.9f), new Vector3(-3.2f, 0f, 2.9f), new Vector3(3.2f, 0f, 2.9f) })
                PottedPlant(room, at, 1.1f);
            HangPicture(room, new Vector3(9f, PictureHeight, 2.2f), Vector3.left, new Color(0.72f, 0.56f, 0.36f));
        }

        // The boss's office, 18 × 10 m: his desk at the end of the carpet, the warmest light on the
        // floor, and the only way to the tailor (west), the miniature (east) and the terrace.
        private static void BuildBossOffice(Transform rooms)
        {
            var room = Room(rooms, "BossOffice");
            Cube(room, "RugBorder", Toon("OfficeBrass", Brass), new Vector3(0f, 0.008f, 9.6f), new Vector3(8.2f, 0.012f, 5.6f));
            Cube(room, "Rug", Toon("OfficeRug", new Color(0.38f, 0.10f, 0.12f)), new Vector3(0f, 0.01f, 9.6f), new Vector3(8f, 0.012f, 5.4f));

            Model(room, "Desk", OfficeModel, "OfficeDeskBody", Toon("OfficeWalnut", Walnut), new Vector3(0f, 0f, 10.6f), 180f, collider: new Vector3(2.4f, 0.8f, 1.05f));
            Model(room, "DeskLeather", OfficeModel, "OfficeDeskLeather", Toon("OfficeLeather", Leather), new Vector3(0f, 0f, 10.6f), 180f);
            Model(room, "DeskHandles", OfficeModel, "OfficeDeskHandles", Toon("OfficeBrass", Brass), new Vector3(0f, 0f, 10.6f), 180f);
            Block(room, "Papers", Toon("OfficePaper", Cream), new Vector3(-0.3f, 0.77f, 10.45f), new Vector3(0.42f, 0.02f, 0.3f));
            Model(room, "Trophy", OfficeModel, "OfficeTrophy", Toon("OfficeBrass", Brass), new Vector3(-0.95f, 0.76f, 10.8f), 0f, scale: 0.7f);
            Model(room, "DeskLampStand", OfficeModel, "OfficeLampStand", Toon("OfficeBrass", Brass), new Vector3(0.95f, 0.76f, 10.85f), 0f, scale: 0.45f);
            Model(room, "DeskLampShade", OfficeModel, "OfficeLampShade", Unlit("OfficeLampGlow", new Color(1f, 0.86f, 0.55f)), new Vector3(0.95f, 0.76f, 10.85f), 0f, scale: 0.45f);
            OfficeChair(room, new Vector3(0f, 0f, 12.0f), 180f);
            Boss(room, new Vector3(0f, 0f, 11.45f));
            Armchair(room, new Vector3(-1.2f, 0f, 8.6f), 0f, new Color(0.52f, 0.16f, 0.16f));
            Armchair(room, new Vector3(1.2f, 0f, 8.6f), 0f, new Color(0.52f, 0.16f, 0.16f));

            // A sitting corner on the left, his books and his safe on the right; the doors to the
            // tailor and the miniature (z = 6) are kept clear.
            // (Further back than the door to the tailor, beside the fireplace: OfficeBuilder.Lighting.cs.)
            Sofa(room, new Vector3(-7.4f, 0f, 11.5f), 90f, Leather);
            Block(room, "CoffeeTable", Toon("OfficeWalnut", Walnut), new Vector3(-6.0f, 0f, 11.5f), new Vector3(0.9f, 0.42f, 1.6f), collider: true);
            Model(room, "Decanter", OfficeModel, "OfficeTrophy", PilotArt.EnsureGlassMaterial("OfficeDecanter", new Color(0.7f, 0.45f, 0.2f, 0.7f)), new Vector3(-6.0f, 0.42f, 11.1f), 0f, scale: 0.6f);
            for (var i = 0; i < 2; i++)
                Bookcase(room, new Vector3(8.6f, 0f, 9.4f + i * 1.9f), -90f);
            Block(room, "Safe", Toon("OfficeSafe", new Color(0.24f, 0.25f, 0.28f)), new Vector3(8.3f, 0f, 4.3f), new Vector3(0.9f, 1.0f, 0.8f), collider: true);
            Shape(room, PrimitiveType.Cylinder, "SafeDial", Toon("OfficeBrass", Brass), new Vector3(8.3f, 0.6f, 3.89f), new Vector3(0.22f, 0.02f, 0.22f), Quaternion.Euler(90f, 0f, 0f));
            FloorLamp(room, new Vector3(-3f, 0f, 12.8f));
            FloorLamp(room, new Vector3(3f, 0f, 12.8f));
            foreach (var at in new[] { new Vector3(-8.3f, 0f, 4.2f), new Vector3(-8.3f, 0f, 12.8f), new Vector3(8.3f, 0f, 12.8f), new Vector3(-3.2f, 0f, 4.2f), new Vector3(3.2f, 0f, 4.2f) })
                PottedPlant(room, at, 1.15f);
        }

        // The tailor, 8.5 × 10 m, through the boss's office: three colour plinths before mirrors,
        // rails of clothes, the tailor.
        // The tailor's room: furnished in OfficeBuilder.Tailor.cs, its ladders and tailor added by AddTailor.
        private static void BuildTailor(Transform rooms)
        {
            var room = Room(rooms, "Tailor");
            FurnishTailor(room);
        }

        // The test kitchen, 8.5 × 8 m: bare minimum (decision 2026-11-09), from the kitchen's own
        // counters, hob, fridge, board, knife rack and shelf. Visual copies for now; the real
        // stations come in Phase 6.
        private static void BuildTestKitchen(Transform rooms)
        {
            var room = Room(rooms, "TestKitchen");
            var row = new[] { ("PF_Fridge", -16.5f), ("PF_Counter", -15.5f), ("PF_Hob", -14.5f), ("PF_CuttingBoard", -13.5f),
                ("PF_KnifeRack", -12.5f), ("PF_Counter", -11.5f), ("PF_Shelf", -10.5f) };
            foreach (var (prefab, x) in row)
                Prop(room, prefab, new Vector3(x, 0f, 2.9f), 0f);
            for (var i = 0; i < 3; i++)
                Prop(room, "PF_Counter", new Vector3(-14.25f + i, 0f, -0.4f), 0f);
            Prop(room, "PF_Counter", new Vector3(-16.9f, 0f, -2.4f), 90f);
            Model(room, "Bin", "SM_Bin", "BinBody", Toon("OfficeBin", new Color(0.12f, 0.12f, 0.13f)), new Vector3(-16.9f, 0f, -3.7f), 90f, collider: new Vector3(0.6f, 0.9f, 0.6f));
            PottedPlant(room, new Vector3(-9.6f, 0f, -3.8f), 0.8f);
        }

        // The miniature room, 8.5 × 10 m, through the boss's office: the city miniature, lit from above.
        private static void BuildBackRoom(Transform rooms)
        {
            var room = Room(rooms, "BackRoom");
            var centre = new Vector3(13.4f, 0f, 9.0f);
            Block(room, "MiniatureTable", Toon("OfficeWalnut", Walnut), centre, new Vector3(5.0f, 0.82f, 3.2f), collider: true);
            Block(room, "MiniatureTrim", Toon("OfficeBrass", Brass), centre + Vector3.up * 0.82f, new Vector3(5.1f, 0.05f, 3.3f));
            Cube(room, "MiniatureGround", Toon("OfficeMiniGround", new Color(0.18f, 0.2f, 0.28f)), centre + Vector3.up * 0.87f, new Vector3(4.8f, 0.02f, 3.0f));
            Miniature(room, centre + Vector3.up * 0.89f, new Vector2(4.6f, 2.8f));
            for (var i = 0; i < 2; i++)
            {
                Block(room, "MapChest", Toon("OfficeWalnut", Walnut), new Vector3(17.0f, 0f, 4.6f + i * 1.6f), new Vector3(0.7f, 1.0f, 1.5f), collider: true);
                for (var k = 0; k < 3; k++)
                    Shape(room, PrimitiveType.Cylinder, "Plan", Toon("OfficePaper", Cream), new Vector3(17.0f, 1.06f, 4.2f + i * 1.6f + k * 0.4f), new Vector3(0.12f, 0.32f, 0.12f), Quaternion.Euler(0f, 0f, 90f));
            }
            FloorLamp(room, new Vector3(9.6f, 0f, 12.9f));
            foreach (var at in new[] { new Vector3(9.6f, 0f, 4.2f), new Vector3(16.8f, 0f, 12.9f) })
                PottedPlant(room, at, 1.1f);
            HangPicture(room, new Vector3(9f, PictureHeight, 10.6f), Vector3.right, new Color(0.38f, 0.48f, 0.62f));
        }

        // The hall of fame, 8.5 × 8 m: the cookbook on its lectern, a trophy for each restaurant,
        // portraits of erased chefs.
        private static void BuildHallOfFame(Transform rooms)
        {
            var room = Room(rooms, "HallOfFame");
            Block(room, "Lectern", Toon("OfficeWalnut", Walnut), new Vector3(13.25f, 0f, -0.4f), new Vector3(0.8f, 1.05f, 0.6f), collider: true);
            var book = Block(room, "Cookbook", Toon("OfficeCookbook", new Color(0.62f, 0.16f, 0.14f)), new Vector3(13.25f, 1.05f, -0.4f), new Vector3(0.6f, 0.1f, 0.46f));
            book.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Block(room, "CookbookPages", Toon("OfficePaper", Cream), new Vector3(13.25f, 1.1f, -0.42f), new Vector3(0.54f, 0.04f, 0.4f)).transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Cube(room, "LecternRug", Toon("OfficeRug", new Color(0.38f, 0.10f, 0.12f)), new Vector3(13.25f, 0.01f, -0.7f), new Vector3(2.6f, 0.012f, 2.0f));
            // Trophies for restaurants owned: one so far, plinths waiting for the rest.
            var plinths = new[] { 10.4f, 11.85f, 13.3f, 14.75f, 16.2f };
            for (var i = 0; i < plinths.Length; i++)
            {
                var at = new Vector3(plinths[i], 0f, 2.7f);
                Block(room, "Plinth", Toon("OfficeColumn", Cream), at, new Vector3(0.7f, 1.0f, 0.7f), collider: true);
                if (i == 0)
                    Model(room, "Trophy", OfficeModel, "OfficeTrophy", Toon("OfficeBrass", Brass), at + Vector3.up * 1.0f, 0f, scale: 1.4f);
            }
            // Portraits of erased chefs, dusty and dim (§15.3's story, without words).
            foreach (var z in new[] { -3.0f, -1.2f, 0.6f })
                HangPicture(room, new Vector3(17.5f, PictureHeight, z), Vector3.left, new Color(0.44f, 0.42f, 0.4f));
            Sofa(room, new Vector3(11.0f, 0f, -3.7f), 0f, Velvet);
            PottedPlant(room, new Vector3(16.8f, 0f, -3.8f), 1.1f);
        }

        // The waiting lounge, 8.5 × 9 m: for fun and easter eggs later (decision 2026-11-09).
        private static void BuildLounge(Transform rooms)
        {
            var room = Room(rooms, "WaitingLounge");
            Sofa(room, new Vector3(-13.6f, 0f, -12.7f), 0f, Velvet);
            Sofa(room, new Vector3(-16.9f, 0f, -9.4f), 90f, Velvet);
            Armchair(room, new Vector3(-10.4f, 0f, -9.0f), -90f, Leather);
            Armchair(room, new Vector3(-10.4f, 0f, -10.8f), -90f, Leather);
            Block(room, "CoffeeTable", Toon("OfficeWalnut", Walnut), new Vector3(-13.6f, 0f, -9.8f), new Vector3(1.6f, 0.42f, 0.9f), collider: true);
            // The bodyguards' coffee cups (spec §5).
            foreach (var x in new[] { -14.0f, -13.7f })
                Shape(room, PrimitiveType.Cylinder, "Cup", Toon("OfficeCup", new Color(0.94f, 0.93f, 0.9f)), new Vector3(x, 0.47f, -9.7f), new Vector3(0.1f, 0.05f, 0.1f));
            Block(room, "Magazines", Toon("OfficeLedger", Burgundy), new Vector3(-13.1f, 0.42f, -9.9f), new Vector3(0.34f, 0.03f, 0.25f));
            // A coffee corner: the kitchen's bar tables and their stools.
            Prop(room, "PF_BarTable", new Vector3(-16.4f, 0f, -5.4f), 180f);
            Prop(room, "PF_BarTable", new Vector3(-15.4f, 0f, -5.4f), 180f);
            Block(room, "WaterCooler", Toon("OfficeCooler", new Color(0.88f, 0.88f, 0.86f)), new Vector3(-17.0f, 0f, -6.8f), new Vector3(0.45f, 1.0f, 0.45f), collider: true);
            Shape(room, PrimitiveType.Cylinder, "WaterBottle", PilotArt.EnsureGlassMaterial("OfficeWater", new Color(0.4f, 0.7f, 0.95f, 0.5f)), new Vector3(-17.0f, 1.2f, -6.8f), new Vector3(0.3f, 0.2f, 0.3f));
            FloorLamp(room, new Vector3(-16.9f, 0f, -12.9f));
            PottedPlant(room, new Vector3(-9.6f, 0f, -12.9f), 1.2f);
            PottedPlant(room, new Vector3(-9.6f, 0f, -5.2f), 1.0f);
        }

        // The accountant, 8.5 × 9 m: also for fun (decision 2026-11-09); the books stay on the dashboard.
        private static void BuildAccountant(Transform rooms)
        {
            var room = Room(rooms, "Accountant");
            var desk = new Vector3(13.0f, 0f, -9.8f);
            Model(room, "Desk", OfficeModel, "OfficeDeskBody", Toon("OfficeDeskOak", WoodMid), desk, 180f, scale: 0.85f, collider: new Vector3(2.1f, 0.7f, 0.9f));
            Model(room, "DeskLeather", OfficeModel, "OfficeDeskLeather", Toon("OfficeLeatherBrown", new Color(0.36f, 0.24f, 0.16f)), desk, 180f, scale: 0.85f);
            Model(room, "DeskHandles", OfficeModel, "OfficeDeskHandles", Toon("OfficeBrass", Brass), desk, 180f, scale: 0.85f);
            Block(room, "AddingMachine", Toon("OfficeSafe", new Color(0.24f, 0.25f, 0.28f)), desk + new Vector3(0.5f, 0.65f, 0f), new Vector3(0.36f, 0.14f, 0.3f));
            // A stack of unpaid invoices (spec §5).
            for (var i = 0; i < 7; i++)
                Block(room, "Invoice", Toon("OfficePaper", Cream), desk + new Vector3(-0.45f + i * 0.012f, 0.65f + i * 0.025f, 0f), new Vector3(0.34f, 0.02f, 0.26f));
            OfficeChair(room, desk + new Vector3(0f, 0f, 1.2f), 180f);
            Person(room, "Accountant", desk + new Vector3(0f, 0f, 0.8f), 180f, new Color(0.40f, 0.38f, 0.30f), new Color(0.25f, 0.24f, 0.2f), dress: look =>
            {
                // Frames and a green visor (concept v16).
                look.Skin = CharacterArt.SkinTones[0];
                look.Hair = CharacterArt.NaturalHair[3];
                look.Glasses = true;
                look.Hat = CharacterArt.HatKind.Headband;
                look.HatAccent = new Color(0.2f, 0.45f, 0.3f);
                look.Tie = true;
                look.TieColor = new Color(0.45f, 0.18f, 0.16f);
                look.Garment = CharacterArt.Garment.Vest;
                look.Under = CharacterArt.ShirtColours[1];
            });
            foreach (var x in new[] { -0.6f, 0.6f })
                Model(room, "VisitorChair", "SM_Dining", "DiningChair", Toon("OfficeVisitorChair", WoodMid), desk + new Vector3(x, 0f, -1.4f), 0f, collider: new Vector3(0.45f, 0.9f, 0.45f));
            for (var i = 0; i < 6; i++)
            {
                var at = new Vector3(17.1f, 0f, -12.6f + i * 0.6f);
                Model(room, "FilingCabinet", OfficeModel, "OfficeFilingBody", Toon("OfficeFiling", new Color(0.48f, 0.48f, 0.44f)), at, -90f, collider: new Vector3(0.55f, 1.32f, 0.62f));
                Model(room, "FilingHandles", OfficeModel, "OfficeFilingHandles", Toon("OfficeBrass", Brass), at, -90f);
            }
            Prop(room, "PF_Shelf", new Vector3(10.4f, 0f, -5.1f), 0f);
            FloorLamp(room, new Vector3(9.6f, 0f, -12.9f));
            PottedPlant(room, new Vector3(16.8f, 0f, -5.2f), 1.1f);
            HangPicture(room, new Vector3(9f, PictureHeight, -11.4f), Vector3.right, new Color(0.56f, 0.66f, 0.5f));
        }

        // The terrace, 35 × 6 m: loungers, an outdoor bar, trees and lamps from the city, the helipad.
        private static void BuildTerrace(Transform rooms)
        {
            var room = Room(rooms, "Terrace");
            var cushion = Toon("OfficeCushion", new Color(0.45f, 0.55f, 0.75f));
            var teak = Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f));
            foreach (var x in new[] { -16.2f, -14.8f, -13.4f })
            {
                Model(room, "Lounger", OfficeModel, "OfficeLoungerFrame", teak, new Vector3(x, 0f, 17.6f), 180f, collider: new Vector3(0.72f, 0.4f, 1.9f));
                Model(room, "LoungerCushion", OfficeModel, "OfficeLoungerCushion", cushion, new Vector3(x, 0f, 17.6f), 180f);
            }
            Model(room, "UmbrellaPole", OfficeModel, "OfficeUmbrellaPole", teak, new Vector3(-11.4f, 0f, 16.6f), 0f);
            Model(room, "UmbrellaCanopy", OfficeModel, "OfficeUmbrellaCanopy", Toon("OfficeUmbrella", new Color(0.18f, 0.40f, 0.32f)), new Vector3(-11.4f, 0f, 16.6f), 22.5f);
            for (var i = 0; i < 3; i++)
                Prop(room, "PF_BarTable", new Vector3(1f + i, 0f, 16.4f), 180f);
            foreach (var x in new[] { -8f, -2f, 5f })
                Tree(room, new Vector3(x, 0f, 18.9f));
            foreach (var x in new[] { -9.8f, -0.5f, 7.6f })
                StreetLamp(room, new Vector3(x, 0f, 14.2f));

            // The helipad, with the boss's helicopter on it (the ending takes it away).
            var pad = new Vector3(12.6f, 0f, 16.5f);
            var paint = Toon("OfficeHelipadPaint", new Color(0.95f, 0.78f, 0.28f));
            Shape(room, PrimitiveType.Cylinder, "HelipadRing", paint, pad + Vector3.up * 0.012f, new Vector3(5.6f, 0.012f, 5.6f));
            Shape(room, PrimitiveType.Cylinder, "Helipad", Toon("OfficeHelipad", new Color(0.24f, 0.25f, 0.29f)), pad + Vector3.up * 0.02f, new Vector3(5.3f, 0.02f, 5.3f));
            Cube(room, "H_Left", paint, pad + new Vector3(-0.6f, 0.04f, 0f), new Vector3(0.24f, 0.01f, 1.7f));
            Cube(room, "H_Right", paint, pad + new Vector3(0.6f, 0.04f, 0f), new Vector3(0.24f, 0.01f, 1.7f));
            Cube(room, "H_Bar", paint, pad + new Vector3(0f, 0.04f, 0f), new Vector3(1.2f, 0.01f, 0.24f));
            var heli = new GameObject("Helicopter").transform;
            heli.SetParent(room, false);
            heli.localPosition = pad + new Vector3(0.2f, 0.04f, 0f);
            heli.localRotation = Quaternion.Euler(0f, 110f, 0f);
            heli.localScale = Vector3.one * 0.85f;
            Model(heli, "Body", OfficeModel, "OfficeHeliBody", Toon("OfficeHeli", new Color(0.16f, 0.18f, 0.24f)), Vector3.zero, 0f, collider: new Vector3(1.7f, 2f, 2.6f));
            Model(heli, "Glass", OfficeModel, "OfficeHeliGlass", PilotArt.EnsureGlassMaterial("OfficeHeliGlass", new Color(0.55f, 0.75f, 0.92f, 0.7f)), Vector3.zero, 0f);
            Model(heli, "Skids", OfficeModel, "OfficeHeliSkids", Toon("OfficeBrass", Brass), Vector3.zero, 0f);
            Model(heli, "Rotor", OfficeModel, "OfficeHeliRotor", Toon("OfficeInk", Ink), Vector3.zero, 25f);
        }

        // ---- The city below -----------------------------------------------------------------------

        // The city round the boss's tower (user's note, 2026-11-09: the first one, flat navy boxes,
        // had nothing to do with the game's look). Now it's the kitchen's world at dusk: warm brick,
        // sandstone and cream buildings in the same toon light, framed windows, and the near roofs,
        // the part the camera sees best, as busy as the kitchen with water towers, air conditioners,
        // skylights and roof gardens. Distance fades into a violet dusk haze (fog), which gives
        // depth and keeps the far city quiet. Three rings: near roofs a few storeys down in full
        // detail, a middle ring lower and plainer, and a skyline of tall towers far behind.
        private static void BuildCity()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Dusk;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;

            var city = new GameObject("City").transform;
            BossTower(city);

            var placed = new List<Rect> { new Rect(West - 3f, South - 3f, East - West + 6f, TerraceNorth - South + 6f), StreetBounds, OpeningCorridor };
            BuildStreet(city); // OfficeBuilder.Street.cs: the street at the tower's foot, where the opening starts
            var near = new GameObject("NearRoofs").transform;
            near.SetParent(city, false);
            Ring(near, placed, 26, 22f, 46f, -9f, -22f, detail: 2, specials: new Queue<string>(RooftopSpecials));
            var middle = new GameObject("MiddleRing").transform;
            middle.SetParent(city, false);
            Ring(middle, placed, 34, 46f, 95f, -14f, -40f, detail: 1);
            var skyline = new GameObject("Skyline").transform;
            skyline.SetParent(city, false);
            for (var i = 0; i < 16; i++)
            {
                var x = -110f + i * 14f + (float)_random.NextDouble() * 6f;
                var z = 120f + (float)_random.NextDouble() * 40f;
                Building(skyline, new Vector2(x, z), 10f + (float)_random.NextDouble() * 8f, 10f + (float)_random.NextDouble() * 8f,
                    5f + (float)_random.NextDouble() * 35f, 0);
            }
        }

        // Up to `count` buildings between `minDistance` and `maxDistance` from the floor's middle,
        // roofs between `highRoof` and `lowRoof`, never overlapping each other or the tower.
        private static void Ring(Transform parent, List<Rect> placed, int count, float minDistance, float maxDistance, float highRoof, float lowRoof, int detail,
            Queue<string> specials = null)
        {
            var middle = new Vector2(0f, (South + TerraceNorth) / 2f);
            for (int i = 0, tries = 0; i < count && tries < count * 30; tries++)
            {
                var angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                var distance = Mathf.Lerp(minDistance, maxDistance, (float)_random.NextDouble());
                var at = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var w = 9f + (float)_random.NextDouble() * 9f;
                var d = 9f + (float)_random.NextDouble() * 9f;
                var footprint = new Rect(at.x - w / 2f - 1.5f, at.y - d / 2f - 1.5f, w + 3f, d + 3f);
                if (placed.Exists(r => r.Overlaps(footprint)))
                    continue;
                placed.Add(footprint);
                Building(parent, at, w, d, Mathf.Lerp(highRoof, lowRoof, (float)_random.NextDouble()), detail,
                    specials != null && specials.Count > 0 ? specials.Dequeue() : null);
                i++;
            }
        }

        // One building: a body down into the haze, a cornice, framed windows on the faces the camera
        // sees (south, and the side towards the tower), and roof clutter for the near ones.
        private static void Building(Transform parent, Vector2 at, float w, float d, float roof, int detail, string special = null)
        {
            var building = new GameObject("Building").transform;
            building.SetParent(parent, false);
            building.localPosition = new Vector3(at.x, roof, at.y);
            var c = _random.Next(FacadeColors.Length);
            // A touch of the dusk in every facade, so the city sits behind the office, not level with it.
            var wall = Toon($"OfficeFacade{c}", Color.Lerp(FacadeColors[c], Dusk, 0.18f));
            var trim = Toon($"OfficeFacadeTrim{c}", Color.Lerp(FacadeColors[c], Cream, 0.55f));
            var height = roof + CityDepth;
            Model(building, "Body", CityModel, "CityBlock", wall, new Vector3(0f, -height, 0f), 0f, scale3: new Vector3(w, height, d));
            // Roofs in a dark gravel tone: big pale roofs would outshine the office above them.
            Model(building, "Cornice", CityModel, "CityCornice", Toon("OfficeRoofGravel", new Color(0.40f, 0.36f, 0.36f)), Vector3.zero, 0f, scale3: new Vector3(w + 0.5f, 1f, d + 0.5f));
            if (detail == 0)
            {
                // The far skyline: a few lit windows only, the haze does the rest.
                for (var y = -3f; y > -30f; y -= 3.4f)
                for (var x = -w / 2f + 1.5f; x < w / 2f - 1f; x += 2.6f)
                    if (_random.NextDouble() < 0.4)
                        Shape(building, PrimitiveType.Quad, "Window", Unlit("OfficeWindowLit", WindowLit), new Vector3(x, y, -d / 2f - 0.03f), new Vector3(1f, 1.5f, 1f), Quaternion.identity);
                return;
            }

            var rows = detail == 2 ? 6 : 4;
            Windows(building, w, d, rows, trim, side: 0, framed: detail == 2); // the haze hides frames further out
            // The side towards the tower, which the camera sees at an angle.
            if (detail == 2 && Mathf.Abs(at.x) > 14f)
                Windows(building, w, d, rows, trim, side: at.x > 0f ? -1 : 1, framed: true);
            // Every roof is gravel inside a parapet, as real flat roofs are (OfficeBuilder.Life.cs).
            var roofProps = RoofShell(building, w, d, trim);
            if (detail < 2)
            {
                SimpleRoof(roofProps, w, d, wall, trim);
                return;
            }

            // The near roofs, the ones the camera sees best: machinery round the edges, much of it
            // moving, and in the middle either one of the special stories or an ordinary use.
            RoofKit(roofProps, w, d, wall, trim);
            if (special == null || SpecialRoof(special, roofProps, w, d, wall, trim))
                CentrePiece(roofProps, w, d, wall, trim);
        }

        // Framed, sill-ed windows in rows under the cornice, on the south face (side 0) or on the
        // west (side −1) or east (side 1) face. About four in ten are lit.
        // Side 2 is the north face (+Z), for the street's buildings facing the tower.
        private static void Windows(Transform building, float w, float d, int rows, Material trim, int side, bool framed)
        {
            var lit = Unlit("OfficeWindowLit", WindowLit);
            var dark = Unlit("OfficeWindowDark", WindowDark);
            var alongX = side == 0 || side == 2;
            var span = alongX ? w : d;
            var turn = Quaternion.Euler(0f, side == 0 ? 0f : side == 2 ? 180f : side * -90f, 0f);
            for (var r = 0; r < rows; r++)
            {
                var y = -2.2f - r * 3.2f;
                for (var a = -span / 2f + 1.4f; a < span / 2f - 0.9f; a += 2.3f)
                {
                    var face = side == 0 ? new Vector3(a + 0.4f, y, -d / 2f) : side == 2 ? new Vector3(a + 0.4f, y, d / 2f) : new Vector3(side * w / 2f, y, a + 0.4f);
                    var outward = side == 0 ? Vector3.back : side == 2 ? Vector3.forward : Vector3.right * side;
                    var window = Shape(building, PrimitiveType.Quad, "Window", _random.NextDouble() < 0.4 ? lit : dark, face + outward * 0.02f, new Vector3(0.9f, 1.2f, 1f), turn);
                    // Now and then, someone walking past behind a lit window (user's note, 2026-11-09).
                    if (RegisterWindow(window) && framed && _random.NextDouble() < 0.18)
                        PassingSilhouette(window);
                    if (!framed)
                        continue;
                    var frame = Model(building, "Frame", CityModel, "CityWindowFrame", trim, face, 0f);
                    var sill = Model(building, "Sill", CityModel, "CityWindowSill", trim, face, 0f);
                    if (frame != null) frame.transform.localRotation = turn;
                    if (sill != null) sill.transform.localRotation = turn;
                }
            }
        }

        // The boss's own tower under the floor (user's note, 2026-11-09: if the penthouse is glass
        // on all four sides, so is the rest of the tower). A curtain wall: on every floor a band of
        // tinted glass between bronze mullions, over a sandstone spandrel, with some offices lit
        // behind the glass. The camera sees the front and, at an angle, both sides.
        private static void BossTower(Transform city)
        {
            var tower = new GameObject("BossTower").transform;
            tower.SetParent(city, false);
            var width = East - West + 0.6f;
            var depth = TerraceNorth - South + 0.6f;
            var middle = (South + TerraceNorth) / 2f;
            var spandrel = Toon("OfficeTowerStone", new Color(0.66f, 0.55f, 0.43f));
            var bronze = Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f));
            var lit = WindowArt.LitWindows()[2]; // offices, behind the curtain wall
            // The core, set back behind the modelled top floors, and the curtain wall all the way down
            // to the street (the opening climbs it): a painted, tiled facade with lit offices.
            var core = Cube(tower, "Core", WindowArt.TowerCurtain(), new Vector3(0f, -CityDepth, middle), new Vector3(width - 0.2f, CityDepth - 0.8f, depth - 0.2f));
            core.GetComponent<Renderer>().sharedMaterial.SetTextureScale("_BaseMap", new Vector2((width - 0.2f) / (8f * 2.2f), (CityDepth - 0.8f) / (8f * 3.4f)));
            Block(tower, "Cornice", Toon("OfficeBrass", Brass), new Vector3(0f, -1.3f, middle), new Vector3(width + 0.5f, 0.5f, depth + 0.5f));
            Block(tower, "CorniceDark", Toon("OfficeWalnut", Walnut), new Vector3(0f, -0.8f, middle), new Vector3(width + 0.3f, 0.4f, depth + 0.3f));

            const float storey = 3.4f;
            const float bay = 2.2f;
            for (var f = 0; f < 9; f++)
            {
                var top = -1.4f - f * storey;
                // A sandstone spandrel band round all four sides at each floor's slab.
                Block(tower, "Spandrel", spandrel, new Vector3(0f, top - 0.9f, middle), new Vector3(width + 0.12f, 0.9f, depth + 0.12f));
                foreach (var side in new[] { 0, -1, 1 })
                {
                    var span = side == 0 ? width : depth;
                    for (var a = -span / 2f + bay; a < span / 2f - 0.5f; a += bay)
                    {
                        var face = side == 0 ? new Vector3(a, 0f, middle - depth / 2f) : new Vector3(side * width / 2f, 0f, middle + a);
                        var mullion = side == 0 ? new Vector3(0.12f, storey - 0.9f, 0.12f) : new Vector3(0.12f, storey - 0.9f, 0.12f);
                        Block(tower, "Mullion", bronze, face + Vector3.up * (top - storey), mullion);
                        // Some offices still lit behind the glass.
                        if (_random.NextDouble() < 0.3)
                        {
                            var outward = side == 0 ? Vector3.back : Vector3.right * side;
                            var along = side == 0 ? Vector3.right : Vector3.forward;
                            var turn = Quaternion.Euler(0f, side == 0 ? 0f : side * -90f, 0f);
                            Shape(tower, PrimitiveType.Quad, "LitOffice", lit, face + along * (bay / 2f) + outward * 0.02f + Vector3.up * (top - 0.9f - (storey - 0.9f) / 2f),
                                new Vector3(bay - 0.3f, storey - 1.3f, 1f), turn);
                        }
                    }
                }
            }
        }

        private static void WaterTower(Transform parent, Vector3 at)
        {
            var iron = Toon("CityTowerIron", new Color(0.15f, 0.13f, 0.16f));
            Model(parent, "TowerLegs", CityModel, "CityTowerLegs", iron, at, 0f);
            Model(parent, "TowerTank", CityModel, "CityTowerTank", Toon("CityTowerWood", new Color(0.45f, 0.3f, 0.2f)), at, 0f);
            Model(parent, "TowerRoof", CityModel, "CityTowerRoof", iron, at, 0f);
        }

        // ---- People -------------------------------------------------------------------------------

        // A person made of the players' own character kit (CharacterArt), so everyone on the floor
        // belongs with the players: the top in `top`, trousers in `bottom`, and `dress` for their
        // face, hair and the rest. Without the kit, the first modelled chef stands in.
        private static Transform Person(Transform parent, string name, Vector3 at, float facing, Color top, Color bottom,
            float scale = 1f, System.Action<CharacterArt.Look> dress = null)
        {
            var person = new GameObject(name).transform;
            person.SetParent(parent, false);
            person.localPosition = at;
            person.localRotation = Quaternion.Euler(0f, facing, 0f);
            person.localScale = Vector3.one * scale;
            var look = new CharacterArt.Look { Name = name, Top = top, Bottom = bottom, Skin = Skin };
            dress?.Invoke(look);
            if (CharacterArt.Build(person, look) == null)
            {
                var jacket = Toon($"OfficeJacket_{name}", top);
                Model(person, "Legs", ChefModel, "ChefLegs", Toon($"OfficeTrousers_{name}", bottom), Vector3.zero, 0f);
                Model(person, "Skirt", ChefModel, "ChefSkirt", jacket, Vector3.zero, 0f);
                var waist = new Vector3(0f, ChefWaist, 0f);
                Model(person, "Torso", ChefModel, "ChefTorso", jacket, waist, 0f);
                Model(person, "Head", ChefModel, "ChefHead", Toon("OfficeSkin", Skin), waist, 0f);
                Model(person, "Face", ChefModel, "ChefFace", Toon("OfficeFace", Face), waist, 0f);
                foreach (var side in new[] { -1f, 1f })
                {
                    var shoulder = waist + new Vector3(side * ChefShoulderWidth, ChefShoulderHeight, 0f);
                    Model(person, "Arm", ChefModel, "ChefArm", jacket, shoulder, 0f);
                    Model(person, "Hand", ChefModel, "ChefHand", Toon("OfficeSkin", Skin), shoulder, 0f);
                }
            }
            var body = person.gameObject.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.85f, 0f);
            body.height = 1.7f;
            body.radius = 0.32f;
            return person;
        }

        private static void Bodyguard(Transform parent, string name, Vector3 at)
        {
            // Same suit and red tie, each his own shades (wayfarers, aviators), a big coiled
            // earpiece; one bald, one cropped (concept v16 and the user's note).
            var left = name.EndsWith("_L");
            Person(parent, name, at, 180f, Suit, Suit, 1.12f, look =>
            {
                look.Build = 1.15f;
                look.Garment = CharacterArt.Garment.Jacket;
                look.Sunglasses = left;
                look.Aviators = !left;
                look.Earpiece = true;
                look.Tie = true;
                look.TieColor = new Color(0.8f, 0.2f, 0.18f);
                look.Skin = name.EndsWith("_L") ? CharacterArt.SkinTones[4] : CharacterArt.SkinTones[7];
                look.HairStyle = name.EndsWith("_L") ? CharacterArt.HairKind.Bald : CharacterArt.HairKind.Crop;
                look.Hair = CharacterArt.DarkHair;
            });
        }

        // The boss: a big man in a camel suit, bolo tie and white cowboy hat, behind his desk.
        private static void Boss(Transform parent, Vector3 at)
        {
            var boss = Person(parent, "Boss", at, 180f, new Color(0.72f, 0.52f, 0.30f), new Color(0.42f, 0.30f, 0.2f), 1.22f, look =>
            {
                look.Build = 1.15f;
                look.Skin = CharacterArt.SkinTones[2];
                look.HairStyle = CharacterArt.HairKind.Fringe;
                look.Hair = CharacterArt.GreyHair[1];
                look.Moustache = true;
                look.Nose = CharacterArt.NoseKind.Big;
                look.Garment = CharacterArt.Garment.Jacket;
                look.Under = CharacterArt.ShirtColours[2];
            });
            var hat = Toon("OfficeHat", Cream);
            // The hat was made for the first chef's head (0.19 m, its centre 0.66 m above a 0.85 m
            // waist): scale it to the kit's head (0.27 m, its centre at 1.36 m) about that centre.
            const float oldHeadCentre = 0.66f, oldHeadRadius = 0.19f, newHeadRadius = 0.27f, newHeadCentre = 1.36f;
            var hatScale = CharacterArt.Available ? newHeadRadius / oldHeadRadius : 1f;
            var hatAt = CharacterArt.Available
                ? new Vector3(0f, newHeadCentre - hatScale * oldHeadCentre, 0f)
                : new Vector3(0f, ChefWaist, 0f);
            Model(boss, "HatBrim", OfficeModel, "OfficeHatBrim", hat, hatAt, 0f, scale: hatScale);
            Model(boss, "HatCrown", OfficeModel, "OfficeHatCrown", hat, hatAt, 0f, scale: hatScale);
            Model(boss, "HatBand", OfficeModel, "OfficeHatBand", Toon("OfficeWalnut", Walnut), hatAt, 0f, scale: hatScale);
            // A bolo tie at the collar.
            var bolo = CharacterArt.Available ? new Vector3(0f, 1.0f, 0.33f) : new Vector3(0f, ChefWaist + 0.36f, 0.27f);
            Block(boss, "Bolo", Toon("OfficeBrass", Brass), bolo, new Vector3(0.08f, 0.08f, 0.03f));
        }

        // The tailor's mannequin: the chef's own jacket on a stand, a size too big.
        private static void Mannequin(Transform parent, Vector3 at)
        {
            var dummy = new GameObject("Mannequin").transform;
            dummy.SetParent(parent, false);
            dummy.localPosition = at;
            dummy.localRotation = Quaternion.Euler(0f, 200f, 0f);
            var white = Toon("ChefJacket", new Color(0.97f, 0.97f, 0.95f));
            Shape(dummy, PrimitiveType.Cylinder, "Stand", Toon("OfficeWalnut", Walnut), new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 0.45f, 0.06f));
            Shape(dummy, PrimitiveType.Cylinder, "Foot", Toon("OfficeWalnut", Walnut), new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.02f, 0.5f));
            if (CharacterArt.Available)
            {
                // The kit's barrel jacket, its buttons and the hips under it, on the stand.
                Model(dummy, "Torso", CharacterArt.Model, "CharTorso", white, new Vector3(0f, 0.78f, 0f), 0f, scale: 1.18f);
                Model(dummy, "Buttons", CharacterArt.Model, "CharButtons", Toon("OfficeInk", Ink), new Vector3(0f, 0.78f, 0f), 0f, scale: 1.18f);
                Model(dummy, "Skirt", CharacterArt.Model, "CharHips", white, new Vector3(0f, -0.06f, 0f), 0f, scale: 1.18f);
            }
            else
            {
                Model(dummy, "Torso", ChefModel, "ChefTorso", white, new Vector3(0f, ChefWaist, 0f), 0f, scale: 1.18f);
                Model(dummy, "Skirt", ChefModel, "ChefSkirt", white, new Vector3(0f, -0.1f, 0f), 0f, scale: 1.18f);
            }
            var collider = dummy.gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.85f, 0f);
            collider.height = 1.7f;
            collider.radius = 0.32f;
        }

        // ---- Furniture ----------------------------------------------------------------------------

        private static void Sofa(Transform parent, Vector3 at, float facing, Color fabric)
        {
            var key = ColorUtility.ToHtmlStringRGB(fabric);
            Model(parent, "Sofa", OfficeModel, "OfficeSofaFrame", Toon($"OfficeSofa_{key}", fabric), at, facing, collider: new Vector3(2.1f, 0.8f, 0.9f));
            Model(parent, "SofaCushions", OfficeModel, "OfficeSofaCushions", Toon($"OfficeCushion_{key}", Color.Lerp(fabric, Cream, 0.15f)), at, facing);
        }

        private static void Armchair(Transform parent, Vector3 at, float facing, Color fabric)
        {
            var key = ColorUtility.ToHtmlStringRGB(fabric);
            Model(parent, "Armchair", OfficeModel, "OfficeArmchairFrame", Toon($"OfficeSofa_{key}", fabric), at, facing, collider: new Vector3(0.95f, 0.8f, 0.88f));
            Model(parent, "ArmchairCushion", OfficeModel, "OfficeArmchairCushion", Toon($"OfficeCushion_{key}", Color.Lerp(fabric, Cream, 0.15f)), at, facing);
        }

        private static void OfficeChair(Transform parent, Vector3 at, float facing)
        {
            Model(parent, "OfficeChair", OfficeModel, "OfficeChairSeat", Toon("OfficeChairLeather", new Color(0.32f, 0.12f, 0.12f)), at, facing, collider: new Vector3(0.66f, 1.4f, 0.66f));
            Model(parent, "OfficeChairBase", OfficeModel, "OfficeChairBase", Toon("OfficeChrome", new Color(0.55f, 0.56f, 0.6f)), at, facing);
        }

        private static void PottedPlant(Transform parent, Vector3 at, float scale)
        {
            Model(parent, "PlantPot", OfficeModel, "OfficePlantPot", Toon("OfficePot", Terracotta), at, 0f, scale: scale, collider: new Vector3(0.5f, 0.5f, 0.5f));
            Model(parent, "PlantLeaves", OfficeModel, "OfficePlantLeaves", Toon("OfficePlant", Plant), at, (float)_random.NextDouble() * 360f, scale: scale);
        }

        private static void FloorLamp(Transform parent, Vector3 at)
        {
            Model(parent, "LampStand", OfficeModel, "OfficeLampStand", Toon("OfficeBrass", Brass), at, 0f, collider: new Vector3(0.36f, 1.6f, 0.36f));
            Model(parent, "LampShade", OfficeModel, "OfficeLampShade", Unlit("OfficeLampGlow", new Color(1f, 0.86f, 0.55f)), at, 0f);
            Point(parent, "FloorLampLight", at + Vector3.up * 1.45f, Incandescent, 4.5f, 1.8f);
        }

        private static void Bookcase(Transform parent, Vector3 at, float facing)
        {
            var bookcase = new GameObject("Bookcase").transform;
            bookcase.SetParent(parent, false);
            bookcase.localPosition = at;
            bookcase.localRotation = Quaternion.Euler(0f, facing, 0f);
            Block(bookcase, "Case", Toon("OfficeWalnut", Walnut), Vector3.zero, new Vector3(1.8f, 1.7f, 0.45f), collider: true);
            var colours = new[] { Burgundy, Leather, new Color(0.32f, 0.42f, 0.55f), Cream, new Color(0.55f, 0.36f, 0.22f) };
            foreach (var y in new[] { 0.12f, 0.66f, 1.2f })
                for (var x = -0.8f; x < 0.78f; x += 0.11f)
                {
                    var c = _random.Next(colours.Length);
                    Block(bookcase, "Book", Toon($"OfficeBook{c}", colours[c]), new Vector3(x + 0.05f, y, 0.06f), new Vector3(0.08f, 0.34f + (float)_random.NextDouble() * 0.1f, 0.34f));
                }
        }

        private static void Mirror(Transform parent, Vector3 at, float facing)
        {
            var mirror = new GameObject("Mirror").transform;
            mirror.SetParent(parent, false);
            mirror.localPosition = at;
            mirror.localRotation = Quaternion.Euler(0f, facing, 0f);
            Block(mirror, "Frame", Toon("OfficeBrass", Brass), Vector3.zero, new Vector3(1.1f, 2.1f, 0.1f), collider: true);
            Cube(mirror, "Glass", Unlit("OfficeMirror", new Color(0.74f, 0.84f, 0.9f)), new Vector3(0f, 0.1f, 0.055f), new Vector3(0.92f, 1.88f, 0.01f));
        }

        private static void HangPicture(Transform parent, Vector3 at, Vector3 facing, Color canvas)
        {
            // On the wall's face, not its middle.
            DiningArt.HangPicture(parent, $"Picture_{ColorUtility.ToHtmlStringRGB(canvas)}", at + facing * (WallThickness / 2f), facing, canvas);
        }

        // A tree in a walnut planter, in the night city's tree model.
        private static void Tree(Transform parent, Vector3 at)
        {
            Block(parent, "Planter", Toon("OfficeWalnut", Walnut), at, new Vector3(0.9f, 0.6f, 0.9f), collider: true);
            Model(parent, "TreeTrunk", CityModel, "CityTreeTrunk", Toon("CityTreeTrunk", new Color(0.32f, 0.22f, 0.15f)), at + Vector3.up * 0.5f, 0f, scale: 0.55f);
            Model(parent, "TreeCrown", CityModel, "CityTreeCrown", Toon("CityTreeCrown", new Color(0.2f, 0.36f, 0.26f)), at + Vector3.up * 0.5f, 0f, scale: 0.55f);
        }

        private static void StreetLamp(Transform parent, Vector3 at)
        {
            Model(parent, "LampPost", CityModel, "CityLampPost", Toon("CityLampIron", new Color(0.12f, 0.16f, 0.14f)), at, 0f, scale: 0.7f, collider: new Vector3(0.3f, 4f, 0.3f));
            Model(parent, "LampGlass", CityModel, "CityLampGlass", Unlit("OfficeLampGlow", new Color(1f, 0.86f, 0.55f)), at, 0f, scale: 0.7f);
            Point(parent, "LampPostLight", at + Vector3.up * 2.9f, Incandescent, 7f, 2.5f);
        }

        // The city on the miniature table: the night city's blocks at about 1/60, with streets,
        // a few lit windows, and the grill's pin (restaurants as pins come in Phase 6).
        private static void Miniature(Transform parent, Vector3 ground, Vector2 size)
        {
            var city = new GameObject("MiniatureCity").transform;
            city.SetParent(parent, false);
            const float step = 0.34f;
            var lit = Unlit("OfficeMiniLit", WindowLit);
            var columns = Mathf.FloorToInt(size.x / step);
            var rows = Mathf.FloorToInt(size.y / step);
            for (var i = 0; i < columns; i++)
            for (var j = 0; j < rows; j++)
            {
                if (i % 4 == 3 || j % 3 == 2)
                    continue; // streets
                var x = -size.x / 2f + step * (i + 0.5f);
                var z = -size.y / 2f + step * (j + 0.5f);
                var height = 0.05f + (float)_random.NextDouble() * (_random.NextDouble() < 0.15 ? 0.5f : 0.22f);
                var c = _random.Next(TowerColors.Length);
                var at = ground + new Vector3(x, 0f, z);
                Model(city, "Block", CityModel, "CityBlock", Toon($"OfficeTower{c}", TowerColors[c]), at, 0f, scale3: new Vector3(0.26f, height, 0.26f));
                if (_random.NextDouble() < 0.3)
                    Cube(city, "Lit", lit, at + new Vector3(0f, height * 0.45f, -0.132f), new Vector3(0.12f, height * 0.35f, 0.004f));
            }
            var pin = ground + new Vector3(-1.2f, 0f, -0.7f);
            Shape(city, PrimitiveType.Cylinder, "GrillPin", Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f)), pin + Vector3.up * 0.25f, new Vector3(0.04f, 0.25f, 0.04f));
            Shape(city, PrimitiveType.Sphere, "GrillPinHead", Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f)), pin + Vector3.up * 0.52f, Vector3.one * 0.14f);
        }

        // A copy of one of the kitchen's furniture prefabs, visual only: unpacked and stripped of
        // its scripts (they need the kitchen's systems), keeping meshes and colliders, and without
        // the plates laid on tables. `keep: "Body"` also drops its chairs.
        private static GameObject Prop(Transform parent, string prefabName, Vector3 at, float facing, string keep = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Furniture/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[OfficeBuilder] No {prefabName}: run Tools/Game/Build Greybox Kitchen.");
                return null;
            }
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copy.name = prefabName.Replace("PF_", "");
            // Scripts may require each other: remove until none are left.
            for (var pass = 0; pass < 4; pass++)
                foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script != null)
                        Object.DestroyImmediate(script);
            foreach (var child in new List<Transform>(copy.GetComponentsInChildren<Transform>(true)))
                if (child != null && (child.name.StartsWith("Plate") || (keep == "Body" && child.name.StartsWith("Chair"))))
                    Object.DestroyImmediate(child.gameObject);
            copy.transform.localPosition = at;
            copy.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = true;
            return copy;
        }

        // ---- Meshes and materials ------------------------------------------------------------------

        // A mesh from one of the Blender models, placed on `at` facing `facing`; a box collider of
        // `collider` (width, height, depth) standing on its base, when given.
        private static GameObject Model(Transform parent, string name, string model, string mesh, Material material, Vector3 at, float facing,
            float scale = 1f, Vector3? scale3 = null, Vector3? collider = null)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (!PilotArt.TryUseMesh(part, model, mesh))
            {
                Debug.LogWarning($"[OfficeBuilder] No mesh {mesh} in {model}.");
                Object.DestroyImmediate(part);
                return null;
            }
            part.transform.localPosition = at;
            part.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            part.transform.localScale = scale3 ?? Vector3.one * scale;
            part.isStatic = true;
            if (collider.HasValue)
            {
                // In the part's own (scaled) space, so divide the size back out.
                var s = part.transform.localScale;
                var box = part.AddComponent<BoxCollider>();
                box.size = new Vector3(collider.Value.x / s.x, collider.Value.y / s.y, collider.Value.z / s.z);
                box.center = new Vector3(0f, box.size.y / 2f, 0f);
            }
            return part;
        }

        // The bevelled block from SM_Office, standing on `bottom` (the centre of its base), so simple
        // shapes catch the light on their edges like the modelled props.
        private static GameObject Block(Transform parent, string name, Material material, Vector3 bottom, Vector3 size, bool collider = false) =>
            Model(parent, name, OfficeModel, "OfficeBlock", material, bottom, 0f, scale3: size, collider: collider ? size : (Vector3?)null);

        // A plain box standing on `bottom`: for floors, rugs, glass and other flat things.
        private static GameObject Cube(Transform parent, string name, Material material, Vector3 bottom, Vector3 size, bool collider = false)
        {
            var box = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: collider);
            box.transform.localPosition = bottom + Vector3.up * (size.y / 2f);
            box.transform.localScale = size;
            box.isStatic = true;
            return box;
        }

        private static GameObject Shape(Transform parent, PrimitiveType type, string name, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var shape = GreyboxAssets.CreatePrimitive(type, name, parent, material, keepCollider: false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            if (rotation.HasValue)
                shape.transform.localRotation = rotation.Value;
            shape.isStatic = true;
            return shape;
        }

        private static Material Toon(string name, Color color) => StyleTest.EnsureToonMaterial(name, color);

        // The office's panelling, in the kitchen's room shader: walnut boards nearly to the top, a
        // brass rail and cap. Seen from above, the cap is what reads, so it outlines every room.
        private static Material OfficeWall() => HouseStyle.RoomMaterial("OfficeWall", "WALL", m =>
        {
            m.SetColor("_BaseColor", new Color(0.86f, 0.80f, 0.70f));
            m.SetColor("_PanelColor", new Color(0.40f, 0.26f, 0.17f));
            m.SetColor("_CapColor", new Color(0.78f, 0.58f, 0.30f));
            m.SetColor("_SeamColor", new Color(0.24f, 0.15f, 0.10f));
            m.SetFloat("_PanelHeight", 0.86f);
            m.SetFloat("_PanelWidth", 0.5f);
            m.SetFloat("_SeamWidth", 0.012f);
            m.SetFloat("_Variation", 0.04f);
            m.SetFloat("_Grain", 0.5f);
        });

        // Floors in the kitchen's room shader, at true size in world space; calmer than the
        // kitchen's (less grain, softer seams), as polished floors.
        private static Material Planks(string name, Color wood, Color seam, float width, float length) => HouseStyle.RoomMaterial(name, "PLANKS", m =>
        {
            m.SetColor("_BaseColor", wood);
            m.SetColor("_SeamColor", seam);
            m.SetFloat("_Size", width);
            m.SetFloat("_Length", length);
            m.SetFloat("_SeamWidth", 0.006f);
            m.SetFloat("_Variation", 0.05f);
            m.SetFloat("_Grain", 0.35f);
        });

        private static Material Tiles(string name, Color tile, Color seam, float size, float seamWidth = 0.012f) => HouseStyle.RoomMaterial(name, "TILES", m =>
        {
            m.SetColor("_BaseColor", tile);
            m.SetColor("_SeamColor", seam);
            m.SetFloat("_Size", size);
            m.SetFloat("_SeamWidth", seamWidth);
            m.SetFloat("_Variation", 0.05f);
            m.SetFloat("_Grain", 0f);
        });

        // Things that give off light at night (windows, lamps): unlit, so they read bright in the dark.
        private static Material Unlit(string name, Color color)
        {
            var path = $"Assets/_Project/Art/Materials/MAT_Unlit_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
