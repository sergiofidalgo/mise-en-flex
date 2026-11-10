using Game.Presentation.Office;
using Game.Presentation.Office.Ambience;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The boss's own tower under the floor, from the street to the penthouse. Glass on every side
    /// (user's note, 2026-11-09: if the penthouse is glass on all four sides, so is the rest of the
    /// tower), every floor lit the same way (user's note: the floors weren't), and built in the
    /// city's own chunky, warm style (user's note: "the tower model more detailed, aligned with the
    /// rest of the aesthetic"): a stone podium round the lobby, stone piers at the corners and every
    /// few bays running the full height, a sandstone spandrel and a ledge at every floor, bronze
    /// mullions, painted glass in every bay with some offices lit and people passing behind them, a
    /// louvred plant floor every ten. On its front, the glass lift the chef rides up in the opening.
    /// </summary>
    public static partial class OfficeBuilder
    {
        private const float TowerStorey = 3.4f;
        private const float TowerBay = 2.2f;
        private const float TowerPodium = 5f; // the lobby's height at the street
        // Where the camera climbs (the user's notes, 2026-11-10): the middle of the front, the lift
        // and real rooms behind clear glass; everywhere else the panes paint their rooms (SH_InteriorCard).
        private const float BandWest = -9.0f;
        private const float BandEast = 8.6f;
        private const float RoomDepth = 7.4f;
        // The lift's shaft: the same place as the boss's floor's elevator (OfficeBuilder.BuildElevator).
        private const float ShaftHalfWidth = 2.0f;
        private static float ShaftSouth => South - 0.1f;
        private static float ShaftNorth => CarDoorZ + 0.1f;

        private static void BossTower(Transform city)
        {
            var tower = new GameObject("BossTower").transform;
            tower.SetParent(city, false);
            var width = East - West + 0.6f;
            var depth = TerraceNorth - South + 0.6f;
            var middle = (South + TerraceNorth) / 2f;
            var stone = Toon("OfficeTowerStone", new Color(0.66f, 0.55f, 0.43f));
            var stoneDark = Toon("OfficeTowerStoneDark", new Color(0.5f, 0.41f, 0.33f));
            var bronze = Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f));
            var louvre = Toon("OfficeTowerLouvre", new Color(0.32f, 0.31f, 0.33f));
            var card = InteriorCards();
            var clear = PilotArt.EnsureGlassMaterial("OfficeTowerPane", new Color(0.55f, 0.7f, 0.85f, 0.12f));
            var ground = -CityDepth;
            var top = -1.4f; // under the penthouse's cornice

            // The core, behind the rooms along the front, so no gap ever shows the sky through the tower.
            var coreFront = middle - depth / 2f + RoomDepth + 0.2f;
            var coreBack = middle + depth / 2f - 0.1f;
            Cube(tower, "Core", Toon("OfficeTowerGlass", new Color(0.17f, 0.21f, 0.31f)), new Vector3(0f, ground, (coreFront + coreBack) / 2f), new Vector3(width - 0.2f, CityDepth - 0.8f, coreBack - coreFront));
            Block(tower, "Cornice", Toon("OfficeBrass", Brass), new Vector3(0f, -1.3f, middle), new Vector3(width + 0.5f, 0.5f, depth + 0.5f));
            Block(tower, "CorniceDark", Toon("OfficeWalnut", Walnut), new Vector3(0f, -0.8f, middle), new Vector3(width + 0.3f, 0.4f, depth + 0.3f));

            // The podium: stone round the street floor, open in front for the lobby's glass.
            var podiumTop = ground + TowerPodium;
            const float proud = 0.3f;
            // West of the lobby the podium is stone (another place to eat has its front on it); east,
            // the restaurant's windows run to a stone pier at the tower's corner (OfficeBuilder.Entrance.cs).
            var lobbyHalf = 8.2f;
            var edge = width / 2f + proud;
            Block(tower, "Podium", stone, new Vector3(-(lobbyHalf + edge) / 2f, ground, TowerFace - proud / 2f), new Vector3(edge - lobbyHalf, TowerPodium, proud));
            Block(tower, "PodiumPier", stone, new Vector3((RestaurantEast + edge) / 2f, ground, TowerFace - proud / 2f), new Vector3(edge - RestaurantEast, TowerPodium, proud));
            Block(tower, "PodiumLintel", stone, new Vector3((RestaurantEast - lobbyHalf) / 2f, ground + 4.35f, TowerFace - proud / 2f), new Vector3(RestaurantEast + lobbyHalf, TowerPodium - 4.35f, proud));
            foreach (var sign in new[] { -1f, 1f })
                Block(tower, "PodiumSide", stone, new Vector3(sign * (width / 2f + proud / 2f), ground, middle), new Vector3(proud, TowerPodium, depth + 2f * proud));
            Block(tower, "PodiumLedge", stoneDark, new Vector3(0f, podiumTop, middle), new Vector3(width + 1.0f, 0.35f, depth + 1.0f));
            // Bronze lettering-band over the lobby doors: a plain strip, the boss needs no name up.
            Block(tower, "LobbyBand", bronze, new Vector3(-3f, ground + 4.45f, TowerFace - proud - 0.05f), new Vector3(9.6f, 0.35f, 0.08f));

            // Piers: at the corners, and in place of every fourth mullion, full height in one piece.
            var pierHeight = top - podiumTop - 0.35f;
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                Block(tower, "CornerPier", stone, new Vector3(x * (width / 2f), podiumTop + 0.35f, middle + z * depth / 2f), new Vector3(1.1f, pierHeight, 1.1f));
            foreach (var side in new[] { 0, -1, 1 })
            {
                var span = side == 0 ? width : depth;
                var index = 0;
                for (var a = -span / 2f + TowerBay; a < span / 2f - 0.5f; a += TowerBay, index++)
                {
                    var face = side == 0 ? new Vector3(a, 0f, middle - depth / 2f) : new Vector3(side * width / 2f, 0f, middle + a);
                    var pier = index % 4 == 2;
                    var size = pier
                        ? (side == 0 ? new Vector3(0.7f, pierHeight, 0.7f) : new Vector3(0.7f, pierHeight, 0.7f))
                        : new Vector3(0.12f, pierHeight, 0.12f);
                    Block(tower, pier ? "Pier" : "Mullion", pier ? stone : bronze, face + Vector3.up * (podiumTop + 0.35f), size);
                }
            }

            // Every floor: a spandrel band and a ledge, and a pane in every bay, lit or dark; every
            // tenth floor is plant, behind louvres.
            var floors = Mathf.FloorToInt((top - podiumTop - 0.35f) / TowerStorey);
            for (var f = 0; f < floors; f++)
            {
                var slab = top - f * TowerStorey;
                Block(tower, "Spandrel", stone, new Vector3(0f, slab - 0.9f, middle), new Vector3(width + 0.12f, 0.9f, depth + 0.12f));
                Block(tower, "Ledge", stoneDark, new Vector3(0f, slab - 0.95f, middle), new Vector3(width + 0.5f, 0.1f, depth + 0.5f));
                var plant = f % 10 == 9;
                var paneHeight = TowerStorey - 1.3f;
                var paneMiddle = slab - 0.9f - (TowerStorey - 0.9f) / 2f;
                foreach (var side in new[] { 0, -1, 1 })
                {
                    var span = side == 0 ? width : depth;
                    var outward = side == 0 ? Vector3.back : Vector3.right * side;
                    var along = side == 0 ? Vector3.right : Vector3.forward;
                    var turn = Quaternion.Euler(0f, side == 0 ? 0f : side * -90f, 0f);
                    if (plant)
                    {
                        // Louvres across the whole floor: slats, a little proud of the glass.
                        var louvreAt = (side == 0 ? new Vector3(0f, 0f, middle - depth / 2f) : new Vector3(side * width / 2f, 0f, middle)) + outward * 0.05f;
                        var louvreSize = side == 0 ? new Vector3(width - 0.4f, 0.12f, 0.1f) : new Vector3(0.1f, 0.12f, depth - 0.4f);
                        for (var y = slab - TowerStorey + 0.2f; y < slab - 1.0f; y += 0.35f)
                            Block(tower, "Louvre", louvre, louvreAt + Vector3.up * y, louvreSize);
                        continue;
                    }
                    for (var a = -span / 2f + TowerBay; a < span / 2f - 0.5f; a += TowerBay)
                    {
                        var face = side == 0 ? new Vector3(a, 0f, middle - depth / 2f) : new Vector3(side * width / 2f, 0f, middle + a);
                        // Clear glass where the camera climbs, onto real rooms; painted rooms elsewhere.
                        var centreX = a + TowerBay / 2f;
                        var clearGlass = side == 0 && centreX > BandWest && centreX < BandEast;
                        Shape(tower, PrimitiveType.Quad, clearGlass ? "Glass" : "Window", clearGlass ? clear : card,
                            face + along * (TowerBay / 2f) + outward * 0.02f + Vector3.up * paneMiddle, new Vector3(TowerBay - 0.3f, paneHeight, 1f), turn);
                    }
                }
            }

            var rooms = new GameObject("Rooms").transform;
            rooms.SetParent(tower, false);
            for (var f = 0; f < floors; f++)
                if (f % 10 != 9)
                    TowerRoom(rooms, top - f * TowerStorey - TowerStorey, f);
            TowerLift(tower, ground + 0.15f, top - TowerStorey);
        }

        // The panes' painted rooms (SH_InteriorCard), sized to the panes.
        private static Material InteriorCards()
        {
            const string path = "Assets/_Project/Art/Materials/MAT_Tower_InteriorCard.mat";
            var shader = Shader.Find("Game/InteriorCard");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetVector("_PaneSize", new Vector4(TowerBay - 0.3f, TowerStorey - 1.3f, 0f, 0f));
            material.SetFloat("_RoomDepth", 5.5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // One floor behind the clear glass where the camera climbs (the user's notes, 2026-11-10:
        // "through the window you can see each floor's layout"; the floors are the boss's empire, and
        // every object as crisp as the kitchen's). Each floor is a part of his food business, from
        // the modelled kit (Kit.cs) and the kitchen's own appliances: a test kitchen with chefs at
        // the hobs, a photo studio shooting a dish (its flash going), the social-media room with
        // walls of screens, the reservations call centre, the accountants and the safe, the staff
        // canteen, the uniform room; now and then a floor being fitted out. `floorY` is its level.
        private static void TowerRoom(Transform rooms, float floorY, int f)
        {
            var room = new GameObject($"Floor{f}").transform;
            room.SetParent(rooms, false);
            room.localPosition = new Vector3(0f, floorY, 0f);
            var front = TowerFace + 0.15f;
            var back = TowerFace + RoomDepth;
            var ceiling = TowerStorey - 0.9f;
            var fitOut = f % 11 == 6;
            var kind = fitOut ? -1 : (f * 3 + 1) % 7; // steps through all seven, never the same twice running
            Color[] walls = { new Color(0.86f, 0.8f, 0.68f), new Color(0.72f, 0.78f, 0.7f), new Color(0.84f, 0.68f, 0.56f), new Color(0.9f, 0.86f, 0.78f) };
            Color[] carpets = { new Color(0.32f, 0.35f, 0.42f), new Color(0.45f, 0.36f, 0.28f), new Color(0.3f, 0.4f, 0.34f), new Color(0.5f, 0.48f, 0.45f) };
            var wall = Toon($"TowerWall{f % walls.Length}", walls[f % walls.Length]);
            var floor = kind == 0 || kind == 5
                ? Tiles("TowerTiles", new Color(0.82f, 0.8f, 0.76f), new Color(0.6f, 0.58f, 0.55f), 0.6f)
                : Toon($"TowerCarpet{f % carpets.Length}", carpets[f % carpets.Length]);
            var white = Toon("TowerCeiling", new Color(0.9f, 0.89f, 0.86f));
            var warm = f % 3 != 0;
            var glow = warm ? new Color(1f, 0.9f, 0.72f) : new Color(0.88f, 0.94f, 1f);

            // The room's shell: floor and ceiling either side of and behind the lift shaft (so
            // neither cuts through the cab), the back wall with a door, the band's side walls,
            // a skirting board, light panels in the ceiling.
            foreach (var (x0, x1, z0) in new[] { (BandWest, -ShaftHalfWidth - 0.1f, front), (ShaftHalfWidth + 0.1f, BandEast, front), (-ShaftHalfWidth - 0.1f, ShaftHalfWidth + 0.1f, ShaftNorth + 0.1f) })
            {
                Cube(room, "Floor", floor, new Vector3((x0 + x1) / 2f, 0.005f, (z0 + back) / 2f), new Vector3(x1 - x0, 0.02f, back - z0));
                Cube(room, "Ceiling", white, new Vector3((x0 + x1) / 2f, ceiling - 0.06f, (z0 + back) / 2f), new Vector3(x1 - x0, 0.05f, back - z0));
            }
            Block(room, "BackWall", wall, new Vector3((BandWest + BandEast) / 2f, 0f, back), new Vector3(BandEast - BandWest, ceiling, 0.12f));
            Block(room, "Skirting", white, new Vector3((BandWest + BandEast) / 2f, 0f, back - 0.08f), new Vector3(BandEast - BandWest, 0.12f, 0.04f));
            foreach (var x in new[] { BandWest, BandEast })
                Block(room, "SideWall", wall, new Vector3(x, 0f, (front + back) / 2f), new Vector3(0.12f, ceiling, back - front));
            var doorX = f % 2 == 0 ? -8.3f : 8f;
            Block(room, "Door", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), new Vector3(doorX, 0f, back - 0.07f), new Vector3(1f, 2.1f, 0.04f));
            foreach (var x in new[] { -6.9f, -4.1f, 4f, 6.8f })
                Kit.CeilingPanel(room, new Vector3(x, ceiling - 0.06f, front + RoomDepth * 0.45f), glow, lit: !fitOut);
            if (!fitOut)
                foreach (var x in new[] { -5.5f, 5.4f })
                    Point(room, "RoomLight", new Vector3(x, ceiling - 0.5f, front + RoomDepth * 0.45f), warm ? Incandescent : new Color(0.85f, 0.92f, 1f), 9f, 3.2f);

            var westMid = (BandWest - ShaftHalfWidth) / 2f;   // the middle of the room west of the lift
            var eastMid = (BandEast + ShaftHalfWidth) / 2f;   // and east of it
            switch (kind)
            {
                case 0: // the test kitchen: a line of appliances along the back, an island, chefs cooking; a tasting table
                {
                    var line = new[] { "PF_Fridge", "PF_Counter", "PF_Hob", "PF_Hob", "PF_CuttingBoard", "PF_Counter" };
                    for (var i = 0; i < line.Length; i++)
                        Prop(room, line[i], new Vector3(BandWest + 0.75f + i, 0f, back - 0.6f), 180f);
                    for (var i = 0; i < 3; i++)
                        Prop(room, i == 1 ? "PF_CuttingBoard" : "PF_Counter", new Vector3(westMid - 1f + i, 0f, front + 3f), 0f);
                    Cook(room, new Vector3(BandWest + 3.2f, 0f, back - 1.5f), 0f);
                    var runner = Cook(room, new Vector3(westMid, 0f, front + 3.9f), 180f);
                    if (runner != null) // between the island and the line
                        Walk(runner, new Vector3(westMid - 1.2f, 0f, front + 3.9f), new Vector3(westMid + 1.4f, 0f, front + 3.9f), 0.9f, 1.8f, f * 0.37f % 1f);
                    Steam(room, new Vector3(BandWest + 2.75f, 1.0f, back - 0.6f));
                    Steam(room, new Vector3(BandWest + 3.75f, 1.0f, back - 0.6f));
                    var fire = Point(room, "HobFire", new Vector3(BandWest + 3.2f, 1.3f, back - 0.9f), new Color(1f, 0.6f, 0.3f), 4.5f, 2.6f);
                    fire.gameObject.AddComponent<FlickerLight>().Set(fire, FlickerLight.Mood.Fire, null, 0.2f, f);
                    fire.gameObject.isStatic = false;
                    Kit.Part(room, "SM_Dining", "TableBody", Toon("KitTableTop", new Color(0.62f, 0.42f, 0.26f)), new Vector3(eastMid, 0f, front + 3.2f));
                    foreach (var side in new[] { -1f, 1f })
                        Kit.Part(room, "SM_Dining", "DiningChair", Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f)), new Vector3(eastMid + side * 0.75f, 0f, front + 3.2f), side < 0f ? -90f : 90f);
                    foreach (var side in new[] { -1f, 1f })
                        Seated(room, "Taster", new Vector3(eastMid + side * 0.75f, 0f, front + 3.2f), side < 0f ? 90f : -90f, 0.44f);
                    Talker(room, "HeadChef", new Vector3(eastMid + 0.2f, 0f, front + 4.3f), 190f);
                    Prop(room, "PF_Shelf", new Vector3(BandEast - 0.8f, 0f, back - 0.6f), 180f);
                    break;
                }
                case 1: // the photo studio: a dish on a table before a paper backdrop, softboxes, a camera flashing
                {
                    Kit.Backdrop(room, new Vector3(westMid, 0f, back - 1.2f), 180f, new Color(0.95f, 0.75f, 0.3f));
                    Kit.BistroTable(room, new Vector3(westMid, 0f, back - 2.2f));
                    Kit.Bottle(room, new Vector3(westMid + 0.12f, 0.762f, back - 2.2f), new Color(0.55f, 0.2f, 0.25f));
                    Kit.Softbox(room, new Vector3(westMid - 2f, 0f, back - 3.2f), 40f);
                    Kit.Softbox(room, new Vector3(westMid + 2f, 0f, back - 3.2f), -40f);
                    var camera = Kit.TripodCamera(room, new Vector3(westMid + 0.3f, 0f, front + 2.2f), 0f);
                    Flash(camera, camera, 2.5f + (f % 3) * 0.7f, new Vector3(0f, 0.08f, 0.05f));
                    var shooter = Passerby(room, "Shooter", new Vector3(westMid - 0.5f, 0f, front + 1.7f), 10f);
                    Walk(shooter, new Vector3(westMid - 1.3f, 0f, front + 1.7f), new Vector3(westMid - 0.4f, 0f, front + 1.7f), 0.6f, 2.5f, 0.2f);
                    Talker(room, "Stylist", new Vector3(westMid + 1.1f, 0f, back - 2.6f), -120f);
                    Desk(room, new Vector3(eastMid - 1f, 0f, front + 2.6f), new Color(0.6f, 0.8f, 1f));
                    Desk(room, new Vector3(eastMid + 1.2f, 0f, front + 2.6f), new Color(1f, 0.75f, 0.55f));
                    Talker(room, "Editor", new Vector3(eastMid + 0.2f, 0f, front + 4.6f), 170f);
                    break;
                }
                case 2: // the social-media room: walls of screens showing the feeds, desks facing them
                {
                    var pink = new Color(1f, 0.45f, 0.65f);
                    var blue = new Color(0.45f, 0.75f, 1f);
                    var gold = new Color(1f, 0.82f, 0.4f);
                    Kit.ScreenWall(room, new Vector3(westMid, 0.95f, back - 0.1f), 180f, pink, blue, gold);
                    Kit.ScreenWall(room, new Vector3(eastMid, 0.95f, back - 0.1f), 180f, gold, pink, blue);
                    foreach (var mid in new[] { westMid, eastMid })
                    for (var i = -1; i <= 1; i++)
                        Desk(room, new Vector3(mid + i * 1.9f, 0f, front + 2.3f), i == 0 ? pink : blue);
                    Talker(room, "Poster", new Vector3(westMid + 1f, 0f, front + 4.2f), 0f);
                    var pacer = Passerby(room, "Poster", new Vector3(eastMid - 1.4f, 0f, front + 4.4f), 90f);
                    Walk(pacer, new Vector3(eastMid - 1.6f, 0f, front + 4.4f), new Vector3(eastMid + 1.6f, 0f, front + 4.4f), 0.8f, 2f, 0.6f);
                    // The feeds never stop: the walls' light jumps as the posts change.
                    foreach (var (mid, colour) in new[] { (westMid, pink), (eastMid, gold) })
                    {
                        var feed = Point(room, "FeedGlow", new Vector3(mid, 1.6f, back - 1.2f), colour, 6f, 2.2f);
                        feed.gameObject.AddComponent<FlickerLight>().Set(feed, FlickerLight.Mood.Television, null, 0.6f, f + mid);
                        feed.gameObject.isStatic = false;
                    }
                    break;
                }
                case 3: // reservations: rows of desks between partitions, the phones never stopping
                {
                    foreach (var mid in new[] { westMid, eastMid })
                    foreach (var z in new[] { front + 2.2f, front + 4.9f })
                    for (var i = -1; i <= 1; i++)
                    {
                        Desk(room, new Vector3(mid + i * 1.8f, 0f, z), new Color(0.7f, 0.9f, 0.8f));
                        if (i < 1)
                            Kit.Cubicle(room, new Vector3(mid + i * 1.8f + 0.9f, 0f, z - 0.2f), 90f, new Color(0.35f, 0.5f, 0.6f));
                    }
                    var supervisor = Passerby(room, "Supervisor", new Vector3(westMid - 2.4f, 0f, front + 3.6f), 90f);
                    Walk(supervisor, new Vector3(westMid - 2.4f, 0f, front + 3.6f), new Vector3(westMid + 2.4f, 0f, front + 3.6f), 0.8f, 1.5f, 0.1f);
                    Talker(room, "Operator", new Vector3(eastMid - 0.3f, 0f, front + 3.5f), -70f);
                    break;
                }
                case 4: // the accountants: the safe standing open to its money, filing cabinets, desks, the figures on a board
                {
                    Kit.Safe(room, new Vector3(westMid - 1.6f, 0f, back - 0.6f), 180f);
                    Kit.Part(room, "SM_Dining", "TableBody", Toon("KitTableTop", new Color(0.62f, 0.42f, 0.26f)), new Vector3(westMid + 0.6f, 0f, back - 1.6f));
                    Kit.Money(room, new Vector3(westMid + 0.6f, 0.75f, back - 1.6f), 20f);
                    Kit.Money(room, new Vector3(westMid + 0.9f, 0.75f, back - 1.35f), -30f);
                    for (var i = 0; i < 3; i++)
                    {
                        Kit.Part(room, "SM_Office", "OfficeFilingBody", Toon("OfficeFiling", new Color(0.45f, 0.5f, 0.48f)), new Vector3(BandWest + 0.6f + i * 0.6f, 0f, back - 0.45f), 180f);
                        Kit.Part(room, "SM_Office", "OfficeFilingHandles", Toon("OfficeBrass", Brass), new Vector3(BandWest + 0.6f + i * 0.6f, 0f, back - 0.45f), 180f);
                    }
                    Desk(room, new Vector3(westMid, 0f, front + 2.4f), new Color(0.75f, 0.95f, 0.7f));
                    Kit.Whiteboard(room, new Vector3(eastMid, 0.9f, back - 0.07f), 180f);
                    Desk(room, new Vector3(eastMid - 1.2f, 0f, front + 2.4f), new Color(0.75f, 0.95f, 0.7f));
                    Desk(room, new Vector3(eastMid + 1f, 0f, front + 2.4f), new Color(0.75f, 0.95f, 0.7f));
                    Talker(room, "Counting", new Vector3(westMid + 1.7f, 0f, back - 1.9f), -100f);
                    var carrier = Passerby(room, "Accountant", new Vector3(eastMid - 2f, 0f, front + 4.6f), 90f);
                    Walk(carrier, new Vector3(eastMid - 2f, 0f, front + 4.6f), new Vector3(eastMid + 2f, 0f, front + 4.6f), 0.9f, 2.2f, 0.4f);
                    break;
                }
                case 5: // the staff canteen: tables, a counter with the coffee machine, a vending machine, the water cooler
                {
                    var wood = Toon("KitTableTop", new Color(0.62f, 0.42f, 0.26f));
                    var chair = Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f));
                    foreach (var (x, z) in new[] { (westMid - 1.6f, front + 2.6f), (westMid + 1.4f, front + 3.4f), (eastMid, front + 2.8f) })
                    {
                        Kit.Part(room, "SM_Dining", "TableBody", wood, new Vector3(x, 0f, z));
                        foreach (var side in new[] { -1f, 1f })
                        {
                            Kit.Part(room, "SM_Dining", "DiningChair", chair, new Vector3(x + side * 0.75f, 0f, z), side < 0f ? -90f : 90f);
                            var eater = Seated(room, "Eating", new Vector3(x + side * 0.75f, 0f, z), side < 0f ? 90f : -90f, 0.44f);
                            if (eater != null && side < 0f)
                            {
                                eater.RightShoulder.localRotation = Quaternion.Euler(-55f, 0f, -10f);
                                Wobble(eater.RightShoulder.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(-50f, 0f, 0f), 2.2f + (float)_random.NextDouble() * 2f, 0.4f);
                            }
                        }
                    }
                    for (var i = 0; i < 3; i++)
                    {
                        Kit.Part(room, "SM_Dining", "BarBody", Toon("OfficeWalnut", Walnut), new Vector3(westMid - 1f + i, 0f, back - 0.5f), 180f);
                        Kit.Part(room, "SM_Dining", "BarTop", Toon("OfficeBrass", Brass), new Vector3(westMid - 1f + i, 0f, back - 0.5f), 180f);
                    }
                    Kit.CoffeeMachine(room, new Vector3(westMid, 1.0f, back - 0.5f), 180f);
                    Kit.Vending(room, new Vector3(BandEast - 0.8f, 0f, back - 0.6f), 180f, new Color(0.8f, 0.25f, 0.2f));
                    Kit.Vending(room, new Vector3(BandEast - 1.9f, 0f, back - 0.6f), 180f, new Color(0.2f, 0.45f, 0.7f));
                    Kit.WaterCooler(room, new Vector3(ShaftHalfWidth + 0.6f, 0f, back - 0.4f), 180f);
                    Talker(room, "Colleague", new Vector3(westMid + 0.6f, 0f, back - 1.5f), 0f);
                    var queuer = Passerby(room, "Colleague", new Vector3(eastMid - 1.5f, 0f, back - 1.6f), 90f);
                    Walk(queuer, new Vector3(eastMid - 1.5f, 0f, back - 1.6f), new Vector3(BandEast - 1.4f, 0f, back - 1.6f), 0.8f, 3f, 0.5f);
                    break;
                }
                case 6: // the uniform room: rails of chef's whites, a sofa to wait on
                {
                    Color[] whites = { new Color(0.95f, 0.95f, 0.93f), new Color(0.2f, 0.2f, 0.24f), new Color(0.75f, 0.25f, 0.2f) };
                    foreach (var mid in new[] { westMid - 1.2f, westMid + 1.6f, eastMid })
                    {
                        Kit.Part(room, "SM_Office", "OfficeRail", Toon("OfficeBrass", Brass), new Vector3(mid, 0f, back - 1.4f), 0f);
                        for (var j = 0; j < 5; j++)
                            Kit.Part(room, "SM_Office", "OfficeJacket", Toon($"OfficeJacket{(j + f) % 3}", whites[(j + f) % 3]), new Vector3(mid - 0.6f + j * 0.3f, 1.6f, back - 1.4f), 90f);
                    }
                    Sofa(room, new Vector3(eastMid + 0.4f, 0f, front + 2.2f), 180f, new Color(0.3f, 0.45f, 0.6f));
                    Cook(room, new Vector3(westMid + 0.2f, 0f, front + 2.6f), 160f);
                    Talker(room, "Tailor", new Vector3(westMid - 0.8f, 0f, front + 3f), 100f);
                    var fitter = Passerby(room, "Fitter", new Vector3(westMid - 2.4f, 0f, back - 2.3f), 90f);
                    Walk(fitter, new Vector3(westMid - 2.4f, 0f, back - 2.3f), new Vector3(westMid + 2.6f, 0f, back - 2.3f), 0.8f, 2f, 0.3f);
                    Seated(room, "Waiting", new Vector3(eastMid + 0.4f, 0f, front + 2.2f), 180f, 0.42f);
                    break;
                }
                default: // being fitted out: dust sheets, a ladder, paint, a work light, an exit sign
                {
                    var sheet = Toon("TowerDustSheet", new Color(0.82f, 0.82f, 0.8f));
                    Block(room, "Sheeted", sheet, new Vector3(westMid, 0f, front + 3f), new Vector3(2.4f, 1.1f, 1.4f));
                    Block(room, "Sheeted", sheet, new Vector3(eastMid, 0f, front + 4f), new Vector3(1.8f, 0.9f, 1.6f));
                    var ladder = Block(room, "Ladder", Toon("TowerLadder", new Color(0.85f, 0.7f, 0.3f)), new Vector3(3f, 0f, front + 2f), new Vector3(0.5f, 2f, 0.08f));
                    if (ladder != null)
                        ladder.transform.localRotation = Quaternion.Euler(10f, 20f, 0f);
                    Cube(room, "ExitSign", Unlit("TowerExit", new Color(0.3f, 1f, 0.45f)), new Vector3(doorX, 2.25f, back - 0.08f), new Vector3(0.4f, 0.16f, 0.03f));
                    Point(room, "WorkLight", new Vector3(westMid - 1f, 1.4f, front + 2f), Incandescent, 5f, 1.6f);
                    break;
                }
            }
            if (!fitOut)
            {
                // Every floor is busy (the user's note, 2026-11-10: activity and movement on each):
                // people walk the aisle by the window either side of the lift, back and forth,
                // stopping at each end; cooks on the kitchen floors.
                var cooks = kind == 0 || kind == 5;
                foreach (var (x0, x1) in new[] { (BandWest + 1.5f, -ShaftHalfWidth - 0.7f), (ShaftHalfWidth + 0.7f, BandEast - 0.9f) })
                {
                    var from = new Vector3(x0, 0f, front + 0.7f);
                    var to = new Vector3(x1, 0f, front + 0.7f);
                    var walker = cooks && x0 < 0f ? Cook(room, from, 90f) : Passerby(room, "Walker", from, 90f);
                    if (walker != null)
                        Walk(walker, from, to, 0.9f + (float)_random.NextDouble() * 0.5f, 1f + (float)_random.NextDouble() * 2.5f, (float)_random.NextDouble());
                }
                PottedPlant(room, new Vector3(BandWest + 0.6f, 0f, front + 0.8f), 1.05f);
                if (kind != 5)
                    Kit.WaterCooler(room, new Vector3(-ShaftHalfWidth - 0.5f, 0f, back - 0.4f), 180f);
            }
        }

        // One of the boss's cooks, in whites and toque.
        private static Transform Cook(Transform parent, Vector3 at, float facing)
        {
            var cook = new GameObject("Cook").transform;
            cook.SetParent(parent, false);
            cook.localPosition = at;
            cook.localRotation = Quaternion.Euler(0f, facing, 0f);
            if (CharacterArt.Build(cook, CharacterArt.PlayerChef()) != null)
                return cook;
            Object.DestroyImmediate(cook.gameObject);
            return null;
        }

        // A desk with someone at it (most of the time): sitting with their back to the window,
        // typing, their screen glowing out at the street.
        private static void Desk(Transform room, Vector3 at, Color screen)
        {
            Kit.WorkStation(room, at, 0f, screen);
            if (_random.NextDouble() < 0.2)
                return; // away from their desk
            var worker = Seated(room, "Worker", at + new Vector3(0.1f, 0f, -0.75f), 0f, 0.46f);
            if (worker == null)
                return;
            worker.RightShoulder.localRotation = Quaternion.Euler(-68f, 0f, -8f);
            worker.LeftShoulder.localRotation = Quaternion.Euler(-68f, 0f, 8f);
            Wobble(worker.RightShoulder.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(5f, 0f, 0f), 0.3f + (float)_random.NextDouble() * 0.2f);
            Wobble(worker.LeftShoulder.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(5f, 0f, 0f), 0.36f + (float)_random.NextDouble() * 0.2f);
        }

        // Someone standing and talking, an arm going as they make their point.
        private static void Talker(Transform room, string name, Vector3 at, float facing)
        {
            var rig = Figure(room, name, at, facing);
            if (rig == null)
                return;
            Wobble(rig.RightShoulder.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(-60f, 0f, 0f), 1.8f + (float)_random.NextDouble() * 1.4f, 0.45f);
            Breathe(rig.Figure != null ? rig.Figure.gameObject : room.gameObject);
        }

        // The tower's lift (the user's notes, 2026-11-10): inside the building, in the same place as
        // the boss's floor's elevator, so the chef who gets in at the street comes out upstairs. A
        // glass cab between bronze posts, seen through the front glass as the camera climbs beside it;
        // OpeningElevator drives it.
        private static void TowerLift(Transform tower, float bottom, float topStop)
        {
            var bronze = Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f));
            var brass = Toon("OfficeBrass", Brass);
            var shaft = new GameObject("TowerLift").transform;
            shaft.SetParent(tower, false);
            var height = topStop + 2.8f - bottom;
            foreach (var x in new[] { -ShaftHalfWidth, ShaftHalfWidth })
            foreach (var z in new[] { ShaftSouth + 0.25f, ShaftNorth })
                Block(shaft, "Post", bronze, new Vector3(x, bottom, z), new Vector3(0.14f, height, 0.14f));
            // A bronze frame round the shaft at every floor.
            for (var y = topStop; y > bottom + 3f; y -= TowerStorey)
            {
                foreach (var z in new[] { ShaftSouth + 0.25f, ShaftNorth })
                    Block(shaft, "Ring", bronze, new Vector3(0f, y, z), new Vector3(2f * ShaftHalfWidth, 0.12f, 0.12f));
                foreach (var x in new[] { -ShaftHalfWidth, ShaftHalfWidth })
                    Block(shaft, "Ring", bronze, new Vector3(x, y, (ShaftSouth + ShaftNorth) / 2f + 0.12f), new Vector3(0.12f, 0.12f, ShaftNorth - ShaftSouth - 0.25f));
            }

            // The cab: floor, roof, brass posts, glass on the front and sides, its doors at the back
            // (towards the lobby, as upstairs), a warm light inside.
            var cab = new GameObject("Cab").transform;
            cab.SetParent(shaft, false);
            const float w = 3.5f, h = 2.5f;
            var cabDepth = ShaftNorth - ShaftSouth - 0.45f;
            cab.localPosition = new Vector3(0f, bottom, (ShaftSouth + ShaftNorth) / 2f + 0.12f);
            Block(cab, "Floor", brass, new Vector3(0f, -0.15f, 0f), new Vector3(w, 0.15f, cabDepth));
            Block(cab, "Roof", brass, new Vector3(0f, h, 0f), new Vector3(w, 0.15f, cabDepth));
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                Block(cab, "CabPost", brass, new Vector3(x * (w / 2f - 0.05f), 0f, z * (cabDepth / 2f - 0.05f)), new Vector3(0.1f, h, 0.1f));
            Block(cab, "Handrail", brass, new Vector3(0f, 0.95f, -cabDepth / 2f + 0.08f), new Vector3(w - 0.2f, 0.05f, 0.05f));
            var glass = PilotArt.EnsureGlassMaterial("OfficeWindow", new Color(0.62f, 0.76f, 0.86f, 0.18f));
            Cube(cab, "GlassFront", glass, new Vector3(0f, 0f, -cabDepth / 2f), new Vector3(w - 0.1f, h, 0.03f));
            foreach (var x in new[] { -1f, 1f })
                Cube(cab, "GlassSide", glass, new Vector3(x * w / 2f, 0f, 0f), new Vector3(0.03f, h, cabDepth - 0.1f));
            foreach (var x in new[] { -1f, 1f })
                Block(cab, "Door", Toon("OfficeLiftDoor", new Color(0.62f, 0.5f, 0.3f)), new Vector3(x * w / 4f, 0f, cabDepth / 2f - 0.05f), new Vector3(w / 2f - 0.04f, h, 0.06f));
            Cube(cab, "StripLight", Unlit("OfficeBulb", Bulb), new Vector3(0f, h - 0.06f, 0f), new Vector3(w - 0.6f, 0.04f, 0.3f));
            Point(cab, "CabLight", new Vector3(0f, h - 0.5f, -0.3f), Incandescent, 5f, 2.4f);
            MakeDynamic(cab.gameObject);

            var lift = new SerializedObject(shaft.gameObject.AddComponent<OpeningElevator>());
            lift.FindProperty("_cab").objectReferenceValue = cab;
            lift.FindProperty("_bottom").floatValue = bottom;
            lift.FindProperty("_top").floatValue = topStop;
            lift.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
