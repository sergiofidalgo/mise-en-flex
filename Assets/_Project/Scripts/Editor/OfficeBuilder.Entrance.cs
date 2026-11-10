using Game.Data;
using Game.Presentation;
using Game.Presentation.Office;
using Game.Presentation.Office.Ambience;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The boss's tower at street level, where the opening stops for the menu (the user's notes,
    /// 2026-11-10): the ground floor is the boss's own restaurant, packed and seen through clear
    /// glass, under a big neon name. Outside, a queue along the front behind a rope, phones up,
    /// selfies, photographers flanking a red carpet, an influencer filming with a ring light,
    /// flashes everywhere and hearts, stars and comments popping over the phones: the game is about
    /// social media and reviews. The tower's own door, its doorman, and, waiting at the kerb,
    /// player 1's chef, who walks in to the lift when a campaign is chosen.
    /// </summary>
    public static partial class OfficeBuilder
    {
        // The two doors in the lobby's glass: the tower's (west) and the restaurant's (east).
        private const float TowerDoorX = -5f;
        private const float RestaurantDoorX = 5f;
        private const float DoorHalfWidth = 0.8f;
        private const float LobbyHalf = 8f;

        private static void TowerEntrance(Transform street)
        {
            var entrance = new GameObject("TowerEntrance").transform;
            entrance.SetParent(street, false);
            Lobby(entrance);
            Restaurant(entrance);
            Marquee(entrance);
            RedCarpet(entrance);
            Queue(entrance);
            EntranceMoments(entrance);
            WaitingChef(street);
        }

        // The lobby's front: clear glass between brass mullions, open at the two doors.
        private static void Lobby(Transform entrance)
        {
            var glass = PilotArt.EnsureGlassMaterial("OfficeTowerPane", new Color(0.55f, 0.7f, 0.85f, 0.12f));
            var brass = Toon("OfficeBrass", Brass);
            var bronze = Toon("OfficeTowerBronze", new Color(0.45f, 0.32f, 0.2f));
            const float height = 4.2f;
            const float doorHeight = 2.7f;
            var z = TowerFace - 0.05f;
            // Glass in pieces round the two door openings, and over them.
            var edges = new[] { -LobbyHalf, TowerDoorX - DoorHalfWidth, TowerDoorX + DoorHalfWidth, RestaurantDoorX - DoorHalfWidth, RestaurantDoorX + DoorHalfWidth, LobbyHalf };
            for (var i = 0; i < edges.Length - 1; i++)
            {
                var door = i % 2 == 1;
                var from = door ? doorHeight : 0f;
                Cube(entrance, door ? "Transom" : "LobbyGlass", glass, new Vector3((edges[i] + edges[i + 1]) / 2f, 0.15f + from, z), new Vector3(edges[i + 1] - edges[i], height - from, 0.04f));
            }
            for (var x = -LobbyHalf; x <= LobbyHalf + 0.01f; x += 2f)
                if (Mathf.Abs(x - TowerDoorX) > DoorHalfWidth + 0.1f && Mathf.Abs(x - RestaurantDoorX) > DoorHalfWidth + 0.1f)
                    Block(entrance, "LobbyMullion", bronze, new Vector3(x, 0.15f, z - 0.03f), new Vector3(0.12f, height, 0.12f));
            foreach (var doorX in new[] { TowerDoorX, RestaurantDoorX })
            {
                foreach (var side in new[] { -1f, 1f })
                    Block(entrance, "DoorFrame", brass, new Vector3(doorX + side * DoorHalfWidth, 0.15f, z - 0.03f), new Vector3(0.14f, doorHeight, 0.16f));
                Block(entrance, "DoorHead", brass, new Vector3(doorX, 0.15f + doorHeight, z - 0.03f), new Vector3(2f * DoorHalfWidth + 0.14f, 0.14f, 0.16f));
            }
            // The tower's door: a small brass canopy, a lamp, the doorman.
            Block(entrance, "TowerCanopy", brass, new Vector3(TowerDoorX, 3.1f, TowerFace - 0.8f), new Vector3(2.6f, 0.18f, 1.6f));
            Point(entrance, "TowerDoorLight", new Vector3(TowerDoorX, 2.7f, TowerFace - 1.2f), Incandescent, 6f, 1.4f);
            Person(entrance, "Doorman", new Vector3(TowerDoorX - 1.3f, 0.15f, TowerFace - 0.7f), 180f, Burgundy, Suit, 1f,
                look => { look.Hat = CharacterArt.HatKind.Cap; look.HatColor = Burgundy; look.Tie = true; look.TieColor = new Color(0.86f, 0.62f, 0.26f); });
        }

        // The boss's restaurant behind the glass: warm wood, a lit bar with its bottles along the
        // back, pendant lights over high tables, every table taken, a waiter. The lift's glass shaft
        // stands in the middle of the room.
        private static void Restaurant(Transform entrance)
        {
            var room = new GameObject("Restaurant").transform;
            room.SetParent(entrance, false);
            var front = TowerFace + 0.1f;
            var back = TowerFace + RoomDepth;
            const float ceiling = 4.3f;
            var wood = Toon("RestaurantFloor", new Color(0.42f, 0.27f, 0.18f));
            var walnut = Toon("OfficeWalnut", Walnut);
            var brass = Toon("OfficeBrass", Brass);
            Cube(room, "Floor", wood, new Vector3(0f, 0.15f, (front + back) / 2f), new Vector3(2f * LobbyHalf + 0.4f, 0.02f, back - front));
            Cube(room, "Ceiling", Toon("RestaurantCeiling", new Color(0.32f, 0.2f, 0.15f)), new Vector3(0f, ceiling, (front + back) / 2f), new Vector3(2f * LobbyHalf + 0.4f, 0.1f, back - front));
            Block(room, "BackWall", walnut, new Vector3(0f, 0.15f, back), new Vector3(2f * LobbyHalf + 0.4f, ceiling - 0.15f, 0.15f));
            foreach (var x in new[] { -LobbyHalf - 0.2f, LobbyHalf + 0.2f })
                Block(room, "SideWall", walnut, new Vector3(x, 0.15f, (front + back) / 2f), new Vector3(0.15f, ceiling - 0.15f, back - front));

            // The bar along the back: a glowing bottle wall, shelves of bottles, the counter.
            var glow = Unlit("RestaurantBottleGlow", new Color(1f, 0.72f, 0.4f));
            Color[] bottles = { new Color(0.3f, 0.6f, 0.35f), new Color(0.75f, 0.45f, 0.2f), new Color(0.85f, 0.85f, 0.9f), new Color(0.55f, 0.2f, 0.25f) };
            foreach (var side in new[] { -1f, 1f })
            {
                var centre = side * 5.2f;
                Cube(room, "BottleWall", glow, new Vector3(centre, 1.2f, back - 0.1f), new Vector3(5f, 2.4f, 0.02f));
                for (var shelf = 0; shelf < 3; shelf++)
                {
                    var y = 1.35f + shelf * 0.75f;
                    Block(room, "Shelf", brass, new Vector3(centre, y - 0.04f, back - 0.25f), new Vector3(5f, 0.04f, 0.3f));
                    for (var b = 0; b < 12; b++)
                        Shape(room, PrimitiveType.Cylinder, "Bottle", Toon($"RestaurantBottle{(b + shelf) % 4}", bottles[(b + shelf) % 4]),
                            new Vector3(centre - 2.3f + b * 0.42f, y + 0.16f, back - 0.25f), new Vector3(0.09f, 0.16f, 0.09f));
                }
                Block(room, "BarCounter", walnut, new Vector3(centre, 0.15f, back - 1.1f), new Vector3(5f, 1.05f, 0.6f));
                Block(room, "BarTop", brass, new Vector3(centre, 1.2f, back - 1.1f), new Vector3(5.1f, 0.05f, 0.7f));
            }

            // High tables with their diners, away from the chef's way to the lift (west of it) and
            // the lift's shaft.
            var tableTop = Toon("RestaurantTable", new Color(0.62f, 0.42f, 0.26f));
            foreach (var (x, z) in new[] { (-7f, front + 4.6f), (-4.6f, front + 5.2f), (3.6f, front + 1.4f), (6.6f, front + 2.2f), (3.8f, front + 4.6f), (6.6f, front + 5f) })
            {
                Shape(room, PrimitiveType.Cylinder, "TableTop", tableTop, new Vector3(x, 1.08f, z), new Vector3(0.9f, 0.025f, 0.9f));
                Shape(room, PrimitiveType.Cylinder, "TablePost", walnut, new Vector3(x, 0.6f, z), new Vector3(0.1f, 0.45f, 0.1f));
                Shape(room, PrimitiveType.Sphere, "Pendant", Unlit("OfficeBulb", Bulb), new Vector3(x, 3.1f, z), Vector3.one * 0.28f);
                Block(room, "PendantCord", Toon("OfficeInk", Ink), new Vector3(x, 3.2f, z), new Vector3(0.02f, ceiling - 3.2f, 0.02f));
                Passerby(room, "Diner", new Vector3(x - 0.6f, 0.15f, z), 90f);
                Passerby(room, "Diner", new Vector3(x + 0.6f, 0.15f, z), -90f);
            }
            Passerby(room, "Waiter", new Vector3(1.2f + 2.6f, 0.15f, front + 3.1f), -30f);
            foreach (var x in new[] { -5.5f, 0f, 5.5f })
                Point(room, "RestaurantLight", new Vector3(x, 3.4f, front + 3.4f), Incandescent, 8f, 1.8f);
        }

        // The restaurant's name in big neon over the lintel, and stars under it: the boss's flagship.
        private static void Marquee(Transform entrance)
        {
            var pink = new Color(1f, 0.38f, 0.62f);
            var name = SignArt.Neon("MISE EN FLEX", pink);
            const float height = 1.05f;
            var width = height * name.mainTexture.width / name.mainTexture.height;
            Block(entrance, "MarqueeRaceway", Toon("StreetNeonBacking", Iron), new Vector3(0f, 5.55f, TowerFace - 0.45f), new Vector3(width * 0.92f, 0.95f, 0.12f));
            Shape(entrance, PrimitiveType.Quad, "MarqueeName", name, new Vector3(0f, 6.05f, TowerFace - 0.55f), new Vector3(width, height, 1f));
            var star = SignArt.SocialIcon(SignArt.Social.Star);
            for (var s = 0; s < 5; s++)
                Shape(entrance, PrimitiveType.Quad, "MarqueeStar", star, new Vector3(-1.6f + s * 0.8f, 5.15f, TowerFace - 0.56f), Vector3.one * 0.6f);
            Point(entrance, "MarqueeWash", new Vector3(0f, 5.8f, TowerFace - 1.6f), pink, 8f, 2f);
        }

        // From the restaurant's door to the kerb: a red carpet between brass posts and ropes, a
        // canopy over the door, a couple posing on the carpet and photographers either side of it.
        private static void RedCarpet(Transform entrance)
        {
            var brass = Toon("OfficeBrass", Brass);
            var rope = Toon("OfficeCarpet", Burgundy);
            Cube(entrance, "RedCarpet", rope, new Vector3(RestaurantDoorX, 0.16f, (TowerFace + NorthKerb) / 2f), new Vector3(2.2f, 0.01f, TowerFace - NorthKerb));
            Block(entrance, "Canopy", brass, new Vector3(RestaurantDoorX, 3.3f, TowerFace - 1.6f), new Vector3(3.4f, 0.25f, 3.2f));
            foreach (var x in new[] { -1.5f, 1.5f })
                Block(entrance, "CanopyPost", brass, new Vector3(RestaurantDoorX + x, 0.15f, TowerFace - 3.05f), new Vector3(0.1f, 3.15f, 0.1f));
            Point(entrance, "CanopyLight", new Vector3(RestaurantDoorX, 2.9f, TowerFace - 1.8f), Incandescent, 7f, 2f);
            for (var z = TowerFace - 1.5f; z > NorthKerb + 0.6f; z -= 1.5f)
            foreach (var side in new[] { -1.4f, 1.4f })
                Block(entrance, "Stanchion", brass, new Vector3(RestaurantDoorX + side, 0.15f, z), new Vector3(0.1f, 0.9f, 0.1f));
            foreach (var side in new[] { -1.4f, 1.4f })
                Block(entrance, "CarpetRope", rope, new Vector3(RestaurantDoorX + side, 0.93f, (TowerFace - 1.5f + NorthKerb + 0.6f) / 2f), new Vector3(0.05f, 0.05f, TowerFace - 1.5f - NorthKerb - 0.6f));

            // The couple on the carpet, posing for the photographers.
            var couple = new GameObject("Posing").transform;
            couple.SetParent(entrance, false);
            couple.localPosition = new Vector3(RestaurantDoorX, 0.15f, TowerFace - 2.2f);
            couple.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Poser(couple, "Star", new Vector3(-0.3f, 0f, 0f), 0f, phone: false);
            Poser(couple, "Partner", new Vector3(0.35f, 0f, 0.05f), -10f, phone: false);
            // Photographers outside the ropes, cameras up, flashing.
            foreach (var (x, z, facing) in new[] { (RestaurantDoorX - 2.5f, NorthKerb + 1.4f, 30f), (RestaurantDoorX + 2.5f, NorthKerb + 1.2f, -30f), (RestaurantDoorX - 2.6f, TowerFace - 4.4f, 70f) })
                Photographer(entrance, new Vector3(x, 0.15f, z), facing);
        }

        // The queue for the restaurant along the front, behind a rope: most of them on their phones,
        // filming, taking selfies, the flash going, hearts and stars rising.
        private static void Queue(Transform entrance)
        {
            var brass = Toon("OfficeBrass", Brass);
            var ropeZ = TowerFace - 1.8f;
            var start = RestaurantDoorX + 1.6f;
            const float end = 16f;
            for (var x = start; x <= end; x += 1.4f)
                Block(entrance, "QueueStanchion", brass, new Vector3(x, 0.15f, ropeZ), new Vector3(0.1f, 0.9f, 0.1f));
            Block(entrance, "QueueRope", Toon("OfficeCarpet", Burgundy), new Vector3((start + end) / 2f, 0.93f, ropeZ), new Vector3(end - start, 0.05f, 0.05f));
            var k = 0;
            for (var x = start + 0.3f; x < end; x += 0.85f, k++)
            {
                var at = new Vector3(x, 0.15f, TowerFace - 0.9f + ((k % 3) - 1) * 0.15f);
                if (k % 3 == 1)
                    Passerby(entrance, "Queue", at, -90f + (k % 2 == 0 ? 15f : -20f));
                else
                    PhoneUp(entrance, at, -90f + (k % 2 == 0 ? 60f : 110f), selfie: k % 3 == 0);
            }
            // Two at a time going in and coming out, so the queue is for somewhere.
            for (var w = 0; w < 2; w++)
            {
                var walker = Passerby(entrance, "GoingIn", new Vector3(RestaurantDoorX, 0.15f, TowerFace - 2.6f), 0f);
                walker.gameObject.AddComponent<LaneMover>().Set(new Vector3(RestaurantDoorX + (w == 0 ? -0.3f : 0.3f), 0.15f, TowerFace - 2.6f),
                    new Vector3(RestaurantDoorX + (w == 0 ? -0.6f : 1.4f), 0.15f, TowerFace + 2.4f), 1.1f, true, 3f + w, w * 0.5f);
                MakeDynamic(walker.gameObject);
            }
        }

        // More of it along the front: selfies by the glass, an influencer filming under a ring light,
        // friends photographing each other, all popping hearts and flashing.
        private static void EntranceMoments(Transform entrance)
        {
            PhoneUp(entrance, new Vector3(-1.4f, 0.15f, TowerFace - 1.6f), 180f, selfie: true);
            PhoneUp(entrance, new Vector3(-0.6f, 0.15f, TowerFace - 1.7f), 170f, selfie: false);
            PhoneUp(entrance, new Vector3(-9.6f, 0.15f, TowerFace - 2.6f), 160f, selfie: true);
            Passerby(entrance, "Friend", new Vector3(-8.9f, 0.15f, TowerFace - 2.5f), 190f);
            PhoneUp(entrance, new Vector3(1.6f, 0.15f, NorthKerb + 1.2f), 0f, selfie: false); // photographing the marquee
            PhoneUp(entrance, new Vector3(-2.8f, 0.15f, NorthKerb + 1.0f), 10f, selfie: false);
            PhoneUp(entrance, new Vector3(9.5f, 0.15f, NorthKerb + 1.3f), -20f, selfie: false);

            // The influencer: filming themselves with the tower behind, under a ring light on a stand.
            var at = new Vector3(-12.5f, 0.15f, TowerFace - 3.2f);
            Passerby(entrance, "Influencer", at, 180f);
            var light = new GameObject("RingLight").transform;
            light.SetParent(entrance, false);
            light.localPosition = at + new Vector3(0f, 0f, -1.3f);
            Block(light, "Stand", Toon("OfficeInk", Ink), Vector3.zero, new Vector3(0.05f, 1.45f, 0.05f));
            var ring = Unlit("StreetRingLight", new Color(1f, 0.97f, 0.92f));
            for (var r = 0; r < 14; r++)
            {
                var angle = r / 14f * Mathf.PI * 2f;
                Shape(light, PrimitiveType.Sphere, "RingBulb", ring, new Vector3(Mathf.Cos(angle) * 0.24f, 1.6f + Mathf.Sin(angle) * 0.24f, 0f), Vector3.one * 0.07f);
            }
            Block(light, "Phone", Toon("OfficeInk", Ink), new Vector3(0f, 1.52f, 0.01f), new Vector3(0.08f, 0.15f, 0.02f));
            Point(light, "RingGlow", new Vector3(0f, 1.6f, 0.4f), new Color(1f, 0.97f, 0.92f), 3.5f, 2.6f);
            Pops(light, new Vector3(0f, 1.95f, 0f), 2.6f);
        }

        // Someone holding their phone up, at arm's length for a selfie or out in front to film,
        // its flash going and a heart, star or comment popping over it now and then.
        private static void PhoneUp(Transform parent, Vector3 at, float facing, bool selfie)
        {
            var rig = Figure(parent, selfie ? "Selfie" : "Filming", at, facing);
            if (rig == null)
                return;
            rig.RightShoulder.localRotation = selfie ? Quaternion.Euler(-125f, 0f, -15f) : Quaternion.Euler(-95f, 0f, -10f);
            if (!selfie)
                rig.LeftShoulder.localRotation = Quaternion.Euler(-85f, 0f, 15f);
            var phone = Phone(rig.RightShoulder);
            Flash(rig.RightShoulder, phone, 2.2f + (float)_random.NextDouble() * 2.6f);
            Pops(phone, new Vector3(0f, 0.25f, 0f), 3.5f + (float)_random.NextDouble() * 3f);
            Breathe(rig.Figure != null ? rig.Figure.gameObject : parent.gameObject);
        }

        // The pops over a phone: one icon of each kind, which SocialPop shows one at a time.
        private static void Pops(Transform owner, Vector3 at, float period)
        {
            var holder = new GameObject("SocialPop").transform;
            holder.SetParent(owner, false);
            holder.localPosition = at;
            var kinds = new[] { SignArt.Social.Heart, SignArt.Social.Star, SignArt.Social.Comment };
            var icons = new GameObject[kinds.Length];
            for (var i = 0; i < kinds.Length; i++)
                icons[i] = Shape(holder, PrimitiveType.Quad, $"Pop{kinds[i]}", SignArt.SocialIcon(kinds[i]), Vector3.zero, Vector3.one * 0.45f);
            // Sized in the world, whatever the owner's scale.
            var scale = owner.lossyScale.x > 1e-4f ? 1f / owner.lossyScale.x : 1f;
            holder.gameObject.AddComponent<SocialPop>().Set(icons, period, 1.7f, 0.9f * scale, 0.42f * scale, (float)_random.NextDouble() * 50f);
            MakeDynamic(holder.gameObject);
        }

        // Player 1's chef, waiting at the kerb by the tower's door; OpeningChef walks them in to the
        // lift. Built with the whole tailor's rail, so they wear player 1's look.
        private static void WaitingChef(Transform street)
        {
            var cab = GameObject.Find("City/BossTower/TowerLift/Cab")?.transform;
            var chef = new GameObject("OpeningChef").transform;
            chef.SetParent(street, false);
            var waiting = new Vector3(TowerDoorX - 1.8f, 0.15f, NorthKerb + 1.0f);
            chef.localPosition = waiting;
            chef.localRotation = Quaternion.Euler(0f, 20f, 0f);
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(chef, false);
            var rig = CharacterArt.BuildChef(visuals, swingArms: true);
            if (rig == null || cab == null)
            {
                Object.DestroyImmediate(chef.gameObject);
                return;
            }
            MakeDynamic(chef.gameObject);
            // In through the tower's door, round the west side of the lift's shaft to its doors
            // at the back, and in.
            var cabCentre = cab.position;
            var path = new[]
            {
                street.TransformPoint(waiting),
                street.TransformPoint(new Vector3(TowerDoorX, 0.15f, TowerFace - 2.4f)),
                street.TransformPoint(new Vector3(TowerDoorX, 0.15f, TowerFace + 0.9f)),
                street.TransformPoint(new Vector3(-ShaftHalfWidth - 0.7f, 0.15f, ShaftNorth + 0.6f)),
                street.TransformPoint(new Vector3(0f, 0.15f, ShaftNorth + 0.6f)),
                new Vector3(cabCentre.x, street.TransformPoint(new Vector3(0f, 0.15f, 0f)).y, cabCentre.z),
            };
            var doors = new System.Collections.Generic.List<Transform>();
            foreach (Transform part in cab)
                if (part.name == "Door")
                    doors.Add(part);
            var so = new SerializedObject(chef.gameObject.AddComponent<OpeningChef>());
            var list = so.FindProperty("_path");
            list.arraySize = path.Length;
            for (var i = 0; i < path.Length; i++)
                list.GetArrayElementAtIndex(i).vector3Value = path[i];
            so.FindProperty("_cab").objectReferenceValue = cab;
            var doorList = so.FindProperty("_doors");
            doorList.arraySize = doors.Count;
            for (var i = 0; i < doors.Count; i++)
                doorList.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
            so.FindProperty("_wardrobe").objectReferenceValue = rig.Figure.GetComponent<ChefWardrobe>();
            var tinted = so.FindProperty("_tinted");
            tinted.arraySize = rig.Tinted.Count;
            for (var i = 0; i < rig.Tinted.Count; i++)
                tinted.GetArrayElementAtIndex(i).objectReferenceValue = rig.Tinted[i];
            so.FindProperty("_coop").objectReferenceValue = GreyboxAssets.EnsureAsset<CoopSettings>(SettingsFolder + "/CoopSettings.asset");
            so.ApplyModifiedPropertiesWithoutUndo();

            // The lift carries the chef up, and lets them go once the camera has landed.
            var lift = Object.FindFirstObjectByType<OpeningElevator>();
            if (lift != null)
            {
                var liftSo = new SerializedObject(lift);
                liftSo.FindProperty("_rider").objectReferenceValue = chef.gameObject;
                liftSo.FindProperty("_lead").floatValue = -1.1f; // the cab level with the camera, the chef in the middle of the frame
                liftSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
