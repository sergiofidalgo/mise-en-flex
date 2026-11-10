using Game.Presentation.Office;
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
            var lobbyHalf = 8.2f;
            var sideWidth = width / 2f + proud - lobbyHalf;
            foreach (var sign in new[] { -1f, 1f })
                Block(tower, "Podium", stone, new Vector3(sign * (lobbyHalf + sideWidth / 2f), ground, TowerFace - proud / 2f), new Vector3(sideWidth, TowerPodium, proud));
            Block(tower, "PodiumLintel", stone, new Vector3(0f, ground + 4.35f, TowerFace - proud / 2f), new Vector3(2f * lobbyHalf, TowerPodium - 4.35f, proud));
            foreach (var sign in new[] { -1f, 1f })
                Block(tower, "PodiumSide", stone, new Vector3(sign * (width / 2f + proud / 2f), ground, middle), new Vector3(proud, TowerPodium, depth + 2f * proud));
            Block(tower, "PodiumLedge", stoneDark, new Vector3(0f, podiumTop, middle), new Vector3(width + 1.0f, 0.35f, depth + 1.0f));
            // Bronze lettering-band over the lobby doors: a plain strip, the boss needs no name up.
            Block(tower, "LobbyBand", bronze, new Vector3(0f, ground + 4.45f, TowerFace - proud - 0.05f), new Vector3(9f, 0.35f, 0.08f));

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

        // One floor's real rooms behind the clear glass where the camera climbs (the user's notes,
        // 2026-11-10: "through the window you can see each floor's layout"). Five kinds, each varied:
        // an open-plan office, a meeting room, a kitchen with a sofa, a creative floor with a
        // ping-pong table and beanbags, a floor being fitted out. Most are lit, by a ceiling panel
        // and one light; the dark ones keep a desk lamp or an exit sign. `floorY` is the floor's level.
        private static void TowerRoom(Transform rooms, float floorY, int f)
        {
            var room = new GameObject($"Floor{f}").transform;
            room.SetParent(rooms, false);
            room.localPosition = new Vector3(0f, floorY, 0f);
            var front = TowerFace + 0.15f;
            var back = TowerFace + RoomDepth;
            var ceiling = TowerStorey - 0.9f;
            var kind = _random.Next(5);
            var lit = kind != 4 && _random.NextDouble() < 0.8;
            Color[] walls = { new Color(0.86f, 0.8f, 0.68f), new Color(0.72f, 0.78f, 0.7f), new Color(0.84f, 0.68f, 0.56f), new Color(0.9f, 0.86f, 0.78f) };
            Color[] carpets = { new Color(0.32f, 0.35f, 0.42f), new Color(0.45f, 0.36f, 0.28f), new Color(0.3f, 0.4f, 0.34f), new Color(0.5f, 0.48f, 0.45f) };
            var w = _random.Next(walls.Length);
            var c = _random.Next(carpets.Length);
            var wall = Toon($"TowerWall{w}", walls[w]);
            var carpet = Toon($"TowerCarpet{c}", carpets[c]);
            var white = Toon("TowerCeiling", new Color(0.9f, 0.89f, 0.86f));
            var wood = Toon("OfficeTeak", new Color(0.55f, 0.38f, 0.24f));
            var dark = Toon("OfficeInk", Ink);

            // The room's shell: carpet and ceiling either side of and behind the lift shaft (so
            // neither cuts through the cab), the back wall with a door, the band's side walls.
            foreach (var (x0, x1, z0) in new[] { (BandWest, -ShaftHalfWidth - 0.1f, front), (ShaftHalfWidth + 0.1f, BandEast, front), (-ShaftHalfWidth - 0.1f, ShaftHalfWidth + 0.1f, ShaftNorth + 0.1f) })
            {
                Cube(room, "Carpet", carpet, new Vector3((x0 + x1) / 2f, 0.005f, (z0 + back) / 2f), new Vector3(x1 - x0, 0.02f, back - z0));
                Cube(room, "Ceiling", white, new Vector3((x0 + x1) / 2f, ceiling - 0.06f, (z0 + back) / 2f), new Vector3(x1 - x0, 0.05f, back - z0));
            }
            Block(room, "BackWall", wall, new Vector3((BandWest + BandEast) / 2f, 0f, back), new Vector3(BandEast - BandWest, ceiling, 0.12f));
            foreach (var x in new[] { BandWest, BandEast })
                Block(room, "SideWall", wall, new Vector3(x, 0f, (front + back) / 2f), new Vector3(0.12f, ceiling, back - front));
            var doorX = _random.NextDouble() < 0.5 ? -6f : 6f;
            Block(room, "Door", wood, new Vector3(doorX, 0f, back - 0.07f), new Vector3(1f, 2.1f, 0.04f));

            if (lit)
            {
                var warm = _random.NextDouble() < 0.6;
                var panel = Unlit(warm ? "TowerPanelWarm" : "TowerPanelCool", warm ? new Color(1f, 0.9f, 0.72f) : new Color(0.88f, 0.94f, 1f));
                foreach (var x in new[] { -5.5f, 5.5f })
                    Cube(room, "LightPanel", panel, new Vector3(x, ceiling - 0.09f, (front + back) / 2f), new Vector3(2.6f, 0.02f, 0.6f));
                foreach (var x in new[] { -5f, 5f })
                    Point(room, "RoomLight", new Vector3(x, ceiling - 0.5f, front + RoomDepth * 0.45f), warm ? Incandescent : new Color(0.85f, 0.92f, 1f), 9f, 3.4f);
            }
            else
            {
                // An exit sign over the door, and sometimes a desk lamp someone left on.
                Cube(room, "ExitSign", Unlit("TowerExit", new Color(0.3f, 1f, 0.45f)), new Vector3(doorX, 2.25f, back - 0.08f), new Vector3(0.4f, 0.16f, 0.03f));
                if (_random.NextDouble() < 0.5)
                    Point(room, "DeskLamp", new Vector3(-doorX * 0.6f, 1.1f, front + 3.5f), Incandescent, 3.5f, 1.4f);
            }

            switch (kind)
            {
                case 0: // open-plan: rows of desks facing the window, screens lit
                    foreach (var z in new[] { front + 2.6f, front + 4.9f })
                    foreach (var x in new[] { -8.0f, -6.3f, -4.6f, 3.4f, 5.1f, 6.8f })
                        Desk(room, new Vector3(x, 0f, z), lit, dark, wood);
                    break;
                case 1: // a meeting room on one side, armchairs on the other
                    Block(room, "Table", wood, new Vector3(-5.6f, 0.7f, front + 3.6f), new Vector3(4.2f, 0.06f, 1.4f));
                    Block(room, "TableBase", dark, new Vector3(-5.6f, 0f, front + 3.6f), new Vector3(2.8f, 0.7f, 0.3f));
                    for (var x = -7.2f; x <= -4f; x += 1.1f)
                    foreach (var z in new[] { front + 2.6f, front + 4.6f })
                        Block(room, "Chair", dark, new Vector3(x, 0f, z), new Vector3(0.5f, 0.85f, 0.5f));
                    Cube(room, "Screen", Unlit(lit ? "TowerScreenOn" : "TowerScreenOff", lit ? new Color(0.55f, 0.75f, 1f) : new Color(0.08f, 0.1f, 0.14f)), new Vector3(-5.6f, 0.9f, back - 0.08f), new Vector3(2.4f, 1.3f, 0.03f));
                    Armchair(room, new Vector3(4.2f, 0f, front + 3.2f), 200f, new Color(0.7f, 0.3f, 0.25f));
                    Armchair(room, new Vector3(6.4f, 0f, front + 3.2f), 160f, new Color(0.7f, 0.3f, 0.25f));
                    PottedPlant(room, new Vector3(7.8f, 0f, front + 1.2f), 1f);
                    break;
                case 2: // the floor's kitchen and a sofa
                    Block(room, "Counter", Toon("TowerCounter", new Color(0.9f, 0.9f, 0.88f)), new Vector3(-5.5f, 0f, back - 0.45f), new Vector3(5f, 0.92f, 0.65f));
                    Block(room, "Fridge", Toon("TowerFridge", new Color(0.78f, 0.8f, 0.82f)), new Vector3(-8.3f, 0f, back - 0.5f), new Vector3(0.8f, 1.9f, 0.7f));
                    Block(room, "Coffee", dark, new Vector3(-4.5f, 0.92f, back - 0.45f), new Vector3(0.4f, 0.45f, 0.4f));
                    Sofa(room, new Vector3(5.4f, 0f, front + 2.4f), 180f, new Color(0.3f, 0.45f, 0.6f));
                    Block(room, "LowTable", wood, new Vector3(5.4f, 0f, front + 1.2f), new Vector3(1.2f, 0.4f, 0.6f));
                    PottedPlant(room, new Vector3(-8.2f, 0f, front + 1.0f), 1.1f);
                    break;
                case 3: // a creative floor: a ping-pong table, beanbags, plants
                    Block(room, "PingPong", Toon("TowerPingPong", new Color(0.2f, 0.45f, 0.3f)), new Vector3(-5.6f, 0.72f, front + 3.4f), new Vector3(2.7f, 0.05f, 1.5f));
                    Block(room, "PingPongLegs", dark, new Vector3(-5.6f, 0f, front + 3.4f), new Vector3(2.2f, 0.72f, 0.1f));
                    Block(room, "Net", Toon("TowerCeiling", new Color(0.9f, 0.89f, 0.86f)), new Vector3(-5.6f, 0.77f, front + 3.4f), new Vector3(0.03f, 0.15f, 1.6f));
                    Color[] bags = { new Color(0.9f, 0.45f, 0.3f), new Color(0.95f, 0.8f, 0.3f), new Color(0.4f, 0.6f, 0.85f) };
                    for (var b = 0; b < 3; b++)
                        Shape(room, PrimitiveType.Sphere, "Beanbag", Toon($"TowerBeanbag{b}", bags[b]), new Vector3(4f + b * 1.4f, 0.3f, front + 2.2f + (b % 2) * 0.9f), new Vector3(0.9f, 0.6f, 0.9f));
                    foreach (var x in new[] { -8.2f, 8f })
                        PottedPlant(room, new Vector3(x, 0f, front + 1.0f), 1.2f);
                    break;
                default: // being fitted out: dust sheets, a ladder, paint, a work light
                    var sheet = Toon("TowerDustSheet", new Color(0.82f, 0.82f, 0.8f));
                    Block(room, "Sheeted", sheet, new Vector3(-5.5f, 0f, front + 3f), new Vector3(2.4f, 1.1f, 1.4f));
                    Block(room, "Sheeted", sheet, new Vector3(5f, 0f, front + 4f), new Vector3(1.8f, 0.9f, 1.6f));
                    var ladder = Block(room, "Ladder", Toon("TowerLadder", new Color(0.85f, 0.7f, 0.3f)), new Vector3(3f, 0f, front + 2f), new Vector3(0.5f, 2f, 0.08f));
                    if (ladder != null)
                        ladder.transform.localRotation = Quaternion.Euler(10f, 20f, 0f);
                    Shape(room, PrimitiveType.Cylinder, "Paint", Toon("TowerPaint", new Color(0.85f, 0.85f, 0.88f)), new Vector3(-3.6f, 0.15f, front + 1.6f), new Vector3(0.3f, 0.15f, 0.3f));
                    Point(room, "WorkLight", new Vector3(-6f, 1.4f, front + 2f), Incandescent, 5f, 1.6f);
                    break;
            }

            // Someone about on most lit floors: by a desk, at the window, or on the way somewhere.
            if (lit && kind != 4)
            {
                var people = 1 + _random.Next(2);
                for (var k = 0; k < people; k++)
                {
                    var x = _random.NextDouble() < 0.5 ? -4f - (float)_random.NextDouble() * 4f : 3f + (float)_random.NextDouble() * 4.6f;
                    Passerby(room, "OfficeWorker", new Vector3(x, 0f, front + 1.4f + (float)_random.NextDouble() * 4f), (float)_random.NextDouble() * 360f);
                }
            }
        }

        // A desk with its back to the window: whoever sits there faces into the room, so their screen
        // glows out at the street. A top, a modesty panel, the screen, the chair on the window side.
        private static void Desk(Transform room, Vector3 at, bool lit, Material dark, Material wood)
        {
            Block(room, "Desk", wood, at + new Vector3(0f, 0.72f, 0f), new Vector3(1.4f, 0.05f, 0.7f));
            Block(room, "DeskPanel", dark, at + new Vector3(0f, 0f, 0.3f), new Vector3(1.3f, 0.72f, 0.04f));
            Block(room, "Monitor", dark, at + new Vector3(0f, 0.77f, 0.1f), new Vector3(0.55f, 0.36f, 0.05f));
            Cube(room, "MonitorScreen", Unlit(lit ? "TowerScreenOn" : "TowerScreenOff", lit ? new Color(0.55f, 0.75f, 1f) : new Color(0.08f, 0.1f, 0.14f)),
                at + new Vector3(0f, 0.8f, 0.07f), new Vector3(0.5f, 0.3f, 0.01f));
            Block(room, "Chair", dark, at + new Vector3(0f, 0f, -0.65f), new Vector3(0.5f, 0.48f, 0.5f));
            Block(room, "ChairBack", dark, at + new Vector3(0f, 0.48f, -0.88f), new Vector3(0.5f, 0.5f, 0.06f));
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
