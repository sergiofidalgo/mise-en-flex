using System.Collections.Generic;
using Game.Gameplay;
using Game.Presentation.Office.Ambience;
using Game.Presentation.Street;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The street's life (the user's request, 2026-11-11: "a lot of details just like we do in the
    /// city, birds, people roaming around"): people walking past, some stopping at a menu board;
    /// pigeons that scatter when a chef comes close; lamps and windows that light at dusk; the
    /// air conditioning's fan turning faster the harder the kitchen cooks; a bench, planters, a hydrant.
    /// Everything stands clear of the three things that move out here: the lane guests arrive by,
    /// their queue along the front wall, and the wheelie bin's track round the building.
    /// </summary>
    public static partial class GreyboxStreet
    {
        // The pavement is 3.2 m deep. From the wall: the queue (about 0.6), a lane of walkers
        // heading east (1.25), the sign's pole (1.7), the guests' lane (2.2), a lane of walkers
        // heading west (2.75), the kerb.
        private const float WalkerLaneFromWall = 1.25f;
        private const float WalkerLaneFromKerb = 0.45f;
        private const int Walkers = 6;
        private const float PigeonScale = 0.65f;
        private static float MenuX => SignX - SignPostsApart / 2f - 1f; // beside the sign, away from the building

        private static readonly Color LampIron = new Color(0.16f, 0.17f, 0.2f);
        private static readonly Color LampLight = new Color(1f, 0.82f, 0.58f);
        private static readonly Color WindowLit = new Color(1f, 0.8f, 0.5f);
        private static readonly Color WindowDark = new Color(0.24f, 0.29f, 0.38f);
        private static readonly Color Steel = new Color(0.72f, 0.73f, 0.75f);

        private static void Life(Transform street, KitchenCompositionRoot root)
        {
            var life = new GameObject("StreetLife").transform;
            life.SetParent(street, false);

            Neighbours(street);
            Park(street);
            BackStreet(street);
            Facade(street, root);
            // Steam from two manholes in the road: one in the near lane off the plaza, one in the far
            // lane beyond the driveway.
            if (Kit.Available)
            {
                var middle = (Kerb + RoadSouth) / 2f;
                OfficeBuilder.StreetManhole(street, new Vector3(BuildingWest - 2.5f, 0f, middle + 1.5f));
                OfficeBuilder.StreetManhole(street, new Vector3(BuildingEast + 7.5f, 0f, middle - 1.7f));
            }
            LampsAndWindows(life, root);
            Furniture(life);
            var (board, spot) = MenuBoard(life);
            // The menu stands by the street door and goes where the door goes (DoorMenuView).
            var director = Object.FindFirstObjectByType<Game.Gameplay.Customers.CustomerDirector>();
            if (director != null)
            {
                foreach (var part in board.GetComponentsInChildren<Transform>())
                    part.gameObject.isStatic = false;
                var attach = new SerializedObject(board.gameObject.AddComponent<DoorMenuView>());
                attach.FindProperty("_director").objectReferenceValue = director;
                attach.FindProperty("_reader").objectReferenceValue = spot;
                attach.ApplyModifiedPropertiesWithoutUndo();
            }
            Passersby(life, board, spot);
            Pigeons(life);
            OutdoorUnit(life, root);
        }

        // What closes the scene (the user's note, 2026-11-11: "model adjacent scenery to close the
        // scene so the boundaries are justified organically"). The plaza and the parking lot used to
        // end at an invisible wall on open grass. Now the restaurant stands in a street: a block of
        // the city's shops and flats on each side, their fronts on the same pavement and their side
        // walls where the plaza and the lot end, running on past anything the camera shows.
        private const float NeighbourGap = 0.3f;
        private const float NeighbourWidth = 8f;

        private static void Neighbours(Transform street)
        {
            if (!Kit.Available)
                return;
            var blocks = new GameObject("Neighbours").transform;
            blocks.SetParent(street, false);
            // From the pavement back past the fence, so nothing shows behind them from the plaza.
            var depth = GroundsNorth + 1.5f - BuildingSouth;
            var z = BuildingSouth + depth / 2f;
            var index = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                var edge = side < 0f ? GroundsWest - NeighbourGap : GroundsEast + NeighbourGap;
                for (var k = 0; Mathf.Abs(edge) + NeighbourWidth <= StreetEnd + NeighbourWidth; k++)
                {
                    // Two storeys by the restaurant, so the camera's view of the plaza and the lot
                    // stays open; taller further off.
                    var height = k == 0 ? 7.8f : 9f + (index % 3) * 1.6f;
                    var building = OfficeBuilder.StreetBuilding(blocks, new Vector3(edge + side * NeighbourWidth / 2f, 0f, z), 180f,
                        NeighbourWidth, depth, height, index++);
                    if (k == 0)
                    {
                        // A mural on the wall that looks over the plaza (the steak) or the lot (the
                        // sunset), so the scene's edge is something to look at. Turned 180°, the
                        // building's local −X is east.
                        OfficeBuilder.StreetMural(building, NeighbourWidth, height, plusX: side > 0f, mural: side < 0f ? 2 : 1, size: 4.6f);
                        // Old graffiti, on the wall by the parking lot only.
                        if (side > 0f)
                            OfficeBuilder.StreetTags(building, NeighbourWidth, depth, height, plusX: true, first: 0);
                        // Its side wall is where the plaza (or the lot) ends: solid, floor to roof.
                        var wall = building.gameObject.AddComponent<BoxCollider>();
                        wall.center = new Vector3(0f, -height / 2f, 0f);
                        wall.size = new Vector3(NeighbourWidth, height, depth);
                    }
                    edge += side * (NeighbourWidth + 0.5f); // a narrow alley between buildings
                }
            }
        }

        // Where the houses stand (the user's note, 2026-11-12: "the houses look nice, but the scenery
        // around them doesn't feel great"). They were set down on a bare lawn with nothing behind.
        // Now a cobbled lane runs behind the restaurant's fence; each house has a front garden
        // behind a low hedge, a path to its door and flowers under its windows; hedges part the
        // plots; and a tall hedge and a line of trees stand behind the houses, closing the view.
        private static readonly Color[] Blooms =
        {
            new Color(0.92f, 0.42f, 0.48f), new Color(0.98f, 0.8f, 0.32f), new Color(0.96f, 0.95f, 0.9f), new Color(0.62f, 0.47f, 0.82f),
        };

        private static void BackStreet(Transform street)
        {
            var back = new GameObject("BackStreet").transform;
            back.SetParent(street, false);
            Slab(back, "BackLane", Surfaces.Slabs("StreetBackLane", new Color(0.6f, 0.57f, 0.53f), 0.24f), -StreetEnd, StreetEnd, BackLaneSouth, BackLaneNorth);

            var hedge = Kit.Toon("KitHedge", new Color(0.24f, 0.45f, 0.3f));
            var path = Surfaces.Slabs("StreetGardenPath", Paving, 0.45f);
            var soil = Kit.Toon("StreetSoil", new Color(0.36f, 0.26f, 0.2f));
            var leaf = Kit.Toon("StreetShrub", new Color(0.33f, 0.5f, 0.3f));
            var front = HouseZ - HouseSize.y / 2f - 0.05f;
            var gardenHedge = BackLaneNorth + 0.25f;
            const float plot = 3.55f; // half a plot's width: houses stand 7.5 m apart
            for (var i = 0; i < HouseCount; i++)
            {
                var x = HouseX(i);
                var cottage = i % 2 == 0;
                // The taller house's door is off to one side (the model's −x, which is east here).
                var door = x + (cottage ? 0f : 1.15f);
                Slab(back, "GardenPath", path, door - 0.5f, door + 0.5f, BackLaneNorth, front);
                foreach (var (from, to) in new[] { (x - plot, door - 0.75f), (door + 0.75f, x + plot) })
                    Box(back, "GardenHedge", hedge, new Vector3((from + to) / 2f, 0.3f, gardenHedge), new Vector3(to - from, 0.6f, 0.4f));
                // The hedge between this plot and the next, as far back as the tall hedge behind.
                Box(back, "PlotHedge", hedge, new Vector3(x + 3.75f, 0.55f, (gardenHedge + HouseZ + 3.3f) / 2f), new Vector3(0.45f, 1.1f, HouseZ + 3.3f - gardenHedge));
                // A bed under each ground-floor window: soil, greenery, a row of flowers.
                foreach (var window in cottage ? new[] { -1.75f, 1.75f } : new[] { -1.2f })
                {
                    var bed = new Vector3(x + window, 0f, front - 0.32f);
                    Box(back, "Bed", soil, bed + Vector3.up * 0.05f, new Vector3(1.5f, 0.1f, 0.5f));
                    Box(back, "BedGreen", leaf, bed + Vector3.up * 0.17f, new Vector3(1.3f, 0.16f, 0.34f));
                    for (var k = 0; k < 5; k++)
                    {
                        var colour = Blooms[(i + k) % Blooms.Length];
                        var bloom = GreyboxAssets.CreatePrimitive(PrimitiveType.Sphere, "Bloom", back,
                            Kit.Toon($"StreetBloom_{ColorUtility.ToHtmlStringRGB(colour)}", colour), keepCollider: false);
                        bloom.transform.localPosition = bed + new Vector3(-0.5f + k * 0.25f, 0.3f + (k % 2) * 0.04f, (k % 2) * 0.08f - 0.04f);
                        bloom.transform.localScale = Vector3.one * 0.17f;
                    }
                }
            }
            Box(back, "PlotHedge", hedge, new Vector3(HouseX(0) - 3.75f, 0.55f, (gardenHedge + HouseZ + 3.3f) / 2f), new Vector3(0.45f, 1.1f, HouseZ + 3.3f - gardenHedge));

            // Behind the houses: a tall hedge the whole length, and trees standing over it.
            Box(back, "BackHedge", hedge, new Vector3(0f, 1.1f, HouseZ + 3.5f), new Vector3(StreetEnd * 2f, 2.2f, 0.9f));
            if (!Outside.Available)
                return;
            var n = 0;
            for (var x = -StreetEnd + 2f; x <= StreetEnd - 2f; x += 4.6f, n++)
                Outside.Tree(back, new Vector3(x + (n % 3) * 0.5f, 0f, HouseZ + 5.4f + (n % 2) * 1.5f), poplar: n % 3 == 1, scale: 1.3f + (n % 3) * 0.18f, phase: n * 0.37f);
        }

        // Across the road: a park. A pavement, a clipped hedge with gaps, trees behind it, benches
        // looking over the road. Low, so it never comes between the camera and the restaurant.
        private static void Park(Transform street)
        {
            var park = new GameObject("Park").transform;
            park.SetParent(street, false);
            Slab(park, "FarPavement", Surfaces.Slabs("StreetFarPavement", Paving * 0.96f, 0.8f), -StreetEnd, StreetEnd, RoadSouth - 2.4f, RoadSouth);
            Box(park, "FarKerb", Mat("StreetKerb", KerbStone), new Vector3(0f, 0.05f, RoadSouth), new Vector3(StreetEnd * 2f, 0.1f, 0.2f));

            var hedge = Kit.Toon("KitHedge", new Color(0.24f, 0.45f, 0.3f));
            var hedgeZ = RoadSouth - 2.8f;
            const float run = 9f;
            for (var x = -StreetEnd + 1f; x + run - 1.6f <= StreetEnd; x += run)
            {
                // A gap in the hedge every nine metres: a way into the park.
                Box(park, "Hedge", hedge, new Vector3(x + (run - 1.6f) / 2f, 0.5f, hedgeZ), new Vector3(run - 1.6f, 1f, 0.7f));
                if (!Kit.Available)
                    continue;
                var bay = Mathf.RoundToInt((x + StreetEnd) / run);
                if (Outside.Available)
                {
                    Outside.Tree(park, new Vector3(x + 2.2f, 0f, hedgeZ - 2.2f), poplar: false, scale: 0.85f, phase: bay * 0.23f);
                    Outside.Tree(park, new Vector3(x + 6.4f, 0f, hedgeZ - 4.4f), poplar: bay % 2 == 0, scale: 0.95f, phase: bay * 0.41f);
                }
                else
                {
                    OfficeBuilder.StreetTree(park, new Vector3(x + 2.2f, 0f, hedgeZ - 2.2f), 0.75f);
                    OfficeBuilder.StreetTree(park, new Vector3(x + 6.4f, 0f, hedgeZ - 4.4f), 0.9f);
                }
                if (Mathf.RoundToInt((x + StreetEnd) / run) % 2 == 0)
                    Kit.Bench(park, new Vector3(x + 3.7f, 0f, RoadSouth - 1.9f), 0f); // back to the hedge, looking over the road
            }
        }

        // Lamps along the kerb, on the kerbstone itself, their heads over the pavement; and the
        // houses' windows and doors. Both light at dusk (StreetLampsView).
        private static void LampsAndWindows(Transform life, KitchenCompositionRoot root)
        {
            var iron = Kit.Toon("StreetLampIron", LampIron);
            var bulbOn = Kit.Glow("StreetBulbOn", new Color(1f, 0.9f, 0.68f));
            var bulbOff = Kit.Toon("StreetBulbOff", new Color(0.82f, 0.82f, 0.78f));
            var lights = new List<Light>();
            var bulbs = new List<Renderer>();
            // Three a side, well apart (the user's note, 2026-11-12), the far side's standing between
            // the near side's: by the plaza's far end, in front of the restaurant, and beyond the
            // driveway on the kerb by the restaurant; six metres on from each on the park's kerb. Every
            // lamp's arm reaches north: over the pavement on this side, over the road on the other.
            var middle = (BuildingWest + BuildingEast) / 2f;
            var spots = new List<Vector3>();
            foreach (var x in new[] { GroundsWest + 1.5f, middle - 1f, DriveEast + 2.5f })
            {
                spots.Add(new Vector3(x, 0f, Kerb - 0.02f));
                spots.Add(new Vector3(x + 6f, 0f, RoadSouth - 0.12f));
            }
            foreach (var spot in spots)
            {
                var lamp = new GameObject("StreetLamp").transform;
                lamp.SetParent(life, false);
                lamp.localPosition = spot;
                Box(lamp, "Post", iron, new Vector3(0f, 1.7f, 0f), new Vector3(0.1f, 3.4f, 0.1f));
                Box(lamp, "Foot", iron, new Vector3(0f, 0.12f, 0f), new Vector3(0.2f, 0.24f, 0.2f));
                Box(lamp, "Arm", iron, new Vector3(0f, 3.37f, 0.35f), new Vector3(0.06f, 0.06f, 0.8f));
                Box(lamp, "Head", iron, new Vector3(0f, 3.32f, 0.62f), new Vector3(0.3f, 0.1f, 0.46f));
                bulbs.Add(Box(lamp, "Bulb", bulbOff, new Vector3(0f, 3.26f, 0.62f), new Vector3(0.2f, 0.04f, 0.34f)).GetComponent<Renderer>());

                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(lamp, false);
                light.transform.localPosition = new Vector3(0f, 3f, 0.7f);
                light.type = LightType.Point;
                light.color = LampLight;
                // Strong enough to pool on the pavement three metres below once the night is dark
                // (light falls off with the square of the distance); by day the lamps are off.
                light.range = 9.5f;
                light.intensity = 8f;
                light.shadows = LightShadows.None;
                lights.Add(light);
            }

            // Two windows and a door on each house's front. Two houses in three have someone in.
            var lit = Kit.Glow("StreetWindowOn", WindowLit);
            var dark = Kit.Toon("StreetWindowOff", WindowDark);
            var door = Kit.Toon("StreetHouseDoor", Wood);
            var front = HouseZ - HouseSize.y / 2f - 0.02f;
            var windows = new List<Renderer>();
            // The modelled houses bring their own panes (two houses in three have someone in); the
            // box houses get a door and two windows each.
            for (var i = 0; i < HouseGlass.Count; i++)
                if (i % 3 != 1 && HouseGlass[i] != null)
                    windows.Add(HouseGlass[i]);
            for (var i = 0; i < HouseCount && HouseGlass.Count == 0; i++)
            {
                Box(life, "HouseDoor", door, new Vector3(HouseX(i), 0.85f, front), new Vector3(0.8f, 1.7f, 0.04f));
                for (var k = 0; k < 2; k++)
                {
                    var window = Box(life, "HouseWindow", dark, new Vector3(HouseX(i) + (k == 0 ? -1.5f : 1.5f), 1.9f, front), new Vector3(0.9f, 1f, 0.04f));
                    if ((i * 2 + k) % 3 != 0)
                        windows.Add(window.GetComponent<Renderer>());
                }
            }

            var view = new SerializedObject(life.gameObject.AddComponent<StreetLampsView>());
            view.FindProperty("_root").objectReferenceValue = root;
            SetObjects(view.FindProperty("_lights"), lights);
            SetObjects(view.FindProperty("_bulbs"), bulbs);
            view.FindProperty("_bulbOn").objectReferenceValue = bulbOn;
            view.FindProperty("_bulbOff").objectReferenceValue = bulbOff;
            SetObjects(view.FindProperty("_windows"), windows);
            view.FindProperty("_windowOn").objectReferenceValue = lit;
            view.FindProperty("_windowOff").objectReferenceValue = dark;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        // A bench on the plaza's far edge, planters at the corners nobody walks through, a hydrant
        // on the kerb.
        private static void Furniture(Transform life)
        {
            var middle = (BuildingSouth + BuildingNorth) / 2f;
            var corners = new[]
            {
                new Vector2(GroundsWest + 0.45f, middle - 2f), new Vector2(GroundsWest + 0.45f, middle + 2f),
                new Vector2(GroundsWest + 0.45f, GroundsNorth - 1f), new Vector2(GroundsEast - 0.45f, GroundsNorth - 1f),
            };
            // The street kit's own models (the city's bench, planter and hydrant), where it's there.
            if (Kit.Available)
            {
                Kit.Bench(life, new Vector3(GroundsWest + 0.5f, 0f, middle), 90f); // its back to the plaza's edge
                Solid(Last(life));
                foreach (var corner in corners)
                {
                    // Long side along the edge (the kit's planter is a 1.7 m hedge trough), out of the stalls' way.
                    Kit.Planter(life, new Vector3(corner.x, 0f, corner.y), 90f);
                    Solid(Last(life));
                }
                // The hydrant, half as big again as the kit's, on a pale pad (the user's notes,
                // 2026-11-12: more noticeable; then without the yellow kerb tried beside it).
                var hydrantAt = new Vector3(BuildingWest - 6.4f, 0f, Kerb - 0.02f);
                Kit.Hydrant(life, hydrantAt);
                Last(life).localScale = Vector3.one * 1.5f;
                Box(life, "HydrantPad", Kit.Toon("StreetHydrantPad", new Color(0.82f, 0.81f, 0.78f)), hydrantAt + new Vector3(0f, 0.012f, 0.12f), new Vector3(0.8f, 0.024f, 0.62f));
                return;
            }

            var wood = Kit.Toon("StreetBenchWood", new Color(0.52f, 0.36f, 0.24f));
            var iron = Kit.Toon("StreetLampIron", LampIron);

            var bench = new GameObject("Bench").transform;
            bench.SetParent(life, false);
            bench.localPosition = new Vector3(GroundsWest + 0.45f, 0f, middle);
            Box(bench, "Seat", wood, new Vector3(0.05f, 0.45f, 0f), new Vector3(0.45f, 0.07f, 1.6f));
            Box(bench, "Back", wood, new Vector3(-0.17f, 0.78f, 0f), new Vector3(0.06f, 0.4f, 1.6f));
            foreach (var z in new[] { -0.65f, 0.65f })
                Box(bench, "Leg", iron, new Vector3(0f, 0.22f, z), new Vector3(0.42f, 0.44f, 0.06f));
            var seat = bench.gameObject.AddComponent<BoxCollider>();
            seat.center = new Vector3(0f, 0.5f, 0f);
            seat.size = new Vector3(0.55f, 1f, 1.6f);

            var pot = Kit.Toon("StreetPlanter", new Color(0.62f, 0.4f, 0.3f));
            var shrub = Kit.Toon("StreetShrub", new Color(0.33f, 0.5f, 0.3f));
            var spots = corners;
            for (var i = 0; i < spots.Length; i++)
            {
                var planter = new GameObject("Planter").transform;
                planter.SetParent(life, false);
                planter.localPosition = new Vector3(spots[i].x, 0f, spots[i].y);
                Box(planter, "Pot", pot, new Vector3(0f, 0.22f, 0f), new Vector3(0.5f, 0.44f, 0.5f));
                var leaves = GreyboxAssets.CreatePrimitive(PrimitiveType.Sphere, "Shrub", planter, shrub, keepCollider: false);
                leaves.transform.localPosition = new Vector3(0f, 0.62f, 0f);
                leaves.transform.localScale = new Vector3(0.6f, 0.55f, 0.6f);
                leaves.AddComponent<Oscillate>().Set(Oscillate.Shape.Wave, Vector3.zero, Vector3.zero, new Vector3(0.03f, -0.02f, 0.03f), 3.5f + i * 0.4f, 0.2f, i * 0.27f);
                var body = planter.gameObject.AddComponent<BoxCollider>();
                body.center = new Vector3(0f, 0.5f, 0f);
                body.size = new Vector3(0.5f, 1f, 0.5f);
            }

            var red = Kit.Toon("StreetHydrant", new Color(0.82f, 0.2f, 0.18f)); // the kit's own red
            var hydrant = new GameObject("Hydrant").transform;
            hydrant.SetParent(life, false);
            hydrant.localPosition = new Vector3(BuildingWest - 6.4f, 0f, Kerb - 0.02f);
            Box(hydrant, "Body", red, new Vector3(0f, 0.28f, 0f), new Vector3(0.2f, 0.56f, 0.2f));
            Box(hydrant, "Cap", red, new Vector3(0f, 0.6f, 0f), new Vector3(0.26f, 0.08f, 0.26f));
            Box(hydrant, "Outlets", red, new Vector3(0f, 0.38f, 0f), new Vector3(0.36f, 0.1f, 0.1f));
        }

        // The menu on an A-board by the sign, beyond the building's corner and clear of the bin's
        // way round it: passers-by stop in front of it to read (StreetWalkersView).
        private static (Transform Board, Transform Spot) MenuBoard(Transform life)
        {
            var reader = new GameObject("MenuBoardReader").transform;
            reader.SetParent(life, false);
            reader.localPosition = new Vector3(MenuX, 0f, BuildingSouth - 1.45f);
            // The chalked A-board (the user's note, 2026-11-12: the lit stand didn't convince), beside the sign's posts.
            if (Outside.Available)
            {
                var chalked = Outside.MenuBoard(life, new Vector3(MenuX, 0f, BuildingSouth - 0.6f));
                Solid(chalked);
                return (chalked, reader);
            }
            if (Kit.Available)
            {
                Kit.MenuStand(life, new Vector3(MenuX, 0f, BuildingSouth - 0.6f), 180f);
                var stand = Last(life);
                stand.name = "MenuBoard";
                Solid(stand);
                return (stand, reader);
            }

            var board = new GameObject("MenuBoard").transform;
            board.SetParent(life, false);
            board.localPosition = new Vector3(MenuX, 0f, BuildingSouth - 0.6f);
            var frame = Kit.Toon("StreetBenchWood", new Color(0.52f, 0.36f, 0.24f));
            var slate = Kit.Toon("StreetSlate", new Color(0.17f, 0.2f, 0.2f));
            var chalk = Kit.Toon("StreetChalk", new Color(0.93f, 0.92f, 0.86f));
            foreach (var side in new[] { -1f, 1f })
            {
                // Each leaf leans in to meet the other at the top; the one facing the street carries the menu.
                var leaf = new GameObject(side < 0f ? "Front" : "Back").transform;
                leaf.SetParent(board, false);
                leaf.localPosition = new Vector3(0f, 0f, side * 0.2f);
                leaf.localRotation = Quaternion.Euler(-side * 12f, 0f, 0f);
                Box(leaf, "Frame", frame, new Vector3(0f, 0.5f, 0f), new Vector3(0.62f, 1f, 0.04f));
                Box(leaf, "Slate", slate, new Vector3(0f, 0.55f, side * 0.022f), new Vector3(0.52f, 0.74f, 0.01f));
                if (side > 0f)
                    continue;
                Box(leaf, "Heading", chalk, new Vector3(0f, 0.82f, -0.03f), new Vector3(0.3f, 0.05f, 0.005f));
                for (var line = 0; line < 4; line++)
                    Box(leaf, "Line", chalk, new Vector3(-0.04f + (line % 2) * 0.03f, 0.68f - line * 0.11f, -0.03f), new Vector3(0.34f - (line % 2) * 0.06f, 0.025f, 0.005f));
            }
            var body = board.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.5f, 0f);
            body.size = new Vector3(0.62f, 1f, 0.5f);

            return (board, reader);
        }

        // The restaurant's front above its cut walls (FacadeView): a pilaster at each corner, a
        // fascia across the whole front with the name on it, and a striped valance under that. It
        // stands on the front wall's top and rises only while every chef is outside, so indoors
        // nothing ever hides the rooms. Nothing in it depends on where the door is, which players
        // can move: the valance simply runs over it.
        private const float WallTop = 1f;
        private const float FacadeHeight = 2.05f;
        private static readonly Color Brand = new Color(0.5f, 0.22f, 0.15f); // the sign's own red-brown

        private static void Facade(Transform street, KitchenCompositionRoot root)
        {
            var facade = new GameObject("Facade").transform;
            facade.SetParent(street, false);
            facade.localPosition = new Vector3((BuildingWest + BuildingEast) / 2f, WallTop, BuildingSouth + 0.1f);
            var rise = new GameObject("Rise").transform;
            rise.SetParent(facade, false);

            var width = BuildingEast - BuildingWest;
            var timber = Kit.Toon("StreetFacadeTimber", new Color(0.3f, 0.2f, 0.15f));
            var cream = Kit.Toon("StreetHouseTrim", new Color(0.94f, 0.91f, 0.84f));
            var brand = Kit.Toon("StreetFacadeBrand", Brand);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (width / 2f - 0.14f);
                Box(rise, "Pilaster", timber, new Vector3(x, FacadeHeight / 2f, 0f), new Vector3(0.3f, FacadeHeight, 0.3f));
                Box(rise, "Capital", cream, new Vector3(x, FacadeHeight + 0.05f, 0f), new Vector3(0.42f, 0.1f, 0.42f));
            }
            // The fascia, its face a little proud of the wall, edged in cream.
            const float fasciaHeight = 0.52f;
            var fasciaY = FacadeHeight - fasciaHeight / 2f - 0.02f;
            Box(rise, "Fascia", brand, new Vector3(0f, fasciaY, -0.06f), new Vector3(width + 0.06f, fasciaHeight, 0.2f));
            foreach (var edge in new[] { -1f, 1f })
                Box(rise, "FasciaEdge", cream, new Vector3(0f, fasciaY + edge * (fasciaHeight / 2f + 0.025f), -0.08f), new Vector3(width + 0.14f, 0.05f, 0.26f));

            // The name, lettered across it at runtime from the restaurant being played.
            var label = new GameObject("Name").AddComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(rise, false);
            label.transform.localPosition = new Vector3(0f, fasciaY, -0.17f);
            label.font = MenuArt.Font("Inter-Bold");
            label.color = new Color(0.97f, 0.93f, 0.84f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.5f;
            label.fontSizeMax = 5f;
            label.rectTransform.sizeDelta = new Vector2(Mathf.Min(width - 1.2f, 6.5f), fasciaHeight - 0.14f);

            // A striped valance hanging flat under the fascia. Not an awning: one that reached out over
            // the pavement hid the doorstep from the camera, and with it a chef just set down there
            // and the queue beside the door (seen in the first stills, 2026-11-12).
            var stripe = Kit.Toon("StreetFacadeAwning", Brand * 1.25f);
            const float band = 0.5f;
            var count = Mathf.Max(1, Mathf.FloorToInt((width - 0.6f) / band));
            for (var k = 0; k < count; k++)
                Box(rise, "Valance", k % 2 == 0 ? stripe : cream,
                    new Vector3((k - (count - 1) / 2f) * band, fasciaY - fasciaHeight / 2f - 0.2f, -0.07f), new Vector3(band, 0.34f, 0.05f));
            foreach (var part in facade.GetComponentsInChildren<Transform>())
                part.gameObject.isStatic = false; // it rises and sinks

            var view = new SerializedObject(facade.gameObject.AddComponent<FacadeView>());
            view.FindProperty("_root").objectReferenceValue = root;
            view.FindProperty("_rise").objectReferenceValue = rise;
            view.FindProperty("_name").objectReferenceValue = label;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Last(Transform parent) => parent.GetChild(parent.childCount - 1);

        // A collider round a kit model's meshes, at least a metre tall so no chef climbs onto it.
        private static void Solid(Transform group)
        {
            var bounds = new Bounds();
            var any = false;
            foreach (var filter in group.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                var mesh = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var local = group.InverseTransformPoint(filter.transform.TransformPoint(point));
                    if (any)
                        bounds.Encapsulate(local);
                    else
                        bounds = new Bounds(local, Vector3.zero);
                    any = true;
                }
            }
            if (!any)
                return;
            var box = group.gameObject.AddComponent<BoxCollider>();
            var height = Mathf.Max(1f, bounds.max.y);
            box.center = new Vector3(bounds.center.x, height / 2f, bounds.center.z);
            box.size = new Vector3(bounds.size.x, height, bounds.size.z);
            Debug.Log($"[GreyboxStreet] {group.name}: {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m.");
        }

        private static void Passersby(Transform life, Transform board, Transform spot)
        {
            var walkers = new GameObject("Passersby").transform;
            walkers.SetParent(life, false);
            var view = new SerializedObject(walkers.gameObject.AddComponent<StreetWalkersView>());
            view.FindProperty("_prefab").objectReferenceValue = GreyboxService.BuildPasserbyPrefab();
            view.FindProperty("_count").intValue = Walkers;
            // In and out where the guests come and go: beyond the camera's view at each end.
            view.FindProperty("_west").floatValue = GroundsWest - CustomerEndsBeyondGrounds;
            view.FindProperty("_east").floatValue = GroundsEast + CustomerEndsBeyondGrounds;
            var lanes = view.FindProperty("_lanes");
            lanes.arraySize = 2;
            lanes.GetArrayElementAtIndex(0).floatValue = BuildingSouth - WalkerLaneFromWall;
            lanes.GetArrayElementAtIndex(1).floatValue = Kerb + WalkerLaneFromKerb;
            view.FindProperty("_minZ").floatValue = Kerb + 0.35f;
            view.FindProperty("_maxZ").floatValue = BuildingSouth - 0.9f;
            view.FindProperty("_board").objectReferenceValue = board;
            view.FindProperty("_boardSpot").objectReferenceValue = spot;
            view.FindProperty("_boardLane").intValue = 0;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        // Pigeons pecking about the plaza and the parking lot, and perched on the sign and the
        // back fence. A chef coming close sends them up and away over the houses; they glide back
        // once it's quiet (the terrace's birds, TerraceBirdsView).
        private static void Pigeons(Transform life)
        {
            var flock = new GameObject("Pigeons").transform;
            flock.SetParent(life, false);
            var middle = (BuildingSouth + BuildingNorth) / 2f;
            var fence = GroundsNorth + 0.2f;
            var perches = new[]
            {
                // The middle of the plaza, between the stalls.
                new Vector3(BuildingWest - 5.6f, 0.01f, middle - 0.3f), new Vector3(BuildingWest - 5.1f, 0.01f, middle - 0.6f),
                new Vector3(BuildingWest - 4.8f, 0.01f, middle + 0.1f), new Vector3(BuildingWest - 5.9f, 0.01f, middle + 0.4f),
                // The plaza's corner by the pavement.
                new Vector3(GroundsWest + 1.1f, 0.01f, BuildingSouth + 0.7f), new Vector3(GroundsWest + 1.6f, 0.01f, BuildingSouth + 0.45f),
                // On the restaurant's sign.
                new Vector3(SignX - 0.7f, 4.18f, SignZ), new Vector3(SignX + 0.66f, 4.18f, SignZ),
                // Along the back fence.
                new Vector3(BuildingWest - 3f, 1f, fence), new Vector3(BuildingWest - 2.55f, 1f, fence), new Vector3(BuildingEast + 2.2f, 1f, fence),
                // The far end of the parking lot.
                new Vector3(GroundsEast - 1.5f, 0.01f, GroundsNorth - 0.45f), new Vector3(GroundsEast - 2.2f, 0.01f, GroundsNorth - 0.3f),
            };
            var birds = new List<Transform>();
            for (var i = 0; i < perches.Length; i++)
            {
                var bird = OfficeBuilder.StreetPigeon(flock, perches[i], Mathf.Repeat(i * 137.5f, 360f));
                // The city's birds are oversized to read from its far camera; here the camera is close.
                bird.localScale = Vector3.one * PigeonScale;
                birds.Add(bird);
            }

            var view = new SerializedObject(flock.gameObject.AddComponent<TerraceBirdsView>());
            SetObjects(view.FindProperty("_birds"), birds);
            // Up and away to the north, over the houses, ending high: there's no roof edge to drop behind.
            view.FindProperty("_outward").vector2Value = new Vector2(0f, 1f);
            view.FindProperty("_flyOffDistance").floatValue = 22f;
            view.FindProperty("_flyOffRise").floatValue = 3f;
            view.FindProperty("_flyOffDip").floatValue = -5f;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        // The air conditioning's outdoor unit (the user's reference, 2026-11-12, in place of the
        // extractor): on the ground against the east wall's north end, its fan to the parking lot.
        // The fan idles, and turns faster the harder the kitchen cooks (OutdoorUnitView). The bin's
        // track stops short of it (OutdoorUnitKept).
        private static void OutdoorUnit(Transform life, KitchenCompositionRoot root)
        {
            if (!Outside.Available)
                return;
            var (unit, fan) = Outside.OutdoorUnit(life, new Vector3(BuildingEast + 0.24f, 0f, BuildingNorth - 0.65f), -90f);
            var body = unit.gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.5f, 0f);
            body.size = new Vector3(0.99f, 1f, 0.44f);
            var view = new SerializedObject(unit.gameObject.AddComponent<OutdoorUnitView>());
            view.FindProperty("_root").objectReferenceValue = root;
            view.FindProperty("_fan").objectReferenceValue = fan;
            view.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjects<T>(SerializedProperty list, IReadOnlyList<T> values) where T : Object
        {
            list.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
