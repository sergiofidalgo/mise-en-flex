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
        private const string ItemCatalogPath = "Assets/_Project/Data/Items/ItemCatalog.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        private const string ItemPrefabFolder = "Assets/_Project/Prefabs/Items";
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

        // Carried items sit in front of the chest, clear of the body so they read from above.
        private static readonly Vector3 CarryAnchorPosition = new Vector3(0f, 1.05f, 0.5f);

        // Stations: standard 0.9 m counter height, 1 m footprint. Crates are lower.
        private const float CounterHeight = 0.9f;
        private const float CounterSize = 1f;
        private const float CrateHeight = 0.6f;
        private const float CrateSize = 0.8f;

        // Kitchen zones (decision 2026-11-02), laid out so food flows storage -> prep -> cooking
        // -> pass and the path crosses the middle, where the island is the hand-off point.
        // Hob, pass and bin are plain counters until Phase 1 gives them real behaviour; each
        // zone has its own colour so it reads from the overhead camera.
        private static readonly (string Name, Color Color, float Size, float Height, Vector3[] Positions)[] CounterZones =
        {
            ("Prep", new Color(0.78f, 0.64f, 0.45f), CounterSize, CounterHeight,
                new[] { new Vector3(-3f, 0f, 3.4f), new Vector3(-2f, 0f, 3.4f), new Vector3(-1f, 0f, 3.4f) }),
            ("Hob", new Color(0.22f, 0.22f, 0.25f), CounterSize, CounterHeight,
                new[] { new Vector3(1f, 0f, 3.4f), new Vector3(2f, 0f, 3.4f), new Vector3(3f, 0f, 3.4f) }),
            ("Island", new Color(0.70f, 0.58f, 0.42f), CounterSize, CounterHeight,
                new[] { new Vector3(-0.5f, 0f, 0.8f), new Vector3(0.5f, 0f, 0.8f) }),
            ("Pass", new Color(0.88f, 0.88f, 0.85f), CounterSize, CounterHeight,
                new[] { new Vector3(-1f, 0f, -2.4f), new Vector3(0f, 0f, -2.4f), new Vector3(1f, 0f, -2.4f) }),
            ("Bin", new Color(0.25f, 0.55f, 0.30f), 0.7f, 0.8f,
                new[] { new Vector3(4.5f, 0f, 1f) }),
        };

        // Strip in front of the pass marking where the dining room will be.
        private const float DiningStripDepth = 1f;
        private static readonly Color DiningColor = new Color(0.55f, 0.45f, 0.40f);

        // Placeholder ingredients: type id, colour, diameter in metres.
        private static readonly (string Id, Color Color, float Size, Vector3 CratePosition)[] ItemTypes =
        {
            ("Tomato", new Color(0.90f, 0.18f, 0.12f), 0.28f, new Vector3(-4.4f, 0f, 1.5f)),
            ("Onion", new Color(0.93f, 0.84f, 0.62f), 0.26f, new Vector3(-4.4f, 0f, 0f)),
        };

        // Greybox palette: darker walls than floor so edges read from the overhead camera.
        private static readonly Color FloorColor = new Color(0.62f, 0.63f, 0.66f);
        private static readonly Color WallColor = new Color(0.30f, 0.33f, 0.40f);
        private static readonly Color CrateColor = new Color(0.50f, 0.36f, 0.22f);
        private static readonly Color NoseColor = new Color(0.20f, 0.20f, 0.22f);

        [MenuItem("Tools/Game/Build Greybox Kitchen")]
        public static void BuildAll()
        {
            // Create the scene before loading any assets: opening a new scene unloads assets
            // nothing references yet, which would leave the references below dead and saved
            // into the scene as empty.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var settings = GreyboxAssets.EnsureAsset<CoopSettings>(CoopSettingsPath);
            var tuning = GreyboxAssets.EnsureAsset<TuningConfig>(TuningConfigPath);
            var catalog = BuildItemCatalog();
            var playerPrefab = BuildPlayerPrefab(settings, tuning);
            BuildScene(scene, settings, tuning, catalog, playerPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxKitchenBuilder] Built {PlayerPrefabPath}, item prefabs and {ScenePath}.");
        }

        private static ItemCatalog BuildItemCatalog()
        {
            var catalog = GreyboxAssets.EnsureAsset<ItemCatalog>(ItemCatalogPath);
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");
            entries.arraySize = ItemTypes.Length;
            for (var i = 0; i < ItemTypes.Length; i++)
            {
                var type = ItemTypes[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_typeId").stringValue = type.Id;
                entry.FindPropertyRelative("_prefab").objectReferenceValue = BuildItemPrefab(type.Id, type.Color, type.Size);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static GameObject BuildItemPrefab(string id, Color color, float size)
        {
            // Root at the item's base so it sits on whatever anchor holds it.
            var root = new GameObject($"PF_Item_{id}");
            root.AddComponent<ItemView>();
            var body = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Sphere, "Body", root.transform, GreyboxAssets.EnsureMaterial(id, color), keepCollider: false);
            body.transform.localScale = Vector3.one * size;
            body.transform.localPosition = new Vector3(0f, size / 2f, 0f);

            GreyboxAssets.EnsureFolder(ItemPrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ItemPrefabFolder}/PF_Item_{id}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
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

            var movement = root.AddComponent<PlayerMovement>();
            SetReference(movement, "_tuning", tuning);

            var carryAnchor = new GameObject("CarryAnchor").transform;
            carryAnchor.SetParent(root.transform, false);
            carryAnchor.localPosition = CarryAnchorPosition;
            var interactor = root.AddComponent<PlayerInteractor>();
            SetReference(interactor, "_carryAnchor", carryAnchor);

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

        private static void BuildScene(
            UnityEngine.SceneManagement.Scene scene, CoopSettings settings, TuningConfig tuning, ItemCatalog catalog, GameObject playerPrefab)
        {
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
            manager.playerPrefab = playerPrefab;

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
            rootSo.FindProperty("_itemCatalog").objectReferenceValue = catalog;
            rootSo.FindProperty("_tuning").objectReferenceValue = tuning;
            rootSo.FindProperty("_playerInputManager").objectReferenceValue = manager;
            rootSo.ApplyModifiedPropertiesWithoutUndo();

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
            var crateMaterial = GreyboxAssets.EnsureMaterial("Crate", CrateColor);

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

            foreach (var type in ItemTypes)
            {
                var crate = CreateStation<CrateStation>(
                    $"Crate_{type.Id}", type.CratePosition, CrateSize, CrateHeight, crateMaterial, stations, settings);
                var so = new SerializedObject(crate);
                so.FindProperty("_itemTypeId").stringValue = type.Id;
                so.ApplyModifiedPropertiesWithoutUndo();

                // A sample of the contents on top, so each crate reads at a glance.
                var sample = GreyboxAssets.CreatePrimitive(
                    PrimitiveType.Sphere, "Label", crate.transform, GreyboxAssets.EnsureMaterial(type.Id, type.Color), keepCollider: false);
                sample.transform.localScale = Vector3.one * type.Size;
                sample.transform.localPosition = new Vector3(-CrateSize / 4f, CrateHeight + type.Size / 2f, -CrateSize / 4f);
            }
        }

        private static T CreateStation<T>(
            string name, Vector3 position, float size, float height, Material material, Transform parent, CoopSettings settings)
            where T : Interactable
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            var body = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Body", root, material, keepCollider: true);
            body.transform.localScale = new Vector3(size, height, size);
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);

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
