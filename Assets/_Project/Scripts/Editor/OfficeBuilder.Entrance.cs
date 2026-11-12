using Game.Data;
using Game.Presentation;
using Game.Presentation.Office;
using Game.Presentation.Office.Ambience;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The boss's tower at street level, where the opening stops for the menu (the user's notes,
    /// 2026-11-10). West, the tower's own lobby: marble, a reception desk, the lift, the doorman at
    /// its door; at the kerb waits player 1's chef, who walks in to the lift when a campaign is
    /// chosen. East, the boss's restaurant, Mise en Flex, behind clear glass under a painted fascia:
    /// a bar by the door, a raised dining room full of couples eating, waiters with trays, an open
    /// kitchen at the back. Outside: a queue behind a rope, nearly everyone on a phone (scrolling,
    /// filming, taking selfies, the flash from the phone's own LED), a red carpet with
    /// photographers, an influencer under a ring light, delivery riders waiting; now and then hearts,
    /// a like count or a review rises over a phone, as the game is about social media and reviews.
    /// Round it, a few market stalls among other places to eat, on a street closed to traffic.
    /// Furniture is the modelled kit (Kit.cs).
    /// </summary>
    public static partial class OfficeBuilder
    {
        // The two doors in the ground floor's glass: the tower's (west) and the restaurant's (east).
        private const float TowerDoorX = -5f;
        private const float RestaurantDoorX = 5f;
        private const float DoorHalfWidth = 0.86f;
        private const float LobbyHalf = 8f;
        // The restaurant: from the wall that divides it from the tower's lobby to the tower's east
        // corner, its dining room raised on a platform so it's seen over the heads outside.
        private const float DividerX = 2.3f;
        private const float RestaurantEast = 17.4f;
        private const float PlatformX = 8.3f;
        private const float PlatformHeight = 1.2f;

        private static readonly Color[] StallCanopies =
        {
            new Color(0.78f, 0.22f, 0.18f), new Color(0.2f, 0.48f, 0.36f), new Color(0.22f, 0.32f, 0.58f), new Color(0.9f, 0.6f, 0.2f),
        };
        private static readonly Color[][] StallProduce =
        {
            new[] { new Color(0.85f, 0.2f, 0.18f), new Color(0.95f, 0.6f, 0.15f), new Color(0.45f, 0.75f, 0.3f) },
            new[] { new Color(0.95f, 0.85f, 0.3f), new Color(0.55f, 0.25f, 0.5f), new Color(0.9f, 0.4f, 0.2f) },
            new[] { new Color(0.86f, 0.5f, 0.45f), new Color(0.75f, 0.3f, 0.3f) },
            new[] { new Color(0.6f, 0.8f, 0.4f), new Color(0.9f, 0.3f, 0.25f), new Color(0.95f, 0.75f, 0.4f) },
        };

        private enum PhoneUse { Selfie, Film, Scroll }

        private static int _popCount;
        private static Transform _streetRoot;

        private static void TowerEntrance(Transform street)
        {
            _popCount = 0;
            _streetRoot = street;
            var entrance = new GameObject("TowerEntrance").transform;
            entrance.SetParent(street, false);
            GroundFloorGlass(entrance);
            TowerLobby(entrance);
            Restaurant(entrance);
            Fascia(entrance);
            RedCarpet(entrance);
            Queue(entrance);
            WestPlace(entrance);
            EntranceMoments(entrance);
            LiftChef();
        }

        // The ground floor's front: clear glass between bronze mullions from the lobby to the
        // tower's east corner, the two doors standing open.
        private static void GroundFloorGlass(Transform entrance)
        {
            var glass = PilotArt.EnsureGlassMaterial("OfficeTowerPane", new Color(0.55f, 0.7f, 0.85f, 0.12f));
            var brass = Toon("OfficeBrass", Brass);
            var bronze = Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f));
            const float height = 4.2f;
            const float doorHeight = 2.8f;
            var z = TowerFace - 0.05f;
            var east = RestaurantEast - 0.2f;
            // Glass in pieces round the two door openings, and over them.
            var edges = new[] { -LobbyHalf, TowerDoorX - DoorHalfWidth, TowerDoorX + DoorHalfWidth, RestaurantDoorX - DoorHalfWidth, RestaurantDoorX + DoorHalfWidth, east };
            for (var i = 0; i < edges.Length - 1; i++)
            {
                var door = i % 2 == 1;
                var from = door ? doorHeight : 0f;
                Cube(entrance, door ? "Transom" : "Glass", glass, new Vector3((edges[i] + edges[i + 1]) / 2f, 0.15f + from, z), new Vector3(edges[i + 1] - edges[i], height - from, 0.04f));
            }
            for (var x = -LobbyHalf; x <= east + 0.01f; x += 2.1f)
                if (Mathf.Abs(x - TowerDoorX) > DoorHalfWidth + 0.2f && Mathf.Abs(x - RestaurantDoorX) > DoorHalfWidth + 0.2f)
                    Block(entrance, "Mullion", bronze, new Vector3(x, 0.15f, z - 0.03f), new Vector3(0.12f, height, 0.12f));
            foreach (var doorX in new[] { TowerDoorX, RestaurantDoorX })
                Kit.OpenDoor(entrance, new Vector3(doorX, 0.15f, z), 180f, glass);
            foreach (var x in new[] { TowerDoorX - 1.9f, TowerDoorX + 1.9f, RestaurantDoorX - 1.9f })
                Kit.Planter(entrance, new Vector3(x, 0.15f, TowerFace - 0.55f), 0f);
            // The tower's door: a brass canopy, a lamp, the doorman.
            Block(entrance, "TowerCanopy", brass, new Vector3(TowerDoorX, 3.15f, TowerFace - 0.9f), new Vector3(2.8f, 0.2f, 1.8f));
            Point(entrance, "TowerDoorLight", new Vector3(TowerDoorX, 2.7f, TowerFace - 1.2f), Incandescent, 6f, 1.4f);
            Person(entrance, "Doorman", new Vector3(TowerDoorX - 1.4f, 0.15f, TowerFace - 1.3f), 180f, Burgundy, Suit, 1f,
                look => { look.Hat = CharacterArt.HatKind.Cap; look.HatColor = Burgundy; look.Tie = true; look.TieColor = new Color(0.86f, 0.62f, 0.26f); });
        }

        // The tower's own lobby, west of the restaurant: marble, a reception desk with its
        // receptionist under a brass board, plants, and the lift's glass shaft, where the chef goes.
        private static void TowerLobby(Transform entrance)
        {
            var room = new GameObject("TowerLobby").transform;
            room.SetParent(entrance, false);
            var front = TowerFace + 0.1f;
            var back = TowerFace + RoomDepth;
            const float ceiling = 4.3f;
            var west = -LobbyHalf - 0.2f;
            var stone = Toon("OfficeTowerStone", new Color(0.66f, 0.55f, 0.43f));
            var walnut = Toon("OfficeWalnut", Walnut);
            var brass = Toon("OfficeBrass", Brass);
            Cube(room, "Floor", Tiles("LobbyMarble", new Color(0.86f, 0.82f, 0.74f), new Color(0.6f, 0.55f, 0.5f), 1.0f), new Vector3((west + DividerX) / 2f, 0.15f, (front + back) / 2f), new Vector3(DividerX - west, 0.02f, back - front));
            Cube(room, "Ceiling", Toon("TowerCeiling", new Color(0.9f, 0.89f, 0.86f)), new Vector3((west + DividerX) / 2f, ceiling, (front + back) / 2f), new Vector3(DividerX - west, 0.1f, back - front));
            Block(room, "BackWall", stone, new Vector3((west + DividerX) / 2f, 0.15f, back), new Vector3(DividerX - west, ceiling - 0.15f, 0.15f));
            Block(room, "WestWall", stone, new Vector3(west, 0.15f, (front + back) / 2f), new Vector3(0.15f, ceiling - 0.15f, back - front));
            Block(room, "Divider", walnut, new Vector3(DividerX, 0.15f, (front + back) / 2f), new Vector3(0.18f, ceiling - 0.15f, back - front));
            // The reception desk, its board of brass plates behind, the receptionist.
            for (var i = 0; i < 3; i++)
            {
                Kit.Part(room, "SM_Dining", "BarBody", walnut, new Vector3(-7f + i, 0.15f, back - 1.5f));
                Kit.Part(room, "SM_Dining", "BarTop", brass, new Vector3(-7f + i, 0.15f, back - 1.5f));
            }
            Block(room, "Board", brass, new Vector3(-6f, 1.5f, back - 0.12f), new Vector3(2.6f, 1.3f, 0.05f));
            for (var r = 0; r < 4; r++)
                Block(room, "BoardLine", walnut, new Vector3(-6f, 1.68f + r * 0.28f, back - 0.16f), new Vector3(2.2f, 0.1f, 0.02f));
            Person(room, "Receptionist", new Vector3(-6f, 0.15f, back - 0.8f), 180f, new Color(0.5f, 0.2f, 0.3f), Suit);
            PottedPlant(room, new Vector3(-7.6f, 0.15f, front + 0.7f), 1.2f);
            PottedPlant(room, new Vector3(-2.9f, 0.15f, back - 0.6f), 1.2f);
            Sofa(room, new Vector3(-3.6f, 0.15f, front + 4.9f), 180f, new Color(0.3f, 0.4f, 0.5f));
            foreach (var x in new[] { -6f, -3.4f })
                Kit.Pendant(room, new Vector3(x, 3.0f, front + 2.6f), new Color(0.2f, 0.2f, 0.24f));
            Point(room, "LobbyLight", new Vector3(-4.6f, 3.3f, front + 3f), Incandescent, 9f, 2f);
        }

        // The boss's restaurant (the user's note, 2026-11-10: clearer what's going on inside). By the
        // door, a host and the bar, guests on its stools. Beyond, up six steps, the dining room on
        // a platform right behind the glass, so its tables show over the heads of the queue: cloths,
        // candles, couples eating, someone photographing their plate, waiters crossing with trays
        // under cloches. Along the back, the open kitchen: cooks at the hobs behind the pass, steam
        // and firelight.
        private static void Restaurant(Transform entrance)
        {
            var room = new GameObject("Restaurant").transform;
            room.SetParent(entrance, false);
            var front = TowerFace + 0.1f;
            var back = TowerFace + RoomDepth;
            const float ceiling = 4.3f;
            var dining = 0.15f + PlatformHeight; // the platform's floor
            var walnut = Toon("OfficeWalnut", Walnut);
            var brass = Toon("OfficeBrass", Brass);
            var planks = Planks("RestaurantFloor", new Color(0.42f, 0.27f, 0.18f), new Color(0.3f, 0.19f, 0.13f), 0.18f, 1.6f);
            Cube(room, "Floor", planks, new Vector3((DividerX + PlatformX) / 2f, 0.15f, (front + back) / 2f), new Vector3(PlatformX - DividerX, 0.02f, back - front));
            Block(room, "Platform", walnut, new Vector3((PlatformX + RestaurantEast) / 2f, 0.15f, (front + back) / 2f), new Vector3(RestaurantEast - PlatformX, PlatformHeight - 0.02f, back - front));
            Cube(room, "PlatformFloor", planks, new Vector3((PlatformX + RestaurantEast) / 2f, dining - 0.02f, (front + back) / 2f), new Vector3(RestaurantEast - PlatformX, 0.02f, back - front));
            Cube(room, "Ceiling", Toon("RestaurantCeiling", new Color(0.32f, 0.2f, 0.15f)), new Vector3((DividerX + RestaurantEast) / 2f, ceiling, (front + back) / 2f), new Vector3(RestaurantEast - DividerX, 0.1f, back - front));
            Block(room, "BackWall", Toon("RestaurantWall", new Color(0.62f, 0.3f, 0.24f)), new Vector3((DividerX + RestaurantEast) / 2f, 0.15f, back), new Vector3(RestaurantEast - DividerX, ceiling - 0.15f, 0.15f));
            Block(room, "EastWall", walnut, new Vector3(RestaurantEast, 0.15f, (front + back) / 2f), new Vector3(0.15f, ceiling - 0.15f, back - front));
            // Six steps up, and a brass rail along the platform's edge either side of them.
            for (var k = 0; k < 6; k++)
                Block(room, "Step", walnut, new Vector3(PlatformX - 1.65f + k * 0.3f, 0.15f, front + 3.2f), new Vector3(0.3f, (k + 1) * PlatformHeight / 6f - 0.02f, 1.6f));
            foreach (var (z0, z1) in new[] { (front + 0.2f, front + 2.3f), (front + 4.1f, back - 0.3f) })
            {
                Block(room, "Rail", brass, new Vector3(PlatformX + 0.08f, dining + 0.9f, (z0 + z1) / 2f), new Vector3(0.05f, 0.05f, z1 - z0));
                for (var z = z0; z <= z1 + 0.01f; z += (z1 - z0) / 3f)
                    Block(room, "RailPost", brass, new Vector3(PlatformX + 0.08f, dining, z), new Vector3(0.05f, 0.9f, 0.05f));
            }

            // By the door: the host with the menu, and the bar along the back with its bottles.
            Kit.MenuStand(room, new Vector3(6.9f, 0.15f, front + 0.8f), 200f);
            Person(room, "Host", new Vector3(6.2f, 0.15f, front + 1.3f), 200f, Burgundy, Suit, 1f, look => { look.Tie = true; look.TieColor = new Color(0.86f, 0.62f, 0.26f); });
            const float bar = 5.2f;
            Cube(room, "BottleWall", Unlit("RestaurantBottleGlow", new Color(1f, 0.72f, 0.4f)), new Vector3(bar, 1.2f, back - 0.1f), new Vector3(5f, 2.4f, 0.02f));
            Color[] bottles = { new Color(0.3f, 0.6f, 0.35f), new Color(0.75f, 0.45f, 0.2f), new Color(0.85f, 0.85f, 0.9f), new Color(0.55f, 0.2f, 0.25f) };
            for (var shelf = 0; shelf < 3; shelf++)
            {
                var y = 1.35f + shelf * 0.75f;
                Block(room, "Shelf", brass, new Vector3(bar, y - 0.04f, back - 0.25f), new Vector3(5f, 0.04f, 0.3f));
                for (var b = 0; b < 12; b++)
                    Kit.Bottle(room, new Vector3(bar - 2.3f + b * 0.42f, y, back - 0.25f), bottles[(b + shelf) % 4], 1f + ((b * 7 + shelf) % 3) * 0.12f);
            }
            Block(room, "BarCounter", walnut, new Vector3(bar, 0.15f, back - 1.3f), new Vector3(5f, 1.05f, 0.6f));
            Block(room, "BarTop", brass, new Vector3(bar, 1.2f, back - 1.3f), new Vector3(5.1f, 0.05f, 0.7f));
            Kit.CoffeeMachine(room, new Vector3(bar + 1.9f, 1.25f, back - 1.3f), 180f);
            var bartender = Figure(room, "Bartender", new Vector3(bar - 0.4f, 0.15f, back - 0.75f), 180f);
            if (bartender != null) // shaking a cocktail
            {
                bartender.RightShoulder.localRotation = Quaternion.Euler(-100f, 0f, -20f);
                Wobble(bartender.RightShoulder.gameObject, Oscillate.Shape.Wave, Vector3.zero, new Vector3(18f, 0f, 0f), 0.4f);
            }
            for (var st = 0; st < 3; st++)
            {
                var at = new Vector3(bar - 1.5f + st * 1.4f, 0.15f, back - 2.0f);
                Kit.Part(room, "SM_Dining", "Stool", walnut, at);
                Seated(room, "BarGuest", at, st == 1 ? 20f : -10f, 0.44f);
            }
            Point(room, "BarLight", new Vector3(bar, 3.2f, back - 2.2f), Incandescent, 7f, 2.2f);

            // The dining room: tables for two with their cloths, candles and couples; pendants over them.
            var k2 = 0;
            foreach (var (x, z) in new[] { (9.7f, front + 1.35f), (11.9f, front + 1.35f), (14.1f, front + 1.35f), (16.3f, front + 1.35f), (10.8f, front + 3.75f), (13f, front + 3.75f), (15.2f, front + 3.75f) })
            {
                var at = new Vector3(x, dining, z);
                Kit.DiningTable(room, at);
                Kit.Pendant(room, new Vector3(x, dining + 2.05f, z), new Color(0.75f, 0.25f, 0.2f));
                foreach (var side in new[] { -1f, 1f })
                {
                    var seat = at + new Vector3(side * 0.8f, 0f, 0f);
                    Kit.BistroChair(room, seat, side < 0f ? 90f : -90f);
                    var diner = Seated(room, "Diner", seat, side < 0f ? 90f : -90f, 0.46f);
                    if (diner == null)
                        continue;
                    if (side < 0f && k2 % 2 == 0)
                    {
                        // The plate first, for the feed: phone out over the table, the flash going.
                        diner.RightShoulder.localRotation = Quaternion.Euler(-80f, 0f, 10f);
                        Flash(diner.RightShoulder, Phone(diner.RightShoulder, 0.1f, Quaternion.Euler(55f, 0f, 0f)), 3f + (float)_random.NextDouble() * 3f);
                    }
                    else
                    {
                        // Eating: a fork up to the mouth now and then.
                        diner.RightShoulder.localRotation = Quaternion.Euler(-55f, 0f, -10f);
                        Wobble(diner.RightShoulder.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(-50f, 0f, 0f), 2.4f + (float)_random.NextDouble() * 2f, 0.4f);
                    }
                }
                k2++;
            }
            foreach (var x in new[] { 9.7f, 11.9f, 14.1f, 16.3f })
                Point(room, "DiningLight", new Vector3(x, dining + 2.3f, front + 2.4f), Incandescent, 6f, 2.4f);
            // Waiters up and down between the two rows, a tray held high.
            for (var w = 0; w < 2; w++)
                Waiter(room, new Vector3(9.2f, dining, front + 2.55f), new Vector3(16.9f, dining, front + 2.55f), w * 0.5f);

            // The open kitchen along the back: the line, the pass with its heat lamps, the hood.
            var line = new[] { "PF_Counter", "PF_Hob", "PF_Hob", "PF_CuttingBoard", "PF_Counter", "PF_Fridge" };
            for (var i = 0; i < line.Length; i++)
                Prop(room, line[i], new Vector3(10.4f + i, dining, back - 0.6f), 180f);
            Block(room, "Hood", Toon("KitSteel", new Color(0.72f, 0.73f, 0.75f)), new Vector3(12.9f, dining + 2.2f, back - 0.7f), new Vector3(6.2f, 0.5f, 1.1f));
            Cube(room, "HoodLight", Unlit("OfficeBulb", Bulb), new Vector3(12.9f, dining + 2.18f, back - 0.7f), new Vector3(5.6f, 0.02f, 0.2f));
            Block(room, "Pass", walnut, new Vector3(12.9f, dining, back - 2.3f), new Vector3(6.2f, 1.1f, 0.35f));
            Block(room, "PassTop", brass, new Vector3(12.9f, dining + 1.1f, back - 2.3f), new Vector3(6.3f, 0.05f, 0.45f));
            foreach (var x in new[] { 11.2f, 12.9f, 14.6f })
            {
                Kit.Pendant(room, new Vector3(x, dining + 1.75f, back - 2.3f), new Color(0.78f, 0.6f, 0.28f));
                Kit.Tray(room, new Vector3(x + 0.5f, dining + 1.15f, back - 2.3f), Quaternion.identity);
            }
            Cook(room, new Vector3(11.4f, dining, back - 1.45f), 0f);
            var cook = Cook(room, new Vector3(13.2f, dining, back - 1.5f), 90f);
            if (cook != null)
                Walk(cook, new Vector3(12.4f, dining, back - 1.5f), new Vector3(15f, dining, back - 1.5f), 0.9f, 2.4f, 0.3f);
            Steam(room, new Vector3(11.4f, dining + 1.0f, back - 0.6f));
            Steam(room, new Vector3(12.4f, dining + 1.0f, back - 0.6f));
            var fire = Point(room, "HobFire", new Vector3(11.9f, dining + 1.3f, back - 0.9f), new Color(1f, 0.6f, 0.3f), 5f, 3f);
            fire.gameObject.AddComponent<FlickerLight>().Set(fire, FlickerLight.Mood.Fire, null, 0.2f, 4f);
            fire.gameObject.isStatic = false;
        }

        // The restaurant's branding (the user's notes, 2026-11-10: the screen, the neon and the stars
        // didn't belong; then: the first fascia's lettering didn't feel right and it was bare).
        // A dark fascia board with gold edges across the lintel; over the door the name set as the
        // game's own logo is, "Mise en" in gold serif italic beside a heavy rounded "FLEX", between
        // two gold roundels of a crossed fork and spoon; further along, "RESTAURANT & BAR" spaced
        // out in small capitals; a row of bulbs along its foot; and over the dining room's windows a
        // striped awning in the house burgundy and cream.
        private static void Fascia(Transform entrance)
        {
            var z = TowerFace - 0.36f;
            var middle = (DividerX + RestaurantEast) / 2f;
            var width = RestaurantEast - DividerX - 0.6f;
            var gold = new Color(0.93f, 0.72f, 0.35f);
            var cream = new Color(0.98f, 0.93f, 0.8f);
            Block(entrance, "Fascia", Toon("RestaurantFascia", new Color(0.2f, 0.13f, 0.1f)), new Vector3(middle, 4.4f, z), new Vector3(width, 0.62f, 0.1f));
            foreach (var y in new[] { 4.4f, 4.97f })
                Block(entrance, "FasciaEdge", Toon("OfficeBrass", Brass), new Vector3(middle, y, z - 0.01f), new Vector3(width + 0.06f, 0.05f, 0.12f));

            // The lockup over the door, laid out from the lettering's own widths.
            var face = z - 0.07f;
            var first = Lettering(entrance, "Mise en", "Lora-Italic", 4.1f, gold, 0f);
            var second = Lettering(entrance, "FLEX", "Fredoka-SemiBold", 5.6f, cream, 6f);
            const float gap = 0.22f;
            const float emblem = 0.5f;
            var a = first.GetPreferredValues().x;
            var b = second.GetPreferredValues().x;
            var total = emblem + gap + a + gap + b + gap + emblem;
            var x = RestaurantDoorX + 0.5f - total / 2f;
            var mark = SignArt.Emblem();
            Sign(entrance, "Emblem", mark, new Vector3(x + emblem / 2f, 4.71f, face), new Vector2(emblem, emblem), 1);
            x += emblem + gap;
            first.transform.localPosition = new Vector3(x, 4.69f, face);
            x += a + gap;
            second.transform.localPosition = new Vector3(x, 4.71f, face);
            x += b + gap;
            Sign(entrance, "Emblem", mark, new Vector3(x + emblem / 2f, 4.71f, face), new Vector2(emblem, emblem), 1);
            // What it is, further along, over the dining room.
            var strap = Lettering(entrance, "RESTAURANT  &  BAR", "Inter-SemiBold", 2.2f, gold, 34f);
            strap.transform.localPosition = new Vector3(11.1f - strap.GetPreferredValues().x / 2f, 4.71f, face); // inside the frame the camera stops on

            // Bulbs along the fascia's foot, every third one with its glow, and warm light on the board.
            var bulb = Unlit("OfficeBulb", Bulb);
            var count = Mathf.FloorToInt(width / 0.45f);
            for (var i = 0; i <= count; i++)
            {
                var at = new Vector3(middle - width / 2f + i * width / count, 4.36f, z - 0.08f);
                Shape(entrance, PrimitiveType.Sphere, "FasciaBulb", bulb, at, Vector3.one * 0.07f);
                if (i % 3 == 1)
                    Kit.Halo(entrance, at, 0.5f, Bulb, 0.6f);
            }
            foreach (var lx in new[] { RestaurantDoorX + 0.5f, 9.4f, 12.9f, 16.2f })
                Point(entrance, "SignLight", new Vector3(lx, 4.9f, z - 0.8f), Incandescent, 3.4f, 1.5f);

            // The awning over the dining room's windows: high, so the room shows under it.
            var awning = new GameObject("Awning").transform;
            awning.SetParent(entrance, false);
            awning.localPosition = new Vector3(0f, 0.15f, TowerFace - 0.12f);
            awning.localRotation = Quaternion.Euler(0f, 180f, 0f); // the modules' wall side is their local −z
            var stripe = Toon("RestaurantAwning", Burgundy);
            var pale = Toon("CityAwningCream", new Color(0.93f, 0.88f, 0.78f));
            for (var ax = PlatformX + 0.4f; ax < RestaurantEast - 0.6f; ax += 1f)
            {
                Model(awning, "Awning", CityModel, "CityAwningA", stripe, new Vector3(-ax - 0.5f, 4.12f, 0f), 180f);
                Model(awning, "Awning", CityModel, "CityAwningB", pale, new Vector3(-ax - 0.5f, 4.12f, 0f), 180f);
            }
        }

        // A line of lettering for a sign, in one of the game's fonts: its left end at its position.
        private static TextMeshPro Lettering(Transform parent, string text, string font, float size, Color colour, float spacing)
        {
            var label = new GameObject("Lettering").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.font = MenuArt.Font(font);
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(12f, 1f);
            label.ForceMeshUpdate();
            return label;
        }

        // From the restaurant's door to the kerb: a red carpet between brass posts and ropes, a
        // canopy with bulbs round its edge, a couple posing and photographers either side.
        private static void RedCarpet(Transform entrance)
        {
            Cube(entrance, "RedCarpet", Toon("OfficeCarpet", Burgundy), new Vector3(RestaurantDoorX, 0.16f, (TowerFace + NorthKerb) / 2f), new Vector3(2.2f, 0.01f, TowerFace - NorthKerb));
            Kit.Canopy(entrance, new Vector3(RestaurantDoorX, 0.15f, TowerFace - 0.3f), 180f, Burgundy);
            Point(entrance, "CanopyLight", new Vector3(RestaurantDoorX, 2.9f, TowerFace - 1.8f), Incandescent, 7f, 2f);
            foreach (var side in new[] { -1.4f, 1.4f })
                Kit.Stanchions(entrance, new Vector3(RestaurantDoorX + side, 0.15f, TowerFace - 3.7f), new Vector3(RestaurantDoorX + side, 0.15f, NorthKerb + 0.5f), Burgundy);

            // The couple on the carpet, posing for the photographers.
            var couple = new GameObject("Posing").transform;
            couple.SetParent(entrance, false);
            couple.localPosition = new Vector3(RestaurantDoorX, 0.15f, TowerFace - 4.3f);
            couple.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Poser(couple, "Star", new Vector3(-0.3f, 0f, 0f), 0f, phone: false);
            Poser(couple, "Partner", new Vector3(0.35f, 0f, 0.05f), -10f, phone: false);
            // Photographers outside the ropes, cameras up, flashing.
            foreach (var (x, z, facing) in new[] { (RestaurantDoorX - 2.4f, NorthKerb + 0.9f, 40f), (RestaurantDoorX + 2.5f, NorthKerb + 1.0f, -40f) })
                Photographer(entrance, new Vector3(x, 0.15f, z), facing);
        }

        // The queue for the restaurant along its windows, behind a rope: a single file, nearly all
        // of them on their phones, scrolling, filming, taking a selfie.
        private static void Queue(Transform entrance)
        {
            var ropeZ = TowerFace - 1.9f;
            var start = RestaurantDoorX + 2.2f;
            const float end = 14.6f;
            Kit.Stanchions(entrance, new Vector3(start, 0.15f, ropeZ), new Vector3(end, 0.15f, ropeZ), Burgundy);
            var k = 0;
            for (var x = start + 0.4f; x < end; x += 0.95f, k++)
            {
                var at = new Vector3(x, 0.15f, TowerFace - 1.05f);
                switch (k % 4)
                {
                    case 0: PhoneUp(entrance, at, -150f, PhoneUse.Scroll); break;
                    case 1: PhoneUp(entrance, at, 180f + (k % 2 == 0 ? 20f : -15f), PhoneUse.Selfie); break;
                    case 2: Passerby(entrance, "Queue", at, -90f + (k % 2 == 0 ? 15f : -20f)); break;
                    default: PhoneUp(entrance, at, 165f, PhoneUse.Film); break; // filming the street while they wait
                }
            }
            // Two at a time going in and coming out, so the queue is for somewhere.
            for (var w = 0; w < 2; w++)
            {
                var walker = Passerby(entrance, "GoingIn", new Vector3(RestaurantDoorX, 0.15f, TowerFace - 2.6f), 0f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(RestaurantDoorX + (w == 0 ? -0.3f : 0.3f), 0.15f, TowerFace - 2.6f),
                    new Vector3(RestaurantDoorX + (w == 0 ? -0.5f : 0.6f), 0.15f, TowerFace + 2.4f), 1.1f, true, 3f + w, w * 0.5f);
                MakeDynamic(walker.gameObject);
            }
        }

        // Another place to eat in the tower's podium, west of the lobby (the user's note,
        // 2026-11-10: "other restaurants on the sides"): a lit front under awnings, its name in
        // neon, a logo box, and a terrace of high tables under a string of bulbs.
        private static void WestPlace(Transform entrance)
        {
            var place = new GameObject("PlaceWest").transform;
            place.SetParent(entrance, false);
            place.localPosition = new Vector3(-13.1f, 0.15f, TowerFace - 0.32f);
            place.localRotation = Quaternion.Euler(0f, 180f, 0f); // its front (local +z) towards the street
            const float w = 9f;
            LitShop(place, Vector3.zero, w, 0);
            var (icon, word) = Logos[0];
            Shape(place, PrimitiveType.Quad, "LogoBox", SignArt.LightBox(icon, word, BoxColours[0]), new Vector3(w * 0.36f, 2.1f, 0.12f), new Vector3(1.2f, 1.5f, 1f), Quaternion.Euler(0f, 180f, 0f));
            WindowNeon(place, new Vector3(-w * 0.25f, 1.9f, 0.08f), 0);
            for (var t = 0; t < 3; t++)
            {
                var at = new Vector3(-16f + t * 2.5f, 0.15f, TowerFace - 2.4f);
                Kit.BistroTable(entrance, at, high: true);
                Kit.Bottle(entrance, at + new Vector3(0.1f, 1.09f, 0.05f), new Color(0.3f, 0.6f, 0.35f));
                Passerby(entrance, "TerraceGuest", at + new Vector3(-0.6f, 0f, 0.1f), 90f);
                if (t != 1)
                    Passerby(entrance, "TerraceGuest", at + new Vector3(0.6f, 0f, -0.1f), -90f);
                else
                    PhoneUp(entrance, at + new Vector3(0.65f, 0f, -0.1f), -90f, PhoneUse.Film); // photographing the food
            }
            StringLights(entrance, new Vector3(-17.2f, 3.6f, TowerFace - 3.3f), new Vector3(-9.4f, 3.6f, TowerFace - 3.3f), 14, 0.35f);
        }

        // More of it along the front: someone filming the sign, an influencer under a ring light,
        // delivery riders waiting for orders by their scooters.
        private static void EntranceMoments(Transform entrance)
        {
            PhoneUp(entrance, new Vector3(-1.4f, 0.15f, TowerFace - 1.6f), 180f, PhoneUse.Selfie);
            PhoneUp(entrance, new Vector3(2.4f, 0.15f, TowerFace - 3.6f), 30f, PhoneUse.Film); // photographing the restaurant

            // The influencer: filming themselves with the tower behind, under a ring light on a stand.
            var at = new Vector3(-8.4f, 0.15f, TowerFace - 4.4f);
            Passerby(entrance, "Influencer", at, 180f);
            var light = Kit.RingLight(entrance, at + new Vector3(0f, 0f, -1.3f), 0f);
            Point(light, "RingGlow", new Vector3(0f, 1.6f, 0.4f), new Color(1f, 0.97f, 0.92f), 3.5f, 2.6f);
            HeartStream(light, new Vector3(0f, 1.95f, 0f), 6f);

            Couriers(entrance);
        }

        private static readonly Color CourierLime = new Color(0.55f, 0.86f, 0.22f);

        // Delivery couriers (the user's notes, 2026-11-10: a detail he liked; in view beside the menu;
        // two scooters, a little apart; the couriers, or at least their bags, plain to see). They wear
        // one uniform so they read as couriers at a glance: a lime cap and a big lime bag with a white
        // patch, on a dark jacket. One sits on a scooter checking the next order, one stands with
        // their back to us, the bag square on, and one carries orders between the door and the kerb.
        private static void Couriers(Transform entrance)
        {
            var patch = SignArt.CourierPatch(new Color(0.16f, 0.42f, 0.14f));
            var first = Kit.Scooter(entrance, new Vector3(3.2f, 0.02f, NorthKerb - 0.85f), 72f, new Color(0.93f, 0.93f, 0.9f), CourierLime, withBox: false);
            var second = Kit.Scooter(entrance, new Vector3(5.2f, 0.02f, NorthKerb - 0.6f), 104f, new Color(0.22f, 0.24f, 0.28f), CourierLime, patch: patch);
            MakeDynamic(first.gameObject);
            MakeDynamic(second.gameObject);
            var grip = new[] { new Vector3(-58f, 0f, -10f), new Vector3(-58f, 0f, 10f) };

            // On the first scooter's seat (0.38 behind its middle, 0.745 up), scrolling.
            var seated = Courier(first, new Vector3(0f, 0f, -0.36f), 0f, patch);
            if (seated != null)
            {
                Sit(seated, 0.745f);
                seated.RightShoulder.localRotation = Quaternion.Euler(-62f, 0f, -8f);
                var phone = Phone(seated.RightShoulder, 0.1f, Quaternion.Euler(42f, 0f, 0f));
                Point(phone, "ScreenGlow", new Vector3(0f, 0f, -0.2f), new Color(0.75f, 0.85f, 1f), 1.4f, 1.2f);
                Wobble(seated.RightShoulder.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(-5f, 0f, 0f), 1.4f, 0.3f);
                MakeDynamic(seated.RightShoulder.gameObject);
                // Soon after the camera stops, their order's up: the phone goes away and they ride
                // off east (the user's note, 2026-11-11), out through the gap in the barriers.
                first.gameObject.AddComponent<CourierDeparture>().Set(2.2f, null, Vector3.zero, null, new[] { phone.gameObject },
                    new[] { seated.RightShoulder, seated.LeftShoulder }, grip, WayOut(first.localPosition, 6.8f));
            }
            // Waiting between the scooters, looking at the restaurant: their bag faces the street.
            var waiting = Courier(entrance, new Vector3(4.2f, 0.15f, NorthKerb + 0.9f), 8f, patch);
            if (waiting != null)
            {
                Breathe(waiting.Figure.gameObject);
                // A few seconds after the first, this one walks to the other scooter, gets on and
                // follows: the standing figure is swapped for one sitting on it (no bag on that
                // one: the scooter carries the box).
                var mounted = Courier(second, new Vector3(0f, 0f, -0.36f), 0f, patch, bag: false);
                if (mounted != null)
                {
                    Sit(mounted, 0.745f);
                    mounted.RightShoulder.localRotation = Quaternion.Euler(grip[0]);
                    mounted.LeftShoulder.localRotation = Quaternion.Euler(grip[1]);
                    var rider = mounted.Figure.parent.gameObject;
                    rider.SetActive(false);
                    second.gameObject.AddComponent<CourierDeparture>().Set(5.5f, waiting.Figure.parent, new Vector3(4.75f, 0.15f, NorthKerb + 0.25f), rider,
                        System.Array.Empty<GameObject>(), System.Array.Empty<Transform>(), System.Array.Empty<Vector3>(), WayOut(second.localPosition, 8.6f));
                }
            }
            // To and fro with the orders.
            var runner = Courier(entrance, new Vector3(5.6f, 0.15f, NorthKerb + 0.6f), 0f, patch);
            if (runner != null)
                Walk(runner.Figure.parent, new Vector3(5.65f, 0.15f, NorthKerb + 0.5f), new Vector3(5.6f, 0.15f, TowerFace - 2.3f), 1.25f, 2.2f, 0.2f);
        }

        // A parked scooter's way out: off the kerb onto the couriers' lane, east through the gap in
        // the barriers and away.
        private static Vector3[] WayOut(Vector3 from, float joinsLaneAt) => new[]
        {
            from, new Vector3(joinsLaneAt, from.y, CourierLane), new Vector3(46f, from.y, CourierLane),
        };

        // A courier: the uniform and the bag, on anyone.
        private static CharacterArt.Rig Courier(Transform parent, Vector3 at, float facing, Material patch, bool bag = true)
        {
            var person = new GameObject("Courier").transform;
            person.SetParent(parent, false);
            person.localPosition = at;
            person.localRotation = Quaternion.Euler(0f, facing, 0f);
            var look = new CharacterArt.Look { Name = "Courier" };
            Dress(look, 0f);
            look.Top = new Color(0.2f, 0.22f, 0.26f);
            look.Bottom = new Color(0.16f, 0.17f, 0.2f);
            look.Garment = CharacterArt.Garment.Jacket;
            look.Under = CourierLime;
            look.Hat = CharacterArt.HatKind.Cap;
            look.HatColor = CourierLime;
            look.HatAccent = new Color(0.16f, 0.42f, 0.14f);
            look.Scarf = false;
            var rig = CharacterArt.Build(person, look, swingArms: true);
            if (rig == null)
            {
                Object.DestroyImmediate(person.gameObject);
                return null;
            }
            if (bag)
                Kit.DeliveryBag(rig.Waist, new Vector3(0f, -0.02f, -0.37f), 180f, CourierLime, 1.3f, patch);
            MakeDynamic(person.gameObject);
            return rig;
        }

        // Sits a standing figure on a seat `seatHeight` up from where it stands: legs out in front, no walk.
        private static void Sit(CharacterArt.Rig rig, float seatHeight)
        {
            if (rig.Gait != null)
                Object.DestroyImmediate(rig.Gait);
            rig.Figure.localPosition = new Vector3(0f, seatHeight + 0.07f - CharacterArt.Hip.y, 0f);
            rig.LeftHip.localRotation = rig.RightHip.localRotation = Quaternion.Euler(-88f, 0f, 0f);
        }

        // The street in front of the tower, closed to traffic (the user's notes, 2026-11-10): a few
        // market stalls among actual places to eat. Either side of the avenue's mouth, where the
        // camera starts, the corner buildings are restaurants with terraces, one stall each side;
        // more stalls along the tower's street; barriers at both ends; bulbs strung overhead;
        // selfie pairs where the camera passes.
        private static void StreetMarket(Transform street)
        {
            var market = new GameObject("NightMarket").transform;
            market.SetParent(street, false);
            // One stall each side of the avenue's mouth, staggered, and three along the tower's street.
            // The mouth's two stand on its west side: the camera comes through east of the middle.
            MarketStall(market, new Vector3(-3.6f, 0.02f, -42.6f), 90f, 0);
            MarketStall(market, new Vector3(-3.6f, 0.02f, -36.2f), 90f, 1);
            var k = 2;
            foreach (var x in new[] { -17.4f, -12.8f, 16.9f })
                MarketStall(market, new Vector3(x, 0.02f, NorthKerb - 1.5f), 180f, k++);
            foreach (var side in new[] { -1f, 1f })
                MouthPlace(market, side);

            // Closed to traffic: barriers across the street at both ends.
            foreach (var x in new[] { -StreetClosed, StreetClosed })
            foreach (var z in new[] { NorthKerb - 1.4f, (NorthKerb + SouthKerb) / 2f, SouthKerb + 1.4f })
                Kit.Barrier(market, new Vector3(x, 0.02f, z), 90f);

            // Bulbs strung across the avenue's mouth, and from the row's roofs to the tower's podium.
            foreach (var z in new[] { -45f, -41f, -37f, -33f })
                StringLights(market, new Vector3(-AvenueFronts, 6.2f, z), new Vector3(AvenueFronts, 6.2f, z), 18, 1.1f);
            foreach (var x in new[] { -15f, -9.5f, 10.5f, 16f })
                StringLights(market, new Vector3(x, 6.4f, SouthFronts), new Vector3(x, 5.3f, TowerFace - 0.4f), 16, 0.9f);

            // Selfies where the camera passes, the tower behind them (they face it coming), and in the road.
            SelfiePair(market, new Vector3(0.4f, 0.02f, -33.4f), 190f);
            SelfiePair(market, new Vector3(4.6f, 0.02f, -39.4f), 170f);
            // This pair stands back on the tower's kerb, left of where the menu hangs: in the road they
            // were close enough for the menu to cut them off at the chest (the user's note, 2026-11-10).
            SelfiePair(market, new Vector3(-4.65f, 0.15f, NorthKerb + 1.9f), 172f);
            // A portrait: one posing with the restaurant behind, a friend taking it.
            var portrait = new GameObject("Portrait").transform;
            portrait.SetParent(market, false);
            portrait.localPosition = new Vector3(8.6f, 0.02f, -25.4f); // south of the couriers' lanes
            Poser(portrait, "Sitter", Vector3.zero, 180f, phone: false);
            Photographer(portrait, new Vector3(0.2f, 0f, -2.2f), 0f, phone: true);

            // People strolling: along the street either side of the camera's view, and a few right across it at the far kerb.
            for (var w = 0; w < 12; w++)
            {
                var east = w % 2 == 0;
                var from = east ? 10f + (float)_random.NextDouble() * 4f : -21f + (float)_random.NextDouble() * 6f;
                var length = 7f + (float)_random.NextDouble() * 5f;
                // South of the couriers' lanes and of the people crossing the whole street.
                var z = w < 9 ? -27f + (float)_random.NextDouble() * 1.2f : NorthKerb - 0.6f - (float)_random.NextDouble() * 0.5f;
                if (w >= 9)
                {
                    from = -9f;
                    length = 18f;
                }
                var walker = Passerby(market, "Stroller", new Vector3(from, 0.02f, z), 90f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(from, 0.02f, z), new Vector3(from + length, 0.02f, z),
                    0.9f + (float)_random.NextDouble() * 0.4f, true, 1f + (float)_random.NextDouble() * 3f, (float)_random.NextDouble());
                MakeDynamic(walker.gameObject);
            }
            StreetTraffic(market);
        }

        // Where things ride and walk the length of the closed street (z). The couriers' lanes run
        // through the gap in the barriers at either end; the far one is 7 m from the camera's stop,
        // so nothing passes close to the lens (the user's note, 2026-11-10).
        private const float CourierLane = -22.7f;
        private const float CourierLaneSouth = -23.1f; // no nearer: at −23.9 a rider filled the bottom of the frame at the stop
        private const float CrossingLane = -24.9f;

        // Movement from edge to edge (the user's note, 2026-11-11: the approach felt a bit static;
        // "delivery motorbikes approaching or leaving, or just people around the scene from edge to
        // edge"). Two couriers on scooters work the street: one comes in from the east, pulls up at
        // the kerb by the restaurant, waits for the order and rides off west; the other rides
        // straight through from the west, on its way somewhere else. And people cross the whole
        // street on foot, both ways, some in a hurry. All of them start beyond the barriers, out of
        // the camera's view, and come round again.
        private static void StreetTraffic(Transform market)
        {
            var patch = SignArt.CourierPatch(new Color(0.16f, 0.42f, 0.14f));
            const float beyond = 44f;
            // From the east: along the lane, in to the kerb east of the parked scooters, out again, away west.
            Rider(market, "CourierFromEast", new Color(0.86f, 0.3f, 0.22f), patch, 6.5f, 3, 5f, 7f, 0.07f,
                new Vector3(beyond, 0.02f, CourierLane), new Vector3(15f, 0.02f, CourierLane), new Vector3(12.4f, 0.02f, NorthKerb - 1.5f),
                new Vector3(10.6f, 0.02f, NorthKerb - 1.1f), new Vector3(6.5f, 0.02f, CourierLane), new Vector3(-beyond, 0.02f, CourierLane));
            // From the west, on the lane nearer the camera: an order for somewhere else, straight through.
            Rider(market, "CourierFromWest", new Color(0.22f, 0.42f, 0.62f), patch, 7f, -1, 0f, 8f, 0.45f,
                new Vector3(-beyond, 0.02f, CourierLane), new Vector3(-24.5f, 0.02f, CourierLane), new Vector3(-20f, 0.02f, CourierLaneSouth),
                new Vector3(20f, 0.02f, CourierLaneSouth), new Vector3(24.5f, 0.02f, CourierLane), new Vector3(beyond, 0.02f, CourierLane));

            // On foot, the length of the street: one lane each way, a pace of their own each.
            for (var w = 0; w < 8; w++)
            {
                var east = w % 2 == 0;
                var z = CrossingLane - (east ? 0f : 0.55f);
                var hurry = w % 4 == 3;
                var walker = Passerby(market, "Crosser", new Vector3(0f, 0.02f, z), 90f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(east ? -34f : 34f, 0.02f, z), new Vector3(east ? 34f : -34f, 0.02f, z),
                    hurry ? 2.3f + (float)_random.NextDouble() * 0.4f : 1.2f + (float)_random.NextDouble() * 0.5f, false, 0f, (w / 2 + (float)_random.NextDouble() * 0.6f) / 4f);
                MakeDynamic(walker.gameObject);
            }
        }

        // A courier riding a scooter round a route (RouteMover), in the couriers' uniform, the bag on their back.
        private static void Rider(Transform parent, string name, Color paint, Material patch, float speed, int stopAt, float stopSeconds, float awaySeconds, float phase, params Vector3[] route)
        {
            var mover = new GameObject(name).transform;
            mover.SetParent(parent, false);
            mover.localPosition = route[0];
            var scooter = Kit.Scooter(mover, Vector3.zero, 0f, paint, CourierLime, withBox: false); // the rider wears the bag
            var rider = Courier(scooter, new Vector3(0f, 0f, -0.36f), 0f, patch);
            if (rider != null)
            {
                Sit(rider, 0.745f);
                // Both hands out to the bars.
                rider.RightShoulder.localRotation = Quaternion.Euler(-58f, 0f, -10f);
                rider.LeftShoulder.localRotation = Quaternion.Euler(-58f, 0f, 10f);
            }
            MakeDynamic(mover.gameObject);
            mover.gameObject.AddComponent<RouteMover>().Set(route, speed, stopAt, stopSeconds, awaySeconds, phase, scooter.gameObject);
        }

        // A restaurant in the corner building beside the avenue's mouth, its front on the avenue:
        // lit windows under awnings, its name in neon, a menu on a stand, planters along the kerb,
        // and a terrace of high tables with people eating, drinking and on their phones.
        private static void MouthPlace(Transform market, float side)
        {
            const float w = 11f;
            const float z = -38.2f;
            var place = new GameObject(side < 0f ? "MouthPlaceWest" : "MouthPlaceEast").transform;
            place.SetParent(market, false);
            place.localPosition = new Vector3(side * AvenueFronts, 0.15f, z);
            place.localRotation = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f); // its front (local +z) towards the road
            LitShop(place, Vector3.zero, w, side < 0f ? 4 : 6);
            var (icon, word) = Logos[side < 0f ? 4 : 5];
            Shape(place, PrimitiveType.Quad, "LogoBox", SignArt.LightBox(icon, word, BoxColours[side < 0f ? 3 : 1]), new Vector3(w * 0.38f, 2.1f, 0.12f), new Vector3(1.2f, 1.5f, 1f), Quaternion.Euler(0f, 180f, 0f));
            Kit.MenuStand(place, new Vector3(-0.8f, 0f, 1.0f), 180f);
            // The terrace, on the avenue's sidewalk, and planters between it and the road.
            for (var t = 0; t < 3; t++)
            {
                var at = new Vector3(-3.6f + t * 3.2f, 0f, 2.1f);
                Kit.BistroTable(place, at, high: true);
                Kit.Bottle(place, at + new Vector3(0.1f, 1.09f, 0.05f), new Color(0.55f, 0.2f, 0.25f));
                Passerby(place, "TerraceGuest", at + new Vector3(-0.62f, 0f, 0f), 90f);
                if (t == 1)
                    PhoneUp(place, at + new Vector3(0.65f, 0f, 0f), -90f, PhoneUse.Film);
                else
                    PhoneUp(place, at + new Vector3(0.65f, 0f, 0f), -90f, PhoneUse.Scroll);
                Kit.Planter(place, new Vector3(-3.6f + t * 3.2f, 0f, 4.4f), 0f);
            }
            Point(place, "TerraceLight", new Vector3(0f, 2.6f, 2.4f), Incandescent, 7f, 1.8f);
        }

        // A string of bulbs from `from` to `to`, sagging by `sag` in the middle, on its wire: without
        // the wire and their glow they were dots floating in the sky (the user's note, 2026-11-10).
        private static void StringLights(Transform parent, Vector3 from, Vector3 to, int bulbs, float sag)
        {
            var bulb = Unlit("OfficeBulb", Bulb);
            var wire = Toon("StreetIron", Iron);
            Vector3 At(float t) => Vector3.Lerp(from, to, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * sag);
            for (var b = 0; b < bulbs; b++)
            {
                var t = b / (bulbs - 1f);
                TwinklingBulb(parent, bulb, At(t) + Vector3.down * 0.07f, 6f + (float)_random.NextDouble() * 5f, 0.95f);
                if (b == 0)
                    continue;
                // The wire's stretch from the bulb before.
                var a = At((b - 1) / (bulbs - 1f));
                var c = At(t);
                var piece = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, "Wire", parent, wire, keepCollider: false);
                piece.transform.localPosition = (a + c) / 2f;
                piece.transform.localRotation = Quaternion.LookRotation(c - a);
                piece.transform.localScale = new Vector3(0.025f, 0.025f, (c - a).magnitude);
                piece.isStatic = true;
            }
        }

        // One stall of the market: the kit's stall, its holder behind the counter, a light, and someone
        // buying or photographing the produce.
        private static void MarketStall(Transform market, Vector3 at, float yaw, int k)
        {
            var produce = StallProduce[k % StallProduce.Length];
            var stall = Kit.Stall(market, at, yaw, StallCanopies[k % StallCanopies.Length], produce, slabs: k % 4 == 2);
            Point(stall, "StallLight", new Vector3(0f, 1.9f, 0.3f), Incandescent, 5f, 1.7f);
            Passerby(stall, "Stallholder", new Vector3(0.25f, 0f, -0.85f), 0f);
            if (k % 2 == 0)
                PhoneUp(stall, new Vector3(-0.45f, 0f, 1.25f), 180f, PhoneUse.Film); // a photo of the food first
            else
                Passerby(stall, "Shopper", new Vector3(-0.45f, 0f, 1.2f), 180f + (k % 2 == 0 ? 15f : -10f));
            if (k % 2 == 1)
                Passerby(stall, "Shopper", new Vector3(0.55f, 0f, 1.35f), 170f);
        }

        // Two friends and a phone at arm's length.
        private static void SelfiePair(Transform parent, Vector3 at, float facing)
        {
            var pair = new GameObject("Selfie").transform;
            pair.SetParent(parent, false);
            pair.localPosition = at;
            pair.localRotation = Quaternion.Euler(0f, facing, 0f);
            PhoneUp(pair, new Vector3(-0.32f, 0f, 0f), 0f, PhoneUse.Selfie);
            Poser(pair, "Friend", new Vector3(0.36f, 0f, -0.05f), -8f, phone: false);
        }

        // Someone on their phone (the user's note, 2026-11-10: special emphasis on customers using
        // their phones): at arm's length for a selfie, out in front in both hands to film or
        // photograph (the flash going, from the LED on its back), or held low, scrolling, the
        // screen lighting their front. Over a few of them something pops: hearts, a like count, a review.
        private static CharacterArt.Rig PhoneUp(Transform parent, Vector3 at, float facing, PhoneUse use)
        {
            var rig = Figure(parent, use.ToString(), at, facing);
            if (rig == null)
                return null;
            Transform phone;
            switch (use)
            {
                case PhoneUse.Selfie:
                    // On a short stick, out to the side and up, the phone turned back to their face.
                    rig.RightShoulder.localRotation = Quaternion.Euler(-112f, 0f, 58f);
                    Cube(rig.RightShoulder, "SelfieStick", Toon("KitSteel", new Color(0.72f, 0.73f, 0.75f)), new Vector3(0f, -CharacterArt.Reach - 0.42f, 0f), new Vector3(0.025f, 0.44f, 0.025f)).isStatic = false;
                    phone = Phone(rig.RightShoulder, 0.5f, Quaternion.Euler(-8f, 58f, 0f));
                    break;
                case PhoneUse.Film:
                    // Up in both hands, on its side, over whatever's in the way.
                    rig.RightShoulder.localRotation = Quaternion.Euler(-128f, 0f, 16f);
                    rig.LeftShoulder.localRotation = Quaternion.Euler(-122f, 0f, -16f);
                    phone = Phone(rig.RightShoulder, 0.14f, Quaternion.Euler(0f, 0f, 90f));
                    break;
                default:
                    // Low in front, tilted up to read.
                    rig.RightShoulder.localRotation = Quaternion.Euler(-62f, 0f, -8f);
                    phone = Phone(rig.RightShoulder, 0.1f, Quaternion.Euler(42f, 0f, 0f));
                    break;
            }
            if (use == PhoneUse.Scroll)
            {
                // A thumb at work: the arm twitches as they scroll, and the screen lights them.
                Point(phone, "ScreenGlow", new Vector3(0f, 0f, -0.2f), new Color(0.75f, 0.85f, 1f), 1.4f, 1.2f);
                Wobble(rig.RightShoulder.gameObject, Oscillate.Shape.Pulse, Vector3.zero, new Vector3(-5f, 0f, 0f), 1.1f + (float)_random.NextDouble(), 0.3f);
            }
            else
            {
                Flash(rig.RightShoulder, phone, 2.2f + (float)_random.NextDouble() * 2.6f);
            }
            Pops(phone, new Vector3(0f, 0.25f, 0f));
            Breathe(rig.Figure != null ? rig.Figure.gameObject : parent.gameObject);
            MakeDynamic(rig.RightShoulder.gameObject);
            return rig;
        }

        // Someone sitting on a seat `seatHeight` up from `at`, facing `facing`: the same figure with
        // the legs out in front, and no walk.
        private static CharacterArt.Rig Seated(Transform parent, string name, Vector3 at, float facing, float seatHeight)
        {
            var rig = Figure(parent, name, at, facing);
            if (rig == null)
                return null;
            Sit(rig, seatHeight);
            return rig;
        }

        // A waiter in a long apron, a tray with a cloche held high, walking between two points and back.
        private static void Waiter(Transform parent, Vector3 from, Vector3 to, float phase)
        {
            var waiter = new GameObject("Waiter").transform;
            waiter.SetParent(parent, false);
            waiter.localPosition = from;
            var look = new CharacterArt.Look
            {
                Name = "Waiter",
                Top = new Color(0.96f, 0.96f, 0.94f),
                Bottom = new Color(0.12f, 0.12f, 0.14f),
                Apron = true,
                Skin = CharacterArt.SkinTones[_random.Next(CharacterArt.SkinTones.Length)],
                HairStyle = StreetHair[_random.Next(StreetHair.Length)],
                Hair = CharacterArt.NaturalHair[_random.Next(CharacterArt.NaturalHair.Length)],
            };
            var rig = CharacterArt.Build(waiter, look, swingArms: false);
            if (rig == null)
            {
                Object.DestroyImmediate(waiter.gameObject);
                return;
            }
            rig.RightShoulder.localRotation = Quaternion.Euler(-165f, 0f, -18f);
            Kit.Tray(rig.RightShoulder, new Vector3(0f, -CharacterArt.Reach - 0.04f, 0f), Quaternion.Euler(165f, 0f, 0f));
            Walk(waiter, from, to, 1.0f, 1.6f, phase);
        }

        // Sets someone walking between two points (in their parent's space) and back, pausing at each end.
        private static void Walk(Transform who, Vector3 from, Vector3 to, float speed, float pause, float phase)
        {
            who.gameObject.AddComponent<LaneMover>().Set(from, to, speed, true, pause, phase);
            MakeDynamic(who.gameObject);
        }

        // ---- Social pops (the user's choice, 2026-11-10: all the kinds; then: fewer, the scene was too busy) ----

        // What rises over a phone now and then: hearts, a like or rating pill, or a review bubble.
        // Only one phone in four pops at all, and each of those rarely, so the street reads as
        // people on their phones first and the feed second.
        private static void Pops(Transform owner, Vector3 at)
        {
            var k = _popCount++;
            if (k % 4 != 1)
                return;
            var period = 8f + (float)_random.NextDouble() * 6f;
            // Left of the lift and near the camera's last stop the menu covers the street: a card
            // there would peek out from behind it (seen in the captures), so those phones get hearts.
            var where = _streetRoot != null ? _streetRoot.InverseTransformPoint(owner.position) : Vector3.zero;
            var underMenu = where.x < 1.2f && where.z > PushTo - 2f && where.z < TowerFace;
            // On the tower's floors too: the cards live in the street's own space, which isn't there.
            switch (underMenu || _indoors ? 0 : (k / 4) % 3)
            {
                case 0:
                    HeartStream(owner, at, period);
                    break;
                case 1:
                    PopCards(owner, at, period, LikePill(k), RatingPill(k));
                    break;
                default:
                    PopCards(owner, at, period + 2f, ReviewBubble(k), PhotoPost(k), ReviewBubble(k + 3));
                    break;
            }
        }

        // Hearts floating up, like the reactions on a live video: outlined, four of them now and then.
        private static void HeartStream(Transform owner, Vector3 at, float period)
        {
            var holder = new GameObject("Hearts");
            holder.transform.SetParent(owner, false);
            holder.transform.localPosition = at;
            holder.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // the cone opens upwards
            var hearts = holder.AddComponent<ParticleSystem>();
            var main = hearts.main;
            main.duration = period;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.46f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = hearts.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst((float)_random.NextDouble() * Mathf.Max(0.1f, period - 1.6f), 1, 1, 4, 0.3f) });
            var shape = hearts.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.06f;
            var size = hearts.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.14f, 1.15f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.75f)));
            var fade = hearts.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var noise = hearts.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.7f;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SignArt.SocialIcon(SignArt.Social.Heart, particle: true);
            renderer.sortingOrder = 13;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MakeDynamic(holder);
        }

        // One of `cards` at a time rises over the phone, facing the camera (SocialPop).
        private static void PopCards(Transform owner, Vector3 at, float period, params GameObject[] cards)
        {
            // Not under the phone: a character's right arm is a mirrored copy of the left, so anything
            // under it shows back to front (the user's screenshot, 2026-11-10: a review card with its
            // text mirrored). It follows the phone from the street's own space instead.
            var holder = new GameObject("SocialPop").transform;
            holder.SetParent(_streetRoot, false);
            holder.position = owner.position + Vector3.up * (at.y + 0.1f);
            foreach (var card in cards)
                card.transform.SetParent(holder, false);
            holder.gameObject.AddComponent<SocialPop>().Set(cards, owner, at.y + 0.1f, period, 2.4f, 1.0f, 1f, (float)_random.NextDouble() * 50f);
            MakeDynamic(holder.gameObject);
        }

        private static readonly string[] LikeCounts = { "12.4k", "2.1k", "986", "34k", "7.7k" };
        private static readonly string[] Ratings = { "4.8", "5.0", "4.9", "4.6" };
        private static readonly (string Handle, Color Avatar)[] Handles =
        {
            ("@ines.eats", new Color(0.45f, 0.75f, 0.5f)), ("@forkyeah", new Color(0.95f, 0.6f, 0.3f)), ("@crumb.daily", new Color(0.5f, 0.6f, 0.95f)),
            ("@table4two", new Color(0.9f, 0.45f, 0.6f)), ("@saucy.sam", new Color(0.7f, 0.5f, 0.9f)), ("@nomnom.nat", new Color(0.95f, 0.8f, 0.3f)),
        };

        // A pink pill with a heart and a count, like the menu's own.
        private static GameObject LikePill(int k) =>
            Pill("LikePill", new Color(1f, 0.3f, 0.5f), SignArt.Social.Heart, LikeCounts[k % LikeCounts.Length], Color.white);

        // A dark pill with a gold star and a rating.
        private static GameObject RatingPill(int k) =>
            Pill("RatingPill", new Color(0.2f, 0.14f, 0.16f), SignArt.Social.Star, Ratings[k % Ratings.Length], new Color(1f, 0.86f, 0.4f));

        private static GameObject Pill(string name, Color back, SignArt.Social icon, string text, Color ink)
        {
            var pill = new GameObject(name).transform;
            Sign(pill, "Back", SignArt.Pill(back), Vector3.zero, new Vector2(1.05f, 0.35f), 10);
            Sign(pill, "Icon", SignArt.SocialIcon(icon), new Vector3(-0.32f, 0f, -0.005f), new Vector2(0.3f, 0.3f), 11);
            Label(pill, text, new Vector3(0.13f, 0f, -0.01f), 2.3f, ink, 12);
            return pill.gameObject;
        }

        // A review card: an avatar, a handle, five stars.
        private static GameObject ReviewBubble(int k)
        {
            var (handle, avatar) = Handles[k % Handles.Length];
            var bubble = new GameObject("ReviewBubble").transform;
            Sign(bubble, "Card", SignArt.ReviewCard(), Vector3.zero, new Vector2(1.5f, 0.75f), 10);
            Sign(bubble, "Avatar", SignArt.Disc(avatar), new Vector3(-0.5f, 0.1f, -0.005f), new Vector2(0.32f, 0.32f), 11);
            Label(bubble, handle, new Vector3(0.16f, 0.2f, -0.01f), 1.55f, new Color(0.16f, 0.09f, 0.12f), 12);
            var star = SignArt.SocialIcon(SignArt.Social.Star);
            for (var s = 0; s < 5; s++)
                Sign(bubble, "Star", star, new Vector3(-0.2f + s * 0.18f, -0.03f, -0.005f), new Vector2(0.19f, 0.19f), 11);
            return bubble.gameObject;
        }

        // A photo post: a polaroid of a plate, its tag and its likes underneath (the one that used to
        // be stuck to the menu's sign).
        private static readonly string[] PhotoTags = { "#mediumrare", "#chefskiss", "#nonotes", "#datenight" };

        private static GameObject PhotoPost(int k)
        {
            var post = new GameObject("PhotoPost").transform;
            Sign(post, "Photo", SignArt.PhotoPost(), Vector3.zero, new Vector2(0.94f, 1.1f), 10);
            Label(post, PhotoTags[k % PhotoTags.Length], new Vector3(-0.1f, -0.41f, -0.01f), 1.15f, new Color(0.16f, 0.09f, 0.12f), 12);
            Sign(post, "Heart", SignArt.SocialIcon(SignArt.Social.Heart), new Vector3(0.33f, -0.41f, -0.005f), new Vector2(0.15f, 0.15f), 11);
            post.localRotation = Quaternion.Euler(0f, 0f, k % 2 == 0 ? 5f : -4f);
            return post.gameObject;
        }

        // A flat sign piece, drawn in order (they're all see-through, so depth can't sort them).
        private static void Sign(Transform parent, string name, Material material, Vector3 at, Vector2 size, int order)
        {
            var quad = GreyboxAssets.CreatePrimitive(PrimitiveType.Quad, name, parent, material, keepCollider: false);
            quad.transform.localPosition = at;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<Renderer>().sortingOrder = order;
            quad.isStatic = false;
        }

        private static void Label(Transform parent, string text, Vector3 at, float size, Color colour, int order)
        {
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = at;
            label.font = MenuArt.Font("Inter-Bold");
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            label.GetComponent<MeshRenderer>().sortingOrder = order;
            label.ForceMeshUpdate();
        }

        // Player 1's chef, already in the lift at the street (the user's note, 2026-11-10: the walk
        // in was hard to notice; be in the lift already, and go up at once). Built with the whole
        // tailor's rail, so OpeningChef shows player 1's look; the lift carries them up.
        private static void LiftChef()
        {
            var cab = GameObject.Find("City/BossTower/TowerLift/Cab")?.transform;
            if (cab == null)
                return;
            var chef = new GameObject("OpeningChef").transform;
            chef.SetParent(cab, false);
            // On the cab's east side: in the camera's last frame that's just clear of the menu's edge,
            // so the chef shows waiting beside it.
            chef.localPosition = new Vector3(1.2f, 0f, -0.3f);
            chef.localRotation = Quaternion.Euler(0f, 172f, 0f); // looking out over the street
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(chef, false);
            var rig = CharacterArt.BuildChef(visuals, swingArms: true);
            if (rig == null)
            {
                Object.DestroyImmediate(chef.gameObject);
                return;
            }
            MakeDynamic(chef.gameObject);
            Breathe(chef.gameObject);
            var so = new SerializedObject(chef.gameObject.AddComponent<OpeningChef>());
            so.FindProperty("_wardrobe").objectReferenceValue = rig.Figure.GetComponent<ChefWardrobe>();
            var tinted = so.FindProperty("_tinted");
            tinted.arraySize = rig.Tinted.Count;
            for (var i = 0; i < rig.Tinted.Count; i++)
                tinted.GetArrayElementAtIndex(i).objectReferenceValue = rig.Tinted[i];
            so.FindProperty("_coop").objectReferenceValue = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            so.ApplyModifiedPropertiesWithoutUndo();

            var lift = Object.FindFirstObjectByType<OpeningElevator>();
            if (lift == null)
                return;
            var liftSo = new SerializedObject(lift);
            liftSo.FindProperty("_rider").objectReferenceValue = chef.gameObject;
            liftSo.FindProperty("_lead").floatValue = -1.1f; // the cab level with the camera, the chef in the middle of the frame
            liftSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
