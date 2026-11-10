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
        private const string ServiceConfigPath = SettingsFolder + "/ServiceConfig.asset";
        private const string StaffConfigPath = SettingsFolder + "/StaffConfig.asset";
        private const string FurnitureCatalogPath = SettingsFolder + "/FurnitureCatalog.asset";
        private const string FloorPlanPath = SettingsFolder + "/FloorPlanConfig.asset";
        private const string FurniturePrefabFolder = "Assets/_Project/Prefabs/Furniture";

        // Furniture ids, and what each costs the first time the catalogue is created. After that the
        // asset is what's tuned.
        private const string CounterFurniture = "counter";
        private const string HobFurniture = "hob";
        private const string InductionFurniture = "induction hob";
        private const string GrillFurniture = "grill";
        private const string BoardFurniture = "board";
        private const string KnifeRackFurniture = "knife rack";
        private const string FridgeFurniture = "fridge";
        private const string ShelfFurniture = "shelf";
        private const string TableFurniture = "table";
        private const string BarFurniture = "bar";
        private const float CounterPrice = 30f;
        private const float HobPrice = 80f;
        private const float InductionPrice = 150f;
        private const float GrillPrice = 250f;
        private const float BoardPrice = CounterPrice + 20f; // a counter with the board built in
        private const float KnifeRackPrice = CounterPrice + 25f; // a counter with the rack built in
        private const float KnifePrice = 15f;
        private const float PanPrice = 25f;
        private const float PlatePrice = 3f;
        private const float ShelfPrice = 60f;
        private const float FridgePrice = 120f;
        private const float TablePrice = 50f;
        private const float BarPrice = 30f;
        private const float TablePlateOffset = 0.22f;

        // A bar table: higher, and half as deep, so it takes little room against a wall.
        private const float BarHeight = 1f;
        private const float BarDepth = 0.5f;
        private const float BarChairGap = 0.35f;
        private const float BarPlateOffset = 0.08f;
        private static readonly Color BarColor = new Color(0.62f, 0.44f, 0.26f);

        // A table: a little lower than a counter, with a chair at its south side where its customer sits.
        private const float TableHeight = 0.75f;
        private const float ChairDistance = 0.85f;
        private static readonly Color TableColor = new Color(0.50f, 0.34f, 0.22f);
        private static readonly Color ChairColor = new Color(0.35f, 0.25f, 0.18f);
        private static readonly Vector3 ChairScale = new Vector3(0.36f, 0.22f, 0.36f); // cylinder: 0.44 m tall
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/PF_Player.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/Kitchen_Greybox.unity";
        private const string JoinActionPath = "Gameplay/Join";

        // Greybox layout, in metres. The kitchen is 10 x 8 with the camera looking north (+Z).
        private const float FloorWidth = 8f;
        private const float FloorNorth = 2f;
        private const float FloorSouth = -7f;
        private const float DividingWallZ = -3f;
        private const float PictureHeight = 0.78f;
        private static readonly Color PictureSage = new Color(0.56f, 0.66f, 0.5f);
        private static readonly Color PictureBlue = new Color(0.5f, 0.62f, 0.72f);
        private const float KitchenDoorThickness = 0.08f;
        private static readonly Color KitchenDoorColor = new Color(0.45f, 0.33f, 0.22f);
        // One per player, up to four (decision 2026-11-09): P1 and P2 beside the island's ends,
        // P3 and P4 in the aisle in front of it, clear of the island, the plate stack and the door.
        private static readonly Vector3[] SpawnPositions =
        {
            new Vector3(-2.45f, 0f, -0.8f), new Vector3(2.45f, 0f, -0.8f),
            new Vector3(-1.0f, 0f, -1.75f), new Vector3(0.3f, 0f, -1.75f),
        };
        private const float WallHeight = 1f;
        private const float WallThickness = 0.2f;

        // Fixed overhead camera (decision 2026-11-02): high and tilted so the whole kitchen
        // and everyone in it stay in view.
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
        // Where the chef bends; must match CHEF_WAIST in blender/build_pilot_models.py.
        private const float ChefWaistHeight = 0.85f;
        // Where the arms hang from, above the waist and either side (the chef model's shoulders).
        private const float ChefShoulderHeight = 0.38f;
        private const float ChefShoulderWidth = 0.27f;
        private static readonly Color ChefHatColor = new Color(0.97f, 0.97f, 0.95f);
        private static readonly Color SheathColor = new Color(0.42f, 0.24f, 0.13f);
        private static readonly Color SheathRimColor = new Color(0.26f, 0.15f, 0.09f);
        private static readonly Color ChefTrousersColor = new Color(0.2f, 0.2f, 0.24f);
        private static readonly Color ChefSkinColor = new Color(0.96f, 0.78f, 0.63f);
        private static readonly Color ChefFaceColor = new Color(0.16f, 0.11f, 0.09f);
        // The knife sits at the chef's right side, so it reads separately from what's carried.
        private static readonly Vector3 ToolAnchorPosition = new Vector3(0.4f, 0.95f, 0.15f);

        // Prep zone on the back wall, left: a counter with the kitchen's one knife lying on it, and the
        // cutting board, a counter-sized piece of its own (decision 2026-11-08).
        private static readonly Color PrepColor = new Color(0.78f, 0.64f, 0.45f);
        // On the west wall, by prep: the back-left corner is the tall storage (decision 2026-11-07).
        private static readonly Vector3 KnifeCounterPosition = new Vector3(-3.4f, 0f, -0.6f);
        private static readonly Vector3 PrepCounterPosition = new Vector3(-1.4f, 0f, 1.4f);
        // The board's size: most of its counter, and thick; its top is the cutting surface. Must
        // match build_board() in the Blender script.
        private static readonly Vector3 CuttingBoardScale = new Vector3(0.88f, 0.07f, 0.75f);
        private static readonly Color CuttingBoardColor = new Color(0.92f, 0.82f, 0.62f);

        // A knife rack: a block along the counter's back with knives standing in it, one per place.
        private const int KnifeRackPlaces = 4; // as many as the block's slots (decision 2026-11-08)
        private static readonly Vector3 KnifeRackBlockScale = new Vector3(0.6f, 0.12f, 0.1f);
        private const float KnifeRackBack = 0.32f;
        private static readonly Color KnifeRackColor = new Color(0.36f, 0.26f, 0.18f);

        // Stations: standard 0.9 m counter height, 1 m footprint.
        private const float CounterHeight = 0.9f;
        private const float CounterSize = 1f;

        // Storage on the west wall: a fridge for meat and a shelf for vegetables. Purchases arrive
        // straight here (free each morning until shopping, Phase 5). Anything can go in either.
        private static readonly GreyboxStorage.Unit[] StorageUnits =
        {
            new GreyboxStorage.Unit
            {
                Name = "Fridge", FurnitureId = FridgeFurniture, Position = new Vector3(-3.4f, 0f, 1.4f), Refrigerated = true,
                Color = new Color(0.94f, 0.96f, 0.97f),
                SlotCapacity = 6,
                Deliveries = new[] { ("Steak", 18) },
            },
            new GreyboxStorage.Unit
            {
                Name = "Shelf", FurnitureId = ShelfFurniture, Position = new Vector3(-2.4f, 0f, 1.4f), Refrigerated = false,
                Color = new Color(0.58f, 0.43f, 0.28f),
                SlotCapacity = 6,
                Deliveries = new[] { ("Onion", 8) },
            },
        };

        // Kitchen zones (decision 2026-11-02), laid out so food flows storage -> prep -> cooking
        // -> pass and the path crosses the middle, where the island is the hand-off point.
        // Pass and bin are plain counters until later milestones give them real behaviour; each
        // zone has its own colour so it reads from the overhead camera.
        private static readonly (string Name, Color Color, float Size, float Height, Vector3[] Positions)[] CounterZones =
        {
            ("Island", new Color(0.70f, 0.58f, 0.42f), CounterSize, CounterHeight,
                new[] { new Vector3(-1.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(1.5f, 0f, -0.5f) }),
            // Extra counters along the walls, for testing containers and trays (2026-11-04). The one
            // by the sink is where the dirty pile stood: dirty plates now pile on any counter (2026-11-08).
            ("WallCounter", new Color(0.50f, 0.46f, 0.42f), CounterSize, CounterHeight,
                new[] { new Vector3(3.4f, 0f, -0.6f) }),
        };

        // The pass along the front, facing the dining side: plates put here are served and scored.
        private static readonly Vector3[] BarTablePositions = { new Vector3(-1.6f, 0f, -3.35f), new Vector3(-0.6f, 0f, -3.35f), new Vector3(0.4f, 0f, -3.35f) };


        // One hob on the back wall, right of prep, with its burner ring and its own pan. More are bought.
        private static readonly Vector3[] HobPositions = { new Vector3(0.6f, 0f, 1.4f) };
        private static readonly Color HobColor = new Color(0.22f, 0.22f, 0.25f);

        // An induction hob: a black glass top with a flat cooking zone that glows deep red with the
        // step, where gas has a burner ring glowing orange. Told apart at a glance from above.
        private static readonly Color InductionColor = new Color(0.06f, 0.06f, 0.07f);
        private static readonly Color InductionZoneOff = new Color(0.14f, 0.14f, 0.16f);
        private static readonly Color InductionZoneOn = new Color(0.85f, 0.08f, 0.06f);

        // A charcoal grill: two counters wide, coals in a firebox under bars, the hot zone on its
        // left as built. Places for two pieces on each zone, one towards each long side.
        private static readonly Color GrillColor = new Color(0.16f, 0.15f, 0.15f);
        private static readonly Color CoalColor = new Color(0.8f, 0.25f, 0.05f);
        private static readonly Color GrillBarColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Vector3 CoalBedScale = new Vector3(0.82f, 0.02f, 0.78f);
        private const int GrillBars = 7;
        private static readonly Vector3 GrillBarScale = new Vector3(1.8f, 0.012f, 0.025f);
        private const float GrillPlaceOffset = 0.2f;
        private static readonly Color GrillLightColor = new Color(1f, 0.55f, 0.25f);
        private const float GrillLightRange = 3.5f;
        private const float GrillLightHeight = 0.5f;
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
        private static readonly Vector3 PlateStackPosition = new Vector3(1.4f, 0f, -2.4f);
        private static readonly Color PlateStackColor = new Color(0.80f, 0.80f, 0.78f);
        private const int PlateStackVisualCount = 6;
        private static readonly Vector3 TrayRackPosition = new Vector3(1.6f, 0f, 1.4f);
        private static readonly Vector3 ContainerRackPosition = new Vector3(-0.4f, 0f, 1.4f);
        private static readonly Color RackColor = new Color(0.46f, 0.48f, 0.52f);
        private static readonly Vector3 SinkPosition = new Vector3(3.4f, 0f, 0.4f);
        private const float SinkYaw = 90f; // back (+z) to the east wall
        private static readonly Color SinkColor = new Color(0.55f, 0.62f, 0.68f);
        private static readonly Color DirtyPlateColor = new Color(0.45f, 0.36f, 0.25f);
        private static readonly Vector3 BinPosition = new Vector3(3.4f, 0f, 1.4f);
        private static readonly Color BinColor = new Color(0.25f, 0.55f, 0.30f);
        private const float BinSize = 0.7f;
        private const float BinHeight = 0.8f;

        private static readonly Color NoseColor = new Color(0.20f, 0.20f, 0.22f);

        /// <summary>Rebuilds only the shop's furniture prefabs and catalog, leaving the scene as it is.</summary>
        [MenuItem("Tools/Game/Rebuild Shop Furniture")]
        public static void RebuildFurniture()
        {
            BuildFurniture();
            AssetDatabase.SaveAssets();
            Debug.Log("[GreyboxKitchenBuilder] Rebuilt the shop's furniture.");
        }

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
                ServiceConfig = GreyboxAssets.EnsureAsset<ServiceConfig>(ServiceConfigPath),
                StaffConfig = GreyboxAssets.EnsureAsset<StaffConfig>(StaffConfigPath),
                FloorPlan = GreyboxAssets.EnsureAsset<FloorPlanConfig>(FloorPlanPath),
                Catalog = GreyboxIngredients.BuildCatalog(
                    (GreyboxKitchenware.PanTypeId, GreyboxKitchenware.BuildPan()),
                    (Game.Simulation.Items.Plate.PlateTypeId, GreyboxKitchenware.BuildPlate()),
                    (Game.Simulation.Items.Knife.KnifeTypeId, GreyboxKitchenware.BuildKnife()),
                    (Game.Simulation.Items.Tray.TrayTypeId, GreyboxKitchenware.BuildTray()),
                    (Game.Simulation.Items.FoodContainer.ContainerTypeId, GreyboxKitchenware.BuildContainer())),
            };
            SetMenu(assets.ServiceConfig, assets.Catalog);
            // Pictures for menus, from the food prefabs just built; only missing ones are rendered.
            IngredientIconRenderer.Render(force: false);
            assets.PlayerPrefab = BuildPlayerPrefab(assets.Settings, assets.Tuning);
            assets.Furniture = BuildFurniture();
            BuildScene(scene, assets);
            HouseStyle.ApplyToMaterials();
            RenderLook.Apply(); // ink, ambient occlusion and shadows in the PC pipeline
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxKitchenBuilder] Built {PlayerPrefabPath}, ingredients and {ScenePath}.");

            // The main menu comes first in a build and loads this kitchen (decision 2026-11-08).
            MainMenuBuilder.Build();
        }

        private sealed class SceneAssets
        {
            public CoopSettings Settings;
            public TuningConfig Tuning;
            public EquipmentTier Equipment;
            public RealismSettings Realism;
            public ServiceConfig ServiceConfig;
            public StaffConfig StaffConfig;
            public FurnitureCatalog Furniture;
            public FloorPlanConfig FloorPlan;
            public ItemCatalog Catalog;
            public GameObject PlayerPrefab;
        }

        // Phase 2 menu: steak to order, sometimes with sautéed onions. Only fills empty fields, so a
        // menu tuned in the Inspector survives a rebuild.
        private static void SetMenu(ServiceConfig config, ItemCatalog catalog)
        {
            var so = new SerializedObject(config);
            var main = so.FindProperty("_main");
            if (main.objectReferenceValue == null)
                main.objectReferenceValue = catalog.Find("Steak");
            var side = so.FindProperty("_side");
            if (side.objectReferenceValue == null)
                side.objectReferenceValue = catalog.Find("DicedOnion");
            so.ApplyModifiedPropertiesWithoutUndo();
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
            // The character kit holds things where its arms reach (CharacterArt.CarryAnchor).
            carryAnchor.localPosition = CharacterArt.Available ? CharacterArt.CarryAnchor : CarryAnchorPosition;
            // What a chef carries shows bigger than where it lies, so it reads at a glance
            // (the user's note, 2026-11-08); it's back to its size once put down.
            carryAnchor.localScale = Vector3.one * ItemSizes.Carried;
            var toolAnchor = new GameObject("ToolAnchor").transform;
            toolAnchor.SetParent(root.transform, false);
            toolAnchor.localPosition = ToolAnchorPosition;
            toolAnchor.localScale = Vector3.one * ItemSizes.Carried;
            var interactor = root.AddComponent<PlayerInteractor>();
            SetReference(interactor, "_carryAnchor", carryAnchor);
            SetReference(interactor, "_toolAnchor", toolAnchor);

            // Visuals live under one child so the join pop scales them without moving the root.
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);

            // The v9.2 character (concept 2026-11-09) when its kit is exported; otherwise the
            // first modelled chef, or capsules without any model.
            Transform waist;
            System.Collections.Generic.List<Renderer> tinted;
            if (CharacterArt.Available)
                waist = BuildCharacterChef(root, visuals, interactor, carryAnchor, toolAnchor, out tinted);
            else
            {
                // Two parts, so the chef can bend at the waist (reaching into a shelf). Placeholder
                // capsule halves unless the chef model exists; the torso hangs from the waist.
                var halfScale = new Vector3(BodyScale.x, BodyScale.y / 2f, BodyScale.z);
                var legs = GreyboxAssets.CreatePrimitive(PrimitiveType.Capsule, "Legs", visuals, null, keepCollider: false);
                legs.transform.localScale = halfScale;
                legs.transform.localPosition = new Vector3(0f, halfScale.y, 0f); // capsule is 2 units tall
                var modelled = PilotArt.TryUseMesh(legs, "SM_Chef", "ChefLegs");

                waist = new GameObject("Waist").transform;
                waist.SetParent(visuals, false);
                waist.localPosition = new Vector3(0f, ChefWaistHeight, 0f);
                var torso = GreyboxAssets.CreatePrimitive(PrimitiveType.Capsule, "Torso", waist, null, keepCollider: false);
                torso.transform.localScale = halfScale;
                torso.transform.localPosition = new Vector3(0f, halfScale.y, 0f);
                PilotArt.TryUseMesh(torso, "SM_Chef", "ChefTorso");

                // The modelled chef (decision 2026-11-08, the art overhaul): white jacket and toque, dark
                // trousers, a face, and an apron and neckerchief in the player's colour, so co-op
                // players tell each other apart. Without the model, the capsules take the colour.
                tinted = new System.Collections.Generic.List<Renderer>();
                if (modelled)
                {
                    legs.GetComponent<Renderer>().sharedMaterial = StyleTest.EnsureToonMaterial("ChefTrousers", ChefTrousersColor);
                    torso.GetComponent<Renderer>().sharedMaterial = StyleTest.EnsureToonMaterial("ChefJacket", ChefHatColor);
                    AddChefPart(visuals, "Skirt", "ChefSkirt", StyleTest.EnsureToonMaterial("ChefJacket", ChefHatColor));
                    tinted.Add(AddChefPart(visuals, "Apron", "ChefApron", StyleTest.EnsureToonMaterial("ChefAccent", Color.white)));
                    AddChefPart(waist, "Head", "ChefHead", StyleTest.EnsureToonMaterial("ChefSkin", ChefSkinColor));
                    AddChefPart(waist, "Face", "ChefFace", StyleTest.EnsureToonMaterial("ChefFace", ChefFaceColor));
                    tinted.Add(AddChefPart(waist, "Scarf", "ChefScarf", StyleTest.EnsureToonMaterial("ChefAccent", Color.white)));
                    AddChefPart(waist, "Hat", "ChefHat", StyleTest.EnsureToonMaterial("ChefHat", ChefHatColor));
                    tinted.RemoveAll(renderer => renderer == null);

                    // Arms (the user's note, 2026-11-08): a sleeve and mitten hand on each shoulder,
                    // which bend with the torso; ChefArmsView swings them and puts the hands on what's held.
                    var shoulders = new Transform[2];
                    for (var i = 0; i < 2; i++)
                    {
                        var shoulder = new GameObject(i == 0 ? "Shoulder_L" : "Shoulder_R").transform;
                        shoulder.SetParent(waist, false);
                        shoulder.localPosition = new Vector3(i == 0 ? -ChefShoulderWidth : ChefShoulderWidth, ChefShoulderHeight, 0f);
                        AddChefPart(shoulder, "Arm", "ChefArm", StyleTest.EnsureToonMaterial("ChefJacket", ChefHatColor));
                        AddChefPart(shoulder, "Hand", "ChefHand", StyleTest.EnsureToonMaterial("ChefSkin", ChefSkinColor));
                        shoulders[i] = shoulder;
                    }
                    // The knife's sheath on the right hip (the user's choice, 2026-11-08): the knife rides
                    // in it until the board, and it only shows while the chef has one. It hangs from the
                    // belt, so it doesn't bend with the torso.
                    var sheath = new[]
                    {
                        AddChefPart(visuals, "Sheath", "ChefSheath", StyleTest.EnsureToonMaterial("ChefSheath", SheathColor)),
                        AddChefPart(visuals, "SheathRim", "ChefSheathRim", StyleTest.EnsureToonMaterial("ChefSheathRim", SheathRimColor)),
                    }.Where(part => part != null).ToArray();
                    foreach (var part in sheath)
                        part.enabled = false; // until the chef takes a knife

                    var arms = new SerializedObject(root.AddComponent<ChefArmsView>());
                    arms.FindProperty("_interactor").objectReferenceValue = interactor;
                    arms.FindProperty("_leftShoulder").objectReferenceValue = shoulders[0];
                    arms.FindProperty("_rightShoulder").objectReferenceValue = shoulders[1];
                    arms.FindProperty("_carryAnchor").objectReferenceValue = carryAnchor;
                    arms.FindProperty("_toolAnchor").objectReferenceValue = toolAnchor;
                    var sheathList = arms.FindProperty("_sheath");
                    sheathList.arraySize = sheath.Length;
                    for (var i = 0; i < sheath.Length; i++)
                        sheathList.GetArrayElementAtIndex(i).objectReferenceValue = sheath[i];
                    arms.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    tinted.Add(legs.GetComponent<Renderer>());
                    tinted.Add(torso.GetComponent<Renderer>());

                    // A nose shows facing direction from the overhead camera; it bends with the torso.
                    var nose = GreyboxAssets.CreatePrimitive(
                        PrimitiveType.Cube, "Nose", waist, GreyboxAssets.EnsureMaterial("Nose", NoseColor), keepCollider: false);
                    nose.transform.localScale = NoseScale;
                    nose.transform.localPosition = new Vector3(0f, BodyScale.y * 1.5f - ChefWaistHeight, BodyScale.z * 0.5f);
                }
            }

            var use = new SerializedObject(root.AddComponent<StorageUseView>());
            use.FindProperty("_interactor").objectReferenceValue = interactor;
            use.FindProperty("_waist").objectReferenceValue = waist;
            if (CharacterArt.Available)
                use.FindProperty("_bendAngle").floatValue = CharacterArt.StorageBend;
            use.ApplyModifiedPropertiesWithoutUndo();

            var visual = root.AddComponent<PlayerVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_playerInput").objectReferenceValue = playerInput;
            so.FindProperty("_popTarget").objectReferenceValue = visuals;
            var renderers = so.FindProperty("_tintedRenderers");
            renderers.arraySize = tinted.Count;
            for (var i = 0; i < tinted.Count; i++)
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = tinted[i];
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
            HouseStyle.LightScene(light); // also aims it
            KitchenLighting.Add(); // gentle, even lamps (decision 2026-11-09)

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));
            camera.fieldOfView = CameraFieldOfView;
            HouseStyle.SmoothEdges(camera);
            RenderLook.Backdrop(camera);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(FloorWidth / 10f, 1f, (FloorNorth - FloorSouth) / 10f); // plane is 10x10
            floor.transform.localPosition = new Vector3(0f, 0f, (FloorNorth + FloorSouth) / 2f);
            floor.GetComponent<Renderer>().sharedMaterial = HouseStyle.KitchenFloor();
            BuildDiningFloor();
            BuildWalls(assets.FloorPlan);
            BuildStations();
            CabinetArt.JoinAll(); // side by side counters as one surface, in the saved scene too

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
            rootSo.FindProperty("_serviceConfig").objectReferenceValue = assets.ServiceConfig;
            rootSo.FindProperty("_staffConfig").objectReferenceValue = assets.StaffConfig;
            rootSo.FindProperty("_floorPlan").objectReferenceValue = assets.FloorPlan;
            rootSo.FindProperty("_furniture").objectReferenceValue = assets.Furniture;
            rootSo.FindProperty("_playerInputManager").objectReferenceValue = manager;
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            var overlay = root.gameObject.AddComponent<CookingDebugOverlay>();
            SetReference(overlay, "_root", root);
            root.gameObject.AddComponent<QualityCardOverlay>();
            SetReference(root.gameObject.AddComponent<DayLoop>(), "_root", root);
            GreyboxService.Build(root);
            GreyboxStaff.Build(root, assets.Furniture);

            GreyboxHud.BuildCanvas(GreyboxHud.EnsureStyle(), settings, root, assets.Catalog);

            var framing = new SerializedObject(camera.gameObject.AddComponent<FramingCamera>());
            framing.FindProperty("_root").objectReferenceValue = root;
            framing.FindProperty("_staff").objectReferenceValue = Object.FindFirstObjectByType<Game.Gameplay.Staff.StaffDirector>();
            framing.FindProperty("_editor").objectReferenceValue = Object.FindFirstObjectByType<LayoutEditor>();
            framing.FindProperty("_tilt").floatValue = CameraEuler.x;
            framing.ApplyModifiedPropertiesWithoutUndo();

            GreyboxAudio.AddTo(root, camera);
            CleaningArt.AddTo(root);

            GreyboxAssets.EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // The dining room's floor, in its own colour. Walkable and without a collider; a few
        // millimetres up to avoid z-fighting with the floor.
        private static void BuildDiningFloor()
        {
            const float lift = 0.005f;
            var depth = DividingWallZ - FloorSouth;
            var floor = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "DiningFloor", null, HouseStyle.DiningFloor(), keepCollider: false);
            floor.transform.localScale = new Vector3(FloorWidth, lift, depth);
            floor.transform.localPosition = new Vector3(0f, lift / 2f, FloorSouth + depth / 2f);
        }

        // Low walls around the building so players can't walk off the edge, and between its rooms.
        private static void BuildWalls(FloorPlanConfig floorPlan)
        {
            var walls = new GameObject("Walls").transform;
            var material = HouseStyle.Wall();
            var halfWidth = FloorWidth / 2f;
            var depth = FloorNorth - FloorSouth;
            var centreZ = (FloorNorth + FloorSouth) / 2f;
            var y = WallHeight / 2f;

            CreateWall("Wall_North", new Vector3(0f, y, FloorNorth), new Vector3(FloorWidth, WallHeight, WallThickness));
            CreateWall("Wall_South", new Vector3(0f, y, FloorSouth), new Vector3(FloorWidth, WallHeight, WallThickness));
            CreateWall("Wall_East", new Vector3(halfWidth, y, centreZ), new Vector3(WallThickness, WallHeight, depth));
            CreateWall("Wall_West", new Vector3(-halfWidth, y, centreZ), new Vector3(WallThickness, WallHeight, depth));

            for (var i = 0; i < floorPlan.Walls.Length; i++)
            {
                var wall = floorPlan.Walls[i];
                CreateWall($"Wall_Inside_{i + 1}", new Vector3(wall.center.x, y, wall.center.y), new Vector3(wall.width, WallHeight, wall.height));
            }

            // Pictures on the dining room's side walls, above the panelling (decision 2026-11-08).
            var diningMiddle = (DividingWallZ + FloorSouth) / 2f;
            var inside = halfWidth - WallThickness / 2f;
            DiningArt.HangPicture(walls, "Picture_West", new Vector3(-inside, PictureHeight, diningMiddle), Vector3.right, PictureSage);
            DiningArt.HangPicture(walls, "Picture_East", new Vector3(inside, PictureHeight, diningMiddle), Vector3.left, PictureBlue);

            // The door between the rooms: a panel on a hinge, swung by whoever comes near.
            var hinge = new GameObject("KitchenDoor").transform;
            hinge.SetParent(walls, false);
            hinge.localPosition = new Vector3(floorPlan.KitchenDoorHinge.x, 0f, floorPlan.KitchenDoorHinge.y);
            var panel = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Panel", hinge, StyleTest.EnsureToonMaterial("KitchenDoor", KitchenDoorColor), keepCollider: false);
            panel.transform.localScale = new Vector3(floorPlan.KitchenDoorWidth, WallHeight, KitchenDoorThickness);
            panel.transform.localPosition = new Vector3(floorPlan.KitchenDoorWidth / 2f, y, 0f);
            var door = new SerializedObject(hinge.gameObject.AddComponent<SwingDoorView>());
            door.FindProperty("_doorway").vector3Value = hinge.localPosition + Vector3.right * (floorPlan.KitchenDoorWidth / 2f);
            door.ApplyModifiedPropertiesWithoutUndo();

            void CreateWall(string name, Vector3 position, Vector3 scale)
            {
                var wall = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, walls, material, keepCollider: true);
                wall.transform.localPosition = position;
                wall.transform.localScale = scale;
            }
        }

        private static void BuildStations()
        {
            var stations = new GameObject("Stations").transform;

            foreach (var zone in CounterZones)
            {
                var zoneRoot = new GameObject($"Zone_{zone.Name}").transform;
                zoneRoot.SetParent(stations, false);
                var material = GreyboxAssets.EnsureMaterial(zone.Name, zone.Color);
                for (var i = 0; i < zone.Positions.Length; i++)
                {
                    DressCabinet(CreateStation<CounterStation>(
                        $"{zone.Name}_{i + 1:00}", zone.Positions[i], zone.Size, zone.Height, material, zoneRoot, CounterFurniture));
                }
            }

            BuildPrepZone(stations);

            var hobs = new GameObject("Zone_Hob").transform;
            hobs.SetParent(stations, false);
            var hobMaterial = GreyboxAssets.EnsureMaterial("Hob", HobColor);
            for (var i = 0; i < HobPositions.Length; i++)
                CreateHob($"Hob_{i + 1:00}", HobPositions[i], hobMaterial, hobs);

            // The first seats: bar tables along the dining side of the wall, served across it.
            var bars = new GameObject("Zone_BarTables").transform;
            bars.SetParent(stations, false);
            for (var i = 0; i < BarTablePositions.Length; i++)
                BuildBarTable($"BarTable_{i + 1:00}", BarTablePositions[i], bars);

            var plateStack = DressCabinet(CreateStation<PlateStackStation>(
                "PlateStack", PlateStackPosition, CounterSize, CounterHeight,
                GreyboxAssets.EnsureMaterial("PlateStack", PlateStackColor), stations));
            // Clean plates stand in a wire rack, like a real plate stack.
            var plateStackDiscs = KitchenwareArt.CreatePlates(plateStack.transform, CounterHeight, PlateStackVisualCount,
                GreyboxKitchenware.PlateColor, "Plate", Vector3.zero, KitchenwareArt.PlateLayout.Rack);
            var stackPile = new SerializedObject(plateStack.gameObject.AddComponent<PlatePileView>());
            stackPile.FindProperty("_station").objectReferenceValue = plateStack;
            SetObjects(stackPile.FindProperty("_clean"), plateStackDiscs);
            stackPile.ApplyModifiedPropertiesWithoutUndo();

            var sink = CreateStation<SinkStation>(
                "Sink", SinkPosition, CounterSize, CounterHeight, GreyboxAssets.EnsureMaterial("Sink", SinkColor), stations);
            // Its back, where the tap is, against the east wall; players turn it as they like.
            sink.transform.localRotation = Quaternion.Euler(0f, SinkYaw, 0f);
            var sinkPile = new SerializedObject(sink.gameObject.AddComponent<PlatePileView>());
            sinkPile.FindProperty("_station").objectReferenceValue = sink;
            // The sink washes one plate at a time and keeps none (decision 2026-11-08): the plate
            // being washed shows in the basin.
            // On the shared wooden cabinet, with its steel top and basin (decision 2026-11-08).
            DressCabinet(sink, underSink: true);
            var sinkParts = KitchenwareArt.DressSink(sink.transform.Find("Body"));
            var modelled = sinkParts.Count > 0;
            foreach (var part in sinkParts)
                AddToHighlight(sink, part);
            if (modelled)
                KitchenwareArt.AddWashEffects(sink); // water and bubbles while it's in use
            var sinkPlates = KitchenwareArt.CreatePlates(sink.transform,
                modelled ? KitchenwareArt.SinkBasinFloor : CounterHeight, 1, DirtyPlateColor, "DirtyPlate",
                modelled ? new Vector3(KitchenwareArt.SinkBasinX, 0f, 0f) : Vector3.zero, KitchenwareArt.PlateLayout.Stack, dirty: true);
            if (modelled && sinkPlates.Length > 0 && KitchenwareArt.HasPlateModel)
                KitchenwareArt.LeanInSink(sinkPlates[0]); // lying in the water, the dirty plate's own grime
            SetObjects(sinkPile.FindProperty("_dirty"), sinkPlates);
            sinkPile.ApplyModifiedPropertiesWithoutUndo();

            CreateRack("TrayRack", RackStation.RackKind.Trays, TrayRackPosition, 4, GreyboxKitchenware.TrayColor, stations);
            CreateRack("ContainerRack", RackStation.RackKind.Containers, ContainerRackPosition, 6, GreyboxKitchenware.ContainerColor, stations);

            var bin = CreateStation<BinStation>(
                "Bin", BinPosition, BinSize, BinHeight, GreyboxAssets.EnsureMaterial("Bin", BinColor), stations);
            // Smaller than its slot, it slides within it to sit snug against what's beside it (the
            // user's note, 2026-11-08): its body hangs from a "Snug" frame the station moves, so the
            // highlight's shake still works on the body itself.
            var binBody = bin.transform.Find("Body");
            var snug = new GameObject("Snug").transform;
            snug.SetParent(bin.transform, false);
            binBody.SetParent(snug, false);
            foreach (var part in KitchenwareArt.DressBin(binBody))
                AddToHighlight(bin, part);
            KitchenwareArt.AddBinFill(bin, binBody); // rubbish rises as things are thrown away
            SetReference(bin, "_snug", snug);

            var storage = new GameObject("Zone_Storage").transform;
            storage.SetParent(stations, false);
            foreach (var unit in StorageUnits)
            {
                GreyboxStorage.Build(unit, (name, position, material) =>
                    CreateStation<StorageStation>(name, position, CounterSize, StorageArt.Height(unit.Refrigerated), material, storage, unit.FurnitureId));
            }
        }

        private static void BuildPrepZone(Transform stations)
        {
            var zone = new GameObject("Zone_Prep").transform;
            zone.SetParent(stations, false);
            var material = GreyboxAssets.EnsureMaterial("Prep", PrepColor);

            var knifeCounter = DressCabinet(CreateStation<CounterStation>("KnifeCounter", KnifeCounterPosition, CounterSize, CounterHeight, material, zone, CounterFurniture));
            var knifeSo = new SerializedObject(knifeCounter);
            knifeSo.FindProperty("_startsWithKnife").boolValue = true;
            knifeSo.ApplyModifiedPropertiesWithoutUndo();

            BuildCuttingBoard("CuttingBoard", PrepCounterPosition, material, zone);
        }

        private static GrillStation BuildGrill(string name, Vector3 position, Transform parent)
        {
            var grill = CreateStation<GrillStation>(
                name, position, 2f * CounterSize, CounterHeight, GreyboxAssets.EnsureMaterial("Grill", GrillColor), parent, GrillFurniture, depth: CounterSize);
            var top = CounterHeight;
            var coalMaterial = GreyboxAssets.EnsureMaterial("Coals", CoalColor);

            Transform Point(string pointName, float x, float y, float z)
            {
                var point = new GameObject(pointName).transform;
                point.SetParent(grill.transform, false);
                point.localPosition = new Vector3(x, y, z);
                return point;
            }

            Renderer Coals(string coalsName, float x)
            {
                var bed = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, coalsName, grill.transform, coalMaterial, keepCollider: false);
                bed.transform.localScale = CoalBedScale;
                bed.transform.localPosition = new Vector3(x, top + CoalBedScale.y / 2f, 0f);
                return bed.GetComponent<Renderer>();
            }

            var half = CounterSize / 2f;
            var hotCoals = Coals("HotCoals", -half);
            var coolCoals = Coals("CoolCoals", half);
            var barMaterial = GreyboxAssets.EnsureMaterial("GrillBar", GrillBarColor);
            for (var i = 0; i < GrillBars; i++)
            {
                var bar = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, $"Bar_{i + 1}", grill.transform, barMaterial, keepCollider: false);
                bar.transform.localScale = GrillBarScale;
                var z = (i - (GrillBars - 1) / 2f) * (CoalBedScale.z / GrillBars);
                bar.transform.localPosition = new Vector3(0f, top + CoalBedScale.y + GrillBarScale.y / 2f, z);
            }

            var surface = top + CoalBedScale.y + GrillBarScale.y;
            var so = new SerializedObject(grill);
            so.FindProperty("_hotCentre").objectReferenceValue = Point("HotCentre", -half, surface, 0f);
            so.FindProperty("_coolCentre").objectReferenceValue = Point("CoolCentre", half, surface, 0f);
            void Places(string field, string prefix, float x)
            {
                var list = so.FindProperty(field);
                list.arraySize = 2;
                // Food on the grill shows a little big, as cooking does in a pan (decision 2026-11-08).
                var front = Point($"{prefix}_Front", x, surface, -GrillPlaceOffset);
                var back = Point($"{prefix}_Back", x, surface, GrillPlaceOffset);
                front.localScale = back.localScale = Vector3.one * ItemSizes.Cooking;
                list.GetArrayElementAtIndex(0).objectReferenceValue = front;
                list.GetArrayElementAtIndex(1).objectReferenceValue = back;
            }
            Places("_hotPlaces", "HotPlace", -half);
            Places("_coolPlaces", "CoolPlace", half);
            so.ApplyModifiedPropertiesWithoutUndo();
            grill.ItemAnchor.localPosition = new Vector3(0f, surface, 0f);

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(grill.transform, false);
            lightObject.transform.localPosition = new Vector3(-half / 2f, top + GrillLightHeight, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GrillLightColor;
            light.range = GrillLightRange;
            light.shadows = LightShadows.None; // cheap: no shadow map for a glow

            var view = grill.gameObject.AddComponent<GrillView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_hotCoals").objectReferenceValue = hotCoals;
            viewSo.FindProperty("_coolCoals").objectReferenceValue = coolCoals;
            viewSo.FindProperty("_glow").objectReferenceValue = light;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            return grill;
        }

        // A cutting board built into its own counter (decision 2026-11-08): one piece, placed like a counter.
        private static PrepStation BuildCuttingBoard(string name, Vector3 position, Material material, Transform parent)
        {
            var prep = DressCabinet(CreateStation<PrepStation>(name, position, CounterSize, CounterHeight, material, parent, BoardFurniture));
            var board = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Board", prep.transform, GreyboxAssets.EnsureMaterial("CuttingBoard", CuttingBoardColor), keepCollider: false);
            board.transform.localScale = CuttingBoardScale;
            board.transform.localPosition = new Vector3(0f, CounterHeight + CuttingBoardScale.y / 2f, 0f);
            KitchenwareArt.DressBoard(board, CounterHeight); // the bamboo board, the same size, when modelled
            var boardAnchor = new GameObject("BoardAnchor").transform;
            boardAnchor.SetParent(prep.transform, false);
            boardAnchor.localPosition = new Vector3(0f, CounterHeight + CuttingBoardScale.y, 0f);
            boardAnchor.localScale = Vector3.one * ItemSizes.OnCounter; // big on the board, like a counter

            var so = new SerializedObject(prep);
            so.FindProperty("_boardAnchor").objectReferenceValue = boardAnchor;
            so.ApplyModifiedPropertiesWithoutUndo();
            VegetableArt.DressCutProgress(prep, boardAnchor); // an onion comes apart as it's cut
            AddToHighlight(prep, board.GetComponent<Renderer>());
            return prep;
        }

        // A knife rack built into its own counter (decision 2026-11-08): one piece, placed like a counter.
        private static KnifeRackStation BuildKnifeRack(string name, Transform parent)
        {
            var rack = DressCabinet(CreateStation<KnifeRackStation>(
                name, Vector3.zero, CounterSize, CounterHeight, GreyboxAssets.EnsureMaterial(CounterZones[0].Name, CounterZones[0].Color), parent, KnifeRackFurniture));
            var block = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cube, "Block", rack.transform, GreyboxAssets.EnsureMaterial("KnifeRack", KnifeRackColor), keepCollider: false);
            block.transform.localScale = KnifeRackBlockScale;
            block.transform.localPosition = new Vector3(0f, CounterHeight + KnifeRackBlockScale.y / 2f, KnifeRackBack);

            Transform[] places;
            // The modelled block stands in the middle of its counter, its face to the front (the
            // user's note, 2026-11-08); the greybox block keeps to the back.
            var blockSlots = KnifeArt.DressBlock(block, new Vector3(0f, CounterHeight, 0f));
            if (blockSlots != null)
            {
                // The knives stand in the angled block's slots, handles up and out (decision 2026-11-08).
                places = KnifeArt.SlotPlaces(rack.transform, new Vector3(0f, CounterHeight, 0f));
                AddToHighlight(rack, blockSlots);
            }
            else
            {
                // Knives lie along the counter in front of the block, side by side, handles to the back.
                places = new Transform[KnifeRackPlaces];
                var spacing = KnifeRackBlockScale.x / KnifeRackPlaces;
                for (var i = 0; i < KnifeRackPlaces; i++)
                {
                    var place = new GameObject($"Place_{i + 1}").transform;
                    place.SetParent(rack.transform, false);
                    place.localPosition = new Vector3((i - (KnifeRackPlaces - 1) / 2f) * spacing, CounterHeight, KnifeRackBack - 0.25f);
                    places[i] = place;
                }
            }
            var so = new SerializedObject(rack);
            var list = so.FindProperty("_places");
            list.arraySize = places.Length;
            for (var i = 0; i < places.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = places[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            AddToHighlight(rack, block.GetComponent<Renderer>());
            return rack;
        }

        // The kit's chef (concepts v9.2 → v16, 2026-11-09): big head, toque with the player's colour on its band,
        // apron and neckerchief, short arms that ChefArmsView aims, and the old chef's knife sheath
        // moved down to the barrel's hip. Returns the waist, which bends.
        private static Transform BuildCharacterChef(GameObject root, Transform visuals, PlayerInteractor interactor,
            Transform carryAnchor, Transform toolAnchor, out System.Collections.Generic.List<Renderer> tinted)
        {
            // The whole tailor's rail on the chef; ChefWardrobe wears the slot's look (decision 2026-11-09).
            var rig = CharacterArt.BuildChef(visuals, swingArms: false);
            tinted = rig.Tinted;
            // The knife rides in the sheath on the figure, so it sways with the walk and stays in it
            // (the user's playtest: it showed while walking).
            toolAnchor.SetParent(rig.Figure, worldPositionStays: true);

            var belt = new GameObject("Belt").transform;
            belt.SetParent(rig.Figure, false);
            belt.localPosition = CharacterArt.SheathShift;
            var sheath = new[]
            {
                AddChefPart(belt, "Sheath", "ChefSheath", StyleTest.EnsureToonMaterial("ChefSheath", SheathColor)),
                AddChefPart(belt, "SheathRim", "ChefSheathRim", StyleTest.EnsureToonMaterial("ChefSheathRim", SheathRimColor)),
            }.Where(part => part != null).ToArray();
            foreach (var part in sheath)
                part.enabled = false; // until the chef takes a knife

            var arms = new SerializedObject(root.AddComponent<ChefArmsView>());
            arms.FindProperty("_interactor").objectReferenceValue = interactor;
            arms.FindProperty("_leftShoulder").objectReferenceValue = rig.LeftShoulder;
            arms.FindProperty("_rightShoulder").objectReferenceValue = rig.RightShoulder;
            arms.FindProperty("_gait").objectReferenceValue = rig.Gait;
            arms.FindProperty("_carryAnchor").objectReferenceValue = carryAnchor;
            arms.FindProperty("_toolAnchor").objectReferenceValue = toolAnchor;
            var sheathList = arms.FindProperty("_sheath");
            sheathList.arraySize = sheath.Length;
            for (var i = 0; i < sheath.Length; i++)
                sheathList.GetArrayElementAtIndex(i).objectReferenceValue = sheath[i];
            // The knife rides in the sheath, so it moves with it (the tool anchor is scaled up).
            var beltOffset = arms.FindProperty("_beltOffset");
            beltOffset.vector3Value += CharacterArt.SheathShift / ItemSizes.Carried;
            arms.ApplyModifiedPropertiesWithoutUndo();
            return rig.Waist;
        }

        // A part of the modelled chef, under the given frame (feet or waist); null without the model.
        private static Renderer AddChefPart(Transform parent, string name, string mesh, Material material)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            if (PilotArt.TryUseMesh(part, "SM_Chef", mesh))
                return part.GetComponent<Renderer>();
            Object.DestroyImmediate(part);
            return null;
        }

        // A plain counter's body becomes the shared wooden cabinet (decision 2026-11-08), lighting up as one piece.
        private static T DressCabinet<T>(T station, bool underSink = false) where T : Interactable
        {
            foreach (var part in CabinetArt.Dress(station.transform, underSink))
                AddToHighlight(station, part);
            return station;
        }

        // What's built into a counter lights up with it, as one piece.
        private static void AddToHighlight(Interactable station, Renderer renderer)
        {
            var highlight = new SerializedObject(station.GetComponent<InteractableHighlightView>());
            var renderers = highlight.FindProperty("_renderers");
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(renderers.arraySize - 1).objectReferenceValue = renderer;
            highlight.ApplyModifiedPropertiesWithoutUndo();
        }

        // A hob: counter body, a burner ring that glows with the dial around the pan, and heat
        // pips along the back of the top.
        // Its pan is a separate, carryable item created at runtime.
        private static HobStation CreateHob(string name, Vector3 position, Material material, Transform parent, Game.Simulation.Cooking.HeatKind kind = Game.Simulation.Cooking.HeatKind.Gas)
        {
            var induction = kind == Game.Simulation.Cooking.HeatKind.Induction;
            var hob = CreateStation<HobStation>(name, position, CounterSize, CounterHeight, material, parent, induction ? InductionFurniture : HobFurniture);
            var top = CounterHeight;
            var kindSo = new SerializedObject(hob);
            kindSo.FindProperty("_kind").enumValueIndex = (int)kind;
            kindSo.ApplyModifiedPropertiesWithoutUndo();

            var burner = GreyboxAssets.CreatePrimitive(
                PrimitiveType.Cylinder, induction ? "CookingZone" : "Burner", hob.transform, GreyboxAssets.EnsureMaterial("Burner", Color.black), keepCollider: false);
            burner.transform.localScale = new Vector3(BurnerDiameter, 0.005f, BurnerDiameter); // cylinder is 2 units tall
            burner.transform.localPosition = new Vector3(0f, top + 0.005f, 0f);
            hob.ItemAnchor.localPosition = new Vector3(0f, top + 0.01f, 0f);

            // The art pilot's model is a gas hob; induction stays greybox until it gets its own.
            var fire = new StyleTest.HobFire();
            if (!induction)
            {
                PilotArt.TryUseMesh(hob.transform.Find("Body").gameObject, "SM_Hob", "HobBody");
                PilotArt.TryUseMesh(burner, "SM_Hob", "HobBurner");
                fire = StyleTest.DressHob(hob.transform, burner); // the cartoony style test (decision 2026-11-08)
            }

            // Only the induction hob shows its heat in lights (green, orange, red); a gas hob's
            // heat shows in its fire alone (the user's note, 2026-11-08).
            var pipCount = induction ? HeatPipCount : 0;
            var pips = new Renderer[pipCount];
            for (var i = 0; i < pipCount; i++)
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
            chipSo.ApplyModifiedPropertiesWithoutUndo();

            var view = hob.gameObject.AddComponent<HobView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_hob").objectReferenceValue = hob;
            viewSo.FindProperty("_burner").objectReferenceValue = burner.GetComponent<Renderer>();
            if (induction)
            {
                viewSo.FindProperty("_burnerOff").colorValue = InductionZoneOff;
                viewSo.FindProperty("_burnerFull").colorValue = InductionZoneOn;
            }
            SetObjects(viewSo.FindProperty("_flames"), fire.Flames);
            SetObjects(viewSo.FindProperty("_flameCores"), fire.Cores);
            SetObjects(viewSo.FindProperty("_puffs"), fire.Puffs);
            SetObjects(viewSo.FindProperty("_embers"), fire.Embers);
            SetObjects(viewSo.FindProperty("_glow"), fire.Glow);
            var pipList = viewSo.FindProperty("_pips");
            pipList.arraySize = pips.Length;
            for (var i = 0; i < pips.Length; i++)
                pipList.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            return hob;
        }

        private static void CreateRack(string name, RackStation.RackKind kind, Vector3 position, int discs, Color color, Transform parent)
        {
            var rack = DressCabinet(CreateStation<RackStation>(name, position, CounterSize, CounterHeight, GreyboxAssets.EnsureMaterial("Rack", RackColor), parent));
            var rackSo = new SerializedObject(rack);
            rackSo.FindProperty("_kind").enumValueIndex = (int)kind;
            rackSo.ApplyModifiedPropertiesWithoutUndo();

            var pile = new SerializedObject(rack.gameObject.AddComponent<PlatePileView>());
            pile.FindProperty("_station").objectReferenceValue = rack;
            // Trays stand on runners in a steel rack, containers nest in a stack (decision
            // 2026-11-08); greybox discs without the models.
            var pieces = kind == RackStation.RackKind.Trays
                ? PanArt.TrayRack(rack.transform, CounterHeight)
                : PanArt.ContainerStack(rack.transform, CounterHeight, discs);
            if (pieces.Length == 0)
                pieces = GreyboxKitchenware.CreatePlatePile(rack.transform, CounterHeight, discs, color, name, Vector3.zero);
            SetObjects(pile.FindProperty("_clean"), pieces);
            pile.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjects(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T CreateStation<T>(
            string name, Vector3 position, float size, float height, Material material, Transform parent, string furnitureId = "", float depth = 0f)
            where T : Interactable
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;

            var body = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Body", root, material, keepCollider: false);
            var deep = depth > 0f ? depth : size;
            body.transform.localScale = new Vector3(size, height, deep);
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);

            var blocker = root.gameObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(size, StationBlockerHeight, deep);
            blocker.center = new Vector3(0f, StationBlockerHeight / 2f, 0f);

            var anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, height, 0f);
            // What sits on a plain counter shows big, to read at a glance (decision 2026-11-08).
            if (typeof(T) == typeof(CounterStation))
                anchor.localScale = Vector3.one * ItemSizes.OnCounter;

            var station = root.gameObject.AddComponent<T>();
            SetReference(station, "_itemAnchor", anchor);
            var stationSo = new SerializedObject(station);
            stationSo.FindProperty("_furnitureId").stringValue = furnitureId;
            if (depth > 0f)
                stationSo.FindProperty("_halfSize").vector2Value = new Vector2(size / 2f, depth / 2f);
            stationSo.ApplyModifiedPropertiesWithoutUndo();

            var highlight = root.gameObject.AddComponent<InteractableHighlightView>();
            var so = new SerializedObject(highlight);
            so.FindProperty("_interactable").objectReferenceValue = station;
            so.FindProperty("_shakeTarget").objectReferenceValue = body.transform;
            var renderers = so.FindProperty("_renderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return station;
        }

        // What can be bought: built the same way as the starting kitchen, saved as prefabs. Prices are
        // only set when an entry is first made, so tuning in the asset survives a rebuild.
        private static FurnitureCatalog BuildFurniture()
        {
            GreyboxAssets.EnsureFolder(FurniturePrefabFolder);
            var island = CounterZones[0];

            var counter = SaveFurniture(DressCabinet(CreateStation<CounterStation>(
                "PF_Counter", Vector3.zero, CounterSize, CounterHeight, GreyboxAssets.EnsureMaterial(island.Name, island.Color), null, CounterFurniture)));
            var hob = SaveFurniture(CreateHob("PF_Hob", Vector3.zero, GreyboxAssets.EnsureMaterial("Hob", HobColor), null));
            var grill = SaveFurniture(BuildGrill("PF_Grill", Vector3.zero, null));
            var board = SaveFurniture(BuildCuttingBoard("PF_CuttingBoard", Vector3.zero, GreyboxAssets.EnsureMaterial(island.Name, island.Color), null));
            var knifeRack = SaveFurniture(BuildKnifeRack("PF_KnifeRack", null));
            var inductionHob = SaveFurniture(CreateHob("PF_InductionHob", Vector3.zero, GreyboxAssets.EnsureMaterial("InductionHob", InductionColor), null, Game.Simulation.Cooking.HeatKind.Induction));
            var fridge = SaveFurniture(BuildStorageForSale("PF_Fridge", StorageUnits[0]));
            var shelf = SaveFurniture(BuildStorageForSale("PF_Shelf", StorageUnits[1]));
            var table = SaveFurniture(BuildTable());
            var bar = SaveFurniture(BuildBarTable("PF_BarTable", Vector3.zero, null));

            var catalog = GreyboxAssets.EnsureAsset<FurnitureCatalog>(FurnitureCatalogPath);
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");
            SetFurniture(entries, CounterFurniture, "Counter", CounterPrice, keepOne: false, counter);
            SetFurniture(entries, HobFurniture, "Gas hob", HobPrice, keepOne: true, hob);
            SetFurniture(entries, InductionFurniture, "Induction hob", InductionPrice, keepOne: true, inductionHob);
            SetFurniture(entries, GrillFurniture, "Charcoal grill", GrillPrice, keepOne: false, grill);
            // The last board stays: a helper hired to prep needs one to cut on.
            SetFurniture(entries, BoardFurniture, "Cutting board", BoardPrice, keepOne: true, board);
            SetFurniture(entries, KnifeRackFurniture, "Knife rack", KnifeRackPrice, keepOne: false, knifeRack);
            SetFurniture(entries, "knife", "Knife", KnifePrice, keepOne: false, null, Game.Simulation.Items.Knife.KnifeTypeId);
            SetFurniture(entries, "pan", "Pan", PanPrice, keepOne: false, null, GreyboxKitchenware.PanTypeId);
            // Broken plates aren't replaced free any more (decision 2026-11-08): new ones join the clean pile.
            SetFurniture(entries, "plate", "Plate", PlatePrice, keepOne: false, null, Game.Simulation.Items.Plate.PlateTypeId);
            SetFurniture(entries, ShelfFurniture, "Shelf", ShelfPrice, keepOne: true, shelf);
            SetFurniture(entries, FridgeFurniture, "Fridge", FridgePrice, keepOne: true, fridge);
            SetFurniture(entries, TableFurniture, "Table", TablePrice, keepOne: false, table);
            SetFurniture(entries, BarFurniture, "Bar table", BarPrice, keepOne: false, bar);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return catalog;
        }

        // A regular table: a chair's place on each side, with a plate towards it. They start hidden;
        // the table shows the ones it has.
        private static TableStation BuildTable()
        {
            var table = CreateStation<TableStation>(
                "PF_Table", Vector3.zero, CounterSize, TableHeight, GreyboxAssets.EnsureMaterial("Table", TableColor), null, TableFurniture);
            DiningArt.DressTable(table.transform, bar: false);
            var directions = new[] { Vector3.back, Vector3.left, Vector3.right, Vector3.forward };
            AddChairs(table, TableHeight, directions, ChairDistance, TablePlateOffset);
            return table;
        }

        // A bar table: narrow, with one chair along its long side.
        private static TableStation BuildBarTable(string name, Vector3 position, Transform parent)
        {
            var table = CreateStation<TableStation>(
                name, position, CounterSize, BarHeight, GreyboxAssets.EnsureMaterial("BarTable", BarColor), parent, BarFurniture, BarDepth);
            var barTop = DiningArt.DressTable(table.transform, bar: true);
            if (barTop != null)
                AddToHighlight(table, barTop);
            AddChairs(table, BarHeight, new[] { Vector3.back }, BarDepth / 2f + BarChairGap, BarPlateOffset);
            return table;
        }

        private static void AddChairs(TableStation table, float height, Vector3[] directions, float seatDistance, float plateOffset)
        {
            var so = new SerializedObject(table);
            var chairs = so.FindProperty("_chairs");
            chairs.arraySize = directions.Length;
            for (var i = 0; i < directions.Length; i++)
            {
                var seat = new GameObject($"Seat_{i + 1}").transform;
                seat.SetParent(table.transform, false);
                seat.localPosition = directions[i] * seatDistance;
                var chair = GreyboxAssets.CreatePrimitive(PrimitiveType.Cylinder, "Chair", seat, GreyboxAssets.EnsureMaterial("Stool", ChairColor), keepCollider: false);
                chair.transform.localScale = ChairScale;
                chair.transform.localPosition = new Vector3(0f, ChairScale.y, 0f);
                // A chair facing the table, or a stool at the bar, the seat at the same height.
                DiningArt.DressChair(chair, stool: height > TableHeight, directions[i]);
                seat.gameObject.SetActive(i == 0);

                var plate = new GameObject($"Plate_{i + 1}").transform;
                plate.SetParent(table.transform, false);
                plate.localPosition = directions[i] * plateOffset + Vector3.up * height;

                var element = chairs.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_seat").objectReferenceValue = seat;
                element.FindPropertyRelative("_plateAnchor").objectReferenceValue = plate;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The same as the starting unit, without its morning delivery.
        private static StorageStation BuildStorageForSale(string name, GreyboxStorage.Unit unit)
        {
            var forSale = new GreyboxStorage.Unit
            {
                Name = unit.Name, FurnitureId = unit.FurnitureId, Position = Vector3.zero,
                Refrigerated = unit.Refrigerated, Color = unit.Color, SlotCapacity = unit.SlotCapacity,
            };
            var station = GreyboxStorage.Build(forSale, (stationName, position, material) =>
                CreateStation<StorageStation>(stationName, position, CounterSize, StorageArt.Height(unit.Refrigerated), material, null, unit.FurnitureId));
            station.name = name;
            return station;
        }

        private static GameObject SaveFurniture(Interactable station)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(station.gameObject, $"{FurniturePrefabFolder}/{station.name}.prefab");
            Object.DestroyImmediate(station.gameObject);
            return prefab;
        }

        private static void SetFurniture(SerializedProperty entries, string id, string displayName, float price, bool keepOne, GameObject prefab, string itemTypeId = "")
        {
            for (var i = 0; i < entries.arraySize; i++)
            {
                var existing = entries.GetArrayElementAtIndex(i);
                if (existing.FindPropertyRelative("_id").stringValue == id)
                {
                    existing.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
                    existing.FindPropertyRelative("_itemTypeId").stringValue = itemTypeId;
                    return;
                }
            }

            entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("_id").stringValue = id;
            entry.FindPropertyRelative("_displayName").stringValue = displayName;
            entry.FindPropertyRelative("_price").floatValue = price;
            entry.FindPropertyRelative("_keepOne").boolValue = keepOne;
            entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("_itemTypeId").stringValue = itemTypeId;
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
        internal static InputActionReference LoadActionReference(string mapAndAction)
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
