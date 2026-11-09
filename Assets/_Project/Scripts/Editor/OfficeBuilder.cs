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
    /// Builds the greybox of the boss's floor (spec: docs/office-scene.md; decision 2026-11-09): a
    /// 3 × 3 grid of 8 × 6 m rooms on the penthouse of his tower, a terrace with a helipad behind,
    /// and the night city below. Players join through the one big elevator in reception. Only the
    /// space and the elevator work; the rooms' jobs (miniature, loans, tailor, test kitchen) come in
    /// Phase 6. Props are primitives in the toon materials until the art phase. Seeded, so every
    /// rebuild is the same. Menu: Tools/Game/Build Office. Needs PF_Player (Build Greybox Kitchen).
    /// </summary>
    public static class OfficeBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Office_Greybox.unity";
        private const string SettingsFolder = "Assets/_Project/Data/Settings";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string SfxFolder = "Assets/_Project/Audio/SFX";
        private const int Seed = 2611_09;

        // The floor: three columns of 8 m and three rows of 6 m, centred on X; the front row is
        // nearest the camera (south, −Z). The terrace runs along the back.
        private const float RoomWidth = 8f;
        private const float RoomDepth = 6f;
        private const float FloorWest = -12f;
        private const float FloorSouth = -9f;
        private const float FloorNorth = 9f;
        private const float TerraceNorth = 14f;

        // Walls (art bible: the dollhouse cut, applied to a whole floor): 1.0 m everywhere, so the
        // overhead camera sees into every room; the back wall is a low sill with glass above.
        private const float WallHeight = 1f;
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 1.6f;
        private const float SillHeight = 0.5f;
        private const float GlassTop = 2.4f;

        // The elevator car, against the front wall in reception, its doors facing north.
        private const float CarHalfWidth = 1.6f;
        private const float CarDoorZ = -6.8f;
        private const float CarWallHeight = 2.2f;
        private const float DoorWallHeight = 2.5f;
        private const float DoorPanelWidth = 1.4f;
        private const float DoorPanelHeight = 2.0f;

        // Palette: the kitchen's warm woods and cream, dressed up; burgundy rather than the "Bad"
        // red for the carpet, and floors kept mid-dark so a white chef pops (spec §5).
        private static readonly Color Ink = new Color32(0x3A, 0x24, 0x14, 0xFF);
        private static readonly Color WoodDark = new Color32(0x5C, 0x3A, 0x21, 0xFF);
        private static readonly Color WoodMid = new Color32(0x8B, 0x5A, 0x33, 0xFF);
        private static readonly Color Cream = new Color32(0xEA, 0xDF, 0xC6, 0xFF);
        private static readonly Color Brass = new Color(0.86f, 0.62f, 0.26f);
        private static readonly Color Burgundy = new Color(0.43f, 0.11f, 0.14f);
        private static readonly Color Leather = new Color(0.20f, 0.36f, 0.24f);
        private static readonly Color Suit = new Color(0.14f, 0.15f, 0.18f);
        private static readonly Color Skin = new Color(0.96f, 0.78f, 0.63f);
        private static readonly Color Plant = new Color(0.24f, 0.44f, 0.26f);
        private static readonly Color Pot = new Color(0.36f, 0.24f, 0.18f);
        private static readonly Color Facade = new Color(0.17f, 0.21f, 0.36f);
        private static readonly Color NightSky = new Color(0.07f, 0.08f, 0.16f);
        private static readonly Color WindowLit = new Color(1f, 0.80f, 0.45f);
        private static readonly Color WindowDark = new Color(0.12f, 0.13f, 0.22f);
        private static readonly Color LampWarm = new Color(1f, 0.85f, 0.66f); // the art bible's #FFD9A8, a touch warmer

        private static readonly Color[] TowerColors =
        {
            new Color(0.17f, 0.20f, 0.34f), new Color(0.22f, 0.20f, 0.32f), new Color(0.14f, 0.17f, 0.27f), new Color(0.24f, 0.25f, 0.36f),
        };

        private static System.Random _random;

        [MenuItem("Tools/Game/Build Office")]
        public static void Build()
        {
            // The scene first: opening one unloads assets nothing references yet (see the kitchen builder).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _random = new System.Random(Seed);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
                throw new System.InvalidOperationException($"No {PlayerPrefabPath}: run Tools/Game/Build Greybox Kitchen first.");
            var coop = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            var elevatorConfig = GreyboxAssets.EnsureAsset<ElevatorConfig>(SettingsFolder + "/ElevatorConfig.asset");

            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            HouseStyle.LightScene(sun); // the kitchen's warm key, so chefs look the same here

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
            camera.backgroundColor = NightSky;
            HouseStyle.SmoothEdges(camera);
            camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<OfficeCamera>();
            camera.transform.SetPositionAndRotation(follow.Wanted(), Quaternion.Euler(55f, 0f, 0f));

            HouseStyle.ApplyToMaterials();
            GreyboxAssets.EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OfficeBuilder] Built {ScenePath}.");
        }

        // ---- The floor, walls and tower ----------------------------------------------------------

        private static float RoomX(int column) => FloorWest + RoomWidth * (column + 0.5f);
        private static float RoomZ(int row) => FloorSouth + RoomDepth * (row + 0.5f);

        // The roof slab everyone walks on, each room's floor on it, and the terrace's decking.
        private static void BuildShell(Transform floor)
        {
            var slab = Box(floor, "RoofSlab", Toon("OfficeSlab", WoodDark), new Vector3(0f, -0.4f, (FloorSouth + TerraceNorth) / 2f),
                new Vector3(24.8f, 0.4f, TerraceNorth - FloorSouth + 0.8f), collider: true);
            slab.isStatic = true;

            // Room floors: a few millimetres up, without colliders.
            RoomFloor(floor, "Floor_Tailor", 0, 2, Wood("OfficeParquet", new Color(0.48f, 0.30f, 0.18f)));
            RoomFloor(floor, "Floor_BossOffice", 1, 2, Wood("OfficeBossWood", new Color(0.30f, 0.19f, 0.12f)));
            RoomFloor(floor, "Floor_BackRoom", 2, 2, Wood("OfficeParquetLight", new Color(0.52f, 0.34f, 0.20f)));
            RoomFloor(floor, "Floor_TestKitchen", 0, 1, HouseStyle.KitchenFloor());
            RoomFloor(floor, "Floor_GuardedDoors", 1, 1, Toon("OfficeAnteroom", new Color(0.24f, 0.19f, 0.16f)));
            RoomFloor(floor, "Floor_HallOfFame", 2, 1, Wood("OfficeHallWood", new Color(0.40f, 0.27f, 0.18f)));
            RoomFloor(floor, "Floor_Lounge", 0, 0, Toon("OfficeLoungeCarpet", new Color(0.18f, 0.26f, 0.27f)));
            RoomFloor(floor, "Floor_Accountant", 2, 0, Toon("OfficeAccountCarpet", new Color(0.33f, 0.31f, 0.21f)));

            // Reception: a checker in two close wood tones, not the draft's black and white, which
            // would swallow a white chef from above (spec §5).
            var tiles = new GameObject("Floor_Reception").transform;
            tiles.SetParent(floor, false);
            var light = Toon("OfficeCheckerLight", new Color(0.46f, 0.32f, 0.22f));
            var dark = Toon("OfficeCheckerDark", new Color(0.38f, 0.26f, 0.18f));
            for (var x = 0; x < 8; x++)
            for (var z = 0; z < 6; z++)
                Box(tiles, "Tile", (x + z) % 2 == 0 ? light : dark, new Vector3(-3.5f + x, 0f, FloorSouth + 0.5f + z), new Vector3(1f, 0.008f, 1f));

            // The carpet: from the elevator's doors straight through the guarded doors to his desk.
            Box(floor, "Carpet", Toon("OfficeCarpet", Burgundy), new Vector3(0f, 0f, (CarDoorZ + 6.2f) / 2f), new Vector3(2f, 0.016f, 6.2f - CarDoorZ));
            Box(floor, "CarpetEdge_W", Toon("OfficeBrass", Brass), new Vector3(-1.04f, 0f, (CarDoorZ + 6.2f) / 2f), new Vector3(0.08f, 0.018f, 6.2f - CarDoorZ));
            Box(floor, "CarpetEdge_E", Toon("OfficeBrass", Brass), new Vector3(1.04f, 0f, (CarDoorZ + 6.2f) / 2f), new Vector3(0.08f, 0.018f, 6.2f - CarDoorZ));

            Box(floor, "Floor_Terrace", Wood("OfficeDecking", new Color(0.36f, 0.30f, 0.26f)), new Vector3(0f, 0f, (FloorNorth + TerraceNorth) / 2f),
                new Vector3(24f, 0.006f, TerraceNorth - FloorNorth));
        }

        private static void RoomFloor(Transform parent, string name, int column, int row, Material material) =>
            Box(parent, name, material, new Vector3(RoomX(column), 0f, RoomZ(row)), new Vector3(RoomWidth, 0.006f, RoomDepth));

        private static void BuildWalls(Transform floor)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(floor, false);
            var none = new float[0];

            // Outside: the front and sides are plain low walls; the back is a sill with glass, with
            // a door from each back room onto the terrace.
            WallAlongX(walls, "Wall_South", FloorSouth, -12f, 12f, none);
            WallAlongZ(walls, "Wall_West", -12f, FloorSouth, FloorNorth, none);
            WallAlongZ(walls, "Wall_East", 12f, FloorSouth, FloorNorth, none);
            GlassAlongX(walls, "Wall_North", FloorNorth, -12f, 12f, new[] { RoomX(0), RoomX(1), RoomX(2) });

            // Between columns. The serving hatch from the test kitchen to the bodyguards is a gap
            // filled by a counter (below).
            WallAlongZ(walls, "Wall_LoungeReception", -4f, -9f, -3f, new[] { -5f });
            WallAlongZ(walls, "Wall_KitchenGuards", -4f, -3f, 3f, new[] { -1f }, hatchAt: 1.6f);
            WallAlongZ(walls, "Wall_TailorBoss", -4f, 3f, 9f, none);
            WallAlongZ(walls, "Wall_ReceptionAccountant", 4f, -9f, -3f, new[] { -5f });
            WallAlongZ(walls, "Wall_GuardsHall", 4f, -3f, 3f, new[] { 0f });
            WallAlongZ(walls, "Wall_BossBackRoom", 4f, 3f, 9f, none);

            // Between rows: every room opens to the one in front and behind, except the boss.
            WallAlongX(walls, "Wall_LoungeKitchen", -3f, -12f, -4f, new[] { -8f });
            WallAlongX(walls, "Wall_ReceptionGuards", -3f, -4f, 4f, new[] { 0f }, 2.4f);
            WallAlongX(walls, "Wall_AccountantHall", -3f, 4f, 12f, new[] { 8f });
            WallAlongX(walls, "Wall_KitchenTailor", 3f, -12f, -4f, new[] { -8f });
            WallAlongX(walls, "Wall_GuardsBoss", 3f, -4f, 4f, new[] { 0f }, 2f);
            WallAlongX(walls, "Wall_HallBackRoom", 3f, 4f, 12f, new[] { 8f });

            // The terrace's railing: posts, glass and a brass top rail at the edge.
            Railing(walls, "Railing_North", new Vector3(-12f, 0f, TerraceNorth), new Vector3(12f, 0f, TerraceNorth));
            Railing(walls, "Railing_West", new Vector3(-12f, 0f, FloorNorth), new Vector3(-12f, 0f, TerraceNorth));
            Railing(walls, "Railing_East", new Vector3(12f, 0f, FloorNorth), new Vector3(12f, 0f, TerraceNorth));
        }

        private static void WallAlongX(Transform parent, string name, float z, float x0, float x1, float[] doors, float doorWidth = DoorWidth) =>
            Wall(parent, name, x0, x1, doors, doorWidth, null, (a, b) => (new Vector3((a + b) / 2f, 0f, z), new Vector3(b - a, 0f, WallThickness)));

        private static void WallAlongZ(Transform parent, string name, float x, float z0, float z1, float[] doors, float doorWidth = DoorWidth, float? hatchAt = null) =>
            Wall(parent, name, z0, z1, doors, doorWidth, hatchAt, (a, b) => (new Vector3(x, 0f, (a + b) / 2f), new Vector3(WallThickness, 0f, b - a)));

        // A low capped wall from `from` to `to` with door gaps; `place` turns a run into a centre and size.
        private static void Wall(Transform parent, string name, float from, float to, float[] doors, float doorWidth, float? hatchAt,
            System.Func<float, float, (Vector3 Centre, Vector3 Size)> place)
        {
            var wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            var gaps = new List<(float From, float To)>();
            foreach (var door in doors)
                gaps.Add((door - doorWidth / 2f, door + doorWidth / 2f));
            const float hatchWidth = 1.2f;
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
                // The serving hatch: a counter in the wall, cream top and brass edge, at counter height.
                var (centre, size) = place(hatchAt.Value - hatchWidth / 2f, hatchAt.Value + hatchWidth / 2f);
                Box(wall, "ServingHatch", Toon("OfficeCabinet", WoodMid), centre, new Vector3(size.x + 0.2f, 0.88f, size.z + 0.4f), collider: true);
                Box(wall, "ServingHatchTop", Toon("OfficeCounterTop", Cream), centre + Vector3.up * 0.88f, new Vector3(size.x + 0.3f, 0.06f, size.z + 0.5f));
            }
        }

        private static void WallRun(Transform wall, (Vector3 Centre, Vector3 Size) run)
        {
            var length = Mathf.Max(run.Size.x, run.Size.z);
            if (length < 0.05f)
                return;
            Box(wall, "Wall", HouseStyle.Wall(), run.Centre, new Vector3(run.Size.x, WallHeight, run.Size.z), collider: true);
            // The trim cap, a touch wider, in dark wood.
            var cap = run.Size.x > run.Size.z ? new Vector3(run.Size.x, 0.06f, WallThickness + 0.08f) : new Vector3(WallThickness + 0.08f, 0.06f, run.Size.z);
            Box(wall, "Cap", Toon("OfficeTrim", WoodDark), run.Centre + Vector3.up * WallHeight, cap);
        }

        // The back wall: a sill with glass above it, and a gap at each door onto the terrace.
        private static void GlassAlongX(Transform parent, string name, float z, float x0, float x1, float[] doors)
        {
            var wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            var glass = PilotArt.EnsureGlassMaterial("OfficeWindow", new Color(0.62f, 0.76f, 0.86f, 0.18f));
            var start = x0;
            void Run(float a, float b)
            {
                if (b - a < 0.05f)
                    return;
                var centre = new Vector3((a + b) / 2f, 0f, z);
                Box(wall, "Sill", HouseStyle.Wall(), centre, new Vector3(b - a, SillHeight, WallThickness), collider: true);
                Box(wall, "Glass", glass, centre + Vector3.up * SillHeight, new Vector3(b - a, GlassTop - SillHeight, 0.04f), collider: true);
                Box(wall, "Head", Toon("OfficeTrim", WoodDark), centre + Vector3.up * GlassTop, new Vector3(b - a, 0.08f, WallThickness));
                for (var x = a; x <= b + 0.01f; x += 2f)
                    Box(wall, "Mullion", Toon("OfficeTrim", WoodDark), new Vector3(Mathf.Min(x, b), 0f, z), new Vector3(0.08f, GlassTop, WallThickness));
            }
            foreach (var door in doors)
            {
                Run(start, door - DoorWidth / 2f);
                start = door + DoorWidth / 2f;
            }
            Run(start, x1);
        }

        private static void Railing(Transform parent, string name, Vector3 from, Vector3 to)
        {
            var railing = new GameObject(name).transform;
            railing.SetParent(parent, false);
            var glass = PilotArt.EnsureGlassMaterial("OfficeWindow", new Color(0.62f, 0.76f, 0.86f, 0.18f));
            var length = Vector3.Distance(from, to);
            var alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);
            var centre = (from + to) / 2f;
            var size = alongX ? new Vector3(length, 0f, 0.05f) : new Vector3(0.05f, 0f, length);
            Box(railing, "Glass", glass, centre, new Vector3(size.x, 1.0f, size.z), collider: true);
            Box(railing, "Rail", Toon("OfficeBrass", Brass), centre + Vector3.up * 1.0f, new Vector3(Mathf.Max(size.x, 0.1f), 0.07f, Mathf.Max(size.z, 0.1f)));
            var posts = Mathf.CeilToInt(length / 1.5f);
            for (var i = 0; i <= posts; i++)
                Box(railing, "Post", Toon("OfficeBrass", Brass), Vector3.Lerp(from, to, i / (float)posts), new Vector3(0.08f, 1.0f, 0.08f));
        }

        // ---- The elevator -------------------------------------------------------------------------

        private static OfficeElevator BuildElevator(Transform floor, PlayerInputManager manager, ElevatorConfig config, out Transform[] spawnPoints)
        {
            var root = new GameObject("Elevator").transform;
            root.SetParent(floor, false);
            var brass = Toon("OfficeBrass", Brass);
            var frame = Toon("OfficeLiftFrame", new Color(0.22f, 0.17f, 0.15f));
            var depth = CarDoorZ - FloorSouth;

            // The car: a brass-trimmed floor, tall side walls and the door wall; the front is the
            // building's own low wall, so the camera sees in.
            Box(root, "CarFloor", Toon("OfficeLiftFloor", new Color(0.30f, 0.24f, 0.22f)), new Vector3(0f, 0f, FloorSouth + depth / 2f), new Vector3(CarHalfWidth * 2f, 0.02f, depth));
            Box(root, "CarFloorTrim", brass, new Vector3(0f, 0f, FloorSouth + depth / 2f), new Vector3(CarHalfWidth * 2f + 0.1f, 0.012f, depth + 0.05f));
            foreach (var side in new[] { -1f, 1f })
            {
                Box(root, side < 0 ? "CarWall_W" : "CarWall_E", frame, new Vector3(side * (CarHalfWidth + 0.1f), 0f, FloorSouth + depth / 2f - 0.15f),
                    new Vector3(0.2f, CarWallHeight, depth - 0.3f), collider: true);
                Box(root, "CarWallTrim", brass, new Vector3(side * (CarHalfWidth + 0.1f), CarWallHeight, FloorSouth + depth / 2f - 0.15f), new Vector3(0.26f, 0.06f, depth - 0.3f));
            }

            // The door wall: pockets either side the panels slide into, and a lintel with the dial.
            var pocket = CarHalfWidth + 1.4f;
            foreach (var side in new[] { -1f, 1f })
            {
                var centreX = side * (DoorPanelWidth + (pocket - DoorPanelWidth) / 2f);
                Box(root, side < 0 ? "Pocket_W" : "Pocket_E", frame, new Vector3(centreX, 0f, CarDoorZ), new Vector3(pocket - DoorPanelWidth, DoorWallHeight, 0.3f), collider: true);
                Box(root, "Jamb", brass, new Vector3(side * DoorPanelWidth, 0f, CarDoorZ - 0.16f), new Vector3(0.1f, DoorPanelHeight, 0.04f));
            }
            Box(root, "Lintel", frame, new Vector3(0f, DoorPanelHeight, CarDoorZ), new Vector3(DoorPanelWidth * 2f, DoorWallHeight - DoorPanelHeight, 0.3f), collider: true);
            Box(root, "LintelTrim", brass, new Vector3(0f, DoorWallHeight, CarDoorZ), new Vector3(pocket * 2f, 0.08f, 0.36f));

            var doorMaterial = Toon("OfficeLiftDoor", new Color(0.78f, 0.58f, 0.30f));
            var left = Box(root, "Door_L", doorMaterial, new Vector3(-DoorPanelWidth / 2f, 0f, CarDoorZ), new Vector3(DoorPanelWidth, DoorPanelHeight, 0.08f), collider: true);
            var right = Box(root, "Door_R", doorMaterial, new Vector3(DoorPanelWidth / 2f, 0f, CarDoorZ), new Vector3(DoorPanelWidth, DoorPanelHeight, 0.08f), collider: true);
            left.isStatic = right.isStatic = false;
            // A brass seam down each panel's meeting edge, so open and shut read from above.
            foreach (var (door, side) in new[] { (left, 1f), (right, -1f) })
            {
                var seam = Box(door.transform.parent, "Seam", brass, Vector3.zero, Vector3.one);
                seam.transform.SetParent(door.transform, false);
                seam.transform.localPosition = new Vector3(side * 0.47f, 0f, -0.6f);
                seam.transform.localScale = new Vector3(0.05f, 1f, 1.3f);
                seam.isStatic = false;
            }

            // Spawn points: up to four chefs inside, facing the doors.
            var spawns = new GameObject("SpawnPoints").transform;
            spawns.SetParent(root, false);
            var spawnAt = new[] { new Vector3(-0.7f, 0f, -7.7f), new Vector3(0.7f, 0f, -7.7f), new Vector3(-0.7f, 0f, -8.5f), new Vector3(0.7f, 0f, -8.5f) };
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
            car.center = new Vector3(0f, 1f, (FloorSouth + CarDoorZ + 0.7f) / 2f);
            car.size = new Vector3(CarHalfWidth * 2f, 2f, CarDoorZ + 0.7f - FloorSouth);

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
            var dial = Shape(root, PrimitiveType.Cylinder, "Dial", Toon("OfficeDialFace", Cream), new Vector3(0f, dialHeight, face),
                new Vector3(0.5f, 0.02f, 0.5f), Quaternion.Euler(90f, 0f, 0f));
            Shape(root, PrimitiveType.Cylinder, "DialRim", Toon("OfficeBrass", Brass), new Vector3(0f, dialHeight, face + 0.01f),
                new Vector3(0.58f, 0.015f, 0.58f), Quaternion.Euler(90f, 0f, 0f));
            var needle = new GameObject("Needle").transform;
            needle.SetParent(root, false);
            needle.localPosition = new Vector3(0f, dialHeight, face - 0.03f);
            // Pointing up at the top of the dial: this floor. The view turns it about local Z.
            var hand = Box(needle, "Hand", Toon("OfficeInk", Ink), new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.2f, 0.01f));
            hand.isStatic = false;
            dial.isStatic = true;

            var lamp = new GameObject("ArrivedLamp");
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0.55f, dialHeight, face - 0.02f); // beside the dial
            var bulb = Shape(lamp.transform, PrimitiveType.Cube, "Bulb", Unlit("OfficeLiftLamp", new Color(1f, 0.72f, 0.3f)), Vector3.zero,
                new Vector3(0.36f, 0.08f, 0.03f));
            bulb.isStatic = false;
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lamp.transform, false);
            light.transform.localPosition = new Vector3(0f, -0.4f, -0.8f);
            light.type = LightType.Point;
            light.color = LampWarm;
            light.range = 4f;
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

        private static void BuildReception(Transform rooms)
        {
            var room = Room(rooms, "Reception");
            Box(room, "Desk", Wood("OfficeDeskWood", WoodMid), new Vector3(2.5f, 0f, -5.4f), new Vector3(2.2f, 1.0f, 0.7f), collider: true);
            Box(room, "DeskTop", Toon("OfficeBrass", Brass), new Vector3(2.5f, 1.0f, -5.4f), new Vector3(2.3f, 0.05f, 0.8f));
            Box(room, "Ledger", Toon("OfficeLedger", Burgundy), new Vector3(2.1f, 1.05f, -5.45f), new Vector3(0.36f, 0.06f, 0.26f));
            Shape(room, PrimitiveType.Sphere, "DeskBell", Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f)), new Vector3(3.2f, 1.1f, -5.4f), Vector3.one * 0.14f);
            Person(room, "Receptionist", new Vector3(2.5f, 0f, -4.6f), 180f, new Color(0.62f, 0.22f, 0.32f), 1f);
            PottedPlant(room, new Vector3(-3.4f, 0f, -3.6f), 1f);
            PottedPlant(room, new Vector3(3.4f, 0f, -8.4f), 1f);
            PottedPlant(room, new Vector3(-3.4f, 0f, -8.4f), 0.8f);
            Lamp(room, "Pendant", new Vector3(0f, 3.2f, -4.6f), 7f, 1.6f);
        }

        private static void BuildGuardedDoors(Transform rooms)
        {
            var room = Room(rooms, "GuardedDoors");
            // The bodyguards flank the boss's door, facing whoever comes up the carpet.
            Bodyguard(room, "Bodyguard_L", new Vector3(-1.6f, 0f, 2.2f));
            Bodyguard(room, "Bodyguard_R", new Vector3(1.6f, 0f, 2.2f));
            PottedPlant(room, new Vector3(-3.4f, 0f, -2.4f), 1.2f);
            PottedPlant(room, new Vector3(3.4f, 0f, -2.4f), 1.2f);
            // Two columns either side of the door, so it reads as the way to someone important.
            foreach (var x in new[] { -1.15f, 1.15f })
            {
                Shape(room, PrimitiveType.Cylinder, "Column", Toon("OfficeColumn", Cream), new Vector3(x, 1.1f, 2.75f), new Vector3(0.36f, 1.1f, 0.36f), collider: true);
                Box(room, "ColumnCap", Toon("OfficeBrass", Brass), new Vector3(x, 2.2f, 2.75f), new Vector3(0.46f, 0.1f, 0.46f));
            }
        }

        private static void BuildBossOffice(Transform rooms)
        {
            var room = Room(rooms, "BossOffice");
            Box(room, "Rug", Toon("OfficeRug", new Color(0.36f, 0.09f, 0.11f)), new Vector3(0f, 0.01f, 6.1f), new Vector3(6f, 0.012f, 4.6f));
            Box(room, "RugBorder", Toon("OfficeBrass", Brass), new Vector3(0f, 0.008f, 6.1f), new Vector3(6.2f, 0.012f, 4.8f));
            Box(room, "Desk", Wood("OfficeBossDesk", new Color(0.26f, 0.16f, 0.10f)), new Vector3(0f, 0f, 7.0f), new Vector3(2.8f, 0.78f, 1.1f), collider: true);
            Box(room, "DeskLeather", Toon("OfficeLeather", Leather), new Vector3(0f, 0.78f, 7.0f), new Vector3(2.6f, 0.03f, 0.9f));
            Box(room, "Papers", Toon("OfficePaper", Cream), new Vector3(-0.2f, 0.81f, 6.85f), new Vector3(0.42f, 0.02f, 0.3f));
            Box(room, "Chair", Toon("OfficeChair", Burgundy), new Vector3(0f, 0f, 8.15f), new Vector3(1.0f, 0.5f, 0.8f), collider: true);
            Box(room, "ChairBack", Toon("OfficeChair", Burgundy), new Vector3(0f, 0.5f, 8.5f), new Vector3(1.1f, 1.3f, 0.2f));
            Boss(room, new Vector3(0f, 0f, 7.9f));
            foreach (var x in new[] { -0.8f, 0.8f })
            {
                Box(room, "GuestChair", Toon("OfficeGuestChair", new Color(0.55f, 0.17f, 0.17f)), new Vector3(x, 0f, 5.6f), new Vector3(0.6f, 0.45f, 0.6f), collider: true);
                Box(room, "GuestChairBack", Toon("OfficeGuestChair", new Color(0.55f, 0.17f, 0.17f)), new Vector3(x, 0.45f, 5.35f), new Vector3(0.6f, 0.5f, 0.12f));
            }
            // The warmest light on the floor: his desk lamp (spec §5, the focal point).
            Shape(room, PrimitiveType.Cylinder, "LampStem", Toon("OfficeBrass", Brass), new Vector3(1.0f, 1.0f, 7.25f), new Vector3(0.05f, 0.2f, 0.05f));
            Shape(room, PrimitiveType.Cylinder, "LampShade", Unlit("OfficeLampShade", new Color(1f, 0.86f, 0.55f)), new Vector3(1.0f, 1.22f, 7.25f), new Vector3(0.36f, 0.08f, 0.36f));
            Lamp(room, "DeskLight", new Vector3(0.6f, 2.0f, 6.6f), 6f, 3f);
            Box(room, "Bookshelf", Wood("OfficeBossDesk", new Color(0.26f, 0.16f, 0.10f)), new Vector3(-3.55f, 0f, 6f), new Vector3(0.5f, 1.6f, 3.6f), collider: true);
            BookRow(room, new Vector3(-3.3f, 0f, 6f), 3.2f);
            Box(room, "Safe", Toon("OfficeSafe", new Color(0.24f, 0.25f, 0.28f)), new Vector3(3.3f, 0f, 8.3f), new Vector3(0.8f, 1.0f, 0.7f), collider: true);
            Shape(room, PrimitiveType.Cylinder, "SafeDial", Toon("OfficeBrass", Brass), new Vector3(3.3f, 0.6f, 7.94f), new Vector3(0.2f, 0.02f, 0.2f), Quaternion.Euler(90f, 0f, 0f));
            PottedPlant(room, new Vector3(-3.3f, 0f, 3.6f), 1.1f);
            PottedPlant(room, new Vector3(3.3f, 0f, 3.6f), 1.1f);
        }

        private static void BuildTailor(Transform rooms)
        {
            var room = Room(rooms, "Tailor");
            // Three colour plinths: step on one to change look (Phase 6). Low enough to step onto.
            var colours = new[] { new Color(0.95f, 0.55f, 0.15f), new Color(0.15f, 0.70f, 0.75f), new Color(0.85f, 0.25f, 0.45f) };
            var plinths = new[] { new Vector3(-10.2f, 0f, 6.4f), new Vector3(-8f, 0f, 6f), new Vector3(-5.8f, 0f, 6.4f) };
            for (var i = 0; i < plinths.Length; i++)
            {
                Shape(room, PrimitiveType.Cylinder, "Plinth", Toon($"OfficePlinth{i}", colours[i]), plinths[i] + Vector3.up * 0.05f, new Vector3(1.2f, 0.05f, 1.2f));
                Shape(room, PrimitiveType.Cylinder, "PlinthRim", Toon("OfficeBrass", Brass), plinths[i] + Vector3.up * 0.03f, new Vector3(1.3f, 0.03f, 1.3f));
            }
            Mirror(room, new Vector3(-11.65f, 0f, 6.4f), 90f);
            Mirror(room, new Vector3(-4.35f, 0f, 6.4f), -90f);
            Mirror(room, new Vector3(-9.6f, 0f, 8.6f), 180f);
            // A rail of jackets along the west wall.
            Box(room, "RackRail", Toon("OfficeBrass", Brass), new Vector3(-11.4f, 1.6f, 4.5f), new Vector3(0.05f, 0.05f, 2.0f));
            foreach (var z in new[] { 3.7f, 5.3f })
                Box(room, "RackPost", Toon("OfficeBrass", Brass), new Vector3(-11.4f, 0f, z), new Vector3(0.05f, 1.65f, 0.05f));
            var jackets = new[] { Cream, Suit, new Color(0.55f, 0.36f, 0.22f), new Color(0.32f, 0.42f, 0.55f), Burgundy };
            for (var i = 0; i < jackets.Length; i++)
                Box(room, "Jacket", Toon($"OfficeJacket{i}", jackets[i]), new Vector3(-11.4f, 0.7f, 3.85f + i * 0.34f), new Vector3(0.5f, 0.85f, 0.18f), collider: i == 2);
            // The tailor's mannequin, wearing a chef's jacket a size too big (spec §5).
            Shape(room, PrimitiveType.Cylinder, "MannequinStand", Toon("OfficeTrim", WoodDark), new Vector3(-5.0f, 0.4f, 8.3f), new Vector3(0.08f, 0.4f, 0.08f));
            Shape(room, PrimitiveType.Capsule, "MannequinJacket", Toon("OfficeChefJacket", new Color(0.97f, 0.97f, 0.95f)), new Vector3(-5.0f, 1.25f, 8.3f), new Vector3(0.85f, 0.5f, 0.7f), collider: true);
            Person(room, "Tailor", new Vector3(-6.6f, 0f, 4.2f), 200f, new Color(0.35f, 0.30f, 0.45f), 0.95f);
            Box(room, "CuttingTable", Wood("OfficeDeskWood", WoodMid), new Vector3(-10.4f, 0f, 3.8f), new Vector3(1.6f, 0.85f, 0.7f), collider: true);
            Box(room, "Fabric", Toon("OfficePlinth1", colours[1]), new Vector3(-10.6f, 0.85f, 3.8f), new Vector3(0.9f, 0.04f, 0.5f));
        }

        private static void BuildTestKitchen(Transform rooms)
        {
            var room = Room(rooms, "TestKitchen");
            // Bare minimum (decision 2026-11-09): a hob, a board, a small fridge, a bin, an island.
            // Stand-ins only: the real stations come in Phase 6, on the kitchen's own prefabs.
            Counter(room, "BackCounter", new Vector3(-10.3f, 0f, 2.55f), new Vector3(2.6f, 0.9f, 0.7f));
            Box(room, "Hob", Toon("OfficeHob", new Color(0.18f, 0.18f, 0.2f)), new Vector3(-10.9f, 0.95f, 2.55f), new Vector3(0.9f, 0.04f, 0.6f));
            foreach (var x in new[] { -11.12f, -10.68f })
                Shape(room, PrimitiveType.Cylinder, "Burner", Toon("OfficeBurner", new Color(0.08f, 0.08f, 0.09f)), new Vector3(x, 0.98f, 2.55f), new Vector3(0.3f, 0.01f, 0.3f));
            Counter(room, "Island", new Vector3(-8.6f, 0f, -0.2f), new Vector3(2.4f, 0.9f, 1.0f));
            Box(room, "Board", StyleTest.BoardMaterial(), new Vector3(-8.9f, 0.95f, -0.2f), new Vector3(0.7f, 0.05f, 0.5f));
            Counter(room, "ShelfCounter", new Vector3(-5.6f, 0f, 2.55f), new Vector3(2.4f, 0.9f, 0.7f));
            for (var i = 0; i < 4; i++)
                Shape(room, PrimitiveType.Cylinder, "Plate", Toon("OfficePlate", new Color(0.92f, 0.92f, 0.9f)), new Vector3(-5.2f, 0.96f + i * 0.03f, 2.55f), new Vector3(0.4f, 0.012f, 0.4f));
            Box(room, "Fridge", Toon("OfficeFridge", new Color(0.88f, 0.88f, 0.86f)), new Vector3(-11.45f, 0f, -0.6f), new Vector3(0.8f, 1.6f, 0.8f), collider: true);
            Box(room, "FridgeHandle", Toon("OfficeBrass", Brass), new Vector3(-11.02f, 0.9f, -0.35f), new Vector3(0.04f, 0.5f, 0.05f));
            Shape(room, PrimitiveType.Cylinder, "Bin", Toon("OfficeBin", new Color(0.25f, 0.55f, 0.30f)), new Vector3(-11.4f, 0.4f, -2.4f), new Vector3(0.55f, 0.4f, 0.55f), collider: true);
        }

        private static void BuildBackRoom(Transform rooms)
        {
            var room = Room(rooms, "BackRoom");
            // The city miniature: a big dark table with the city on it, lit from above. Your
            // restaurants will show as pins (Phase 6); the grill's is here as a placeholder.
            var centre = new Vector3(8f, 0f, 6f);
            Box(room, "MiniatureTable", Wood("OfficeBossDesk", new Color(0.26f, 0.16f, 0.10f)), centre, new Vector3(4.4f, 0.8f, 2.8f), collider: true);
            Box(room, "MiniatureTrim", Toon("OfficeBrass", Brass), centre + Vector3.up * 0.8f, new Vector3(4.5f, 0.04f, 2.9f));
            Box(room, "MiniatureGround", Toon("OfficeMiniGround", new Color(0.14f, 0.16f, 0.24f)), centre + Vector3.up * 0.84f, new Vector3(4.2f, 0.02f, 2.6f));
            var city = new GameObject("MiniatureCity").transform;
            city.SetParent(room, false);
            var lit = Unlit("OfficeMiniLit", WindowLit);
            for (var x = -1.9f; x < 1.9f; x += 0.32f)
            for (var z = -1.1f; z < 1.1f; z += 0.32f)
            {
                // Streets: leave every fourth row and column empty.
                if (Mathf.Abs((x + 1.9f) / 0.32f % 4f - 3f) < 0.1f || Mathf.Abs((z + 1.1f) / 0.32f % 4f - 3f) < 0.1f)
                    continue;
                var height = 0.04f + (float)_random.NextDouble() * 0.26f;
                var colour = TowerColors[_random.Next(TowerColors.Length)];
                var block = new Vector3(centre.x + x, 0.86f, centre.z + z);
                Box(city, "Block", Toon($"OfficeTower{System.Array.IndexOf(TowerColors, colour)}", colour), block, new Vector3(0.24f, height, 0.24f));
                if (_random.NextDouble() < 0.25)
                    Box(city, "LitWindow", lit, block + new Vector3(0f, height * 0.5f, -0.121f), new Vector3(0.12f, height * 0.4f, 0.004f));
            }
            Shape(room, PrimitiveType.Cylinder, "GrillPin", Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f)), new Vector3(7.1f, 1.15f, 5.3f), new Vector3(0.04f, 0.15f, 0.04f));
            Shape(room, PrimitiveType.Sphere, "GrillPinHead", Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f)), new Vector3(7.1f, 1.32f, 5.3f), Vector3.one * 0.12f);
            Lamp(room, "MiniatureLight", centre + new Vector3(0f, 3f, -0.4f), 6f, 2.6f);
            // Rolled plans and map chests along the east wall.
            Box(room, "MapChest", Wood("OfficeDeskWood", WoodMid), new Vector3(11.5f, 0f, 6f), new Vector3(0.6f, 1.1f, 3.2f), collider: true);
            for (var i = 0; i < 4; i++)
                Shape(room, PrimitiveType.Cylinder, "Plan", Toon("OfficePaper", Cream), new Vector3(11.5f, 1.15f, 4.8f + i * 0.7f), new Vector3(0.1f, 0.3f, 0.1f), Quaternion.Euler(0f, 0f, 90f));
            PottedPlant(room, new Vector3(4.6f, 0f, 8.4f), 1f);
        }

        private static void BuildHallOfFame(Transform rooms)
        {
            var room = Room(rooms, "HallOfFame");
            // The cookbook on its lectern, the room's centre: it outlives chefs (§15.3).
            Box(room, "Lectern", Wood("OfficeBossDesk", new Color(0.26f, 0.16f, 0.10f)), new Vector3(8f, 0f, 0.4f), new Vector3(0.7f, 1.05f, 0.55f), collider: true);
            var book = Box(room, "Cookbook", Toon("OfficeCookbook", new Color(0.62f, 0.16f, 0.14f)), new Vector3(8f, 1.05f, 0.4f), new Vector3(0.55f, 0.1f, 0.42f));
            book.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Box(room, "CookbookPages", Toon("OfficePaper", Cream), new Vector3(8f, 1.1f, 0.38f), new Vector3(0.5f, 0.04f, 0.38f)).transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            // Plinths for owned restaurants: one trophy so far, the rest waiting.
            var plinths = new[] { 5.3f, 6.6f, 9.4f, 10.7f };
            for (var i = 0; i < plinths.Length; i++)
            {
                var at = new Vector3(plinths[i], 0f, 2.2f);
                Box(room, "Plinth", Toon("OfficeColumn", Cream), at, new Vector3(0.6f, 0.95f, 0.6f), collider: true);
                if (i == 0)
                {
                    Shape(room, PrimitiveType.Cylinder, "TrophyBase", Toon("OfficeTrim", WoodDark), at + Vector3.up * 1.0f, new Vector3(0.26f, 0.05f, 0.26f));
                    Shape(room, PrimitiveType.Sphere, "Trophy", Toon("OfficeBrass", Brass), at + Vector3.up * 1.22f, new Vector3(0.3f, 0.34f, 0.3f));
                }
            }
            // Portraits of erased chefs on easels, dusty and dim (§15.3's story, without words).
            foreach (var z in new[] { -1.8f, 0f, 1.8f })
            {
                var easel = new GameObject("Portrait").transform;
                easel.SetParent(room, false);
                easel.localPosition = new Vector3(11.4f, 0f, z);
                easel.localRotation = Quaternion.Euler(0f, -90f, 0f);
                Box(easel, "Leg", Toon("OfficeTrim", WoodDark), new Vector3(0f, 0f, 0.1f), new Vector3(0.06f, 1.4f, 0.06f));
                Box(easel, "Frame", Toon("OfficeDustyFrame", new Color(0.52f, 0.46f, 0.36f)), new Vector3(0f, 0.7f, 0f), new Vector3(0.8f, 0.9f, 0.06f), collider: true);
                Box(easel, "Canvas", Toon("OfficeDustyCanvas", new Color(0.42f, 0.40f, 0.38f)), new Vector3(0f, 0.78f, -0.035f), new Vector3(0.62f, 0.7f, 0.01f));
            }
        }

        private static void BuildLounge(Transform rooms)
        {
            var room = Room(rooms, "WaitingLounge");
            // For fun and easter eggs (decision 2026-11-09): room for characters and objects later.
            Sofa(room, new Vector3(-8.8f, 0f, -8.2f), 0f, 2.6f);
            Sofa(room, new Vector3(-11.3f, 0f, -6.0f), 90f, 2.4f);
            Box(room, "CoffeeTable", Wood("OfficeDeskWood", WoodMid), new Vector3(-9.2f, 0f, -6.3f), new Vector3(1.4f, 0.42f, 0.8f), collider: true);
            Box(room, "CoffeeTableTop", Toon("OfficeBrass", Brass), new Vector3(-9.2f, 0.42f, -6.3f), new Vector3(1.45f, 0.03f, 0.85f));
            // The bodyguards' coffee cups (spec §5).
            foreach (var x in new[] { -9.6f, -9.3f })
                Shape(room, PrimitiveType.Cylinder, "Cup", Toon("OfficePlate", new Color(0.92f, 0.92f, 0.9f)), new Vector3(x, 0.5f, -6.15f), new Vector3(0.09f, 0.05f, 0.09f));
            Box(room, "Magazines", Toon("OfficeLedger", Burgundy), new Vector3(-8.8f, 0.45f, -6.4f), new Vector3(0.3f, 0.03f, 0.22f));
            foreach (var z in new[] { -5.6f, -7.0f })
                Armchair(room, new Vector3(-6.4f, 0f, z));
            Box(room, "WaterCooler", Toon("OfficeFridge", new Color(0.88f, 0.88f, 0.86f)), new Vector3(-11.4f, 0f, -3.6f), new Vector3(0.45f, 1.0f, 0.45f), collider: true);
            Shape(room, PrimitiveType.Cylinder, "WaterBottle", PilotArt.EnsureGlassMaterial("OfficeWater", new Color(0.4f, 0.7f, 0.95f, 0.5f)), new Vector3(-11.4f, 1.2f, -3.6f), new Vector3(0.3f, 0.2f, 0.3f));
            PottedPlant(room, new Vector3(-4.6f, 0f, -8.4f), 1.3f);
            Lamp(room, "LoungeLamp", new Vector3(-9.2f, 2.8f, -6.3f), 6f, 1.2f);
        }

        private static void BuildAccountant(Transform rooms)
        {
            var room = Room(rooms, "Accountant");
            // For fun and easter eggs too (decision 2026-11-09); the books stay on the dashboard.
            Box(room, "Desk", Wood("OfficeDeskWood", WoodMid), new Vector3(8.8f, 0f, -6.4f), new Vector3(2.0f, 0.78f, 0.9f), collider: true);
            Box(room, "AddingMachine", Toon("OfficeSafe", new Color(0.24f, 0.25f, 0.28f)), new Vector3(9.4f, 0.78f, -6.5f), new Vector3(0.36f, 0.14f, 0.3f));
            // A stack of unpaid invoices (spec §5).
            for (var i = 0; i < 6; i++)
                Box(room, "Invoice", Toon("OfficePaper", Cream), new Vector3(8.2f + i * 0.01f, 0.78f + i * 0.025f, -6.45f), new Vector3(0.34f, 0.02f, 0.26f));
            Person(room, "Accountant", new Vector3(8.8f, 0f, -5.6f), 180f, new Color(0.40f, 0.38f, 0.30f), 0.95f);
            Box(room, "VisitorChair", Toon("OfficeGuestChair", new Color(0.55f, 0.17f, 0.17f)), new Vector3(8.8f, 0f, -7.6f), new Vector3(0.6f, 0.45f, 0.6f), collider: true);
            for (var i = 0; i < 4; i++)
            {
                Box(room, "FilingCabinet", Toon("OfficeCabinetGrey", new Color(0.46f, 0.46f, 0.42f)), new Vector3(9.6f + i * 0.6f, 0f, -3.45f), new Vector3(0.55f, 1.3f, 0.6f), collider: true);
                Box(room, "Drawer", Toon("OfficeBrass", Brass), new Vector3(9.6f + i * 0.6f, 0.9f, -3.76f), new Vector3(0.2f, 0.04f, 0.02f));
            }
            PottedPlant(room, new Vector3(11.4f, 0f, -8.4f), 1.1f);
        }

        private static void BuildTerrace(Transform rooms)
        {
            var room = Room(rooms, "Terrace");
            var cushion = Toon("OfficeCushion", new Color(0.45f, 0.55f, 0.75f));
            foreach (var x in new[] { -10.6f, -9.2f, -7.8f })
            {
                Box(room, "Lounger", Toon("OfficeTrim", WoodDark), new Vector3(x, 0f, 12.6f), new Vector3(0.75f, 0.3f, 1.8f), collider: true);
                Box(room, "Cushion", cushion, new Vector3(x, 0.3f, 12.4f), new Vector3(0.68f, 0.1f, 1.4f));
                var back = Box(room, "Back", cushion, new Vector3(x, 0.3f, 13.3f), new Vector3(0.68f, 0.5f, 0.12f));
                back.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }
            Shape(room, PrimitiveType.Cylinder, "UmbrellaPole", Toon("OfficeTrim", WoodDark), new Vector3(-6.4f, 1.1f, 12.2f), new Vector3(0.06f, 1.1f, 0.06f));
            Shape(room, PrimitiveType.Cylinder, "UmbrellaCanopy", Toon("OfficeUmbrella", new Color(0.16f, 0.34f, 0.28f)), new Vector3(-6.4f, 2.25f, 12.2f), new Vector3(2.6f, 0.08f, 2.6f));
            foreach (var x in new[] { -3f, 0f, 3f })
                Conifer(room, new Vector3(x, 0f, 13.4f));

            // The helipad, with the boss's helicopter tied down on it (the ending takes it away).
            var pad = new Vector3(7.6f, 0f, 11.5f);
            Shape(room, PrimitiveType.Cylinder, "Helipad", Toon("OfficeHelipad", new Color(0.22f, 0.23f, 0.27f)), pad + Vector3.up * 0.02f, new Vector3(4.4f, 0.02f, 4.4f));
            Shape(room, PrimitiveType.Cylinder, "HelipadRing", Toon("OfficeHelipadPaint", new Color(0.95f, 0.78f, 0.28f)), pad + Vector3.up * 0.015f, new Vector3(4.6f, 0.015f, 4.6f));
            var paint = Toon("OfficeHelipadPaint", new Color(0.95f, 0.78f, 0.28f));
            Box(room, "H_Left", paint, pad + new Vector3(-0.5f, 0.045f, 0f), new Vector3(0.2f, 0.01f, 1.4f));
            Box(room, "H_Right", paint, pad + new Vector3(0.5f, 0.045f, 0f), new Vector3(0.2f, 0.01f, 1.4f));
            Box(room, "H_Bar", paint, pad + new Vector3(0f, 0.045f, 0f), new Vector3(1.0f, 0.01f, 0.2f));
            var heli = new GameObject("Helicopter").transform;
            heli.SetParent(room, false);
            heli.localPosition = pad + new Vector3(0.3f, 0f, 0.4f);
            heli.localRotation = Quaternion.Euler(0f, -20f, 0f);
            var body = Toon("OfficeHeli", new Color(0.18f, 0.20f, 0.24f));
            Shape(heli, PrimitiveType.Capsule, "Body", body, new Vector3(0f, 0.9f, 0f), new Vector3(1.1f, 1.0f, 1.1f), Quaternion.Euler(0f, 0f, 90f), collider: true);
            Shape(heli, PrimitiveType.Sphere, "Canopy", PilotArt.EnsureGlassMaterial("OfficeHeliGlass", new Color(0.5f, 0.7f, 0.9f, 0.6f)), new Vector3(-0.7f, 1.05f, 0f), new Vector3(0.8f, 0.7f, 0.85f));
            Box(heli, "Tail", body, new Vector3(1.9f, 1.0f, 0f), new Vector3(2.2f, 0.18f, 0.18f));
            Box(heli, "TailFin", Toon("OfficeBrass", Brass), new Vector3(2.95f, 1.0f, 0f), new Vector3(0.3f, 0.5f, 0.06f));
            foreach (var z in new[] { -0.45f, 0.45f })
                Box(heli, "Skid", Toon("OfficeBrass", Brass), new Vector3(0f, 0f, z), new Vector3(2.0f, 0.06f, 0.08f));
            Box(heli, "RotorA", Toon("OfficeInk", Ink), new Vector3(0f, 1.62f, 0f), new Vector3(4.2f, 0.04f, 0.18f));
            Box(heli, "RotorB", Toon("OfficeInk", Ink), new Vector3(0f, 1.62f, 0f), new Vector3(0.18f, 0.04f, 4.2f));
        }

        // ---- The city below -----------------------------------------------------------------------

        // The boss's tower under the floor, and the city around and below it, so no void shows at
        // any framing (art bible). Windows face the camera (south) only.
        private static void BuildCity()
        {
            var city = new GameObject("City").transform;
            var lit = Unlit("OfficeWindowLit", WindowLit);
            var darkWindow = Unlit("OfficeWindowDark", WindowDark);

            var tower = new GameObject("BossTower").transform;
            tower.SetParent(city, false);
            const float towerHeight = 160f;
            Box(tower, "Body", Toon("OfficeFacade", Facade), new Vector3(0f, -towerHeight, 2.5f), new Vector3(25.2f, towerHeight - 0.8f, 24.2f));
            Box(tower, "Cornice", Toon("OfficeBrass", Brass), new Vector3(0f, -1.2f, 2.5f), new Vector3(25.6f, 0.4f, 24.6f));
            Box(tower, "CorniceDark", Toon("OfficeTrim", WoodDark), new Vector3(0f, -0.8f, 2.5f), new Vector3(25.4f, 0.4f, 24.4f));
            for (var y = -4f; y > -32f; y -= 3.2f) // the rows the camera can see; the rest is dark façade
            for (var x = -11f; x <= 11f; x += 2.2f)
                Shape(tower, PrimitiveType.Quad, "Window", _random.NextDouble() < 0.45 ? lit : darkWindow,
                    new Vector3(x, y, -9.63f), new Vector3(1.2f, 1.8f, 1f), Quaternion.identity);

            // Neighbouring towers: their roofs well below this one in front and to the sides, and
            // taller ones far behind, for a skyline at the top of the frame.
            var others = new GameObject("Towers").transform;
            others.SetParent(city, false);
            for (var i = 0; i < 50; i++)
            {
                var angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                var distance = 26f + (float)_random.NextDouble() * 70f;
                var at = new Vector3(Mathf.Cos(angle) * distance, 0f, 2.5f + Mathf.Sin(angle) * distance);
                var behind = at.z > 40f;
                var top = behind ? -25f + (float)_random.NextDouble() * 55f : -50f + (float)_random.NextDouble() * 38f;
                var width = 8f + (float)_random.NextDouble() * 10f;
                var depth = 8f + (float)_random.NextDouble() * 10f;
                var colour = TowerColors[_random.Next(TowerColors.Length)];
                var building = new GameObject("Tower").transform;
                building.SetParent(others, false);
                building.localPosition = at;
                Box(building, "Body", Toon($"OfficeTower{System.Array.IndexOf(TowerColors, colour)}", colour), new Vector3(0f, -towerHeight, 0f), new Vector3(width, top + towerHeight, depth));
                Box(building, "Roof", Toon("OfficeRoof", new Color(0.12f, 0.12f, 0.18f)), new Vector3(0f, top, 0f), new Vector3(width + 0.4f, 0.4f, depth + 0.4f));
                for (var y = top - 2.5f; y > top - 16f; y -= 3.2f)
                for (var x = -width / 2f + 1.2f; x < width / 2f - 0.8f; x += 2.2f)
                    Shape(building, PrimitiveType.Quad, "Window", _random.NextDouble() < 0.35 ? lit : darkWindow,
                        new Vector3(x + 0.4f, y, -depth / 2f - 0.02f), new Vector3(1.1f, 1.6f, 1f), Quaternion.identity);
            }
        }

        // ---- Props ---------------------------------------------------------------------------------

        private static void Person(Transform parent, string name, Vector3 at, float facing, Color clothes, float scale)
        {
            var person = new GameObject(name).transform;
            person.SetParent(parent, false);
            person.localPosition = at;
            person.localRotation = Quaternion.Euler(0f, facing, 0f);
            person.localScale = Vector3.one * scale;
            // The chef's proportions (0.7 × 1.7 m), so everyone reads at the same size.
            Shape(person, PrimitiveType.Capsule, "Body", Toon($"OfficeClothes_{name}", clothes), new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.75f, 0.7f), collider: true);
            Shape(person, PrimitiveType.Sphere, "Head", Toon("OfficeSkin", Skin), new Vector3(0f, 1.62f, 0f), Vector3.one * 0.5f);
            Box(person, "Nose", Toon("OfficeSkin", Skin), new Vector3(0f, 1.58f, 0.25f), new Vector3(0.1f, 0.1f, 0.1f));
        }

        private static void Bodyguard(Transform parent, string name, Vector3 at)
        {
            Person(parent, name, at, 180f, Suit, 1.12f);
            var guard = parent.Find(name);
            Box(guard, "Sunglasses", Toon("OfficeInk", Ink), new Vector3(0f, 1.62f, 0.2f), new Vector3(0.4f, 0.08f, 0.12f));
            Box(guard, "Tie", Toon("OfficeBellRed", new Color(0.8f, 0.2f, 0.18f)), new Vector3(0f, 1.05f, 0.34f), new Vector3(0.1f, 0.4f, 0.04f));
        }

        // The boss: a big man in a camel suit and a white cowboy hat, at his desk.
        private static void Boss(Transform parent, Vector3 at)
        {
            Person(parent, "Boss", at, 180f, new Color(0.72f, 0.52f, 0.30f), 1.25f);
            var boss = parent.Find("Boss");
            var hat = Toon("OfficeHat", Cream);
            Shape(boss, PrimitiveType.Cylinder, "HatBrim", hat, new Vector3(0f, 1.82f, 0f), new Vector3(0.85f, 0.02f, 0.85f));
            Shape(boss, PrimitiveType.Cylinder, "HatCrown", hat, new Vector3(0f, 1.95f, 0f), new Vector3(0.42f, 0.13f, 0.42f));
            Box(boss, "HatBand", Toon("OfficeTrim", WoodDark), new Vector3(0f, 1.86f, 0f), new Vector3(0.44f, 0.05f, 0.44f));
            Box(boss, "Bolo", Toon("OfficeBrass", Brass), new Vector3(0f, 1.2f, 0.36f), new Vector3(0.12f, 0.12f, 0.04f));
        }

        private static void PottedPlant(Transform parent, Vector3 at, float scale)
        {
            var plant = new GameObject("Plant").transform;
            plant.SetParent(parent, false);
            plant.localPosition = at;
            plant.localScale = Vector3.one * scale;
            Shape(plant, PrimitiveType.Cylinder, "Pot", Toon("OfficePot", Pot), new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.25f, 0.5f), collider: true);
            Shape(plant, PrimitiveType.Sphere, "Leaves", Toon("OfficePlant", Plant), new Vector3(0f, 0.85f, 0f), new Vector3(0.85f, 0.8f, 0.85f));
        }

        private static void Conifer(Transform parent, Vector3 at)
        {
            var tree = new GameObject("Conifer").transform;
            tree.SetParent(parent, false);
            tree.localPosition = at;
            Box(tree, "Planter", Toon("OfficeTrim", WoodDark), Vector3.zero, new Vector3(0.7f, 0.5f, 0.7f), collider: true);
            for (var i = 0; i < 3; i++)
                Shape(tree, PrimitiveType.Cylinder, "Tier", Toon("OfficeConifer", new Color(0.13f, 0.28f, 0.22f)), new Vector3(0f, 0.8f + i * 0.45f, 0f),
                    new Vector3(0.8f - i * 0.22f, 0.25f, 0.8f - i * 0.22f));
        }

        private static void Counter(Transform parent, string name, Vector3 at, Vector3 size)
        {
            Box(parent, name, Toon("OfficeCabinet", WoodMid), at, size, collider: true);
            Box(parent, name + "Top", Toon("OfficeCounterTop", Cream), at + Vector3.up * size.y, new Vector3(size.x + 0.06f, 0.05f, size.z + 0.06f));
        }

        private static void Mirror(Transform parent, Vector3 at, float facing)
        {
            var mirror = new GameObject("Mirror").transform;
            mirror.SetParent(parent, false);
            mirror.localPosition = at;
            mirror.localRotation = Quaternion.Euler(0f, facing, 0f);
            Box(mirror, "Frame", Toon("OfficeBrass", Brass), Vector3.zero, new Vector3(1.0f, 2.0f, 0.08f), collider: true);
            Box(mirror, "Glass", Unlit("OfficeMirror", new Color(0.72f, 0.82f, 0.88f)), new Vector3(0f, 0.08f, 0.045f), new Vector3(0.86f, 1.84f, 0.01f));
        }

        private static void Sofa(Transform parent, Vector3 at, float facing, float length)
        {
            var sofa = new GameObject("Sofa").transform;
            sofa.SetParent(parent, false);
            sofa.localPosition = at;
            sofa.localRotation = Quaternion.Euler(0f, facing, 0f);
            var fabric = Toon("OfficeSofa", new Color(0.55f, 0.18f, 0.16f));
            Box(sofa, "Seat", fabric, Vector3.zero, new Vector3(length, 0.45f, 0.85f), collider: true);
            Box(sofa, "Back", fabric, new Vector3(0f, 0.45f, -0.33f), new Vector3(length, 0.45f, 0.2f));
            foreach (var side in new[] { -1f, 1f })
                Box(sofa, "Arm", fabric, new Vector3(side * (length / 2f - 0.1f), 0.45f, 0f), new Vector3(0.2f, 0.2f, 0.85f));
        }

        private static void Armchair(Transform parent, Vector3 at)
        {
            var chair = new GameObject("Armchair").transform;
            chair.SetParent(parent, false);
            chair.localPosition = at;
            chair.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var leather = Toon("OfficeArmchair", new Color(0.40f, 0.24f, 0.14f));
            Box(chair, "Seat", leather, Vector3.zero, new Vector3(0.85f, 0.45f, 0.8f), collider: true);
            Box(chair, "Back", leather, new Vector3(0f, 0.45f, -0.3f), new Vector3(0.85f, 0.5f, 0.2f));
        }

        private static void BookRow(Transform parent, Vector3 at, float length)
        {
            var colours = new[] { Burgundy, Leather, new Color(0.32f, 0.42f, 0.55f), Cream, new Color(0.55f, 0.36f, 0.22f) };
            foreach (var y in new[] { 0.25f, 0.8f, 1.3f })
                for (var z = -length / 2f; z < length / 2f - 0.1f; z += 0.16f)
                {
                    var c = _random.Next(colours.Length);
                    Box(parent, "Book", Toon($"OfficeBook{c}", colours[c]), at + new Vector3(0f, y, z + 0.08f), new Vector3(0.05f, 0.3f + (float)_random.NextDouble() * 0.08f, 0.13f));
                }
        }

        private static void Lamp(Transform parent, string name, Vector3 at, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = LampWarm;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // ---- Primitives and materials --------------------------------------------------------------

        // A box standing on `bottom` (the centre of its base).
        private static GameObject Box(Transform parent, string name, Material material, Vector3 bottom, Vector3 size, bool collider = false)
        {
            var box = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: collider);
            box.transform.localPosition = bottom + Vector3.up * (size.y / 2f);
            box.transform.localScale = size;
            box.isStatic = true;
            return box;
        }

        private static GameObject Shape(Transform parent, PrimitiveType type, string name, Material material, Vector3 position, Vector3 scale,
            Quaternion? rotation = null, bool collider = false)
        {
            var shape = GreyboxAssets.CreatePrimitive(type, name, parent, material, keepCollider: collider);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            if (rotation.HasValue)
                shape.transform.localRotation = rotation.Value;
            shape.isStatic = true;
            return shape;
        }

        private static Material Toon(string name, Color color) => StyleTest.EnsureToonMaterial(name, color);

        // Wood with the toon shader's grain, a shade darker than the wood.
        private static Material Wood(string name, Color color) => StyleTest.EnsureToonMaterial(name, color, color * 0.82f);

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
