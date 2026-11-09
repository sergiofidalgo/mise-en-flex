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
        private const float LiftDepth = 2.2f;
        private static float LiftZ => TowerFace - 0.3f - LiftDepth / 2f;

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
            var lit = WindowArt.LitWindows()[2]; // offices, behind the curtain wall: the same light on every floor
            var dark = WindowArt.DarkWindow();
            var ground = -CityDepth;
            var top = -1.4f; // under the penthouse's cornice

            // The glass behind everything, so no gap ever shows the sky through the tower.
            Cube(tower, "Core", Toon("OfficeTowerGlass", new Color(0.17f, 0.21f, 0.31f)), new Vector3(0f, ground, middle), new Vector3(width - 0.2f, CityDepth - 0.8f, depth - 0.2f));
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
                        var isLit = _random.NextDouble() < 0.3;
                        var pane = Shape(tower, PrimitiveType.Quad, isLit ? "LitOffice" : "Pane", isLit ? lit : dark,
                            face + along * (TowerBay / 2f) + outward * 0.02f + Vector3.up * paneMiddle, new Vector3(TowerBay - 0.3f, paneHeight, 1f), turn);
                        // Now and then someone crosses a lit office, for the climb to pass.
                        if (isLit && pane != null && _random.NextDouble() < 0.12)
                            PassingSilhouette(pane);
                    }
                }
            }

            GlassLift(tower, podiumTop + 0.4f, top - 3.2f);
        }

        // The glass lift on the tower's front: bronze rails up the face and a lit glass cab with
        // the chef in it, which OpeningElevator drives during the opening's climb.
        private static void GlassLift(Transform tower, float bottom, float topStop)
        {
            var bronze = Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f));
            var brass = Toon("OfficeBrass", Brass);
            var shaft = new GameObject("GlassLift").transform;
            shaft.SetParent(tower, false);
            // Two rails and a tie at every floor, up the middle of the front.
            foreach (var x in new[] { -1.35f, 1.35f })
                Block(shaft, "Rail", bronze, new Vector3(x, bottom - 0.4f, TowerFace - 0.35f), new Vector3(0.16f, topStop + 3f - bottom, 0.25f));
            for (var y = bottom + 3f; y < topStop + 2.8f; y += TowerStorey)
                Block(shaft, "Tie", bronze, new Vector3(0f, y, TowerFace - 0.2f), new Vector3(2.9f, 0.1f, 0.12f));

            // The cab: floor, roof, brass posts, glass on three sides, a warm light inside.
            var cab = new GameObject("Cab").transform;
            cab.SetParent(shaft, false);
            cab.localPosition = new Vector3(0f, bottom, LiftZ);
            const float w = 2.4f, h = 2.7f;
            Block(cab, "Floor", brass, new Vector3(0f, -0.2f, 0f), new Vector3(w, 0.2f, LiftDepth));
            Block(cab, "Roof", brass, new Vector3(0f, h, 0f), new Vector3(w, 0.18f, LiftDepth));
            Block(cab, "RoofTrim", bronze, new Vector3(0f, h + 0.18f, 0f), new Vector3(w - 0.4f, 0.12f, LiftDepth - 0.4f));
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                Block(cab, "Post", brass, new Vector3(x * (w / 2f - 0.05f), 0f, z * (LiftDepth / 2f - 0.05f)), new Vector3(0.1f, h, 0.1f));
            Block(cab, "Handrail", brass, new Vector3(0f, 0.95f, -LiftDepth / 2f + 0.08f), new Vector3(w - 0.2f, 0.05f, 0.05f));
            var glass = PilotArt.EnsureGlassMaterial("OfficeWindow", new Color(0.62f, 0.76f, 0.86f, 0.18f));
            Cube(cab, "GlassFront", glass, new Vector3(0f, 0f, -LiftDepth / 2f), new Vector3(w - 0.1f, h, 0.03f));
            foreach (var x in new[] { -1f, 1f })
                Cube(cab, "GlassSide", glass, new Vector3(x * w / 2f, 0f, 0f), new Vector3(0.03f, h, LiftDepth - 0.1f));
            // A warm back panel and a strip light, so the chef reads against them from outside.
            Cube(cab, "BackPanel", Unlit("OfficeLiftPanel", new Color(0.95f, 0.78f, 0.52f)), new Vector3(0f, 0f, LiftDepth / 2f - 0.04f), new Vector3(w - 0.1f, h, 0.02f));
            Cube(cab, "StripLight", Unlit("OfficeBulb", Bulb), new Vector3(0f, h - 0.06f, 0f), new Vector3(w - 0.6f, 0.04f, 0.3f));
            Point(cab, "CabLight", new Vector3(0f, h - 0.5f, -0.4f), Incandescent, 5f, 2.2f);

            // The chef, in whites and toque, looking out over the city on the way up.
            var rider = new GameObject("Chef").transform;
            rider.SetParent(cab, false);
            rider.localPosition = new Vector3(0.25f, 0f, 0.1f);
            rider.localRotation = Quaternion.Euler(0f, 165f, 0f);
            if (CharacterArt.Build(rider, CharacterArt.PlayerChef()) != null)
                Breathe(rider.gameObject);
            MakeDynamic(cab.gameObject);

            var lift = new SerializedObject(shaft.gameObject.AddComponent<OpeningElevator>());
            lift.FindProperty("_cab").objectReferenceValue = cab;
            lift.FindProperty("_rider").objectReferenceValue = rider.gameObject;
            lift.FindProperty("_bottom").floatValue = bottom;
            lift.FindProperty("_top").floatValue = topStop;
            lift.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
