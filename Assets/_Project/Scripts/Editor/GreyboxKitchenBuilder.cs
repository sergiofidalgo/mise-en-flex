using System.Linq;
using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Editor
{
    /// <summary>
    /// Builds the Phase 0 greybox content from code: settings assets, materials, item and
    /// player prefabs, and the kitchen scene. Re-run it after changing this script; it
    /// overwrites prefabs, the item catalog's entries and the scene, but keeps existing
    /// settings assets and materials (that's where tuning lives).
    /// </summary>
    public static class GreyboxKitchenBuilder
    {
        private const string ControlsPath = "Assets/_Project/Data/Input/GameControls.inputactions";
        private const string SettingsFolder = "Assets/_Project/Data/Settings";
        private const string CoopSettingsPath = SettingsFolder + "/CoopSettings.asset";
        private const string TuningConfigPath = SettingsFolder + "/TuningConfig.asset";
        private const string EquipmentPath = SettingsFolder + "/EquipmentTier_Basic.asset";
        private const string RealismPath = SettingsFolder + "/RealismSettings.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string JoinActionPath = "Gameplay/Join";

        // Greybox layout, in metres. The kitchen is 10 x 8 with the camera looking north (+Z).
        private const float FloorWidth = 10f;
        private const float FloorDepth = 8f;
        private static readonly Vector3[] SpawnPositions = { new Vector3(-2.5f, 0f, -0.8f), new Vector3(2.5f, 0f, -0.8f) };
        private const float WallHeight = 1f;
        private const float WallThickness = 0.2f;

        // Fixed overhead camera (decision 2026-11-02): high and tilted so the whole kitchen
        // and both players stay in view.
        private static readonly Vector3 CameraPosition = new Vector3(0f, 11f, -8f);
        private static readonly Vector3 CameraEuler = new Vector3(55f, 0f, 0f);
        private const float CameraFieldOfView = 45f;

        // Placeholder chef proportions: ~1.7 m tall, origin at the feet.
        private static readonly Vector3 BodyScale = new Vector3(0.7f, 0.85f, 0.7f);
        private static readonly Vector3 NoseScale = new Vector3(0.2f, 0.2f, 0.3f);
        private const float CollisionRadius = 0.35f;

        // The kitchen floor is flat, so chefs never need to step up or climb. Unity's defaults
        // (0.3 m step, 45° slopes) let the capsule's rounded bottom ride up onto crates.
        private const float StepOffset = 0.02f;
        private const float SlopeLimit = 20f;

        // Station colliders are taller than a chef, whatever the visual height, so the capsule
        // only ever touches their vertical sides and can't climb on top.
        private const float StationBlockerHeight = 2.2f;

        // Carried items sit in front of the chest, clear of the body so they read from above.
        private static readonly Vector3 CarryAnchorPosition = new Vector3(0f, 1.05f, 0.5f);
        // The knife sits at the chef's right side, so it reads separately from what's carried.
        private static readonly Vector3 ToolAnchorPosition = new Vector3(0.4f, 0.95f, 0.15f);

        // Prep zone on the back wall, left: knife rack, mise en place counter, a plain counter.
        private static readonly Color PrepColor = new Color(0.78f, 0.64f, 0.45f);
        private static readonly Vector3 KnifeRackPosition = new Vector3(-3f, 0f, 3.4f);
        private static readonly Vector3 PrepCounterPosition = new Vector3(-2f, 0f, 3.4f);
        private static readonly Vector3 PrepSpareCounterPosition = new Vector3(-1f, 0f, 3.4f);
        private static readonly Vector3 CuttingBoardScale = new Vector3(0.34f, 0.025f, 0.28f);
        private static readonly Color CuttingBoardColor = new Color(0.92f, 0.82f, 0.62f);
        private const float PrepBinOffset = 0.33f;
        private static readonly Vector3 PrepBinScale = new Vector3(0.2f, 0.03f, 0.2f);

        // Stations: standard 0.9 m counter height, 1 m footprint.
        private const float CounterHeight = 0.9f;
        private const float CounterSize = 1f;

        // Storage on the west wall (replaces the Phase 0 crates): a fridge for meat and a shelf
        // for vegetables. Slots in FaceButton order: A, B, X, Y. Capacity and starting stock are
        // placeholders until restocking (Phase 3) and buying storage (Phases 4–5).
        private static readonly GreyboxStorage.Unit[] StorageUnits =
        {
            new GreyboxStorage.Unit
            {
                Name = "Fridge", Position = new Vector3(-4.4f, 0f, 1.0f), Refrigerated = true,
                Color = new Color(0.86f, 0.92f, 0.96f),
                Slots = new (string, int, int)?[] { ("Steak", 6, 6), null, null, null },
            },
            new GreyboxStorage.Unit
            {
                Name = "Shelf", Position = new Vector3(-4.4f, 0f, -0.6f), Refrigerated = false,
                Color = new Color(0.58f, 0.43f, 0.28f),
                Slots = new (string, int, int)?[] { ("Tomato", 6, 6), ("Onion", 6, 6), null, null },
            },
        };

        // Kitchen zones (decision 2026-11-02), laid out so food flows storage -> prep -> cooking
        // -> pass and the path crosses the middle, where the island is the hand-off point.
        // Pass and bin are plain counters until later milestones give them real behaviour; each
        // zone has its own colour so it reads from the overhead camera.
        private static readonly (string Name, Color Color, float Size, float Height, Vector3[] Positions)[] CounterZones =
        {
            ("Island", new Color(0.70f, 0.58f, 0.42f), CounterSize, CounterHeight,
                new[] { new Vector3(-0.5f, 0f, 0.8f), new Vector3(0.5f, 0f, 0.8f) }),
        };

        // The pass along the front, facing the dining side: plates put here are served and scored.
        private static readonly Vector3[] PassPositions = { new Vector3(-1f, 0f, -2.4f), new Vector3(0f, 0f, -2.4f), new Vector3(1f, 0f, -2.4f) };
        private static readonly Color PassColor = new Color(0.88f, 0.88f, 0.85f);

        // Strip in front of the pass marking where the dining room will be.
        private const float DiningStripDepth = 1f;
        private static readonly Color DiningColor = new Color(0.55f, 0.45f, 0.40f);

        // Hobs along the back wall, right of prep. Each has a burner ring and its own pan.
        private static readonly Vector3[] HobPositions = { new Vector3(1f, 0f, 3.4f), new Vector3(2f, 0f, 3.4f), new Vector3(3f, 0f, 3.4f) };
        private static readonly Color HobColor = new Color(0.22f, 0.22f, 0.25f);
        // Wider than the pan (0.44 m), so the glowing ring stays visible around it.
        private const float BurnerDiameter = 0.62f;
        // Heat pips on the hob top along the back edge: one per step above off. On top rather
        // than on the front face, which the chef standing at the hob hides from the camera.
        private const int HeatPipCount = 3;
        private static readonly Vector3 HeatPipScale = new Vector3(0.09f, 0.015f, 0.06f);
        private const float HeatPipSpacing = 0.14f;
        private const float HeatPipBackOffset = 0.42f;

        // Doneness chip floating above the pan while a player faces the hob.
        private const float ChipSize = 0.12f;
        private const float ChipHeightAbovePan = 0.4f;

        // Plate stack next to the pass, and the bin on the east wall.
        private static readonly Vector3 PlateStackPosition = new Vector3(2.2f, 0f, -2.4f);
        private static readonly Color PlateStackColor = new Color(0.80f, 0.80f, 0.78f);
        private const int PlateStackVisualCount = 4;
        private static readonly Vector3 BinPosition = new Vector3(4.5f, 0f, 1f);
        private static readonly Color BinColor = new Color(0.25f, 0.55f, 0.30f);
        private const float BinSize = 0.7f;
        private const float BinHeight = 0.8f;

        // Greybox palette: darker walls than floor so edges read from the overhead camera.
        private static readonly Color FloorColor = new Color(0.62f, 0.63f, 0.66f);
        private static readonly Color WallColor = new Color(0.30f, 0.33f, 0.40f);
        private static readonly Color NoseColor = new Color(0.20f, 0.20f, 0.22f);

        [MenuItem("Tools/Game/Build Greybox Kitchen")]
        public static void BuildAll()
        {
            // Create the scene before loading any assets: opening a new scene unloads assets
            // nothing references yet, which would leave the references below dead and saved
            // into the scene as empty.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var assets = new SceneAssets
            {
                Settings = GreyboxAssets.EnsureAsset<CoopSettings>(CoopSettingsPath),
                Tuning = GreyboxAssets.EnsureAsset<TuningConfig>(TuningConfigPath),
                Equipment = GreyboxAssets.EnsureAsset<EquipmentTier>(EquipmentPath),
                Realism = GreyboxAssets.EnsureAsset<RealismSettings>(RealismPath),
                Catalog = GreyboxIngredients.BuildCatalog(
                    (GreyboxKitchenware.PanTypeId, GreyboxKitchenware.BuildPan()),
                    (Game.Simulation.Items.Plate.PlateTypeId, GreyboxKitchenware.BuildPlate()),
                    (Game.Simulation.Items.Knife.KnifeTypeId, GreyboxKitchenware.BuildKnife())),
            };
            assets.PlayerPrefab = BuildPlayerPrefab(assets.Settings, assets.Tuning);
            BuildScene(scene, assets);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxKitchenBuilder] Built {PlayerPrefabPath}, ingredients and {ScenePath}.");
        }

        private sealed class SceneAssets
        {
            public CoopSettings Settings;
            public TuningConfig Tuning;
            public EquipmentTier Equipment;
            public RealismSettings Realism;
            public ItemCatalog Catalog;
            public GameObject PlayerPrefab;
        }

        private static GameObject BuildPlayerPrefab(CoopSettings settings, TuningConfig tuning)
        {
            var root = new GameObject("PF_Player");
            var playerInput = root.AddComponent<PlayerInput>();
            playerInput.actions = LoadControls();
            playerInput.defaultActionMap = "Gameplay";
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            var controller = root.AddComponent<CharacterController>();
            controller.height = BodyScale.y * 2f;
            controller.radius = CollisionRadius;
            controller.center = new Vector3(0f, BodyScale.y, 0f);
            controller.stepOffset = StepOffset;
            controller.slopeLimit = SlopeLimit;

            var movement = root.AddComponent<PlayerMovement>();
            SetReference(movement, "_tuning", tuning);

            var carryAnchor = new GameObject("CarryAnchor").transform;
            carryAnchor.SetParent(root.transform, false);
            carryAnchor.localPosition = CarryAnchorPosition;
            var toolAnchor = new GameObject("ToolAnchor").transform;
            toolAnchor.SetParent(root.transform, false);
            toolAnchor.localPosition = ToolAnchorPosition;
            var interactor = root.AddComponent<PlayerInteractor>();
            SetReference(interactor, "_carryAnchor", carryAnchor);
            SetReference(interactor, "_toolAnchor", toolAnchor);

            // Visuals live under one child so the join pop scales them without moving the root.
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);

            var body = GreyboxAssets.CreatePrimitive(PrimitiveType.Capsule, "Body", visuals, null, keepCollider: false);
            body.transform.localScale = BodyScale;
            body.transform.localPosition = new Vector3(0f, BodyScale.y, 0f); // capsule is 2 units tall

            // A nose shows facing direction from the overhead camera.
            var nose = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Nose", visuals, GreyboxAssets.EnsureMaterial("Nose", NoseColor), keepCollider: false);
            nose.transform.localScale = NoseScale;
            nose.transform.localPosition = new Vector3(0f, BodyScale.y * 1.5f, BodyScale.z * 0.5f);

            var visual = root.AddComponent<PlayerVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_playerInput").objectReferenceValue = playerInput;
            so.FindProperty("_popTarget").objectReferenceValue = visuals;
            var renderers = so.FindProperty("_tintedRenderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildScene(UnityEngine.SceneManagement.Scene scene, SceneAssets assets)
        {
            var settings = assets.Settings;
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));
            camera.fieldOfView = CameraFieldOfView;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(FloorWidth / 10f, 1f, FloorDepth / 10f); // plane is 10x10
            floor.GetComponent<Renderer>().sharedMaterial = GreyboxAssets.EnsureMaterial("Floor", FloorColor);
            BuildDiningStrip();
            BuildBoundaryWalls();
            BuildStations(settings);

            var spawnRoot = new GameObject("SpawnPoints").transform;
            var spawnPoints = SpawnPositions.Select((position, i) =>
            {
                var point = new GameObject($"Spawn_P{i + 1}").transform;
                point.SetParent(spawnRoot, false);
                point.localPosition = position;
                return point;
            }).ToArray();

            var players = new GameObject("Players");
            var manager = players.AddComponent<PlayerInputManager>();
            manager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenJoinActionIsTriggered;
            manager.joinAction = new InputActionProperty(LoadActionReference(JoinActionPath));
            manager.playerPrefab = assets.PlayerPrefab;

            var join = players.AddComponent<PlayerJoinController>();
            var joinSo = new SerializedObject(join);
            joinSo.FindProperty("_settings").objectReferenceValue = settings;
            var points = joinSo.FindProperty("_spawnPoints");
            points.arraySize = spawnPoints.Length;
            for (var i = 0; i < spawnPoints.Length; i++)
                points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            joinSo.ApplyModifiedPropertiesWithoutUndo();

            var root = new GameObject("CompositionRoot").AddComponent<KitchenCompositionRoot>();
            var rootSo = new SerializedObject(root);
            rootSo.FindProperty("_itemCatalog").objectReferenceValue = assets.Catalog;
            rootSo.FindProperty("_tuning").objectReferenceValue = assets.Tuning;
            rootSo.FindProperty("_equipment").objectReferenceValue = assets.Equipment;
            rootSo.FindProperty("_realism").objectReferenceValue = assets.Realism;
            rootSo.FindProperty("_playerInputManager").objectReferenceValue = manager;
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            var overlay = root.gameObject.AddComponent<CookingDebugOverlay>();
            SetReference(overlay, "_root", root);
            var prompts = root.gameObject.AddComponent<StationPromptOverlay>();
            SetReference(prompts, "_settings", settings);
            root.gameObject.AddComponent<QualityCardOverlay>();
            root.gameObject.AddComponent<ChopRhythmOverlay>();
            foreach (var chip in Object.FindObjectsByType<DonenessChipView>(FindObjectsSortMode.None))
                SetReference(chip, "_root", root);

            GreyboxAssets.EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // A different floor colour along the south wall, where the dining room will be.
        // Walkable and without a collider; a few millimetres up to avoid z-fighting with the floor.
        private static void BuildDiningStrip()
        {
            const float lift = 0.005f;
            var strip = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "DiningStrip", null, GreyboxAssets.EnsureMaterial("Dining", DiningColor), keepCollider: false);
            strip.transform.localScale = new Vector3(FloorWidth, lift, DiningStripDepth);
            strip.transform.localPosition = new Vector3(0f, lift / 2f, -FloorDepth / 2f + WallThickness / 2f + DiningStripDepth / 2f);
        }

        // Low walls around the floor so players can't walk off the edge.
        private static void BuildBoundaryWalls()
        {
            var walls = new GameObject("Walls").transform;
            var material = GreyboxAssets.EnsureMaterial("Wall", WallColor);
            var halfWidth = FloorWidth / 2f;
            var halfDepth = FloorDepth / 2f;
            var y = WallHeight / 2f;

            CreateWall("Wall_North", new Vector3(0f, y, halfDepth), new Vector3(FloorWidth, WallHeight, WallThickness));
            CreateWall("Wall_South", new Vector3(0f, y, -halfDepth), new Vector3(FloorWidth, WallHeight, WallThickness));
            CreateWall("Wall_East", new Vector3(halfWidth, y, 0f), new Vector3(WallThickness, WallHeight, FloorDepth));
            CreateWall("Wall_West", new Vector3(-halfWidth, y, 0f), new Vector3(WallThickness, WallHeight, FloorDepth));

            void CreateWall(string name, Vector3 position, Vector3 scale)
            {
                var wall = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, walls, material, keepCollider: true);
                wall.transform.localPosition = position;
                wall.transform.localScale = scale;
            }
        }

        private static void BuildStations(CoopSettings settings)
        {
            var stations = new GameObject("Stations").transform;

            foreach (var zone in CounterZones)
            {
                var zoneRoot = new GameObject($"Zone_{zone.Name}").transform;
                zoneRoot.SetParent(stations, false);
                var material = GreyboxAssets.EnsureMaterial(zone.Name, zone.Color);
                for (var i = 0; i < zone.Positions.Length; i++)
                {
                    CreateStation<CounterStation>(
                        $"{zone.Name}_{i + 1:00}", zone.Positions[i], zone.Size, zone.Height, material, zoneRoot, settings);
                }
            }

            BuildPrepZone(stations, settings);

            var hobs = new GameObject("Zone_Hob").transform;
            hobs.SetParent(stations, false);
            var hobMaterial = GreyboxAssets.EnsureMaterial("Hob", HobColor);
            for (var i = 0; i < HobPositions.Length; i++)
                CreateHob($"Hob_{i + 1:00}", HobPositions[i], hobMaterial, hobs, settings);

            var passes = new GameObject("Zone_Pass").transform;
            passes.SetParent(stations, false);
            var passMaterial = GreyboxAssets.EnsureMaterial("Pass", PassColor);
            for (var i = 0; i < PassPositions.Length; i++)
                CreateStation<PassStation>($"Pass_{i + 1:00}", PassPositions[i], CounterSize, CounterHeight, passMaterial, passes, settings);

            var plateStack = CreateStation<PlateStackStation>(
                "PlateStack", PlateStackPosition, CounterSize, CounterHeight,
                GreyboxAssets.EnsureMaterial("PlateStack", PlateStackColor), stations, settings);
            GreyboxKitchenware.CreatePlateStackVisual(plateStack.transform, CounterHeight, PlateStackVisualCount);

            CreateStation<BinStation>(
                "Bin", BinPosition, BinSize, BinHeight, GreyboxAssets.EnsureMaterial("Bin", BinColor), stations, settings);

            var storage = new GameObject("Zone_Storage").transform;
            storage.SetParent(stations, false);
            foreach (var unit in StorageUnits)
            {
                GreyboxStorage.Build(unit, CounterHeight, (name, position, material) =>
                    CreateStation<StorageStation>(name, position, CounterSize, CounterHeight, material, storage, settings));
            }
        }

        private static void BuildPrepZone(Transform stations, CoopSettings settings)
        {
            var zone = new GameObject("Zone_Prep").transform;
            zone.SetParent(stations, false);
            var material = GreyboxAssets.EnsureMaterial("Prep", PrepColor);

            CreateStation<KnifeRackStation>("KnifeRack", KnifeRackPosition, CounterSize, CounterHeight, material, zone, settings);
            CreateStation<CounterStation>("Prep_Counter", PrepSpareCounterPosition, CounterSize, CounterHeight, material, zone, settings);

            var prep = CreateStation<PrepStation>("MiseEnPlace", PrepCounterPosition, CounterSize, CounterHeight, material, zone, settings);
            var board = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "CuttingBoard", prep.transform, GreyboxAssets.EnsureMaterial("CuttingBoard", CuttingBoardColor), keepCollider: false);
            board.transform.localScale = CuttingBoardScale;
            board.transform.localPosition = new Vector3(0f, CounterHeight + CuttingBoardScale.y / 2f, 0f);
            var boardAnchor = new GameObject("BoardAnchor").transform;
            boardAnchor.SetParent(prep.transform, false);
            boardAnchor.localPosition = new Vector3(0f, CounterHeight + CuttingBoardScale.y, 0f);

            // Slot bins in FaceButton order, in the controller's diamond as seen from the camera.
            var offsets = new[]
            {
                new Vector3(0f, 0f, -PrepBinOffset), new Vector3(PrepBinOffset, 0f, 0f),
                new Vector3(-PrepBinOffset, 0f, 0f), new Vector3(0f, 0f, PrepBinOffset),
            };
            var binMaterial = GreyboxAssets.EnsureMaterial("StorageBin", new Color(0.55f, 0.57f, 0.60f));
            var so = new SerializedObject(prep);
            so.FindProperty("_boardAnchor").objectReferenceValue = boardAnchor;
            var slots = so.FindProperty("_slotAnchors");
            slots.arraySize = offsets.Length;
            for (var i = 0; i < offsets.Length; i++)
            {
                var bin = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, $"Bin_{(FaceButton)i}", prep.transform, binMaterial, keepCollider: false);
                bin.transform.localScale = PrepBinScale;
                bin.transform.localPosition = offsets[i] + Vector3.up * (CounterHeight + PrepBinScale.y / 2f);
                var anchor = new GameObject($"SlotAnchor_{(FaceButton)i}").transform;
                anchor.SetParent(prep.transform, false);
                anchor.localPosition = offsets[i] + Vector3.up * (CounterHeight + PrepBinScale.y);
                slots.GetArrayElementAtIndex(i).objectReferenceValue = anchor;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A hob: counter body, a burner ring that glows with the dial around the pan, and heat
        // pips along the back of the top.
        // Its pan is a separate, carryable item created at runtime.
        private static void CreateHob(string name, Vector3 position, Material material, Transform parent, CoopSettings settings)
        {
            var hob = CreateStation<HobStation>(name, position, CounterSize, CounterHeight, material, parent, settings);
            var top = CounterHeight;

            var burner = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cylinder, "Burner", hob.transform, GreyboxAssets.EnsureMaterial("Burner", Color.black), keepCollider: false);
            burner.transform.localScale = new Vector3(BurnerDiameter, 0.005f, BurnerDiameter); // cylinder is 2 units tall
            burner.transform.localPosition = new Vector3(0f, top + 0.005f, 0f);
            hob.ItemAnchor.localPosition = new Vector3(0f, top + 0.01f, 0f);

            var pips = new Renderer[HeatPipCount];
            for (var i = 0; i < HeatPipCount; i++)
            {
                var pip = GreyboxAssets.CreatePrimitive(
                    PrimitiveType.Cube, $"HeatPip_{i + 1}", hob.transform, GreyboxAssets.EnsureMaterial("HeatPip", Color.black), keepCollider: false);
                pip.transform.localScale = HeatPipScale;
                pip.transform.localPosition = new Vector3(
                    (i - (HeatPipCount - 1) / 2f) * HeatPipSpacing, top + HeatPipScale.y / 2f, HeatPipBackOffset);
                pips[i] = pip.GetComponent<Renderer>();
            }

            var chip = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Sphere, "DonenessChip", hob.transform, GreyboxAssets.EnsureMaterial("Chip", Color.white), keepCollider: false);
            chip.transform.localScale = Vector3.one * ChipSize;
            chip.transform.localPosition = new Vector3(0f, top + ChipHeightAbovePan, 0f);
            var chipView = hob.gameObject.AddComponent<DonenessChipView>();
            var chipSo = new SerializedObject(chipView);
            chipSo.FindProperty("_hob").objectReferenceValue = hob;
            chipSo.FindProperty("_chip").objectReferenceValue = chip.GetComponent<Renderer>();
            chipSo.ApplyModifiedPropertiesWithoutUndo(); // _root is set once the composition root exists

            var view = hob.gameObject.AddComponent<HobView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_hob").objectReferenceValue = hob;
            viewSo.FindProperty("_burner").objectReferenceValue = burner.GetComponent<Renderer>();
            var pipList = viewSo.FindProperty("_pips");
            pipList.arraySize = pips.Length;
            for (var i = 0; i < pips.Length; i++)
                pipList.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            viewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T CreateStation<T>(
            string name, Vector3 position, float size, float height, Material material, Transform parent, CoopSettings settings)
            where T : Interactable
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            var body = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Body", root, material, keepCollider: false);
            body.transform.localScale = new Vector3(size, height, size);
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);

            var blocker = root.gameObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(size, StationBlockerHeight, size);
            blocker.center = new Vector3(0f, StationBlockerHeight / 2f, 0f);

            var anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, height, 0f);

            var station = root.gameObject.AddComponent<T>();
            SetReference(station, "_itemAnchor", anchor);

            var highlight = root.gameObject.AddComponent<InteractableHighlightView>();
            var so = new SerializedObject(highlight);
            so.FindProperty("_interactable").objectReferenceValue = station;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_shakeTarget").objectReferenceValue = body.transform;
            var renderers = so.FindProperty("_renderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return station;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputActionAsset LoadControls()
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            if (controls == null)
                throw new System.InvalidOperationException($"Input actions not found at {ControlsPath}.");
            return controls;
        }

        // The importer stores an InputActionReference sub-asset per action; using it keeps
        // the scene's join action pointing at the asset instead of a serialized copy.
        private static InputActionReference LoadActionReference(string mapAndAction)
        {
            var action = LoadControls().FindAction(mapAndAction, throwIfNotFound: true);
            var reference = AssetDatabase.LoadAllAssetsAtPath(ControlsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.id == action.id);
            if (reference == null)
                throw new System.InvalidOperationException($"No InputActionReference for {mapAndAction}.");
            return reference;
        }
    }
}
