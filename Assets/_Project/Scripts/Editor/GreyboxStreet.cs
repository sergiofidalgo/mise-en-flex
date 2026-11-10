using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Items;
using Game.Gameplay.Stations;
using Game.Presentation;
using Game.Presentation.Office.Ambience;
using Game.Simulation.Items;
using Game.Simulation.Market;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The street outside the restaurant (Phase 5, milestone 3; the user's mockup, 2026-11-08, and
    /// decision 2026-11-10): out of the front door onto a brick pavement along a road with passing
    /// cars; to the left a paved plaza with market stalls under coloured canopies, a butcher and a
    /// greengrocer, a third stall closed for later; to the right a parking lot and one trash
    /// container against the side wall; behind, houses and trees as backdrop. Chefs walk out through
    /// a doorway in the wall wherever the street door stands. Greybox shapes, on the same prefabs
    /// and objects real models will replace.
    /// </summary>
    public static class GreyboxStreet
    {
        public const string MarketConfigPath = "Assets/_Project/Data/Settings/MarketConfig.asset";

        // The building's outer wall faces, from GreyboxKitchenBuilder (floor 8 m by 9 m, walls 0.2 m).
        private const float BuildingWest = -4.1f;
        private const float BuildingEast = 4.1f;
        private const float BuildingSouth = -7.1f;
        private const float BuildingNorth = 2.1f;

        // Where chefs can walk outside, and the road beyond the kerb. Kept in step with
        // FramingCamera's grounds, which the builder sets from these.
        private const float GroundsWest = -14f;
        private const float GroundsEast = 10f;
        private const float GroundsNorth = 3f;
        private const float Kerb = -10.3f;
        private const float RoadSouth = -17f;
        private const float PlazaEast = -4.6f;
        private const float ParkingWest = 4.6f;

        // The stalls stand on the plaza facing the pavement, so from the overhead camera the chef
        // buying stands in front of the counter, never under its canopy. The butcher is about 10 m
        // from the usual door: a few seconds each way at a walk, the cost of going out in service.
        private static readonly Vector2[] StallSpots = { new Vector2(-7.6f, -4.6f), new Vector2(-11.2f, -4.6f), new Vector2(-9.4f, -0.4f) };
        private static readonly Vector3 CounterSize = new Vector3(1.8f, 0.9f, 0.8f);
        private const float CanopyHeight = 2.3f;
        private const float StationBlockerHeight = 2.2f;

        private static readonly Color Grass = new Color(0.42f, 0.55f, 0.36f);
        private static readonly Color Paving = new Color(0.66f, 0.6f, 0.5f);
        private static readonly Color Brick = new Color(0.62f, 0.33f, 0.27f);
        private static readonly Color KerbStone = new Color(0.72f, 0.72f, 0.7f);
        private static readonly Color Asphalt = new Color(0.25f, 0.25f, 0.28f);
        private static readonly Color LaneLine = new Color(0.92f, 0.78f, 0.3f);
        private static readonly Color ParkingLine = new Color(0.9f, 0.9f, 0.88f);
        private static readonly Color StallCounter = new Color(0.86f, 0.84f, 0.8f);
        private static readonly Color Pole = new Color(0.55f, 0.55f, 0.58f);
        private static readonly Color Bin = new Color(0.2f, 0.36f, 0.3f);
        private static readonly Color Wood = new Color(0.45f, 0.32f, 0.22f);
        private static readonly Color CrateWood = new Color(0.72f, 0.56f, 0.36f);
        private static readonly Color[] Canopies = { new Color(0.3f, 0.45f, 0.75f), new Color(0.75f, 0.3f, 0.27f), new Color(0.38f, 0.6f, 0.35f) };
        private static readonly Color[] HouseWalls = { new Color(0.55f, 0.62f, 0.75f), new Color(0.6f, 0.62f, 0.64f), new Color(0.7f, 0.66f, 0.6f) };
        private static readonly Color[] CarPaints = { new Color(0.35f, 0.5f, 0.6f), new Color(0.85f, 0.8f, 0.72f), new Color(0.7f, 0.3f, 0.25f), new Color(0.3f, 0.32f, 0.38f) };

        // Crates: an open wooden box, six portions in two rows of three.
        private static readonly Vector3 CrateSize = new Vector3(0.62f, 0.2f, 0.42f);
        private const float CrateFloor = 0.05f;

        /// <summary>Builds the street and the doorway into the scene being built. Run after the walls and the street door exist.</summary>
        public static void Build(Camera camera)
        {
            var market = GreyboxAssets.EnsureAsset<MarketConfig>(MarketConfigPath);
            var street = new GameObject("Street").transform;

            Ground(street);
            Road(street);
            Plaza(street, market);
            Parking(street);
            Backdrop(street);
            Bounds(street);
            Doorway();

            if (camera != null && camera.TryGetComponent(out FramingCamera framing))
            {
                var so = new SerializedObject(framing);
                so.FindProperty("_groundsMin").vector2Value = new Vector2(GroundsWest, Kerb);
                so.FindProperty("_groundsMax").vector2Value = new Vector2(GroundsEast, GroundsNorth);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>The crate's prefab, for the item catalogue: an open box with six places for portions.</summary>
        public static GameObject BuildCrate()
        {
            var root = new GameObject($"PF_Item_{Crate.CrateTypeId}");
            var wood = Mat("Crate", CrateWood);
            Box(root.transform, "Floor", wood, new Vector3(0f, CrateFloor / 2f, 0f), new Vector3(CrateSize.x, CrateFloor, CrateSize.z));
            const float side = 0.03f;
            Box(root.transform, "SideN", wood, new Vector3(0f, CrateSize.y / 2f, CrateSize.z / 2f - side / 2f), new Vector3(CrateSize.x, CrateSize.y, side));
            Box(root.transform, "SideS", wood, new Vector3(0f, CrateSize.y / 2f, -CrateSize.z / 2f + side / 2f), new Vector3(CrateSize.x, CrateSize.y, side));
            Box(root.transform, "SideE", wood, new Vector3(CrateSize.x / 2f - side / 2f, CrateSize.y / 2f, 0f), new Vector3(side, CrateSize.y, CrateSize.z));
            Box(root.transform, "SideW", wood, new Vector3(-CrateSize.x / 2f + side / 2f, CrateSize.y / 2f, 0f), new Vector3(side, CrateSize.y, CrateSize.z));

            var slots = new Transform[6];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = new GameObject($"Slot_{i + 1}").transform;
                slots[i].SetParent(root.transform, false);
                slots[i].localPosition = new Vector3((i % 3 - 1) * 0.19f, CrateFloor, (i / 3 - 0.5f) * 0.19f);
                slots[i].localScale = Vector3.one * 0.7f; // portions packed in, smaller than loose
            }
            var view = new SerializedObject(root.AddComponent<ItemView>());
            var list = view.FindProperty("_slotAnchors");
            list.arraySize = slots.Length;
            for (var i = 0; i < slots.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            view.ApplyModifiedPropertiesWithoutUndo();

            const string folder = "Assets/_Project/Prefabs/Items";
            GreyboxAssets.EnsureFolder(folder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/PF_Item_{Crate.CrateTypeId}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Grass under everything, with the one collider the outside is walked on. Just below the
        // restaurant's floor, so it never shows through it.
        private static void Ground(Transform street)
        {
            var grass = Box(street, "Grass", Mat("StreetGrass", Grass), new Vector3(-2f, -0.03f, -4f), new Vector3(60f, 0.02f, 40f));
            grass.AddComponent<BoxCollider>();

            // The brick pavement along the front, from the plaza to the parking lot.
            Slab(street, "Pavement", Brick, GroundsWest, GroundsEast, Kerb, BuildingSouth);
            Box(street, "Kerb", Mat("StreetKerb", KerbStone), new Vector3((GroundsWest + GroundsEast) / 2f, 0.05f, Kerb), new Vector3(GroundsEast - GroundsWest + 20f, 0.1f, 0.2f));
        }

        // The road along the front, with cars passing both ways. Chefs can't step off the kerb.
        private static void Road(Transform street)
        {
            Slab(street, "Road", Asphalt, -30f, 30f, RoadSouth, Kerb - 0.1f);
            var line = Mat("StreetLaneLine", LaneLine);
            var middle = (Kerb + RoadSouth) / 2f;
            for (var x = -28f; x < 28f; x += 3f)
                Box(street, "LaneDash", line, new Vector3(x, 0.006f, middle), new Vector3(1.4f, 0.004f, 0.15f));

            var lanes = new[] { (middle + 1.6f, 1f), (middle - 1.6f, -1f) };
            for (var k = 0; k < 4; k++)
            {
                var (z, dir) = lanes[k % 2];
                var car = Car(street, CarPaints[k % CarPaints.Length]);
                car.gameObject.AddComponent<LaneMover>().Set(new Vector3(-dir * 30f, 0f, z), new Vector3(dir * 30f, 0f, z), 6f + k, false, 0f, k / 4f);
            }
        }

        private static Transform Car(Transform parent, Color paint)
        {
            var car = new GameObject("Car").transform;
            car.SetParent(parent, false);
            var body = Mat($"StreetCar_{ColorUtility.ToHtmlStringRGB(paint)}", paint);
            Box(car, "Body", body, new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.6f, 3.8f));
            Box(car, "Cabin", body, new Vector3(0f, 0.95f, -0.2f), new Vector3(1.5f, 0.45f, 2f));
            Box(car, "Glass", Mat("StreetCarGlass", new Color(0.2f, 0.25f, 0.35f)), new Vector3(0f, 0.97f, 0.82f), new Vector3(1.4f, 0.36f, 0.04f));
            return car;
        }

        // The plaza with the stalls, left of the restaurant.
        private static void Plaza(Transform street, MarketConfig market)
        {
            Slab(street, "Plaza", Paving, GroundsWest, PlazaEast, BuildingSouth, GroundsNorth);

            Stall(street, "Stall_Butcher", 0, market, new[] { new StallStation.Goods("Steak", Grade.Cheap), new StallStation.Goods("Steak", Grade.Premium) },
                new[] { new Color(0.72f, 0.25f, 0.25f), new Color(0.6f, 0.2f, 0.22f) });
            Stall(street, "Stall_Greengrocer", 1, market,
                new[]
                {
                    new StallStation.Goods("Onion", Grade.Cheap), new StallStation.Goods("Onion", Grade.Premium),
                    new StallStation.Goods("Tomato", Grade.Cheap), new StallStation.Goods("Tomato", Grade.Premium),
                },
                new[] { new Color(0.85f, 0.75f, 0.5f), new Color(0.82f, 0.3f, 0.22f), new Color(0.45f, 0.65f, 0.3f) });
            // The third canopy: up but closed, a place for a future cuisine's stall.
            Stall(street, "Stall_Closed", 2, null, null, null);
        }

        private static void Stall(Transform street, string name, int index, MarketConfig market, StallStation.Goods[] goods, Color[] produce)
        {
            var root = new GameObject(name).transform;
            root.SetParent(street, false);
            root.localPosition = new Vector3(StallSpots[index].x, 0f, StallSpots[index].y);
            root.localRotation = Quaternion.Euler(0f, 180f, 0f); // its front faces the pavement, south

            var counter = Box(root, "Body", Mat("StallCounter", StallCounter), new Vector3(0f, CounterSize.y / 2f, 0f), CounterSize);
            var pole = Mat("StallPole", Pole);
            var canopy = Mat($"StallCanopy_{index}", Canopies[index % Canopies.Length]);
            foreach (var x in new[] { -1f, 1f })
                foreach (var z in new[] { -0.7f, 0.7f })
                    Box(root, "Pole", pole, new Vector3(x * (CounterSize.x / 2f + 0.15f), CanopyHeight / 2f, z * 0.75f - 0.25f), new Vector3(0.06f, CanopyHeight, 0.06f));
            var roof = Box(root, "Canopy", canopy, new Vector3(0f, CanopyHeight + 0.15f, -0.25f), new Vector3(CounterSize.x + 0.7f, 0.08f, 1.7f));
            roof.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f); // tipped towards the front, like an awning

            if (goods == null)
            {
                // Closed: a cloth over the counter.
                Box(root, "Cover", canopy, new Vector3(0f, CounterSize.y + 0.03f, 0f), new Vector3(CounterSize.x + 0.04f, 0.06f, CounterSize.z + 0.04f));
                return;
            }

            // What it sells, laid out on the counter so it reads from above.
            for (var i = 0; i < produce.Length; i++)
            {
                var x = (i - (produce.Length - 1) / 2f) * 0.5f;
                Box(root, "Goods", Mat($"StallGoods_{ColorUtility.ToHtmlStringRGB(produce[i])}", produce[i]), new Vector3(x, CounterSize.y + 0.06f, 0f), new Vector3(0.38f, 0.12f, 0.3f));
            }

            var blocker = root.gameObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(CounterSize.x, StationBlockerHeight, CounterSize.z);
            blocker.center = new Vector3(0f, StationBlockerHeight / 2f, 0f);

            // Bought crates appear at the front edge of the counter and fly to the chef.
            var anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, CounterSize.y, CounterSize.z / 2f);

            var station = root.gameObject.AddComponent<StallStation>();
            var so = new SerializedObject(station);
            so.FindProperty("_itemAnchor").objectReferenceValue = anchor;
            so.FindProperty("_halfSize").vector2Value = new Vector2(CounterSize.x / 2f, CounterSize.z / 2f);
            so.FindProperty("_market").objectReferenceValue = market;
            var list = so.FindProperty("_goods");
            list.arraySize = goods.Length;
            for (var i = 0; i < goods.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_typeId").stringValue = goods[i].TypeId;
                element.FindPropertyRelative("_grade").enumValueIndex = (int)goods[i].Grade;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var highlight = new SerializedObject(root.gameObject.AddComponent<InteractableHighlightView>());
            highlight.FindProperty("_interactable").objectReferenceValue = station;
            highlight.FindProperty("_shakeTarget").objectReferenceValue = counter.transform;
            var renderers = highlight.FindProperty("_renderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = counter.GetComponent<Renderer>();
            highlight.ApplyModifiedPropertiesWithoutUndo();
        }

        // Right of the restaurant: the parking lot, the trash container against the side wall, and the sign.
        private static void Parking(Transform street)
        {
            Slab(street, "Parking", Asphalt, ParkingWest, GroundsEast, BuildingSouth, GroundsNorth);
            var line = Mat("StreetParkingLine", ParkingLine);
            for (var z = -6f; z <= 2f; z += 2.6f)
                Box(street, "ParkingLine", line, new Vector3((ParkingWest + 1.5f + GroundsEast) / 2f, 0.006f, z), new Vector3(GroundsEast - ParkingWest - 1.5f, 0.004f, 0.08f));
            var parked = Car(street, CarPaints[1]);
            parked.localPosition = new Vector3(8f, 0f, -4.7f);
            parked.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // The one trash container (the mockup's recycling area is too much, the user's note).
            var bin = Box(street, "TrashContainer", Mat("StreetTrash", Bin), new Vector3(BuildingEast + 0.75f, 0.65f, -1.2f), new Vector3(1.2f, 1.3f, 1.8f));
            bin.AddComponent<BoxCollider>();
            Box(bin.transform, "Lid", Mat("StreetTrashLid", new Color(0.15f, 0.27f, 0.22f)), new Vector3(0f, 0.52f, 0f), new Vector3(1.04f, 0.06f, 1.04f));

            // The restaurant's tall sign at the front corner.
            Box(street, "SignPole", Mat("StreetSignPole", new Color(0.18f, 0.16f, 0.15f)), new Vector3(BuildingEast + 0.8f, 1.8f, BuildingSouth - 1.2f), new Vector3(0.14f, 3.6f, 0.14f))
                .AddComponent<BoxCollider>();
            Box(street, "SignBoard", Mat("StreetSignBoard", new Color(0.95f, 0.9f, 0.8f)), new Vector3(BuildingEast + 0.8f, 3.6f, BuildingSouth - 1.2f), new Vector3(1.8f, 1f, 0.12f));
            Box(street, "SignMark", Mat("StreetSignMark", new Color(0.5f, 0.22f, 0.15f)), new Vector3(BuildingEast + 0.8f, 3.6f, BuildingSouth - 1.27f), new Vector3(0.7f, 0.5f, 0.02f));
        }

        // Behind the restaurant: a fence, houses and trees. Backdrop only.
        private static void Backdrop(Transform street)
        {
            Box(street, "BackFence", Mat("StreetFence", Wood), new Vector3((GroundsWest + GroundsEast) / 2f, 0.5f, GroundsNorth + 0.2f), new Vector3(GroundsEast - GroundsWest, 1f, 0.08f));
            for (var i = 0; i < 4; i++)
            {
                var x = -12f + i * 7.5f;
                var walls = Mat($"StreetHouse_{i % HouseWalls.Length}", HouseWalls[i % HouseWalls.Length]);
                Box(street, "House", walls, new Vector3(x, 1.6f, 9f), new Vector3(5.5f, 3.2f, 4.5f));
                Box(street, "Roof", Mat("StreetRoof", new Color(0.36f, 0.3f, 0.3f)), new Vector3(x, 3.4f, 9f), new Vector3(5.9f, 0.4f, 4.9f));
            }
            var leaves = Mat("StreetLeaves", new Color(0.3f, 0.45f, 0.3f));
            var trunk = Mat("StreetTrunk", Wood);
            foreach (var x in new[] { -15.5f, -6f, 5f, 12f })
            {
                Box(street, "Trunk", trunk, new Vector3(x, 0.9f, 5.5f), new Vector3(0.25f, 1.8f, 0.25f));
                var crown = GreyboxAssets.CreatePrimitive(PrimitiveType.Sphere, "Crown", street, leaves, keepCollider: false);
                crown.transform.localPosition = new Vector3(x, 2.4f, 5.5f);
                crown.transform.localScale = Vector3.one * 2f;
            }
        }

        // Invisible edges to where chefs can walk: the kerb, the plaza's and the lot's far sides, the back fence.
        private static void Bounds(Transform street)
        {
            var edges = new GameObject("StreetBounds").transform;
            edges.SetParent(street, false);
            Edge(edges, "Kerb", new Vector3((GroundsWest + GroundsEast) / 2f, 1f, Kerb - 0.2f), new Vector3(GroundsEast - GroundsWest + 2f, 2f, 0.4f));
            Edge(edges, "West", new Vector3(GroundsWest - 0.2f, 1f, (Kerb + GroundsNorth) / 2f), new Vector3(0.4f, 2f, GroundsNorth - Kerb + 2f));
            Edge(edges, "East", new Vector3(GroundsEast + 0.2f, 1f, (Kerb + GroundsNorth) / 2f), new Vector3(0.4f, 2f, GroundsNorth - Kerb + 2f));
            Edge(edges, "North", new Vector3((GroundsWest + GroundsEast) / 2f, 1f, GroundsNorth + 0.2f), new Vector3(GroundsEast - GroundsWest + 2f, 2f, 0.4f));

            static void Edge(Transform parent, string name, Vector3 centre, Vector3 size)
            {
                var edge = new GameObject(name).transform;
                edge.SetParent(parent, false);
                edge.localPosition = centre;
                edge.gameObject.AddComponent<BoxCollider>().size = size;
            }
        }

        // The opening in the outer wall where the street door stands.
        private static void Doorway()
        {
            var walls = GameObject.Find("Walls");
            var door = Object.FindFirstObjectByType<StreetDoor>();
            if (walls == null || door == null)
            {
                Debug.LogError("[GreyboxStreet] The walls or the street door are missing; no doorway.");
                return;
            }
            var doorway = new SerializedObject(walls.AddComponent<StreetDoorway>());
            doorway.FindProperty("_door").objectReferenceValue = door;
            var outer = doorway.FindProperty("_walls");
            var names = new[] { "Wall_South", "Wall_East", "Wall_West" };
            outer.arraySize = names.Length;
            for (var i = 0; i < names.Length; i++)
                outer.GetArrayElementAtIndex(i).objectReferenceValue = walls.transform.Find(names[i]);
            var hung = doorway.FindProperty("_onWalls");
            var pictures = new[] { "Picture_West", "Picture_East" };
            hung.arraySize = pictures.Length;
            for (var i = 0; i < pictures.Length; i++)
                hung.GetArrayElementAtIndex(i).objectReferenceValue = walls.transform.Find(pictures[i]);
            doorway.ApplyModifiedPropertiesWithoutUndo();
        }

        // A flat piece of ground between two x and two z, its top just above the grass.
        private static void Slab(Transform parent, string name, Color color, float west, float east, float south, float north) =>
            Box(parent, name, Mat($"Street{name}", color), new Vector3((west + east) / 2f, -0.01f, (south + north) / 2f), new Vector3(east - west, 0.02f, north - south));

        private static GameObject Box(Transform parent, string name, Material material, Vector3 position, Vector3 size)
        {
            var box = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, name, parent, material, keepCollider: false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            return box;
        }

        private static Material Mat(string name, Color color) => GreyboxAssets.EnsureMaterial(name, color);
    }
}
