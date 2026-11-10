using System.Collections.Generic;
using System.Linq;
using Game.Simulation.Campaign;
using Game.Simulation.Saving;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Where everything in one restaurant's building goes (Phase 6, milestone 3): its walls, the
    /// starting furniture, the street door, spawn points, lamps and the street around it. The grill's
    /// is today's hand-placed kitchen, value for value, so rebuilding it changes nothing. Any other
    /// restaurant's is generated from its definition (room size and inherited appliances) in the
    /// grill's flow (the user's call, 2026-11-10): storage, prep and cooking along the back wall from
    /// west to east, a long island in the middle, sink and bin on the east wall, tables in a grid.
    /// </summary>
    public sealed class KitchenLayout
    {
        public const float CounterSize = 1f;

        /// <summary>A restaurant's own tables come with a chair on every side (the user's call: 4 tables of 4 at Casa Lucía).</summary>
        public const int ChairsPerTable = 4;

        // The dividing wall between kitchen and dining room stays where the grill's is, so the
        // kitchen keeps its place and a deeper dining room grows south, towards the street.
        public const float DividingWallZ = -3f;

        public string Scene;
        public float FloorWidth;
        public float FloorNorth;
        public float FloorSouth;
        public float HalfWidth => FloorWidth / 2f;

        public Vector3 CameraPosition;
        public Vector3[] SpawnPositions;
        public Vector3 StaffSpawn;

        public Vector3 FridgePosition;
        public Vector3 ShelfPosition;
        public Vector3 KnifeCounterPosition;
        public Vector3 PrepCounterPosition;
        public Vector3[] KnifeRackPositions = new Vector3[0];
        public Vector3[] IslandPositions;
        public Vector3[] WallCounterPositions;
        public Vector3[] HobPositions;
        public Vector3[] BarTablePositions;
        public Vector3[] TablePositions = new Vector3[0];
        public Vector3 PlateStackPosition;
        public Vector3 TrayRackPosition;
        public Vector3 ContainerRackPosition;
        public Vector3 SinkPosition;
        public Vector3 BinPosition;

        public Vector3 StreetDoorPosition;

        // Lamps (KitchenLighting).
        public float[] CeilingLightXs;
        public Vector3[] PendantPositions;
        public float[] SconceZs;

        // The street (GreyboxStreet): canopy colours and the vendors on it, in stall order.
        public Color[] Canopies;
        public string[] Vendors;

        // The floor plan (FloorPlanConfig) for a generated building.
        public Vector2 KitchenDoorHinge;
        public float KitchenDoorWidth = 1.2f;

        /// <summary>Where the outer walls' outside faces are: west, east, south, north (walls 0.2 m).</summary>
        public float BuildingWest => -HalfWidth - 0.1f;
        public float BuildingEast => HalfWidth + 0.1f;
        public float BuildingSouth => FloorSouth - 0.1f;
        public float BuildingNorth => FloorNorth + 0.1f;

        private static readonly Color[] OldTownCanopies = { new Color(0.3f, 0.45f, 0.75f), new Color(0.75f, 0.3f, 0.27f), new Color(0.38f, 0.6f, 0.35f) };
        // Riverside's own colours (the user's call: same street kit, its own dressing): teal, saffron, plum.
        private static readonly Color[] RiversideCanopies = { new Color(0.22f, 0.55f, 0.55f), new Color(0.88f, 0.62f, 0.2f), new Color(0.5f, 0.3f, 0.5f) };

        /// <summary>The layout for this restaurant: the grill's hand-placed one, or one generated from its size.</summary>
        public static KitchenLayout For(RestaurantSettings restaurant)
        {
            if (restaurant == null || restaurant.Id == Version1Restaurant.GrillId)
                return Grill(restaurant);
            return Generate(restaurant);
        }

        /// <summary>Today's greybox kitchen, as GreyboxKitchenBuilder had it before milestone 3.</summary>
        public static KitchenLayout Grill(RestaurantSettings grill = null) => new KitchenLayout
        {
            Scene = !string.IsNullOrEmpty(grill?.KitchenScene) ? grill.KitchenScene : RestaurantDefinitions.GrillScene,
            FloorWidth = 8f,
            FloorNorth = 2f,
            FloorSouth = -7f,
            CameraPosition = new Vector3(0f, 11f, -8f),
            // One per player, up to four (decision 2026-11-09): P1 and P2 beside the island's ends,
            // P3 and P4 in the aisle in front of it, clear of the island, the plate stack and the door.
            SpawnPositions = new[]
            {
                new Vector3(-2.45f, 0f, -0.8f), new Vector3(2.45f, 0f, -0.8f),
                new Vector3(-1.0f, 0f, -1.75f), new Vector3(0.3f, 0f, -1.75f),
            },
            StaffSpawn = new Vector3(2.45f, 0f, -1.4f),
            FridgePosition = new Vector3(-3.4f, 0f, 1.4f),
            ShelfPosition = new Vector3(-2.4f, 0f, 1.4f),
            // On the west wall, by prep: the back-left corner is the tall storage (decision 2026-11-07).
            KnifeCounterPosition = new Vector3(-3.4f, 0f, -0.6f),
            PrepCounterPosition = new Vector3(-1.4f, 0f, 1.4f),
            IslandPositions = new[] { new Vector3(-1.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(1.5f, 0f, -0.5f) },
            WallCounterPositions = new[] { new Vector3(3.4f, 0f, -0.6f) },
            HobPositions = new[] { new Vector3(0.6f, 0f, 1.4f) },
            BarTablePositions = new[] { new Vector3(-1.6f, 0f, -3.35f), new Vector3(-0.6f, 0f, -3.35f), new Vector3(0.4f, 0f, -3.35f) },
            PlateStackPosition = new Vector3(1.4f, 0f, -2.4f),
            TrayRackPosition = new Vector3(1.6f, 0f, 1.4f),
            ContainerRackPosition = new Vector3(-0.4f, 0f, 1.4f),
            SinkPosition = new Vector3(3.4f, 0f, 0.4f),
            BinPosition = new Vector3(3.4f, 0f, 1.4f),
            StreetDoorPosition = new Vector3(2f, 0f, -6.55f),
            CeilingLightXs = new[] { -2.7f, 0f, 2.7f },
            PendantPositions = new[] { new Vector3(-2f, 3.6f, -5f), new Vector3(2f, 3.6f, -5f) },
            SconceZs = new[] { -4.2f, -6.0f },
            Canopies = OldTownCanopies,
            Vendors = (grill?.Vendors ?? new[] { "Butcher", "Greengrocer" }).ToArray(),
            KitchenDoorHinge = new Vector2(-3.5f, -3f),
        };

        /// <summary>
        /// A building generated from a restaurant's size and what it comes with. Pieces sit on a 1 m
        /// grid 0.6 m in from the walls, as the grill's do.
        /// </summary>
        public static KitchenLayout Generate(RestaurantSettings restaurant)
        {
            var width = Mathf.Max(6f, restaurant.RoomWidth);
            var north = DividingWallZ + Mathf.Max(4f, restaurant.KitchenDepth);
            var south = DividingWallZ - Mathf.Max(3f, restaurant.DiningDepth);
            var half = width / 2f;
            var inside = half - 0.6f;
            var back = north - 0.6f;
            var appliances = restaurant.InheritedAppliances ?? new string[0];
            int Count(string id) => appliances.Count(a => a == id);

            // The back wall, west to east: storage, prep, knives, containers, the hobs, then trays;
            // the bin in the north-east corner. Whatever doesn't fit is left out (and logged).
            var x = -inside;
            Vector3 Next()
            {
                var at = new Vector3(x, 0f, back);
                x += CounterSize;
                return at;
            }
            var layout = new KitchenLayout
            {
                Scene = restaurant.KitchenScene,
                FloorWidth = width,
                FloorNorth = north,
                FloorSouth = south,
            };
            layout.FridgePosition = Next();
            layout.ShelfPosition = Next();
            layout.PrepCounterPosition = Next();
            layout.KnifeRackPositions = Enumerable.Range(0, Count("knife rack")).Select(_ => Next()).ToArray();
            layout.ContainerRackPosition = Next();
            var hobs = Mathf.Max(1, Count("hob"));
            layout.HobPositions = Enumerable.Range(0, hobs).Select(_ => Next()).ToArray();
            layout.TrayRackPosition = Next();
            layout.BinPosition = new Vector3(inside, 0f, back);
            if (x - CounterSize >= inside) // the last piece reached the bin's corner
                Debug.LogWarning($"[KitchenLayout] {restaurant.Id}'s back wall is too short for all it comes with.");

            // The east wall: the sink, then a spare counter; the west wall: the counter with the knife.
            layout.SinkPosition = new Vector3(inside, 0f, back - 1f);
            layout.WallCounterPositions = new[] { new Vector3(inside, 0f, back - 2f) };
            layout.KnifeCounterPosition = new Vector3(-inside, 0f, back - 2f);

            // The island in the middle, one counter per 2 m of width (the grill's has 4 in 8 m), and
            // the plate stack south of its east end.
            var island = Mathf.Max(2, Mathf.RoundToInt(width / 2f));
            var islandZ = back - 1.9f;
            layout.IslandPositions = Enumerable.Range(0, island)
                .Select(i => new Vector3((i - (island - 1) / 2f) * CounterSize, 0f, islandZ)).ToArray();
            var islandEnd = island * CounterSize / 2f;
            layout.PlateStackPosition = new Vector3(islandEnd - 0.6f, 0f, DividingWallZ + 0.6f);

            // P1 and P2 beside the island's ends, P3 and P4 in the aisle in front of it (as the grill's).
            layout.SpawnPositions = new[]
            {
                new Vector3(-islandEnd - 0.45f, 0f, islandZ - 0.3f), new Vector3(islandEnd + 0.45f, 0f, islandZ - 0.3f),
                new Vector3(-1.0f, 0f, islandZ - 1.25f), new Vector3(0.3f, 0f, islandZ - 1.25f),
            };
            layout.StaffSpawn = new Vector3(islandEnd + 0.45f, 0f, islandZ - 0.9f);

            // The kitchen door in the middle of the dividing wall, so plates go straight down the
            // aisle between the tables (the grill's is at its west end, by its bar tables).
            layout.KitchenDoorHinge = new Vector2(-layout.KitchenDoorWidth / 2f, DividingWallZ);

            // Tables in two columns either side of that aisle, in rows from the dividing wall to the
            // street wall, each clear of the walls by a chair and a walkway. Bar tables, if any,
            // along the dining side of the dividing wall, like the grill's.
            var tables = Count("table");
            var rows = Mathf.Max(1, Mathf.CeilToInt(tables / 2f));
            var firstRow = DividingWallZ - 1.7f;
            var lastRow = south + 1.7f;
            var columnX = Mathf.Min(half * 0.4f, inside - 1.5f);
            layout.TablePositions = Enumerable.Range(0, tables).Select(i =>
            {
                var row = i / 2;
                var z = rows == 1 ? (firstRow + lastRow) / 2f : Mathf.Lerp(firstRow, lastRow, row / (float)(rows - 1));
                return new Vector3(i % 2 == 0 ? -columnX : columnX, 0f, z);
            }).ToArray();
            layout.BarTablePositions = Enumerable.Range(0, Count("bar"))
                .Select(i => new Vector3(-half + 2.4f + i * CounterSize, 0f, DividingWallZ - 0.35f)).ToArray();

            // The street door in the south wall, 2 m in from the east wall, as the grill's.
            layout.StreetDoorPosition = new Vector3(half - 2f, 0f, south + 0.45f);

            // Lamps: ceiling lights spread over the kitchen, a pendant over each table (or two over
            // the room), sconces near both ends of the dining room's side walls.
            var spread = 2.7f * width / 8f;
            layout.CeilingLightXs = new[] { -spread, 0f, spread };
            var diningMiddle = (DividingWallZ + south) / 2f;
            layout.PendantPositions = layout.TablePositions.Length > 0
                ? layout.TablePositions.Select(t => new Vector3(t.x, 3.6f, t.z)).ToArray()
                : new[] { new Vector3(-2f, 3.6f, diningMiddle), new Vector3(2f, 3.6f, diningMiddle) };
            layout.SconceZs = new[] { DividingWallZ - 1.2f, south + 1f };
            layout.CameraPosition = new Vector3(0f, 11f + (width - 8f) * 0.75f, (north + south) / 2f - 5.5f);
            layout.Canopies = restaurant.Street == "Riverside" ? RiversideCanopies : OldTownCanopies;
            layout.Vendors = (restaurant.Vendors ?? new string[0]).ToArray();
            return layout;
        }

        /// <summary>The inside walls of a generated building: the dividing wall either side of the kitchen door.</summary>
        public Rect[] InsideWalls()
        {
            var doorWest = KitchenDoorHinge.x;
            var doorEast = doorWest + KitchenDoorWidth;
            return new[]
            {
                new Rect(-HalfWidth, DividingWallZ - 0.1f, doorWest + HalfWidth, 0.2f),
                new Rect(doorEast, DividingWallZ - 0.1f, HalfWidth - doorEast, 0.2f),
            };
        }

        /// <summary>Every seat it starts with: four at a table, one at a bar table (as the builder makes them).</summary>
        public int Seats => TablePositions.Length * ChairsPerTable + BarTablePositions.Length;

        /// <summary>The scene's path.</summary>
        public string ScenePath => RestaurantDefinitions.ScenePath(Scene);

        public IEnumerable<Vector3> AllStationPositions() =>
            new[] { FridgePosition, ShelfPosition, KnifeCounterPosition, PrepCounterPosition, PlateStackPosition, TrayRackPosition, ContainerRackPosition, SinkPosition, BinPosition }
                .Concat(KnifeRackPositions).Concat(IslandPositions).Concat(WallCounterPositions).Concat(HobPositions).Concat(BarTablePositions).Concat(TablePositions);
    }
}
