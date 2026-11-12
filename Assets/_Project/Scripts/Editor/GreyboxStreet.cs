using Game.Data;
using Game.Gameplay;
using Game.Gameplay.Stations;
using Game.Presentation;
using Game.Presentation.Office.Ambience;
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
    public static partial class GreyboxStreet
    {

        // The building's outer wall faces, from the restaurant's KitchenLayout (Phase 6, milestone 3).
        // Everything outside is placed from them, so a wider or deeper building gets the same street
        // around it; the grill's (8 m by 9 m, walls 0.2 m: x ±4.1, z −7.1…2.1) gives the original values.
        private static float BuildingWest = -4.1f;
        private static float BuildingEast = 4.1f;
        private static float BuildingSouth = -7.1f;
        private static float BuildingNorth = 2.1f;

        // Where chefs can walk outside, and the road beyond the kerb. Kept in step with
        // FramingCamera's grounds, which the builder sets from these.
        private static float GroundsWest => BuildingWest - 9.9f;
        private static float GroundsEast => BuildingEast + 5.9f;
        private static float GroundsNorth => BuildingNorth + 0.9f;
        private static float Kerb => BuildingSouth - 3.2f;
        private static float RoadSouth => Kerb - 6.7f;
        // How far the road, the pavement and the neighbours run either way: past anything the camera shows.
        private static float StreetEnd => Mathf.Max(-GroundsWest, GroundsEast) + 22f;
        private static float PlazaEast => BuildingWest - 0.5f;
        private static float ParkingWest => BuildingEast + 0.5f;

        // The stalls stand on the plaza facing the pavement, so from the overhead camera the chef
        // buying stands in front of the counter, never under its canopy. The butcher is about 10 m
        // from the usual door: a few seconds each way at a walk, the cost of going out in service.
        private static Vector2[] StallSpots => new[]
        {
            new Vector2(BuildingWest - 3.5f, BuildingSouth + 2.5f), new Vector2(BuildingWest - 7.1f, BuildingSouth + 2.5f),
            new Vector2(BuildingWest - 5.3f, BuildingNorth - 2.5f),
        };

        // What each vendor a restaurant's street can have sells, and the produce shown on its counter.
        // A vendor not listed here (the fishmonger, until fish arrives) stands closed.
        private static readonly System.Collections.Generic.Dictionary<string, (string[] Goods, Color[] Produce)> Vendors =
            new System.Collections.Generic.Dictionary<string, (string[], Color[])>
            {
                ["Butcher"] = (new[] { "Steak" }, new[] { new Color(0.72f, 0.25f, 0.25f), new Color(0.6f, 0.2f, 0.22f) }),
                ["Greengrocer"] = (new[] { "Onion", "Tomato" },
                    new[] { new Color(0.85f, 0.75f, 0.5f), new Color(0.82f, 0.3f, 0.22f), new Color(0.45f, 0.65f, 0.3f) }),
            };
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
        private static Color[] Canopies = { new Color(0.3f, 0.45f, 0.75f), new Color(0.75f, 0.3f, 0.27f), new Color(0.38f, 0.6f, 0.35f) };
        private static readonly Color[] HouseWalls = { new Color(0.55f, 0.62f, 0.75f), new Color(0.6f, 0.62f, 0.64f), new Color(0.7f, 0.66f, 0.6f) };
        private static readonly Color[] CarPaints = { new Color(0.35f, 0.5f, 0.6f), new Color(0.85f, 0.8f, 0.72f), new Color(0.7f, 0.3f, 0.25f), new Color(0.3f, 0.32f, 0.38f) };

        /// <summary>Builds the street and the doorway into the scene being built. Run after the walls and the street door exist.</summary>
        public static void Build(Camera camera, KitchenLayout layout, KitchenCompositionRoot root)
        {
            BuildingWest = layout.BuildingWest;
            BuildingEast = layout.BuildingEast;
            BuildingSouth = layout.BuildingSouth;
            BuildingNorth = layout.BuildingNorth;
            Canopies = layout.Canopies;

            var street = new GameObject("Street").transform;
            // The same cars and neighbours every time this restaurant's street is built.
            OfficeBuilder.StreetSeed(2611_11 + Mathf.RoundToInt(BuildingEast * 10f) + Mathf.RoundToInt(BuildingNorth * 100f));

            Ground(street);
            Road(street);
            Plaza(street, layout.Vendors);
            Parking(street, root);
            Backdrop(street);
            Bounds(street);
            Doorway();
            CustomerStreet(street);
            CourierRoad(street);
            Life(street, root);

            // The light of the day follows the service's clock (DaylightView): white by day, blue
            // by night outdoors, the restaurant's own light inside its walls.
            var daylight = new SerializedObject(street.gameObject.AddComponent<Game.Presentation.Street.DaylightView>());
            daylight.FindProperty("_root").objectReferenceValue = root;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                    daylight.FindProperty("_sun").objectReferenceValue = light;
            daylight.FindProperty("_buildingMin").vector2Value = new Vector2(BuildingWest, BuildingSouth);
            daylight.FindProperty("_buildingMax").vector2Value = new Vector2(BuildingEast, BuildingNorth);
            daylight.ApplyModifiedPropertiesWithoutUndo();

            if (camera != null && camera.TryGetComponent(out FramingCamera framing))
            {
                var so = new SerializedObject(framing);
                so.FindProperty("_groundsMin").vector2Value = new Vector2(GroundsWest, Kerb);
                so.FindProperty("_groundsMax").vector2Value = new Vector2(GroundsEast, GroundsNorth);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Grass under everything, with the one collider the outside is walked on. Just below the
        // restaurant's floor, so it never shows through it.
        private static void Ground(Transform street)
        {
            // 60 m by 40 m around the grill's building, and as much bigger as a bigger building is.
            // Wide enough to lie under the whole street, the neighbours and the park across the road.
            var extraDepth = BuildingNorth - BuildingSouth - 9.2f;
            var grass = Box(street, "Grass", Surfaces.Grass(Grass),
                new Vector3(0f, -0.03f, (BuildingSouth + BuildingNorth) / 2f - 2f),
                new Vector3(StreetEnd * 2f + 10f, 0.02f, 58f + extraDepth));
            grass.AddComponent<BoxCollider>();

            // The brick pavement along the front, from the plaza to the parking lot.
            // It runs on past both ends with the road, in front of the neighbours' shops.
            Slab(street, "Pavement", Surfaces.BrickPaving(Brick), -StreetEnd, StreetEnd, Kerb, BuildingSouth);
            // The kerb, dropped across the mouth of the parking lot's driveway.
            var kerb = Mat("StreetKerb", KerbStone);
            Box(street, "Kerb", kerb, new Vector3((-StreetEnd + DriveWest) / 2f, 0.05f, Kerb), new Vector3(DriveWest + StreetEnd, 0.1f, 0.2f));
            Box(street, "Kerb", kerb, new Vector3((DriveEast + StreetEnd) / 2f, 0.05f, Kerb), new Vector3(StreetEnd - DriveEast, 0.1f, 0.2f));
            Box(street, "DroppedKerb", kerb, new Vector3((DriveWest + DriveEast) / 2f, 0.012f, Kerb), new Vector3(DriveEast - DriveWest, 0.024f, 0.2f));
        }

        // The road along the front, with cars passing both ways. Chefs can't step off the kerb.
        private static void Road(Transform street)
        {
            Slab(street, "Road", Surfaces.Asphalt("StreetRoad", Asphalt), -StreetEnd, StreetEnd, RoadSouth, Kerb - 0.1f);
            var line = Mat("StreetLaneLine", LaneLine);
            var middle = (Kerb + RoadSouth) / 2f;
            for (var x = -StreetEnd + 2f; x < StreetEnd - 2f; x += 3f)
                Box(street, "LaneDash", line, new Vector3(x, 0.006f, middle), new Vector3(1.4f, 0.004f, 0.15f));

            // Keep right: the lane by the restaurant's kerb drives west, the far one east.
            var lanes = new[] { (middle + 1.6f, -1f), (middle - 1.6f, 1f) };
            for (var k = 0; k < 5; k++)
            {
                var (z, dir) = lanes[k % 2];
                var car = Car(street, CarPaints[k % CarPaints.Length]);
                car.gameObject.AddComponent<LaneMover>().Set(new Vector3(-dir * StreetEnd, 0f, z), new Vector3(dir * StreetEnd, 0f, z), 5.5f + k * 0.8f, false, 0f, k / 5f + 0.07f * (k % 2));
            }
        }

        private static Transform Car(Transform parent, Color paint)
        {
            // The city's modelled cars (the user's note, 2026-11-11: the box cars were underwhelming):
            // sedans, hatchbacks, taxis and vans. The boxes stand in without the models.
            if (Kit.Available)
            {
                var modelled = OfficeBuilder.StreetCar(parent);
                if (modelled.GetComponentInChildren<Renderer>() != null)
                    return modelled;
                Object.DestroyImmediate(modelled.gameObject);
            }
            var car = new GameObject("Car").transform;
            car.SetParent(parent, false);
            var body = Mat($"StreetCar_{ColorUtility.ToHtmlStringRGB(paint)}", paint);
            Box(car, "Body", body, new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.6f, 3.8f));
            Box(car, "Cabin", body, new Vector3(0f, 0.95f, -0.2f), new Vector3(1.5f, 0.45f, 2f));
            Box(car, "Glass", Mat("StreetCarGlass", new Color(0.2f, 0.25f, 0.35f)), new Vector3(0f, 0.97f, 0.82f), new Vector3(1.4f, 0.36f, 0.04f));
            return car;
        }

        // The plaza with the stalls, left of the restaurant.
        // The restaurant's vendors in its definition's order, one per stall spot. A vendor with nothing
        // to sell yet stands closed under its own name; a spot with no vendor is closed, a place for a
        // future cuisine's stall.
        private static void Plaza(Transform street, string[] vendors)
        {
            Slab(street, "Plaza", Surfaces.Slabs("StreetPlaza", Paving, 0.6f), GroundsWest, PlazaEast, BuildingSouth, GroundsNorth);

            for (var i = 0; i < StallSpots.Length; i++)
            {
                var vendor = vendors != null && i < vendors.Length ? vendors[i] : null;
                if (vendor != null && Vendors.TryGetValue(vendor, out var stock))
                    Stall(street, $"Stall_{vendor}", i, stock.Goods, stock.Produce);
                else
                    Stall(street, vendor != null ? $"Stall_{vendor}" : "Stall_Closed", i, null, null);
            }
        }

        private static void Stall(Transform street, string name, int index, string[] goods, Color[] produce)
        {
            var root = new GameObject(name).transform;
            root.SetParent(street, false);
            root.localPosition = new Vector3(StallSpots[index].x, 0f, StallSpots[index].y);
            root.localRotation = Quaternion.Euler(0f, 180f, 0f); // its front faces the pavement, south

            // The modelled stall (Kit.cs; the user's note, 2026-11-10: improve the vendor stalls):
            // a panelled counter under a striped canopy, a crate of each thing it sells, scales, a
            // slate, bulbs. Without the kit's model, the greybox boxes stand in.
            GameObject counter;
            if (Kit.Available)
            {
                counter = Kit.StallParts(root, Canopies[index % Canopies.Length], produce, slabs: name.Contains("Butcher"), closed: goods == null);
                foreach (var part in root.GetComponentsInChildren<Transform>())
                    part.gameObject.isStatic = false; // the counter shakes when a chef can't buy
            }
            else
            {
                counter = Box(root, "Body", Mat("StallCounter", StallCounter), new Vector3(0f, CounterSize.y / 2f, 0f), CounterSize);
                var pole = Mat("StallPole", Pole);
                var canopy = Mat($"StallCanopy_{index}", Canopies[index % Canopies.Length]);
                foreach (var x in new[] { -1f, 1f })
                    foreach (var z in new[] { -0.7f, 0.7f })
                        Box(root, "Pole", pole, new Vector3(x * (CounterSize.x / 2f + 0.15f), CanopyHeight / 2f, z * 0.75f - 0.25f), new Vector3(0.06f, CanopyHeight, 0.06f));
                var roof = Box(root, "Canopy", canopy, new Vector3(0f, CanopyHeight + 0.15f, -0.25f), new Vector3(CounterSize.x + 0.7f, 0.08f, 1.7f));
                roof.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f); // tipped towards the front, like an awning
                if (goods == null)
                    Box(root, "Cover", canopy, new Vector3(0f, CounterSize.y + 0.03f, 0f), new Vector3(CounterSize.x + 0.04f, 0.06f, CounterSize.z + 0.04f));
                else
                    for (var i = 0; i < produce.Length; i++)
                    {
                        var x = (i - (produce.Length - 1) / 2f) * 0.5f;
                        Box(root, "Goods", Mat($"StallGoods_{ColorUtility.ToHtmlStringRGB(produce[i])}", produce[i]), new Vector3(x, CounterSize.y + 0.06f, 0f), new Vector3(0.38f, 0.12f, 0.3f));
                    }
            }
            if (goods == null)
                return; // closed

            var blocker = root.gameObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(CounterSize.x, StationBlockerHeight, CounterSize.z);
            blocker.center = new Vector3(0f, StationBlockerHeight / 2f, 0f);

            // The front edge of the counter: where its prompts and highlight sit.
            var anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, CounterSize.y, CounterSize.z / 2f);

            var station = root.gameObject.AddComponent<StallStation>();
            var so = new SerializedObject(station);
            so.FindProperty("_itemAnchor").objectReferenceValue = anchor;
            so.FindProperty("_halfSize").vector2Value = new Vector2(CounterSize.x / 2f, CounterSize.z / 2f);
            var list = so.FindProperty("_goods");
            list.arraySize = goods.Length;
            for (var i = 0; i < goods.Length; i++)
                list.GetArrayElementAtIndex(i).stringValue = goods[i];
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
        private static void Parking(Transform street, KitchenCompositionRoot root)
        {
            Slab(street, "Parking", Surfaces.Asphalt("StreetParking", Asphalt * 1.12f), ParkingWest, GroundsEast, BuildingSouth, GroundsNorth);
            Driveway(street);
            var line = Mat("StreetParkingLine", ParkingLine);
            for (var z = BuildingSouth + 1.1f; z <= BuildingNorth - 0.1f; z += 2.6f)
                Box(street, "ParkingLine", line, new Vector3((ParkingWest + 1.5f + GroundsEast) / 2f, 0.006f, z), new Vector3(GroundsEast - ParkingWest - 1.5f, 0.004f, 0.08f));
            // Two cars in the bays (each 2.6 m wide), nose in from the lot's aisle; a bay left between them.
            // The bay by the driveway stays empty, or nobody could drive in.
            foreach (var bay in new[] { 1, 2 })
            {
                var parked = Car(street, CarPaints[bay]);
                parked.localPosition = new Vector3(GroundsEast - 2.3f, 0f, BuildingSouth + 2.4f + bay * 2.6f);
                parked.localRotation = Quaternion.Euler(0f, 90f, 0f);
                // Solid: a chef walks round a parked car, not through it.
                var body = parked.gameObject.AddComponent<BoxCollider>();
                body.center = new Vector3(0f, 0.9f, 0f);
                body.size = new Vector3(2.1f, 1.8f, 4.4f); // the cars are up to 2.1 m across their tyres, 4.25 m long
            }

            // The one trash container (the mockup's recycling area is too much, the user's note), on the
            // plaza's side of the building, level with the stall at the back (the user's note, 2026-11-12).
            TrashContainer(street, new Vector3(BuildingWest - 0.75f, 0f, StallSpots[2].y));

            // The restaurant's tall sign at the front corner, far enough out that the wheelie bin
            // clears its pole as it rolls round the corner (BinTrack: its middle passes about 0.65 m
            // out from the corner, and it reaches about 1.1 m round that).
            if (Outside.Available)
                Outside.Sign(street, new Vector3(SignX, 0f, SignZ), SignPostsApart);
            else
            {
                Box(street, "SignPole", Mat("StreetSignPole", new Color(0.18f, 0.16f, 0.15f)), new Vector3(SignX, 1.8f, SignZ), new Vector3(0.14f, 3.6f, 0.14f))
                    .AddComponent<BoxCollider>();
                Box(street, "SignBoard", Mat("StreetSignBoard", new Color(0.95f, 0.9f, 0.8f)), new Vector3(SignX, 3.6f, SignZ), new Vector3(1.8f, 1f, 0.12f));
            }
            // A rule between the restaurant's name and its style, which RestaurantSignView letters in
            // (Phase 6, milestone 3: each restaurant's own name on its sign).
            Box(street, "SignMark", Mat("StreetSignMark", new Color(0.5f, 0.22f, 0.15f)), new Vector3(SignX, 3.58f, SignZ - 0.07f), new Vector3(1.4f, 0.03f, 0.02f));
            SignLettering(street, root, new Vector3(SignX, 3.6f, SignZ - 0.08f));
        }

        // The way in for cars (the user's note, 2026-11-12: the lot wasn't "technically" reachable
        // by car): asphalt across the pavement from the dropped kerb to the lot, between the
        // restaurant and the next block, with a zebra crossing where the pavement carries on over it.
        private static void Driveway(Transform street)
        {
            var tar = Surfaces.Asphalt("StreetParking", Asphalt * 1.12f);
            Box(street, "Driveway", tar, new Vector3((DriveWest + DriveEast) / 2f, 0.001f, (Kerb + BuildingSouth) / 2f + 0.05f),
                new Vector3(DriveEast - DriveWest, 0.006f, BuildingSouth - Kerb + 0.1f));
            // Stripes along the way the cars go, across the whole driveway, in the band people walk.
            var paint = Mat("StreetParkingLine", ParkingLine);
            var south = Kerb + 0.55f;
            var north = BuildingSouth - 0.45f;
            const float stripe = 0.45f;
            var count = Mathf.FloorToInt((DriveEast - DriveWest + stripe) / (stripe * 2f));
            var first = (DriveWest + DriveEast) / 2f - (count - 1) * stripe;
            for (var k = 0; k < count; k++)
                Box(street, "ZebraStripe", paint, new Vector3(first + k * stripe * 2f, 0.006f, (south + north) / 2f), new Vector3(stripe, 0.004f, north - south));
        }

        // The communal wheelie bin against the side wall (SM_WheelieBin; the user's reference photo,
        // 2026-11-11), long side along the wall and its front to the parking lot. 1.2 m by 1.8 m on
        // the ground like the box it replaced, which stays as the fallback without the model. It
        // rolls round the outside walls in rearranging (WheelieBin, decisions 2026-11-11), so
        // nothing on it is static.
        private const float BinWallGap = 0.15f;
        // The north end of the east wall is the outdoor unit's (GreyboxStreet.Life.cs): the bin stops short of it.
        private const float OutdoorUnitKept = 1.35f;
        private static readonly Vector2 BinSize = new Vector2(1.8f, 1.2f); // along the wall, out from it
        private const float BinHeight = 1.34f;
        private const float BinColliderHeight = 1.8f;
        // The middle of the sign, whose two posts stand 1.03 m either side. It straddles the building's
        // plaza-side front corner (the user's notes, 2026-11-12: on the left, then closer to the
        // restaurant): one post in front of the wall, one by the plaza, both 1.7 m out, beyond the
        // queue and the bin against the wall (which reaches 1.35 m) and just outside the bin's sweep
        // round the corner (1.84 m from a point 0.25 m inside it; the nearer post is 1.96 m away).
        private static float SignX => BuildingWest - 0.6f;
        // The driveway from the road into the parking lot, across the pavement.
        private static float DriveWest => ParkingWest + 0.7f;
        private static float DriveEast => GroundsEast - 0.3f;
        private const float SignPostsApart = 2.06f;
        private static float SignZ => BuildingSouth - 1.7f;
        private const float BinWheelRadius = 0.12f;
        // Where SM_WheelieBin's castors stand, in the bin's own axes (the Blender script's
        // WHEELIE_BIN_WHEELS, turned into Unity's: x = -x, z = -y).
        private static readonly Vector2[] BinWheels = { new Vector2(0.6f, 0.3f), new Vector2(0.6f, -0.3f), new Vector2(-0.6f, 0.3f), new Vector2(-0.6f, -0.3f) };

        private static void TrashContainer(Transform street, Vector3 at)
        {
            const string model = "SM_WheelieBin";
            // The name GuestSurroundings finds it by, for the smell at the door.
            var bin = new GameObject("TrashContainer");
            bin.transform.SetParent(street, false);
            bin.transform.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, 270f, 0f)); // front (+z) west, away from the wall
            // Taller than the bin, as stations' are, so no chef can climb onto it.
            var collider = bin.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, BinColliderHeight / 2f, 0f);
            collider.size = new Vector3(BinSize.x, BinColliderHeight, BinSize.y);
            var anchor = new GameObject("ItemAnchor").transform; // on the lid; nothing is put there yet
            anchor.SetParent(bin.transform, false);
            anchor.localPosition = new Vector3(0f, BinHeight, 0f);
            // Flies over the lid: it smells, and guests by the door notice (decision 2026-11-11).
            var flies = new SerializedObject(bin.AddComponent<Game.Presentation.BinFlies>());
            flies.FindProperty("_material").objectReferenceValue = Kit.Toon("BinFly", new Color(0.09f, 0.09f, 0.1f));
            flies.ApplyModifiedPropertiesWithoutUndo();

            var parts = new GameObject("Model").transform;
            parts.SetParent(bin.transform, false);
            var wheels = new System.Collections.Generic.List<Transform>();
            if (PilotArt.FindMesh(model, "WheelieBinBody") == null)
            {
                Box(parts, "Body", Mat("StreetTrash", Bin), new Vector3(0f, 0.65f, 0f), new Vector3(BinSize.x, 1.3f, BinSize.y));
                Box(parts, "Lid", Mat("StreetTrashLid", new Color(0.15f, 0.27f, 0.22f)), new Vector3(0f, 1.32f, 0f), new Vector3(BinSize.x + 0.07f, 0.08f, BinSize.y + 0.05f));
            }
            else
            {
                Kit.Part(parts, model, "WheelieBinBody", Kit.Toon("WheelieBinGreen", new Color(0.3f, 0.5f, 0.32f)));
                Kit.Part(parts, model, "WheelieBinLid", Kit.Toon("WheelieBinLid", new Color(0.36f, 0.57f, 0.37f)));
                Kit.Part(parts, model, "WheelieBinLidPanels", Kit.Toon("WheelieBinLidPanel", new Color(0.27f, 0.45f, 0.29f)));
                Kit.Part(parts, model, "WheelieBinTrim", Kit.Toon("WheelieBinTrim", new Color(0.12f, 0.12f, 0.13f)));
                Kit.Part(parts, model, "WheelieBinBrackets", Kit.Toon("KitSteel", new Color(0.72f, 0.73f, 0.75f)));
                // Each wheel on its own, turning about its axle (local z) as the bin rolls.
                for (var i = 0; i < BinWheels.Length; i++)
                {
                    var wheel = new GameObject($"Wheel_{i}").transform;
                    wheel.SetParent(parts, false);
                    wheel.localPosition = new Vector3(BinWheels[i].x, BinWheelRadius, BinWheels[i].y);
                    Kit.Part(wheel, model, "WheelieBinWheel", Kit.Toon("WheelieBinTyre", new Color(0.09f, 0.09f, 0.1f)));
                    Kit.Part(wheel, model, "WheelieBinHub", Kit.Toon("KitSteel", new Color(0.72f, 0.73f, 0.75f)));
                    wheels.Add(wheel);
                }
            }
            foreach (var part in bin.GetComponentsInChildren<Transform>())
                part.gameObject.isStatic = false; // it moves: static batching would leave it behind

            var binView = bin.AddComponent<WheelieBin>();
            var so = new SerializedObject(binView);
            so.FindProperty("_halfSize").vector2Value = BinSize / 2f;
            so.FindProperty("_itemAnchor").objectReferenceValue = anchor;
            so.FindProperty("_buildingMin").vector2Value = new Vector2(BuildingWest, BuildingSouth);
            so.FindProperty("_buildingMax").vector2Value = new Vector2(BuildingEast, BuildingNorth);
            so.FindProperty("_wallGap").floatValue = BinWallGap;
            so.FindProperty("_eastEndKept").floatValue = OutdoorUnitKept;
            so.FindProperty("_size").vector2Value = BinSize;
            so.FindProperty("_wheelRadius").floatValue = BinWheelRadius;
            var wheelList = so.FindProperty("_wheels");
            wheelList.arraySize = wheels.Count;
            for (var i = 0; i < wheels.Count; i++)
                wheelList.GetArrayElementAtIndex(i).objectReferenceValue = wheels[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Lit up under a cursor and red where it can't go, like furniture.
            var highlight = new SerializedObject(bin.AddComponent<InteractableHighlightView>());
            highlight.FindProperty("_interactable").objectReferenceValue = binView;
            highlight.FindProperty("_shakeTarget").objectReferenceValue = parts;
            var renderers = bin.GetComponentsInChildren<Renderer>();
            var list = highlight.FindProperty("_renderers");
            list.arraySize = renderers.Length;
            for (var i = 0; i < renderers.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            highlight.ApplyModifiedPropertiesWithoutUndo();

            var editor = Object.FindFirstObjectByType<LayoutEditor>();
            if (editor == null)
            {
                Debug.LogWarning("[GreyboxStreet] No layout editor yet: the wheelie bin can't be moved.");
                return;
            }
            var wiring = new SerializedObject(editor);
            wiring.FindProperty("_bin").objectReferenceValue = binView;
            wiring.ApplyModifiedPropertiesWithoutUndo();
        }

        // The name in large letters above the rule, the style in small ones below it, facing the
        // street (and the camera). Written at runtime from the restaurant being played.
        private static void SignLettering(Transform street, KitchenCompositionRoot root, Vector3 face)
        {
            var sign = new GameObject("SignLettering").transform;
            sign.SetParent(street, false);
            sign.localPosition = face;
            TMPro.TextMeshPro Line(string name, float y, float height, string font, Color colour)
            {
                var label = new GameObject(name).AddComponent<TMPro.TextMeshPro>();
                label.transform.SetParent(sign, false);
                label.transform.localPosition = new Vector3(0f, y, 0f);
                label.font = MenuArt.Font(font);
                label.color = colour;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.5f;
                label.fontSizeMax = 4f;
                label.rectTransform.sizeDelta = new Vector2(1.6f, height);
                return label;
            }
            var view = new SerializedObject(sign.gameObject.AddComponent<Game.Presentation.RestaurantSignView>());
            view.FindProperty("_root").objectReferenceValue = root;
            view.FindProperty("_name").objectReferenceValue = Line("Name", 0.2f, 0.4f, "Inter-Bold", new Color(0.5f, 0.22f, 0.15f));
            view.FindProperty("_style").objectReferenceValue = Line("Style", -0.24f, 0.24f, "Inter-Bold", new Color(0.3f, 0.26f, 0.22f));
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        // The houses behind: one every 7.5 m along the back, their fronts to the restaurant.
        private static readonly Vector2 HouseSize = new Vector2(5.5f, 4.5f); // along the street, deep
        // Far enough back for a lane behind the restaurant's fence and a front garden before each door.
        private static float HouseZ => BuildingNorth + 8.4f;
        private static float BackLaneSouth => GroundsNorth + 1.6f;
        private static float BackLaneNorth => GroundsNorth + 3.2f;
        private static float GardenTreeZ => BackLaneNorth + 1.1f;
        private static int HouseCount => Mathf.FloorToInt((GroundsEast + 0.5f - (GroundsWest + 2f)) / 7.5f) + 1;
        private static float HouseX(int i) => GroundsWest + 2f + i * 7.5f;
        // Each modelled house's window panes, for the lamps' view to light at dusk (GreyboxStreet.Life.cs).
        private static readonly System.Collections.Generic.List<Renderer> HouseGlass = new System.Collections.Generic.List<Renderer>();

        // Behind the restaurant: a fence, houses and trees. Backdrop only.
        private static void Backdrop(Transform street)
        {
            Box(street, "BackFence", Mat("StreetFence", Wood), new Vector3((GroundsWest + GroundsEast) / 2f, 0.5f, GroundsNorth + 0.2f), new Vector3(GroundsEast - GroundsWest, 1f, 0.08f));
            HouseGlass.Clear();
            for (var i = 0; i < HouseCount; i++)
            {
                var x = HouseX(i);
                if (Outside.Available)
                {
                    HouseGlass.Add(Outside.House(street, new Vector3(x, 0f, HouseZ), i));
                    continue;
                }
                var walls = Mat($"StreetHouse_{i % HouseWalls.Length}", HouseWalls[i % HouseWalls.Length]);
                Box(street, "House", walls, new Vector3(x, 1.6f, HouseZ), new Vector3(HouseSize.x, 3.2f, HouseSize.y));
                Box(street, "Roof", Mat("StreetRoof", new Color(0.36f, 0.3f, 0.3f)), new Vector3(x, 3.4f, HouseZ), new Vector3(HouseSize.x + 0.4f, 0.4f, HouseSize.y + 0.4f));
            }
            var leaves = Mat("StreetLeaves", new Color(0.3f, 0.45f, 0.3f));
            var trunk = Mat("StreetTrunk", Wood);
            // A tree in the gardens between each pair of houses.
            var spots = new float[Mathf.Max(0, HouseCount - 1)];
            for (var k = 0; k < spots.Length; k++)
                spots[k] = HouseX(k) + 3.75f;
            for (var k = 0; k < spots.Length; k++)
            {
                var x = spots[k];
                if (Outside.Available)
                {
                    Outside.Tree(street, new Vector3(x, 0f, GardenTreeZ), poplar: k % 2 == 1, scale: 1f, phase: k * 0.31f);
                    continue;
                }
                Box(street, "Trunk", trunk, new Vector3(x, 0.9f, GardenTreeZ), new Vector3(0.25f, 1.8f, 0.25f));
                var crown = GreyboxAssets.CreatePrimitive(PrimitiveType.Sphere, "Crown", street, leaves, keepCollider: false);
                crown.transform.localPosition = new Vector3(x, 2.4f, GardenTreeZ);
                crown.transform.localScale = Vector3.one * 2f;
                // A breeze in the leaves: each crown leans and swells a little, out of step.
                crown.AddComponent<Oscillate>().Set(Oscillate.Shape.Wave, new Vector3(0.05f, 0f, 0f), Vector3.zero,
                    new Vector3(0.04f, -0.03f, 0.04f), 4.5f + Mathf.Repeat(x * 0.37f, 1.5f), 0.2f, Mathf.Repeat(x * 0.61f, 1f));
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

        // Where customers walk in from and off to (decision 2026-11-11): off screen beyond each end of
        // the pavement, on a line along it nearer the kerb, clear of the queue along the front wall.
        private const float CustomerLaneFromWall = 2.2f;
        private const float CustomerEndsBeyondGrounds = 4f;

        private static void CustomerStreet(Transform street)
        {
            var director = Object.FindFirstObjectByType<Game.Gameplay.Customers.CustomerDirector>();
            if (director == null)
            {
                Debug.LogError("[GreyboxStreet] No customer director; customers will appear in the doorway.");
                return;
            }
            var lane = BuildingSouth - CustomerLaneFromWall;
            var ends = new Transform[2];
            var xs = new[] { GroundsWest - CustomerEndsBeyondGrounds, GroundsEast + CustomerEndsBeyondGrounds };
            for (var i = 0; i < ends.Length; i++)
            {
                ends[i] = new GameObject(i == 0 ? "CustomerStreet_West" : "CustomerStreet_East").transform;
                ends[i].SetParent(street, false);
                ends[i].localPosition = new Vector3(xs[i], 0f, lane);
            }
            var so = new SerializedObject(director);
            var list = so.FindProperty("_streetEnds");
            list.arraySize = ends.Length;
            for (var i = 0; i < ends.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = ends[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The lane couriers ride (decision 2026-11-12): on the road just off the kerb, from off screen
        // at one end to off screen at the other, parking opposite the door.
        private const float CourierLaneFromKerb = 0.9f;
        private const float CourierEndsBeyondGrounds = 12f;

        private static void CourierRoad(Transform street)
        {
            var director = Object.FindFirstObjectByType<Game.Gameplay.Customers.CourierDirector>();
            if (director == null)
                return;
            var xs = new[] { GroundsWest - CourierEndsBeyondGrounds, GroundsEast + CourierEndsBeyondGrounds };
            var so = new SerializedObject(director);
            var list = so.FindProperty("_roadEnds");
            list.arraySize = xs.Length;
            for (var i = 0; i < xs.Length; i++)
            {
                var end = new GameObject(i == 0 ? "CourierRoad_West" : "CourierRoad_East").transform;
                end.SetParent(street, false);
                end.localPosition = new Vector3(xs[i], 0.02f, Kerb - CourierLaneFromKerb);
                list.GetArrayElementAtIndex(i).objectReferenceValue = end;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
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
            Slab(parent, name, Mat($"Street{name}", color), west, east, south, north);

        // Laid in pieces no more than a few metres across, not as one sheet: only four lamps can light
        // any one object here (the pipeline's limit), so a pavement in one piece shows almost none of
        // its street lamps at night. The surfaces' patterns are in world space, so the joins don't show.
        private const float SlabPiece = 6f;

        private static void Slab(Transform parent, string name, Material material, float west, float east, float south, float north)
        {
            var across = Mathf.Max(1, Mathf.CeilToInt((east - west) / SlabPiece));
            var deep = Mathf.Max(1, Mathf.CeilToInt((north - south) / SlabPiece));
            var size = new Vector3((east - west) / across, 0.02f, (north - south) / deep);
            for (var i = 0; i < across; i++)
            for (var j = 0; j < deep; j++)
                Box(parent, name, material, new Vector3(west + (i + 0.5f) * size.x, -0.01f, south + (j + 0.5f) * size.z), size);
        }

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
