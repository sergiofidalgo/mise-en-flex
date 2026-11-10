using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The modelled street and floor furniture (SM_StreetKit and SM_FloorKit, from the Blender
    /// script; the user's notes, 2026-11-10: "every object as crisp as our kitchen appliances", and
    /// improve the vendor stalls). One place that knows each piece's parts and colours, shared by the
    /// office's opening (OfficeBuilder) and the market outside the restaurant (GreyboxStreet).
    /// Fronts face local +z; pieces stand on local y = 0.
    /// </summary>
    public static class Kit
    {
        public const string Street = "SM_StreetKit";
        public const string Floors = "SM_FloorKit";
        private const string Office = "SM_Office";

        private static readonly Color Wood = new Color(0.55f, 0.38f, 0.24f);
        private static readonly Color DarkWood = new Color(0.36f, 0.23f, 0.15f);
        private static readonly Color Iron = new Color(0.16f, 0.15f, 0.17f);
        private static readonly Color BrassColour = new Color(0.78f, 0.6f, 0.28f);
        private static readonly Color Cream = new Color(0.93f, 0.88f, 0.78f);
        private static readonly Color Steel = new Color(0.72f, 0.73f, 0.75f);
        private static readonly Color BulbColour = new Color(1f, 0.9f, 0.62f);

        public static bool Available => PilotArt.FindMesh(Street, "StallFrame") != null && PilotArt.FindMesh(Floors, "WorkDeskTop") != null;

        // ---- Basics ---------------------------------------------------------------------------------

        public static Material Toon(string name, Color colour) => StyleTest.EnsureToonMaterial(name, colour);

        /// <summary>An unlit, glowing colour (bulbs, screens): the same assets as OfficeBuilder's.</summary>
        public static Material Glow(string name, Color colour)
        {
            var path = $"Assets/_Project/Art/Materials/MAT_Unlit_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>One mesh of a kit model, under <paramref name="parent"/>.</summary>
        public static GameObject Part(Transform parent, string model, string mesh, Material material, Vector3 at = default, float yaw = 0f, float scale = 1f)
        {
            var part = GreyboxAssets.CreatePrimitive(PrimitiveType.Cube, mesh, parent, material, keepCollider: false);
            if (!PilotArt.TryUseMesh(part, model, mesh))
            {
                Debug.LogWarning($"[Kit] No mesh {mesh} in {model}: run blender/build_pilot_models.py -- --only {model}.fbx.");
                Object.DestroyImmediate(part);
                return null;
            }
            part.transform.localPosition = at;
            part.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            part.transform.localScale = Vector3.one * scale;
            part.isStatic = true;
            return part;
        }

        private static Transform Group(Transform parent, string name, Vector3 at, float yaw)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = at;
            group.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return group;
        }

        // ---- The market stall ---------------------------------------------------------------------

        /// <summary>
        /// A market stall's parts under <paramref name="root"/> (its counter at the origin, front +z):
        /// frame, panelled counter, striped canopy, a crate for each thing it sells (round produce, or
        /// slabs for meat and fish), scales, a slate, two hanging bulbs. Closed: a cloth over the
        /// counter and no goods. Returns the counter, for a station's highlight.
        /// </summary>
        public static GameObject StallParts(Transform root, Color canopy, Color[] produce, bool slabs = false, bool closed = false)
        {
            Part(root, Street, "StallFrame", Toon("KitStallFrame", DarkWood));
            var counter = Part(root, Street, "StallCounter", Toon("KitStallCounter", Wood));
            Part(root, Street, "StallCounterTop", Toon("KitStallTop", new Color(0.86f, 0.84f, 0.8f)));
            Part(root, Street, "StallCanopyA", Toon($"KitCanopy_{ColorUtility.ToHtmlStringRGB(canopy)}", canopy));
            Part(root, Street, "StallCanopyB", Toon("KitCanopyCream", Cream));
            Part(root, Street, "StallShades", Toon("KitIron", Iron));
            Part(root, Street, "StallBulbs", Glow("KitBulb", BulbColour));
            if (closed)
            {
                Part(root, Street, "StallCover", Toon($"KitCanopy_{ColorUtility.ToHtmlStringRGB(canopy)}", canopy));
                return counter;
            }
            Part(root, Street, "StallSlate", Toon("KitSlate", new Color(0.16f, 0.18f, 0.17f)));
            Part(root, Street, "StallScale", Toon("KitSteel", Steel));
            produce ??= System.Array.Empty<Color>();
            // Crates along the counter, leaving the scales' end free when there's room.
            var room = produce.Length <= 2 ? 1.2f : 1.84f;
            var pitch = Mathf.Min(0.5f, room / Mathf.Max(1, produce.Length));
            var middle = produce.Length <= 2 ? -0.25f : 0f;
            for (var i = 0; i < produce.Length; i++)
            {
                // The models' +x is the game's −x, so lay them out mirrored to keep the builder's order left to right.
                var at = new Vector3(-(middle + (i - (produce.Length - 1) / 2f) * pitch), 0.9f, 0f);
                Part(root, Street, "StallCrate", Toon("KitCrate", new Color(0.7f, 0.55f, 0.35f)), at);
                Part(root, Street, slabs ? "StallProduceSlabs" : "StallProduceRound", Toon($"KitProduce_{ColorUtility.ToHtmlStringRGB(produce[i])}", produce[i]), at);
            }
            return counter;
        }

        /// <summary>A whole stall as its own object, for the opening's street.</summary>
        public static Transform Stall(Transform parent, Vector3 at, float yaw, Color canopy, Color[] produce, bool slabs = false)
        {
            var stall = Group(parent, "Stall", at, yaw);
            StallParts(stall, canopy, produce, slabs);
            return stall;
        }

        // ---- Queues, barriers, the pavement ----------------------------------------------------

        /// <summary>Brass posts 1.4 m apart from <paramref name="from"/> towards <paramref name="to"/>, a rope swagged between each pair.</summary>
        public static void Stanchions(Transform parent, Vector3 from, Vector3 to, Color rope)
        {
            var brass = Toon("OfficeBrass", BrassColour);
            var ropeMaterial = Toon($"KitRope_{ColorUtility.ToHtmlStringRGB(rope)}", rope);
            var span = to - from;
            var count = Mathf.Max(1, Mathf.RoundToInt(span.magnitude / 1.4f));
            var step = span / count;
            // The rope's model runs along its +x, which is the game's −x: turn it onto the line.
            var yaw = Mathf.Atan2(-step.z, step.x) * Mathf.Rad2Deg + 180f;
            for (var i = 0; i <= count; i++)
            {
                Part(parent, Street, "StanchionPost", brass, from + step * i);
                if (i < count)
                {
                    var swag = Part(parent, Street, "StanchionRope", ropeMaterial, from + step * i, yaw);
                    if (swag != null)
                        swag.transform.localScale = new Vector3(step.magnitude / 1.4f, 1f, 1f);
                }
            }
        }

        public static void Barrier(Transform parent, Vector3 at, float yaw)
        {
            var barrier = Group(parent, "Barrier", at, yaw);
            Part(barrier, Street, "BarrierFrame", Toon("KitBarrier", new Color(0.92f, 0.9f, 0.86f)));
            Part(barrier, Street, "BarrierStripes", Toon("KitBarrierRed", new Color(0.85f, 0.25f, 0.2f)));
        }

        public static void Planter(Transform parent, Vector3 at, float yaw)
        {
            var planter = Group(parent, "Planter", at, yaw);
            Part(planter, Street, "PlanterBox", Toon("KitPlanter", new Color(0.3f, 0.3f, 0.33f)));
            Part(planter, Street, "PlanterHedge", Toon("KitHedge", new Color(0.24f, 0.45f, 0.3f)));
        }

        public static void Hydrant(Transform parent, Vector3 at) =>
            Part(parent, Street, "Hydrant", Toon("StreetHydrant", new Color(0.82f, 0.2f, 0.18f)), at);

        public static void Bollard(Transform parent, Vector3 at) =>
            Part(parent, Street, "Bollard", Toon("KitIron", Iron), at);

        public static void Bench(Transform parent, Vector3 at, float yaw)
        {
            var bench = Group(parent, "Bench", at, yaw);
            Part(bench, Street, "BenchFrame", Toon("KitIron", Iron));
            Part(bench, Street, "BenchSlats", Toon("KitBenchWood", Wood));
        }

        public static void ChalkBoard(Transform parent, Vector3 at, float yaw)
        {
            var board = Group(parent, "ChalkBoard", at, yaw);
            Part(board, Street, "ChalkFrame", Toon("KitBenchWood", Wood));
            Part(board, Street, "ChalkFace", Toon("KitSlate", new Color(0.16f, 0.18f, 0.17f)));
        }

        /// <summary>A menu on a stand, lit from inside, outside a restaurant's door.</summary>
        public static void MenuStand(Transform parent, Vector3 at, float yaw)
        {
            var stand = Group(parent, "MenuStand", at, yaw);
            Part(stand, Street, "MenuStandPost", Toon("OfficeBrass", BrassColour));
            Part(stand, Street, "MenuStandFace", Glow("KitMenuFace", new Color(1f, 0.93f, 0.78f)));
        }

        // ---- Scooters and bags ------------------------------------------------------------------------

        public static Transform Scooter(Transform parent, Vector3 at, float yaw, Color paint, Color box)
        {
            var scooter = Group(parent, "Scooter", at, yaw);
            Part(scooter, Street, "ScooterBody", Toon($"KitScooter_{ColorUtility.ToHtmlStringRGB(paint)}", paint));
            Part(scooter, Street, "ScooterSeat", Toon("KitSeat", new Color(0.2f, 0.16f, 0.14f)));
            Part(scooter, Street, "ScooterTyres", Toon("CityTyre", new Color(0.08f, 0.08f, 0.09f)));
            Part(scooter, Street, "ScooterTrim", Toon("KitSteel", Steel));
            Part(scooter, Street, "ScooterLight", Glow("CityHeadlight", new Color(1f, 0.95f, 0.75f)));
            Part(scooter, Street, "ScooterBox", Toon($"KitDelivery_{ColorUtility.ToHtmlStringRGB(box)}", box));
            return scooter;
        }

        public static void DeliveryBag(Transform parent, Vector3 at, float yaw, Color colour)
        {
            var bag = Group(parent, "DeliveryBag", at, yaw);
            Part(bag, Street, "DeliveryBag", Toon($"KitDelivery_{ColorUtility.ToHtmlStringRGB(colour)}", colour));
            Part(bag, Street, "DeliveryBagTrim", Toon("KitSeat", new Color(0.2f, 0.16f, 0.14f)));
        }

        // ---- In hands ------------------------------------------------------------------------------------

        /// <summary>A phone under <paramref name="hand"/>, upright, its lit screen towards local −z.</summary>
        public static Transform Phone(Transform hand, Vector3 at, Quaternion turn)
        {
            var phone = new GameObject("Phone").transform;
            phone.SetParent(hand, false);
            phone.localPosition = at;
            phone.localRotation = turn;
            // The model's screen faces its −y in Blender, the game's +z: turned round, towards the holder.
            Part(phone, Street, "Phone", Toon("KitPhone", new Color(0.1f, 0.1f, 0.12f)), default, 180f);
            Part(phone, Street, "PhoneScreen", Glow("StreetPhoneScreen", new Color(0.7f, 0.85f, 1f)), default, 180f);
            return phone;
        }

        /// <summary>A camera under <paramref name="hand"/>, its lens towards local +z.</summary>
        public static Transform Camera(Transform hand, Vector3 at)
        {
            var camera = new GameObject("Camera").transform;
            camera.SetParent(hand, false);
            camera.localPosition = at;
            Part(camera, Street, "CameraBody", Toon("KitPhone", new Color(0.1f, 0.1f, 0.12f)));
            Part(camera, Street, "CameraLens", Toon("KitLens", new Color(0.2f, 0.22f, 0.28f)));
            return camera;
        }

        public static Transform RingLight(Transform parent, Vector3 at, float yaw)
        {
            var light = Group(parent, "RingLight", at, yaw);
            Part(light, Street, "RingLightStand", Toon("KitIron", Iron));
            Part(light, Street, "RingLightRing", Glow("StreetRingLight", new Color(1f, 0.97f, 0.92f)));
            Part(light, Street, "Phone", Toon("KitPhone", new Color(0.1f, 0.1f, 0.12f)), new Vector3(0f, 1.62f, 0f));
            return light;
        }

        // ---- Tables, chairs, the bar -----------------------------------------------------------------

        public static void BistroTable(Transform parent, Vector3 at, bool high = false)
        {
            var name = high ? "HighTable" : "Bistro";
            Part(parent, Street, name + "Base", Toon("KitIron", Iron), at);
            Part(parent, Street, name + "Top", Toon("KitTableTop", new Color(0.62f, 0.42f, 0.26f)), at);
        }

        /// <summary>A bistro chair; <paramref name="yaw"/> 0 has the sitter facing +z.</summary>
        public static void BistroChair(Transform parent, Vector3 at, float yaw) =>
            Part(parent, Street, "BistroChair", Toon("KitIron", Iron), at, yaw);

        public static void Bottle(Transform parent, Vector3 at, Color glass, float scale = 1f) =>
            Part(parent, Street, "Bottle", Toon($"KitBottle_{ColorUtility.ToHtmlStringRGB(glass)}", glass), at, 0f, scale);

        /// <summary>A pendant lamp whose shade's rim hangs at <paramref name="at"/>.</summary>
        public static void Pendant(Transform parent, Vector3 at, Color shade)
        {
            Part(parent, Street, "PendantShade", Toon($"KitShade_{ColorUtility.ToHtmlStringRGB(shade)}", shade), at);
            Part(parent, Street, "PendantBulb", Glow("KitBulb", BulbColour), at);
        }

        // ---- Entrances ----------------------------------------------------------------------------------

        /// <summary>A canopy out from a wall at <paramref name="at"/> (on the ground), bulbs round its edge.</summary>
        public static Transform Canopy(Transform parent, Vector3 at, float yaw, Color top, float scale = 1f)
        {
            var canopy = Group(parent, "Canopy", at, yaw);
            canopy.localScale = Vector3.one * scale;
            Part(canopy, Street, "CanopyTop", Toon($"KitCanopyTop_{ColorUtility.ToHtmlStringRGB(top)}", top));
            Part(canopy, Street, "CanopyTrim", Toon("OfficeBrass", BrassColour));
            Part(canopy, Street, "CanopyBulbs", Glow("KitBulb", BulbColour));
            return canopy;
        }

        /// <summary>A double door standing open: the frame and two glazed leaves swung in.</summary>
        public static void OpenDoor(Transform parent, Vector3 at, float yaw, Material glass)
        {
            var door = Group(parent, "Door", at, yaw);
            var brass = Toon("OfficeBrass", BrassColour);
            Part(door, Street, "DoorFrame", brass);
            foreach (var side in new[] { -1f, 1f })
            {
                // Hinged at the jamb, swung inwards (−z) nearly flat against the wall inside. The
                // leaf's model runs towards the game's −x from its hinge: right for the +x jamb,
                // mirrored for the other.
                var leaf = Group(door, "Leaf", new Vector3(side * 0.8f, 0f, 0f), side < 0f ? 80f : -80f);
                leaf.localScale = new Vector3(side < 0f ? -1f : 1f, 1f, 1f);
                Part(leaf, Street, "DoorLeaf", brass);
                Part(leaf, Street, "DoorLeafGlass", glass);
            }
        }

        public static void ScreenBezel(Transform parent, Vector3 at, float yaw) =>
            Part(parent, Street, "ScreenBezel", Toon("KitIron", Iron), at, yaw);

        // ---- The tower's floors ---------------------------------------------------------------------

        /// <summary>A desk with its screen, keyboard and chair; the sitter faces +z, so the screen glows towards −z... turned by <paramref name="yaw"/>.</summary>
        public static Transform WorkStation(Transform parent, Vector3 at, float yaw, Color screen, bool chair = true)
        {
            var desk = Group(parent, "WorkStation", at, yaw);
            Part(desk, Floors, "WorkDeskTop", Toon("KitDeskTop", new Color(0.86f, 0.78f, 0.64f)));
            Part(desk, Floors, "WorkDeskLegs", Toon("KitDeskLegs", new Color(0.3f, 0.31f, 0.35f)));
            Part(desk, Floors, "DeskSetDark", Toon("KitPhone", new Color(0.1f, 0.1f, 0.12f)));
            Part(desk, Floors, "DeskSetScreen", Glow($"KitScreen_{ColorUtility.ToHtmlStringRGB(screen)}", screen));
            if (chair)
            {
                // The chair's side is the model's +y, the game's −z.
                Part(desk, Office, "OfficeChairSeat", Toon("KitTaskChair", new Color(0.22f, 0.24f, 0.3f)), new Vector3(0.1f, 0f, -0.75f), 180f, 0.85f);
                Part(desk, Office, "OfficeChairBase", Toon("KitSteel", Steel), new Vector3(0.1f, 0f, -0.75f), 180f, 0.85f);
            }
            return desk;
        }

        public static void Softbox(Transform parent, Vector3 at, float yaw)
        {
            var box = Group(parent, "Softbox", at, yaw);
            Part(box, Floors, "SoftboxStand", Toon("KitIron", Iron));
            Part(box, Floors, "SoftboxHood", Toon("KitPhone", new Color(0.1f, 0.1f, 0.12f)));
            Part(box, Floors, "SoftboxFace", Glow("KitSoftbox", new Color(1f, 0.98f, 0.94f)));
        }

        /// <summary>A camera on a tripod, its lens towards +z turned by <paramref name="yaw"/>; returns the camera.</summary>
        public static Transform TripodCamera(Transform parent, Vector3 at, float yaw)
        {
            var tripod = Group(parent, "TripodCamera", at, yaw);
            Part(tripod, Floors, "Tripod", Toon("KitIron", Iron));
            return Camera(tripod, new Vector3(0f, 1.43f, 0f));
        }

        public static void Backdrop(Transform parent, Vector3 at, float yaw, Color paper)
        {
            var backdrop = Group(parent, "Backdrop", at, yaw);
            Part(backdrop, Floors, "BackdropStand", Toon("KitIron", Iron));
            Part(backdrop, Floors, "BackdropPaper", Toon($"KitPaper_{ColorUtility.ToHtmlStringRGB(paper)}", paper));
        }

        public static void ScreenWall(Transform parent, Vector3 at, float yaw, Color a, Color b, Color c)
        {
            var wall = Group(parent, "ScreenWall", at, yaw);
            Part(wall, Floors, "ScreenWallFrame", Toon("KitPhone", new Color(0.1f, 0.1f, 0.12f)));
            Part(wall, Floors, "ScreenWallA", Glow($"KitScreen_{ColorUtility.ToHtmlStringRGB(a)}", a));
            Part(wall, Floors, "ScreenWallB", Glow($"KitScreen_{ColorUtility.ToHtmlStringRGB(b)}", b));
            Part(wall, Floors, "ScreenWallC", Glow($"KitScreen_{ColorUtility.ToHtmlStringRGB(c)}", c));
        }

        public static void Cubicle(Transform parent, Vector3 at, float yaw, Color fabric)
        {
            var panel = Group(parent, "Cubicle", at, yaw);
            Part(panel, Floors, "CubiclePanel", Toon($"KitFabric_{ColorUtility.ToHtmlStringRGB(fabric)}", fabric));
            Part(panel, Floors, "CubicleTrim", Toon("KitSteel", Steel));
        }

        public static void WaterCooler(Transform parent, Vector3 at, float yaw)
        {
            var cooler = Group(parent, "WaterCooler", at, yaw);
            Part(cooler, Floors, "CoolerBody", Toon("KitCooler", new Color(0.9f, 0.91f, 0.92f)));
            Part(cooler, Floors, "CoolerBottle", Toon("KitCoolerWater", new Color(0.55f, 0.75f, 0.92f)));
        }

        public static void Vending(Transform parent, Vector3 at, float yaw, Color body)
        {
            var machine = Group(parent, "Vending", at, yaw);
            Part(machine, Floors, "VendingBody", Toon($"KitVending_{ColorUtility.ToHtmlStringRGB(body)}", body));
            Part(machine, Floors, "VendingWindow", Glow("KitVendingWindow", new Color(0.95f, 0.97f, 1f)));
            Part(machine, Floors, "VendingTrim", Toon("KitPhone", new Color(0.1f, 0.1f, 0.12f)));
            Part(machine, Floors, "VendingGoods", Toon("KitVendingGoods", new Color(0.95f, 0.6f, 0.2f)));
        }

        public static void Whiteboard(Transform parent, Vector3 at, float yaw)
        {
            var board = Group(parent, "Whiteboard", at, yaw);
            Part(board, Floors, "WhiteboardFrame", Toon("KitSteel", Steel));
            Part(board, Floors, "WhiteboardFace", Toon("KitWhiteboard", new Color(0.96f, 0.96f, 0.94f)));
        }

        /// <summary>A light panel hanging from the ceiling at <paramref name="at"/>, lit or not.</summary>
        public static void CeilingPanel(Transform parent, Vector3 at, Color glow, bool lit)
        {
            Part(parent, Floors, "CeilingPanelFrame", Toon("KitCooler", new Color(0.9f, 0.91f, 0.92f)), at);
            Part(parent, Floors, "CeilingPanelGlow", lit ? Glow($"KitPanel_{ColorUtility.ToHtmlStringRGB(glow)}", glow) : Toon("KitPanelOff", new Color(0.5f, 0.52f, 0.56f)), at);
        }

        public static void CoffeeMachine(Transform parent, Vector3 at, float yaw)
        {
            var machine = Group(parent, "CoffeeMachine", at, yaw);
            Part(machine, Floors, "CoffeeMachine", Toon("KitCoffee", new Color(0.75f, 0.2f, 0.18f)));
            Part(machine, Floors, "CoffeeTrim", Toon("KitSteel", Steel));
        }

        public static void Safe(Transform parent, Vector3 at, float yaw)
        {
            var safe = Group(parent, "Safe", at, yaw);
            Part(safe, Floors, "SafeBody", Toon("KitSafe", new Color(0.26f, 0.3f, 0.34f)));
            Part(safe, Floors, "SafeWheel", Toon("OfficeBrass", BrassColour));
        }

        public static void Money(Transform parent, Vector3 at, float yaw)
        {
            var pile = Group(parent, "Money", at, yaw);
            Part(pile, Floors, "MoneyNotes", Toon("KitMoney", new Color(0.4f, 0.62f, 0.4f)));
            Part(pile, Floors, "MoneyBands", Toon("KitMoneyBand", Cream));
        }
    }
}
